using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Services.Matching;

/// <summary>
/// Computes perceptual difference hashes (dHash) for images to identify visual duplicates.
/// A perceptual hash is used instead of an exact/cryptographic hash because it tolerates lossy
/// re-encoding, resizing, and minor edits that would otherwise produce a completely different hash
/// for what is visually the same photo. Groups duplicates within a configurable Hamming distance
/// threshold.
/// </summary>
public sealed class VisualHasher {
    // BitsPerUlong: bit-width of ulong, used to split the 128-bit hash into hi/lo 64-bit halves.
    private const int BitsPerUlong = 64;
    // Single set bit, shifted into position to mark one dHash comparison result.
    private const ulong SingleBitMask = 1UL;

    private readonly int hammingThreshold;

    /// <summary>
    /// Thresholds for VisualHasher, bound from the "VisualHasher" section of ClassifyConfig.json.
    /// No defaults — every value must be present in the JSON or deserialization fails loud.
    /// </summary>
    public sealed class Config : IValidatableConfig {
        /// <summary>dHash sample width in pixels (columns). Wider than the classic 9 so subtle colour differences register more bits.</summary>
        public required int HashWidth { get; init; }

        /// <summary>dHash sample height in pixels (rows).</summary>
        public required int HashHeight { get; init; }

        public void Validate() {
            List<string> problems = [];

            if (HashWidth <= 1) problems.Add("VisualHasher.HashWidth must be > 1");
            if (HashHeight <= 1) problems.Add("VisualHasher.HashHeight must be > 1");

            if (problems.Count > 0) throw new PrismConfigurationException(string.Join("; ", problems));
        }
    }

    /// <summary>
    /// Creates a VisualHasher with the given Hamming distance threshold.
    /// Distance 0 = byte-identical images; up to 8 is typically "visually identical, re-encoded."
    /// </summary>
    /// <param name="hammingThreshold">Maximum Hamming distance to consider two images duplicates.</param>
    public VisualHasher(int hammingThreshold) {
        this.hammingThreshold = hammingThreshold;
    }

    /// <summary>
    /// Computes the 128-bit dHash for a pre-loaded image. Converts to grayscale internally without mutating the source.
    /// Returns UInt128.Zero on error; callers should treat zero as a sentinel meaning "unHashable / singleton."
    /// </summary>
    public static UInt128 ComputeHash(Image<Rgba32> sourceImage) {
        Config cfg = ConfigLoader.Section<Config>("ClassifyConfig.json", "VisualHasher");

        // dHash: resize to HashWidth×HashHeight → compute horizontal pixel differences → 128-bit hash.
        using Image<L8> image = sourceImage.CloneAs<L8>();

        image.Mutate(ctx => ctx.Resize(new ResizeOptions { Size = new Size(cfg.HashWidth, cfg.HashHeight), Mode = ResizeMode.Stretch }));

        ulong lo = 0, hi = 0;
        int bit = 0;

        for (int y = 0; y < cfg.HashHeight; y++) {
            for (int x = 0; x < cfg.HashWidth - 1; x++) {
                if (image[x, y].PackedValue > image[x + 1, y].PackedValue) {
                    if (bit < BitsPerUlong) lo |= SingleBitMask << bit;
                    else hi |= SingleBitMask << (bit - BitsPerUlong);
                }
                bit++;
            }
        }

        return new UInt128(hi, lo);
    }

    /// <summary>Returns the Hamming distance between two 128-bit hashes.</summary>
    public static int HammingDistance(UInt128 a, UInt128 b) {
        UInt128 xor = a ^ b;
        return BitOperations.PopCount((ulong)xor) + BitOperations.PopCount((ulong)(xor >> BitsPerUlong));
    }

    /// <summary>True when the two hashes are within the configured Hamming distance.</summary>
    public bool AreDuplicates(UInt128 hashA, UInt128 hashB) => HammingDistance(hashA, hashB) <= hammingThreshold;

    /// <summary>
    /// Groups images by visual similarity using pre-computed perceptual hashes.
    /// Each group's <see cref="DedupGroup.Canonical"/> is the highest-resolution image;
    /// <see cref="DedupGroup.Duplicates"/> are the lower-resolution copies to suppress.
    /// Two images are only grouped when they are BOTH visually similar AND share the same base filename —
    /// different filenames indicate intentional reuse of the same photograph across distinct products.
    /// Entries with Hash == UInt128.Zero are treated as singletons (unHashable or load error).
    /// </summary>
    public IReadOnlyList<DedupGroup> FindDuplicates(IReadOnlyList<(ImageRecord_INPUT Record, UInt128 Hash)> entries) {
        bool[] consumed = new bool[entries.Count];
        var groups = new List<DedupGroup>();

        for (int i = 0; i < entries.Count; i++) {
            if (consumed[i]) continue;

            var groupMembers = new List<ImageRecord_INPUT> { entries[i].Record };
            consumed[i] = true;

            // Zero hash (no normalized path or hashing error) is never a duplicate.
            if (entries[i].Hash != UInt128.Zero) {
                string nameI = Path.GetFileName(entries[i].Record.InitialFullName);
                for (int j = i + 1; j < entries.Count; j++) {
                    if (!consumed[j]
                        && entries[j].Hash != UInt128.Zero
                        && AreDuplicates(entries[i].Hash, entries[j].Hash)
                        && string.Equals(nameI, Path.GetFileName(entries[j].Record.InitialFullName), StringComparison.OrdinalIgnoreCase)) {
                        groupMembers.Add(entries[j].Record);
                        consumed[j] = true;
                    }
                }
            }

            // Canonical = highest pixel area; ties resolved by original order.
            ImageRecord_INPUT canonical = groupMembers
                .OrderByDescending(r => (long)r.NormalizedWidth * r.NormalizedHeight)
                .First();

            IReadOnlyList<ImageRecord_INPUT> duplicates = groupMembers
                .Where(r => r != canonical)
                .ToList();

            groups.Add(new DedupGroup(canonical, duplicates));
        }

        return groups;
    }
}
