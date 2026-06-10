/*
Represents one canonical image through the definitive route:
imported, classified, matched, ordered, renamed, generated, transformed, exported.
Implementation fields are currently provisional.
*/

public class ImageRecord_LAMBDA : ImageRecord_Base
{
    //Matching parameters

    //Analysis parameters
    public sealed record IntersectionData
    {
        public bool Top { get; set; }
        public bool Right { get; set; }
        public bool Bottom { get; set; }
        public bool Left { get; set; }
    }

    //Image Labelling/classification tags
    public sealed record TagCollection
    {
        public ClassificationToken[] Influential { get; init; } = [];
        public ClassificationToken[] Trivial { get; init; } = [];
    }
    public TagCollection[] VisionTags { get; set; } = [];

    public IntersectionData Intersection { get; set; } = new();
    public bool IsProductInFullView => Intersection.Top && Intersection.Right && Intersection.Bottom && Intersection.Left;

    public double DetectedSkinToneArea { get; set; }
}
