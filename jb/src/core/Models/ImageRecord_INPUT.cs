/*
Represents one source image after import and pre-pipeline normalization.

*/


public class ImageRecord_INPUT : ImageRecord_Base
{
    public string[] StringTokens { get; set; } = [];
    public string[] NumericTokens { get; set; } = [];
    public ClassificationToken[] ClassificationTokens { get; set; } = [];

    //MatchEvidence.cs is used to store pieces of evidence. They link tokens with the internal excel model.

    public FamilyRecord[] FamilyIDCandidates { get; set; } = [];
}
