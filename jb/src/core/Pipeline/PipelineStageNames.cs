namespace Prism.Core;

/// <summary>
/// Definitive stage name constants. Matches the immutable pipeline stage order exactly.
/// </summary>
internal static class PipelineStageNames
{
    internal const string Imported   = "Imported";
    internal const string Classified = "Classified";
    internal const string Matched    = "Matched";
    internal const string Ordered    = "Ordered";
    internal const string Renamed    = "Renamed";
    internal const string Generated  = "Generated";
    internal const string Transformed = "Transformed";
    internal const string Exported   = "Exported";
}
