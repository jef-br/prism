using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Prism.Core.Tests")]
[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Prism.Services.Matching.Tests")]

namespace Prism.Services.Matching;

/// <summary>Detects whether the image subject intersects one or more image boundaries.
/// <para> Fast path (JPEG only): parses the EXIF APP1 block to extract the embedded IFD1 thumbnail without decoding the main image. Falls back to a full load capped at <c>Config.MaxAnalysisSize</c> on the longest side.</para>
/// <para>Border/foreground tuning values load from the "SubjectEdgeDetector" section of ClassifyConfig.json via <see cref="Config"/>, fetched once at the top of each public <c>Detect</c> overload. JPEG/EXIF/TIFF marker bytes and offsets in the thumbnail-extraction path stay bare literals (S109 suppressed there) — they encode a file-format spec, not a tunable threshold, and naming them would just restate the spec in English.</para>
/// </summary>
public static class SubjectEdgeDetector {
    private const int MaxExifSearchBytes = 65536;

    private const int AlphaOpaqueThreshold = 128;
    private const float MaxChannelValueF = 255f;

    /// <summary>
    /// Tuning values for SubjectEdgeDetector, bound from the "SubjectEdgeDetector" section of
    /// ClassifyConfig.json. No defaults — every value must be present in the JSON or deserialization
    /// fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        // Validation bound, not tunable: StripDepthFraction is a fraction of the short image side, so
        // it must stay below half.
        private const float StripDepthFractionUpperBound = 0.5f;

        /// <summary>Longest side (px) of the image used for analysis in the fallback path.</summary>
        public required int MaxAnalysisSize { get; init; }

        /// <summary>Minimum embedded EXIF thumbnail size (px, shortest side) trusted over a full decode.</summary>
        public required int MinEXIFThumbnailSize { get; init; }

        /// <summary>Border strip depth as a fraction of min(W, H) of the analysis image.</summary>
        public required float StripDepthFraction { get; init; }

        /// <summary>Euclidean RGB distance above which a pixel counts as foreground rather than background.</summary>
        public required float BgColorDiffThreshold { get; init; }

        /// <summary>Fraction of a strip's pixels that must belong to the foreground to flag a boundary intersection.</summary>
        public required float IntersectionFraction { get; init; }

        /// <summary>Minimum consecutive foreground pixels in a row to count as subject contact (noise filter).</summary>
        public required int MinRunLength { get; init; }

        public void Validate() {
            List<string> problems = [];

            if (MaxAnalysisSize < 1) problems.Add("SubjectEdgeDetector.MaxAnalysisSize must be >= 1");
            if (MinEXIFThumbnailSize < 1) problems.Add("SubjectEdgeDetector.MinEXIFThumbnailSize must be >= 1");
            if (StripDepthFraction is <= 0f or >= StripDepthFractionUpperBound) problems.Add("SubjectEdgeDetector.StripDepthFraction must be in (0,0.5)");
            if (BgColorDiffThreshold <= 0f) problems.Add("SubjectEdgeDetector.BgColorDiffThreshold must be > 0");
            if (IntersectionFraction is <= 0f or > 1f) problems.Add("SubjectEdgeDetector.IntersectionFraction must be in (0,1]");
            if (MinRunLength < 1) problems.Add("SubjectEdgeDetector.MinRunLength must be >= 1");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    // --- Public API

    /// <summary> Detects subject-to-edge intersections for the image at <paramref name="imagePath"/>. For JPEG files, attempts to use the embedded EXIF thumbnail to avoid a full decode. </summary>
    public static SubjectEdgeDetectionResult Detect(string imagePath) {
        Config cfg = ConfigLoader.Section<Config>("ClassifyConfig.json", "SubjectEdgeDetector");
        using Image<Rgba32> analysis = AcquireAnalysisImage(imagePath, cfg);
        return DetectOnImage(analysis, cfg);
    }

    /// <summary> Detects subject-to-edge intersections on an image already in memory. When the image exceeds <c>Config.MaxAnalysisSize</c> a scaled-down clone is used internally. Preferred when the caller has already loaded the image to avoid a redundant file read. </summary>
    public static SubjectEdgeDetectionResult Detect(Image<Rgba32> image) {
        Config cfg = ConfigLoader.Section<Config>("ClassifyConfig.json", "SubjectEdgeDetector");
        if (Math.Max(image.Width, image.Height) <= cfg.MaxAnalysisSize) return DetectOnImage(image, cfg);

        using Image<Rgba32> small = ScaleDown(image, cfg);
        return DetectOnImage(small, cfg);
    }

    // --- Image acquisition
    private static Image<Rgba32> AcquireAnalysisImage( string imagePath, Config cfg ) {
        string ext = Path.GetExtension(imagePath);
        bool isJpeg = ext.Equals(".jpg", StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        if (isJpeg) {
            byte[]? thumbBytes = TryExtractJpegExifThumbnail(imagePath);
            if (thumbBytes != null) {
                try {
                    using var ms = new MemoryStream(thumbBytes);
                    var thumb = Image.Load<Rgba32>(ms);
                    if (Math.Min(thumb.Width, thumb.Height) >= cfg.MinEXIFThumbnailSize) {
                        return thumb;   // Fast path: main image never opened.
                    }
                    thumb.Dispose();
                } catch { /* Thumbnail decode failed - use main image.*/
                }
            }
        }

        var full = Image.Load<Rgba32>(imagePath);
        if (Math.Max(full.Width, full.Height) <= cfg.MaxAnalysisSize)
            return full;

        using (full) return ScaleDown(full, cfg);
    }

    private static Image<Rgba32> ScaleDown( Image<Rgba32> source, Config cfg ) {
        float scale = (float) cfg.MaxAnalysisSize / Math.Max(source.Width, source.Height);
        int newW = Math.Max(1, (int) (source.Width * scale));
        int newH = Math.Max(1, (int) (source.Height * scale));
        return source.Clone(ctx => ctx.Resize(newW, newH, KnownResamplers.Box));
    }

    // --- Core detection 

    private static SubjectEdgeDetectionResult DetectOnImage(Image<Rgba32> image, Config cfg) {
        SampleBackground(image, out float bgR, out float bgG, out float bgB);

        int stripPx = Math.Max(2, (int) (Math.Min(image.Width, image.Height) * cfg.StripDepthFraction));

        bool top = StripIntersects(image, 0, 0, image.Width, stripPx, bgR, bgG, bgB, cfg);
        bool bottom = StripIntersects(image, 0, image.Height - stripPx, image.Width, stripPx, bgR, bgG, bgB, cfg);
        bool left = StripIntersects(image, 0, 0, stripPx, image.Height, bgR, bgG, bgB, cfg);
        bool right = StripIntersects(image, image.Width - stripPx, 0, stripPx, image.Height, bgR, bgG, bgB, cfg);

        int count = (top ? 1 : 0) + (bottom ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
        return new SubjectEdgeDetectionResult(top, bottom, left, right, count);
    }

    /// <summary> Counts foreground pixels within a strip that form horizontal runs of at least <c>Config.MinRunLength</c> pixels. The edge is flagged when the qualifying-run total exceeds <c>Config.IntersectionFraction</c> of the strip area. Isolated pixels and short runs (JPEG artifacts, drop-shadow tails) are excluded. </summary>
    private static bool StripIntersects(
        Image<Rgba32> image,
        int x0, int y0, int width, int height,
        float bgR, float bgG, float bgB, Config cfg ) {
        int endX = Math.Min(x0 + width, image.Width);
        int endY = Math.Min(y0 + height, image.Height);
        int totalPixels = (endX - x0) * (endY - y0);
        if (totalPixels <= 0) return false;

        int fgRunPixels = 0;

        for (int y = y0; y < endY; y++) {
            int runLen = 0;
            for (int x = x0; x < endX; x++) {
                Rgba32 px = image[x, y];
                if (px.A >= AlphaOpaqueThreshold && IsForeground(px, bgR, bgG, bgB, cfg)) {
                    runLen++;
                }
                else {
                    CommitRun(ref runLen, ref fgRunPixels, cfg);
                }
            }
            CommitRun(ref runLen, ref fgRunPixels, cfg);
        }

        return (float) fgRunPixels / totalPixels > cfg.IntersectionFraction;
    }

    private static void CommitRun( ref int runLen, ref int fgRunPixels, Config cfg ) {
        if (runLen >= cfg.MinRunLength) {
            fgRunPixels += runLen;
        }
        runLen = 0;
    }

    private static bool IsForeground( Rgba32 px, float bgR, float bgG, float bgB, Config cfg ) {
        float dr = (px.R / MaxChannelValueF) - bgR;
        float dg = (px.G / MaxChannelValueF) - bgG;
        float db = (px.B / MaxChannelValueF) - bgB;
        return MathF.Sqrt(dr * dr + dg * dg + db * db) > cfg.BgColorDiffThreshold;
    }

    // --- Background estimation 

    /// <summary> Samples the outermost 10 % of each image dimension at all four corners to estimate background color. Falls back to white when all corner pixels are transparent. </summary>
    private static void SampleBackground(
        Image<Rgba32> image, out float bgR, out float bgG, out float bgB ) {
        int cw = Math.Max(1, image.Width / 10);
        int ch = Math.Max(1, image.Height / 10);

        float sumR = 0, sumG = 0, sumB = 0;
        int n = 0;

        for (int dy = 0; dy < ch; dy++) {
            for (int dx = 0; dx < cw; dx++) {
                Accumulate(image, dx, dy, ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, image.Width - 1 - dx, dy, ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, dx, image.Height - 1 - dy, ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, image.Width - 1 - dx, image.Height - 1 - dy, ref sumR, ref sumG, ref sumB, ref n);
            }
        }

        if (n == 0) { bgR = bgG = bgB = 1f; return; }
        bgR = sumR / n;
        bgG = sumG / n;
        bgB = sumB / n;
    }

    private static void Accumulate(
        Image<Rgba32> image, int x, int y,
        ref float sumR, ref float sumG, ref float sumB, ref int n ) {
        Rgba32 px = image[x, y];
        if (px.A < AlphaOpaqueThreshold) return;
        sumR += px.R / MaxChannelValueF;
        sumG += px.G / MaxChannelValueF;
        sumB += px.B / MaxChannelValueF;
        n++;
    }

    // --- JPEG EXIF thumbnail extraction 

    /// <summary>Reads the embedded JPEG thumbnail from a JPEG file's EXIF IFD1 block without decoding the main image. Returns <c>null</c> when no thumbnail is present or any parsing step fails - the caller falls back to a full image load.</summary>
    // JPEG/EXIF/TIFF byte markers and offsets below are the file-format spec itself (SOI/EOI/APP1
    // markers, the TIFF magic number 42, tag IDs, field widths) — they will never change, so they
    // stay bare literals rather than named constants that would just restate the spec in English.
#pragma warning disable S109
    internal static byte[]? TryExtractJpegExifThumbnail( string path ) {
        try {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096);

            if (fs.ReadByte() != 0xFF || fs.ReadByte() != 0xD8) return null; // FF D8 = SOI

            while (fs.Position < MaxExifSearchBytes) {
                if (fs.ReadByte() != 0xFF) return null;
                int marker = fs.ReadByte();
                if (marker < 0) return null;

                if (marker == 0xE1) { // APP1
                    byte[]? thumb = TryParseApp1ForThumbnail(fs);
                    if (thumb != null) return thumb;
                    continue; // Non-EXIF APP1: already skipped by TryParseApp1ForThumbnail.
                }

                if (marker == 0xD9) return null; // EOI - no EXIF found.

                // RST0–RST7 (D0-D7) and nested SOI (D8) carry no length field.
                if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0xD8) continue;

                // All other markers: 2-byte big-endian length (includes the 2 bytes).
                int lenHi = fs.ReadByte(), lenLo = fs.ReadByte();
                if (lenHi < 0 || lenLo < 0) return null;
                int segLen = (lenHi << 8) | lenLo;
                if (segLen < 2) return null;
                fs.Seek(segLen - 2, SeekOrigin.Current);
            }

            return null;
        } catch {
            return null;
        }
    }

    ///<summary> Parses one APP1 segment (marker already consumed) for an EXIF IFD1 thumbnail. Skips the segment and returns <c>null</c> when the EXIF signature is absent./// </summary>
    private static byte[]? TryParseApp1ForThumbnail( FileStream fs ) {
        int lenHi = fs.ReadByte(), lenLo = fs.ReadByte();
        if (lenHi < 0 || lenLo < 0) return null;
        int app1Len = (lenHi << 8) | lenLo; // Includes the length field bytes.

        // Read the "Exif\0\0" signature (6 bytes).
        Span<byte> sig = stackalloc byte[6];
        if (fs.Read(sig) != 6) return null;

        ReadOnlySpan<byte> exifSignature = "Exif\0\0"u8;
        if (!sig.SequenceEqual(exifSignature)) {
            // Not an EXIF APP1 - skip the remainder of this segment.
            int remaining = app1Len - 2 - 6;
            if (remaining > 0) fs.Seek(remaining, SeekOrigin.Current);
            return null;
        }

        long tiffBase = fs.Position;

        // TIFF header: byte-order (2) + magic 42 (2) + IFD0 offset (4).
        Span<byte> hdr = stackalloc byte[8];
        if (fs.Read(hdr) != 8) return null;

        bool le = hdr[0] == 0x49; // "II" = little-endian; "MM" = big-endian.
        int magic = le ? BinaryPrimitives.ReadUInt16LittleEndian(hdr[2..])
                       : BinaryPrimitives.ReadUInt16BigEndian(hdr[2..]);
        if (magic != 42) return null;

        int ifd0Offset = le ? BinaryPrimitives.ReadInt32LittleEndian(hdr[4..])
                            : BinaryPrimitives.ReadInt32BigEndian(hdr[4..]);

        // Seek to IFD0, read entry count, skip entries, read IFD1 offset.
        fs.Seek(tiffBase + ifd0Offset, SeekOrigin.Begin);

        Span<byte> u16Buf = stackalloc byte[2];
        if (fs.Read(u16Buf) != 2) return null;
        int ifd0Count = le ? BinaryPrimitives.ReadUInt16LittleEndian(u16Buf)
                           : BinaryPrimitives.ReadUInt16BigEndian(u16Buf);

        fs.Seek(ifd0Count * 12, SeekOrigin.Current); // each IFD entry is 12 bytes

        Span<byte> u32Buf = stackalloc byte[4];
        if (fs.Read(u32Buf) != 4) return null;
        int ifd1Offset = le ? BinaryPrimitives.ReadInt32LittleEndian(u32Buf)
                            : BinaryPrimitives.ReadInt32BigEndian(u32Buf);
        if (ifd1Offset == 0) return null;

        // Parse IFD1 to find JPEGInterchangeFormat (0x0201) and its length (0x0202).
        fs.Seek(tiffBase + ifd1Offset, SeekOrigin.Begin);

        if (fs.Read(u16Buf) != 2) return null;
        int ifd1Count = le ? BinaryPrimitives.ReadUInt16LittleEndian(u16Buf)
                           : BinaryPrimitives.ReadUInt16BigEndian(u16Buf);

        int thumbOffset = -1, thumbLength = -1;
        Span<byte> entry = stackalloc byte[12]; // each IFD entry is 12 bytes

        for (int i = 0; i < ifd1Count; i++) {
            if (fs.Read(entry) != 12) return null;
            int tag = le ? BinaryPrimitives.ReadUInt16LittleEndian(entry)
                         : BinaryPrimitives.ReadUInt16BigEndian(entry);

            if (tag == 0x0201)       // JPEGInterchangeFormat: offset of thumbnail data.
                thumbOffset = le ? BinaryPrimitives.ReadInt32LittleEndian(entry[8..])
                                 : BinaryPrimitives.ReadInt32BigEndian(entry[8..]);
            else if (tag == 0x0202)  // JPEGInterchangeFormatLength: byte count.
                thumbLength = le ? BinaryPrimitives.ReadInt32LittleEndian(entry[8..])
                                 : BinaryPrimitives.ReadInt32BigEndian(entry[8..]);
        }

        if (thumbOffset <= 0 || thumbLength <= 0) return null;

        fs.Seek(tiffBase + thumbOffset, SeekOrigin.Begin);
        byte[] thumb = new byte[thumbLength];
        return fs.Read(thumb, 0, thumbLength) == thumbLength ? thumb : null;
    }
#pragma warning restore S109
}