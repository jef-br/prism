namespace Prism.Api;



/// <summary>
/// JSON result envelope matching the documented top-level shape.
/// </summary>
internal sealed record PrismJsonResultEnvelope(PrismJobResult? Result) {
    public BatchManifest? Manifest => this.Result?.Manifest;
    public PrismJsonImagesEnvelope Images => new() {
        Ok = this.Result?.OkImages ?? [],
        Ko = this.Result?.KoImages ?? []
    };
    public object? OriginalImages => null;
}
