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
    // dHash: resize to 9×8 → compute horizontal pixel differences → 64-bit hash.
    private const int HashWidth = 9;
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
    /// Computes the dHash for an image file.
    /// </summary>
    public ulong ComputeHash(string imagePath)
    {
        using Image<L8> image = Image.Load<L8>(imagePath);
        image.Mutate(ctx => ctx.Resize(new ResizeOptions
        {
            Size = new Size(HashWidth, HashHeight),
            Mode = ResizeMode.Stretch
        }));

        ulong hash = 0;
        int bit = 0;

        for (int y = 0; y < HashHeight; y++)
        {
            for (int x = 0; x < HashWidth - 1; x++)
            {
                if (image[x, y].PackedValue > image[x + 1, y].PackedValue)
                    hash |= 1UL << bit;
                bit++;
            }
        }

        return hash;
    }

    /// <summary>Returns the Hamming distance between two hashes.</summary>
    public static int HammingDistance(ulong a, ulong b) => BitOperations.PopCount(a ^ b);

    /// <summary>True when the two hashes are within the configured Hamming distance.</summary>
    public bool AreDuplicates(ulong hashA, ulong hashB) => HammingDistance(hashA, hashB) <= hammingThreshold;

    /// <summary>
    /// Groups the given images by visual similarity.
    /// Each group's <see cref="DedupGroup.Canonical"/> is the highest-resolution image;
    /// <see cref="DedupGroup.Duplicates"/> are the lower-resolution copies to suppress.
    /// Images with no normalized path are excluded from deduplication and treated as singletons.
    /// </summary>
    public IReadOnlyList<DedupGroup> FindDuplicates(IReadOnlyList<ImageRecord_INPUT> images)
    {
        var entries = new List<(ImageRecord_INPUT Record, ulong Hash, long PixelArea)>();

        foreach (ImageRecord_INPUT record in images)
        {
            if (record.NormalizedJpgPath is null || !File.Exists(record.NormalizedJpgPath))
            {
                entries.Add((record, 0UL, 0L));
                continue;
            }

            try
            {
                ulong hash = ComputeHash(record.NormalizedJpgPath);
                long area = (long)record.NormalizedWidth * record.NormalizedHeight;
                entries.Add((record, hash, area));
            }
            catch
            {
                // Hashing failure: treat as singleton with a unique zero hash.
                entries.Add((record, 0UL, 0L));
            }
        }

        bool[] consumed = new bool[entries.Count];
        var groups = new List<DedupGroup>();

        for (int i = 0; i < entries.Count; i++)
        {
            if (consumed[i]) continue;

            var groupMembers = new List<ImageRecord_INPUT> { entries[i].Record };
            consumed[i] = true;

            // Zero hash (no normalized path or hashing error) is never a duplicate.
            if (entries[i].Hash != 0UL)
            {
                for (int j = i + 1; j < entries.Count; j++)
                {
                    if (!consumed[j] && entries[j].Hash != 0UL && AreDuplicates(entries[i].Hash, entries[j].Hash))
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
