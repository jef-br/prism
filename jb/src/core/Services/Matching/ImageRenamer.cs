namespace Prism.Services.Matching;

/// <summary>
/// Validates det-slot uniqueness within each matched family and counts successfully renamed images.
/// The output filename for each image is the computed <see cref="ImageRecord_Base.NewName"/> value
/// (<c>FamilyID_det#.jpg</c>); this stage does not transform data, only validates the invariant.
/// Families with colliding det indices are KOd in full and excluded from the renamed count.
/// </summary>
internal static class ImageRenamer
{
    /// <summary>
    /// Runs the Renamed stage over a matched LAMBDA collection.
    /// Skips KO records and records without a family assignment.
    /// Groups accepted records by FamilyID, detects det-slot collisions, and KOs the whole
    /// family on collision.
    /// </summary>
    /// <param name="records">Ordered LAMBDA records.</param>
    /// <returns>Count of successfully renamed images and count of images KO'd by collision.</returns>
    internal static (int OkRenamed, int KoAdded) Run(List<ImageRecord_LAMBDA> records)
    {
        IEnumerable<IGrouping<string, ImageRecord_LAMBDA>> families = records
            .Where(r => !r.IsKo && !string.IsNullOrEmpty(r.Family))
            .GroupBy(r => r.Family);

        int okRenamed = 0;
        int koAdded   = 0;

        foreach (IGrouping<string, ImageRecord_LAMBDA> family in families)
        {
            if (HasDetCollision(family))
                koAdded += KoFamily(family);
            else
                okRenamed += family.Count();
        }

        return (okRenamed, koAdded);
    }

    //  Helpers 

    /// <summary>
    /// Returns true when any two images in the family share the same det-slot index.
    /// A collision means ordering produced an impossible result and the whole family must be KOd.
    /// </summary>
    private static bool HasDetCollision(IEnumerable<ImageRecord_LAMBDA> family) =>
        family.GroupBy(r => r.DetOrder).Any(g => g.Count() > 1);

    /// <summary>
    /// Marks every image in the family as KO with reason code <c>RENAME_COLLISION</c>.
    /// </summary>
    /// <returns>Number of records KO'd.</returns>
    private static int KoFamily(IGrouping<string, ImageRecord_LAMBDA> family)
    {
        int koAdded = 0;
        foreach (ImageRecord_LAMBDA record in family)
        {
            record.IsKo          = true;
            record.KoReasonCode  = "RENAME_COLLISION";
            record.KoSafeMessage = $"Det-slot collision in family '{record.Family}': multiple images share the same det index.";
            koAdded++;
        }
        return koAdded;
    }
}
