/*
Represents the combined matching decision for one source image.


It is important to keep the MatchEvidence class usable by multiple consumers
because MatchEvidence can be used in many locations (matching, ordering, transformation, generation, image collection refinment)

It is used by ImageMatcher.cs to perform the matching of images to familyIDs found inside IEM.

*/

public sealed class MatchEvidence
{
    public string ImageId { get; init; }

    public string ExcelTokenId { get; init; }   // reference into ExcelTokenStore

    public double Score { get; init; }

    public MatchType Type { get; init; }

    public List<string> Signals { get; init; } = new();
}