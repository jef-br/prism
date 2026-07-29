namespace Prism.Services.Transform;

/// <summary>
/// The single source of truth for "how many pixels will the Transformed stage actually emit?".
/// Upscale (<c>ImagePreProcessor.UpscaleAsync</c>) has to know the exact final size before Transform
/// runs, and Transform has to produce exactly that size — so both read the canvas geometry from here
/// rather than each carrying its own copy of the formula.
/// <para>
/// Two routings, matching <see cref="ImageTransformer"/>'s selection order: a bbox that touches no
/// image edge goes to <see cref="Tx_CenterAndStretch"/> and its output is the margined canvas around
/// the bbox; anything else goes to <see cref="Tx_CropSquare"/> and its output is the centered square,
/// with no margin term (T-4900 decision 4).
/// </para>
/// </summary>
public static class FinalOutputSize {
    // Antialiasing safety trim taken off the centre-and-stretch canvas after rounding it even.
    private const int CanvasAntialiasTrimPx = 2;

    // The whitespace margin is added on both sides of the bounding box, so it counts twice.
    private const double MarginSideCount = 2.0;

    /// <summary>True when any of the four intersect features says the subject touches an image edge.</summary>
    public static bool HasEdgeIntersect(ImageFeatureSnapshot features) {
        return features.GetValue("intersects-top") == "true" || features.GetValue("intersects-bottom") == "true"
            || features.GetValue("intersects-left") == "true" || features.GetValue("intersects-right") == "true";
    }

    /// <summary>
    /// True when this record routes to <see cref="Tx_CenterAndStretch"/>. Holds in both phenotype modes:
    /// an intersecting bbox goes to the detail cropper or the square crop, never to centre-and-stretch.
    /// </summary>
    public static bool RoutesToCenterAndStretch(ImageRecord_LAMBDA lambda) {
        return lambda.BoundingBox is not null && !HasEdgeIntersect(lambda.Features);
    }

    /// <summary>
    /// The square canvas <see cref="Tx_CenterAndStretch"/> builds around a bbox of the given longest
    /// side: the margined size floored, rounded down to even, then trimmed by the antialiasing margin.
    /// Worked example — longest side 1800 at margin 0.042 gives 1948.
    /// </summary>
    public static int CenterAndStretchCanvasSize(int bboxLongestSide, double whiteSpaceMargin) {
        int flooredRaw = (int)Math.Floor(bboxLongestSide * (1.0 + MarginSideCount * whiteSpaceMargin));
        int evenRaw = flooredRaw - (flooredRaw % 2);
        return evenRaw - CanvasAntialiasTrimPx;
    }

    /// <summary>
    /// The exact longest dimension of the image the Transformed stage will write for this record,
    /// given the pixel dimensions of the bytes it will receive. Both routings emit a square, so this
    /// is the full output size, not an estimate.
    /// </summary>
    public static int LongestDimension(ImageRecord_LAMBDA lambda, int imageWidth, int imageHeight, double whiteSpaceMargin) {
        if (!RoutesToCenterAndStretch(lambda)) return Math.Min(imageWidth, imageHeight);

        BoundingBox bbox = lambda.BoundingBox!.Value;
        return CenterAndStretchCanvasSize(Math.Max(bbox.Width, bbox.Height), whiteSpaceMargin);
    }

    /// <summary>
    /// The smallest uniform image scale that makes <see cref="LongestDimension"/> reach
    /// <paramref name="targetLongestDimension"/> — 1.0 when the record already clears it. The caller
    /// enlarges the pixels and the geometry by this factor together, so the returned scale is exact,
    /// not a prediction.
    /// </summary>
    public static double MinimalScaleToReach(int targetLongestDimension, ImageRecord_LAMBDA lambda, int imageWidth, int imageHeight, double whiteSpaceMargin) {
        if (LongestDimension(lambda, imageWidth, imageHeight, whiteSpaceMargin) >= targetLongestDimension) return 1.0;

        // Square-crop routing: the output side is the image's shorter side, so the image scales directly
        // to the bar. No margin term — a bleeding subject gets no whitespace added around it.
        if (!RoutesToCenterAndStretch(lambda)) return targetLongestDimension / (double)Math.Min(imageWidth, imageHeight);

        BoundingBox bbox = lambda.BoundingBox!.Value;
        int currentLongestSide = Math.Max(bbox.Width, bbox.Height);
        return RequiredBboxLongestSide(targetLongestDimension, whiteSpaceMargin) / (double)currentLongestSide;
    }

    // Smallest bbox longest side whose canvas reaches the target. The continuous inverse of the canvas
    // formula gives a value that is never above the answer (the floor/even/trim steps only ever shrink
    // the canvas), so stepping up from one below it lands on the exact minimum in a handful of passes —
    // and can never come out a pixel short of the bar the way solving the algebra by hand would.
    private static int RequiredBboxLongestSide(int targetLongestDimension, double whiteSpaceMargin) {
        double continuousInverse = (targetLongestDimension + CanvasAntialiasTrimPx) / (1.0 + MarginSideCount * whiteSpaceMargin);
        int side = Math.Max(1, (int)Math.Floor(continuousInverse) - 1);
        while (CenterAndStretchCanvasSize(side, whiteSpaceMargin) < targetLongestDimension) side++;
        return side;
    }
}
