namespace Prism.Contracts;

/// <summary>
/// Axis-aligned rectangle identifying a region of interest within an image, in pixels.
/// Used to describe salient object bounds and crop coordinates.
/// </summary>
public struct BoundingBox {
    /// <summary>Left edge offset from the image origin (pixels).</summary>
    public int X { get; set; }

    /// <summary>Top edge offset from the image origin (pixels).</summary>
    public int Y { get; set; }

    /// <summary>Rectangle width in pixels.</summary>
    public int Width { get; set; }

    /// <summary>Rectangle height in pixels.</summary>
    public int Height { get; set; }

    /// <summary>Top edge coordinate (equals <see cref="Y"/>).</summary>
    public int Top { get; set; }

    /// <summary>Left edge coordinate (equals <see cref="X"/>).</summary>
    public int Left { get; set; }

    /// <summary>Bottom edge coordinate (Y + Height).</summary>
    public int Bottom { get; set; }

    /// <summary>Right edge coordinate (X + Width).</summary>
    public int Right { get; set; }
}
