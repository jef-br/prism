/// <summary>
/// Validates det-slot uniqueness within each matched family and counts successfully renamed images.
/// The output filename for each image is the computed <see cref="ImageRecord_Base.NewName"/> value
/// (<c>FamilyID_det#.jpg</c>); this stage does not transform data, only validates the invariant.
/// Families with colliding det indices are KOd in full and excluded from the renamed count.
/// </summary>
internal static class ImageRenamer
{
    /// <summary>
    /// Runs the Renamed stage for a job context.
    /// Skips KO records and records without a family assignment.
    /// Groups accepted records by FamilyID, detects det-slot collisions, KOs the whole
    /// family on collision, and increments <see cref="PipelineContext.OkRenamedCount"/>.
    /// </summary>
    /// <param name="context">Mutable per-job pipeline context.</param>
    internal static void Run(PipelineContext context)
    {
        IEnumerable<IGrouping<string, ImageRecord_LAMBDA>> families = context.LambdaRecords
            .Where(r => !r.IsKo && !string.IsNullOrEmpty(r.Family))
            .GroupBy(r => r.Family);

        foreach (IGrouping<string, ImageRecord_LAMBDA> family in families)
        {
            if (HasDetCollision(family))
                KoFamily(family, context);
            else
                context.OkRenamedCount += family.Count();
        }
    }

    // ─── Helpers ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Returns true when any two images in the family share the same det-slot index.
    /// A collision means ordering produced an impossible result and the whole family must be KOd.
    /// </summary>
    private static bool HasDetCollision(IEnumerable<ImageRecord_LAMBDA> family) =>
        family.GroupBy(r => r.DetOrder).Any(g => g.Count() > 1);

    /// <summary>
    /// Marks every image in the family as KO with reason code <c>RENAME_COLLISION</c>.
    /// </summary>
    private static void KoFamily(IGrouping<string, ImageRecord_LAMBDA> family, PipelineContext context)
    {
        foreach (ImageRecord_LAMBDA record in family)
        {
            record.IsKo          = true;
            record.KoReasonCode  = "RENAME_COLLISION";
            record.KoSafeMessage = $"Det-slot collision in family '{record.Family}': multiple images share the same det index.";
            context.KoRecordCount++;
        }
    }
}
