namespace Prism.Contracts;

/*
Represents an image generated to supplement an existing FamilyID image collection.

*/

public enum GenerationMethod { DetailCrop, GenAiBackgroundVariation, Both }
public enum GenerationStatus { Gated, Created, Failed }

public class ImageRecord_GENERATED : ImageRecord_Base {
    public string SourceFamilyId { get; set; } = string.Empty;
    public string SourceHeroImageName { get; set; } = string.Empty;
    public GenerationMethod Method { get; set; }
    public GenerationStatus Status { get; set; }
    public string? ParamsSummary { get; set; }   // safe config snapshot
    public bool QualityAccepted { get; set; }
    public string? OutputImagePath { get; set; }   // set when Created
    public string? SafeDiagnostics { get; set; }
}
