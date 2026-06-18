using System.Buffers.Binary;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

[assembly: System.Runtime.CompilerServices.InternalsVisibleTo("Prism.Core.Tests")]

/// <summary>
/// Detects whether the image subject intersects one or more image boundaries.
/// <para>
/// Fast path (JPEG only): parses the EXIF APP1 block to extract the embedded IFD1 thumbnail
/// without decoding the main image. Falls back to a full load capped at
/// <c>MaxAnalysisSize</c> on the longest side.
/// </para>
/// <para>
/// All tuning constants are compile-time values; no runtime configuration is required.
/// </para>
/// </summary>
public static class SubjectEdgeDetector
{
    // Longest side (px) of the image used for analysis in the fallback path.
    private const int MaxAnalysisSize = 512;
    // EXIF thumbnails below this size on their shortest side are too small to be reliable.
    private const int MinThumbnailSize = 64;
    // Stop searching for the EXIF APP1 block past this file offset.
    private const int MaxExifSearchBytes = 65536;

    // Border strip depth = this fraction of min(W, H) of the analysis image.
    private const float StripDepthFraction = 0.08f;
    // Euclidean RGB distance that marks a pixel as foreground relative to detected background.
    private const float BgColorDiffThreshold = 0.15f;
    // Fraction of strip pixels that must belong to qualifying foreground runs to flag an intersection.
    private const float IntersectionFraction = 0.20f;
    // Minimum consecutive foreground pixels in a row to count as subject contact (noise filter).
    private const int MinRunLength = 3;

    // ─── Public API ──────────────────────────────────────────────────────────

    /// <summary>
    /// Detects subject-to-edge intersections for the image at <paramref name="imagePath"/>.
    /// For JPEG files, attempts to use the embedded EXIF thumbnail to avoid a full decode.
    /// </summary>
    public static EdgeIntersectionResult Detect(string imagePath)
    {
        using Image<Rgba32> analysis = AcquireAnalysisImage(imagePath);
        return DetectOnImage(analysis);
    }

    /// <summary>
    /// Detects subject-to-edge intersections on an image already in memory.
    /// When the image exceeds <c>MaxAnalysisSize</c> a scaled-down clone is used internally.
    /// Preferred when the caller has already loaded the image to avoid a redundant file read.
    /// </summary>
    public static EdgeIntersectionResult Detect(Image<Rgba32> image)
    {
        if (Math.Max(image.Width, image.Height) <= MaxAnalysisSize)
            return DetectOnImage(image);

        using Image<Rgba32> small = ScaleDown(image);
        return DetectOnImage(small);
    }

    // ─── Image acquisition ───────────────────────────────────────────────────

    private static Image<Rgba32> AcquireAnalysisImage(string imagePath)
    {
        string ext = Path.GetExtension(imagePath);
        bool isJpeg = ext.Equals(".jpg",  StringComparison.OrdinalIgnoreCase)
                   || ext.Equals(".jpeg", StringComparison.OrdinalIgnoreCase);

        if (isJpeg)
        {
            byte[]? thumbBytes = TryExtractJpegExifThumbnail(imagePath);
            if (thumbBytes != null)
            {
                try
                {
                    using var ms = new MemoryStream(thumbBytes);
                    var thumb = Image.Load<Rgba32>(ms);
                    if (Math.Min(thumb.Width, thumb.Height) >= MinThumbnailSize)
                        return thumb;   // Fast path: main image never opened.
                    thumb.Dispose();
                }
                catch
                {
                    // Thumbnail decode failed — fall through to full load.
                }
            }
        }

        var full = Image.Load<Rgba32>(imagePath);
        if (Math.Max(full.Width, full.Height) <= MaxAnalysisSize)
            return full;

        using (full)
            return ScaleDown(full);
    }

    private static Image<Rgba32> ScaleDown(Image<Rgba32> source)
    {
        float scale = (float)MaxAnalysisSize / Math.Max(source.Width, source.Height);
        int newW = Math.Max(1, (int)(source.Width  * scale));
        int newH = Math.Max(1, (int)(source.Height * scale));
        return source.Clone(ctx => ctx.Resize(newW, newH, KnownResamplers.Box));
    }

