using System.Numerics;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;

namespace Prism.Core;

/// <summary>
/// Computes perceptual difference hashes (dHash) for images to identify visual duplicates.
/// Groups duplicates within a configurable Hamming distance threshold.
/// </summary>
public sealed class VisualHasher
{
    // dHash: resize to 17×8 → compute horizontal pixel differences → 128-bit hash (16 cols × 8 rows).
    // Wider than the original 9×8 so subtle colour differences (e.g. shirt colour) register more bits.
    private const int HashWidth  = 17;
    private const int HashHeight = 8;

    private readonly int hammingThreshold;

    /// <summary>
    /// Creates a VisualHasher with the given Hamming distance threshold.
    /// Distance 0 = byte-identical images; up to 8 is typically "visually identical, re-encoded."
    /// </summary>
    /// <param name="hammingThreshold">Maximum Hamming distance to consider two images duplicates.</param>
    public VisualHasher(int hammingThreshold = 6)
    {
        this.hammingThreshold = hammingThreshold;
    }

    /// <summary>
    /// Computes the 128-bit dHash for a pre-loaded image. Converts to grayscale internally without mutating the source.
    /// Returns UInt128.Zero on error; callers should treat zero as a sentinel meaning "unHashable / singleton."
    /// </summary>
    public static UInt128 ComputeHash(Image<Rgba32> sourceImage)
    {
        using Image<L8> image = sourceImage.CloneAs<L8>();
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(HashWidth, HashHeight),
            Mode = ResizeMode.Stretch
        }));

        ulong lo = 0, hi = 0;
        int bit = 0;

        for (int y = 0; y < HashHeight; y++)
        {
            for (int x = 0; x < HashWidth - 1; x++)
            {
                if (image[x, y].PackedValue > image[x + 1, y].PackedValue)
                {
                    if (bit < 64) lo |= 1UL << bit;
                    else          hi |= 1UL << (bit - 64);
                }
                bit++;
            }
        }

        return new UInt128(hi, lo);
    }

    /// <summary>Returns the Hamming distance between two 128-bit hashes.</summary>
    public static int HammingDistance(UInt128 a, UInt128 b)
    {
        UInt128 xor = a ^ b;
        return BitOperations.PopCount((ulong)xor) + BitOperations.PopCount((ulong)(xor >> 64));
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
    public IReadOnlyList<DedupGroup> FindDuplicates(IReadOnlyList<(ImageRecord_INPUT Record, UInt128 Hash)> entries)
    {
        bool[] consumed = new bool[entries.Count];
        var groups = new List<DedupGroup>();

        for (int i = 0; i < entries.Count; i++)
        {
            if (consumed[i]) continue;

            var groupMembers = new List<ImageRecord_INPUT> { entries[i].Record };
            consumed[i] = true;

            // Zero hash (no normalized path or hashing error) is never a duplicate.
            if (entries[i].Hash != UInt128.Zero)
            {
                string nameI = Path.GetFileName(entries[i].Record.InitialFullName);
                for (int j = i + 1; j < entries.Count; j++)
                {
                    if (!consumed[j]
                        && entries[j].Hash != UInt128.Zero
                        && AreDuplicates(entries[i].Hash, entries[j].Hash)
                        && string.Equals(nameI, Path.GetFileName(entries[j].Record.InitialFullName), StringComparison.OrdinalIgnoreCase))
                    {
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
