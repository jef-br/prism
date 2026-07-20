namespace Prism.Api;

/// <summary>
/// Safe public limits derived from PRISM configuration.
/// </summary>
internal sealed record PrismSafeLimitResponse {
    public long MaximumRequestBytes { get; init; }
    public int MinimumImageCount { get; init; }
    public int MaximumImageCount { get; init; }
    public long MinimumImageBytes { get; init; }
    public long MaximumImageBytes { get; init; }
    public int MinimumExcelCount { get; init; }
    public int MaximumExcelCount { get; init; }
    public long MinimumExcelBytes { get; init; }
    public long MaximumExcelBytes { get; init; }
    public int MaximumZipCount { get; init; }
    public long MaximumZipBytes { get; init; }
}