    // ─── Core detection ──────────────────────────────────────────────────────

    private static EdgeIntersectionResult DetectOnImage(Image<Rgba32> image)
    {
        SampleBackground(image, out float bgR, out float bgG, out float bgB);

        int stripPx = Math.Max(2, (int)(Math.Min(image.Width, image.Height) * StripDepthFraction));

        bool top    = StripIntersects(image, 0,                     0,                       image.Width,  stripPx,       bgR, bgG, bgB);
        bool bottom = StripIntersects(image, 0,                     image.Height - stripPx,   image.Width,  stripPx,       bgR, bgG, bgB);
        bool left   = StripIntersects(image, 0,                     0,                       stripPx,      image.Height,  bgR, bgG, bgB);
        bool right  = StripIntersects(image, image.Width - stripPx, 0,                       stripPx,      image.Height,  bgR, bgG, bgB);

        int count = (top ? 1 : 0) + (bottom ? 1 : 0) + (left ? 1 : 0) + (right ? 1 : 0);
        return new EdgeIntersectionResult(top, bottom, left, right, count);
    }

    /// <summary>
    /// Counts foreground pixels within a strip that form horizontal runs of at least
    /// <c>MinRunLength</c> pixels. The edge is flagged when the qualifying-run total
    /// exceeds <c>IntersectionFraction</c> of the strip area.
    /// Isolated pixels and short runs (JPEG artifacts, drop-shadow tails) are excluded.
    /// </summary>
    private static bool StripIntersects(
        Image<Rgba32> image,
        int x0, int y0, int width, int height,
        float bgR, float bgG, float bgB)
    {
        int endX = Math.Min(x0 + width,  image.Width);
        int endY = Math.Min(y0 + height, image.Height);
        int totalPixels = (endX - x0) * (endY - y0);
        if (totalPixels <= 0) return false;

        int fgRunPixels = 0;

        for (int y = y0; y < endY; y++)
        {
            int runLen = 0;
            for (int x = x0; x < endX; x++)
            {
                Rgba32 px = image[x, y];
                if (px.A >= 128 && IsForeground(px, bgR, bgG, bgB))
                {
                    runLen++;
                }
                else
                {
                    CommitRun(ref runLen, ref fgRunPixels);
                }
            }
            CommitRun(ref runLen, ref fgRunPixels);
        }

        return (float)fgRunPixels / totalPixels > IntersectionFraction;
    }

    private static void CommitRun(ref int runLen, ref int fgRunPixels)
    {
        if (runLen >= MinRunLength)
            fgRunPixels += runLen;
        runLen = 0;
    }

    private static bool IsForeground(Rgba32 px, float bgR, float bgG, float bgB)
    {
        float dr = (px.R / 255f) - bgR;
        float dg = (px.G / 255f) - bgG;
        float db = (px.B / 255f) - bgB;
        return MathF.Sqrt(dr * dr + dg * dg + db * db) > BgColorDiffThreshold;
    }

    // ─── Background estimation ───────────────────────────────────────────────

    /// <summary>
    /// Samples the outermost 10 % of each image dimension at all four corners
    /// to estimate background color. Falls back to white when all corner pixels are transparent.
    /// </summary>
    private static void SampleBackground(
        Image<Rgba32> image, out float bgR, out float bgG, out float bgB)
    {
        int cw = Math.Max(1, image.Width  / 10);
        int ch = Math.Max(1, image.Height / 10);

        float sumR = 0, sumG = 0, sumB = 0;
        int n = 0;

        for (int dy = 0; dy < ch; dy++)
        {
            for (int dx = 0; dx < cw; dx++)
            {
                Accumulate(image, dx,                    dy,                    ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, image.Width  - 1 - dx, dy,                    ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, dx,                    image.Height - 1 - dy, ref sumR, ref sumG, ref sumB, ref n);
                Accumulate(image, image.Width  - 1 - dx, image.Height - 1 - dy, ref sumR, ref sumG, ref sumB, ref n);
            }
        }

        if (n == 0) { bgR = bgG = bgB = 1f; return; }
        bgR = sumR / n;
        bgG = sumG / n;
        bgB = sumB / n;
    }

