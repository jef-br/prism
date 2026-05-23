/*
Represents one canonical image through the definitive route:
imported, classified, matched, ordered, renamed, generated, transformed, exported.
Implementation fields are currently provisional.
*/

using System.Net.Sockets;

public class ImageRecord_LAMBDA : ImageRecord_Base
{
    //Matching parameters
    public

    //Analysis parameters

    private sealed record IntersectionData
    {
        public bool Top { get; set; }
        public bool Right { get; set; }
        public bool Bottom { get; set; }
        public bool Left { get; set; }
    }
    public sealed record TagCollection
    {
        public ClassificationToken[] Influential { get; init; } = [];
        public ClassificationToken[] Trivial { get; init; } = [];
    }
    
    
    public IntersectionData Intersection { get; set; } = new();
    public bool IsProductInFullView => Intersection.Top && Intersection.Right && Intersection.Bottom && Intersection.Left;
    
    public TagCollection[] Tags { get; set; } = new();

    public double DetectedSkinToneArea { get; set; }


}