    private static void Accumulate(
        Image<Rgba32> image, int x, int y,
        ref float sumR, ref float sumG, ref float sumB, ref int n)
    {
        Rgba32 px = image[x, y];
        if (px.A < 128) return;
        sumR += px.R / 255f;
        sumG += px.G / 255f;
        sumB += px.B / 255f;
        n++;
    }

    // ─── JPEG EXIF thumbnail extraction ──────────────────────────────────────

    /// <summary>
    /// Reads the embedded JPEG thumbnail from a JPEG file's EXIF IFD1 block without
    /// decoding the main image. Returns <c>null</c> when no thumbnail is present or
    /// any parsing step fails — the caller falls back to a full image load.
    /// </summary>
    internal static byte[]? TryExtractJpegExifThumbnail(string path)
    {
        try
        {
            using var fs = new FileStream(path, FileMode.Open, FileAccess.Read,
                FileShare.Read, bufferSize: 4096);

            if (fs.ReadByte() != 0xFF || fs.ReadByte() != 0xD8) return null;

            while (fs.Position < MaxExifSearchBytes)
            {
                if (fs.ReadByte() != 0xFF) return null;
                int marker = fs.ReadByte();
                if (marker < 0) return null;

                if (marker == 0xE1)
                {
                    byte[]? thumb = TryParseApp1ForThumbnail(fs);
                    if (thumb != null) return thumb;
                    continue; // Non-EXIF APP1: already skipped by TryParseApp1ForThumbnail.
                }

                if (marker == 0xD9) return null; // EOI — no EXIF found.

                // RST0–RST7 and nested SOI carry no length field.
                if ((marker >= 0xD0 && marker <= 0xD7) || marker == 0xD8) continue;

                // All other markers: 2-byte big-endian length (includes the 2 bytes).
                int lenHi = fs.ReadByte(), lenLo = fs.ReadByte();
                if (lenHi < 0 || lenLo < 0) return null;
                int segLen = (lenHi << 8) | lenLo;
                if (segLen < 2) return null;
                fs.Seek(segLen - 2, SeekOrigin.Current);
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Parses one APP1 segment (marker already consumed) for an EXIF IFD1 thumbnail.
    /// Skips the segment and returns <c>null</c> when the EXIF signature is absent.
    /// </summary>
    private static byte[]? TryParseApp1ForThumbnail(FileStream fs)
    {
        int lenHi = fs.ReadByte(), lenLo = fs.ReadByte();
        if (lenHi < 0 || lenLo < 0) return null;
        int app1Len = (lenHi << 8) | lenLo; // Includes the 2 length bytes.

        // Read the "Exif\0\0" signature (6 bytes).
        Span<byte> sig = stackalloc byte[6];
        if (fs.Read(sig) != 6) return null;

        if (sig[0] != 'E' || sig[1] != 'x' || sig[2] != 'i' || sig[3] != 'f' || sig[4] != 0 || sig[5] != 0)
        {
            // Not an EXIF APP1 — skip the remainder of this segment.
            int remaining = app1Len - 2 - 6; // subtract length field (2) and sig (6)
            if (remaining > 0) fs.Seek(remaining, SeekOrigin.Current);
            return null;
        }

        long tiffBase = fs.Position;

        // TIFF header: byte-order (2) + magic 42 (2) + IFD0 offset (4).
        Span<byte> hdr = stackalloc byte[8];
        if (fs.Read(hdr) != 8) return null;

        bool le  = hdr[0] == 0x49; // "II" = little-endian; "MM" = big-endian.
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

        fs.Seek(ifd0Count * 12L, SeekOrigin.Current); // 12 bytes per IFD entry.

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
        Span<byte> entry = stackalloc byte[12];

        for (int i = 0; i < ifd1Count; i++)
        {
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
}
