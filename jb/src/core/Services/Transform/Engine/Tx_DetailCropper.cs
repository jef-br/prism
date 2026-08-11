using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Crops the image to a square using the bbox's edge-touched pattern as a gravitational anchor:
/// a touched edge stays flush (plus <see cref="CropTransformSettings.WhiteSpaceMargin"/> on the far
/// edge for the single-intersection case), and every axis without a touched edge centers on the bbox.
/// Each axis independently shrinks (crop, bbox-preserving) when possible, otherwise extends through
/// <see cref="Tx_util_BgStretch"/> — this class never reimplements fill mechanics locally.
/// Supports optional headcut placement via <see cref="Tx_util_HeadCutter"/>.
/// </summary>
public class Tx_DetailCropper : IImageTransformation {
    private readonly bool _headcut;
    private readonly Mat? _colorMat;
    private readonly CropTransformSettings _crop;
    private readonly BgStretchConfig _bgStretch;
    private readonly HeadCutterConfig _headCutter;

    /// <summary>Creates the transformer with the headcut flag, pre-decoded BGR Mat, and config sections.</summary>
    public Tx_DetailCropper(bool headcut, Mat? colorMat, CropTransformSettings crop, BgStretchConfig bgStretch, HeadCutterConfig headCutter) {
        this._headcut = headcut;
        this._colorMat = colorMat;
        this._crop = crop;
        this._bgStretch = bgStretch;
        this._headCutter = headCutter;
    }

    /// <inheritdoc/>
    public ImageRecord_LAMBDA Transform(ImageRecord_LAMBDA InputImage) {
        if (this._headcut && this._colorMat is not null) Tx_util_HeadCutter.Analyze(InputImage, this._colorMat, this._headCutter);

        byte[]? bytes = InputImage.ProcessedBytes;
        if (bytes is null) {
            InputImage.OutputRecord = new ImageRecord_OUTPUT {
                TransformStatus = TransformationStatus.Ko,
                TransformerType = nameof(Tx_DetailCropper),
                InputWidth = InputImage.Width,
                InputHeight = InputImage.Height,
                FailureReason = "ProcessedBytes is null.",
                SafeSummaryText = "Detail crop skipped: no preprocessed bytes."
            };
            return InputImage;
        }

        BoundingBox bbox = InputImage.BoundingBox!.Value;   // null-bbox routed to Tx_ProblemImageProcessor

        using Mat decoded = Cv2.ImDecode(bytes, ImreadModes.Color);
        int imgW = decoded.Cols, imgH = decoded.Rows;

        EdgeIntersects intersects = new(
            InputImage.Features.GetValue("intersects-top") == "true",
            InputImage.Features.GetValue("intersects-bottom") == "true",
            InputImage.Features.GetValue("intersects-left") == "true",
            InputImage.Features.GetValue("intersects-right") == "true");

        (byte[] result, int side, bool extended, string warning) = this.ApplyDecisionTree(bytes, bbox, imgW, imgH, intersects);
        InputImage.ProcessedBytes = result;

        var warnings = new System.Collections.Generic.List<string> { warning };
        if (this._headcut) warnings.Add("Headcut applied.");

        InputImage.OutputRecord = new ImageRecord_OUTPUT {
            TransformStatus = TransformationStatus.Ok,
            TransformerType = nameof(Tx_DetailCropper),
            InputWidth = InputImage.Width,
            InputHeight = InputImage.Height,
            OutputWidth = side,
            OutputHeight = side,
            ResizeMode = "none",
            ScaleFactor = 1.0,
            BackgroundFillMethod = extended ? "background-stretch" : string.Empty,
            Warnings = [.. warnings],
            SafeSummaryText = "Detail crop applied."
        };

        return InputImage;
    }

    /// <inheritdoc/>
    public byte[] Process(byte[] arr, int stride, float upscale_factor, ImageRecord_LAMBDA? lambda = null) {
        using Mat decoded = Cv2.ImDecode(arr, ImreadModes.Color);
        int imgW = decoded.Empty() ? 1 : decoded.Cols;
        int imgH = decoded.Empty() ? 1 : decoded.Rows;

        BoundingBox bbox;
        EdgeIntersects intersects;
        if (lambda is not null) {
            bbox = lambda.BoundingBox!.Value;
            intersects = new EdgeIntersects(
                lambda.Features.GetValue("intersects-top") == "true",
                lambda.Features.GetValue("intersects-bottom") == "true",
                lambda.Features.GetValue("intersects-left") == "true",
                lambda.Features.GetValue("intersects-right") == "true");
        }
        else {
            // ImagePreProcessor lives in Prism.Core, which itself depends on this project
            // (Prism.Services.Transform) — it cannot be referenced back from here without a
            // circular project reference. Tx_CenterAndStretch.Process() establishes the precedent
            // for this constraint: self-derive full-frame bounds locally instead. This treats the
            // no-lambda caller as the degenerate 0-intersection case (whole frame is the subject).
            bbox = FullFrameBounds(imgW, imgH);
            intersects = new EdgeIntersects(
                bbox.Left <= 0, bbox.Right >= imgW, bbox.Top <= 0, bbox.Bottom >= imgH);
        }

        (byte[] result, int side, _, _) = this.ApplyDecisionTree(arr, bbox, imgW, imgH, intersects);

        if (upscale_factor is not 0f and not 1f) {
            int scaledSide = (int)Math.Round(side * upscale_factor);
            using Mat canvas = Cv2.ImDecode(result, ImreadModes.Color);
            using Mat scaled = new();
            Cv2.Resize(canvas, scaled, new OpenCvSharp.Size(scaledSide, scaledSide),
                interpolation: InterpolationFlags.Lanczos4);
            Cv2.ImEncode(".jpg", scaled, out byte[] scaledBytes);
            return scaledBytes;
        }

        return result;
    }

    //  Decision tree

    /// <summary>Edge-intersection pattern for the salient bounding box against the current frame.</summary>
    private readonly record struct EdgeIntersects(bool Top, bool Bottom, bool Left, bool Right) {
        public int Count => (this.Top ? 1 : 0) + (this.Bottom ? 1 : 0) + (this.Left ? 1 : 0) + (this.Right ? 1 : 0);
    }

    /// <summary> Routes to the branch matching the bbox's edge-intersection count/pattern and returns
    /// the resulting JPEG bytes, the final square canvas side, whether <see cref="Tx_util_BgStretch"/>
    /// was used to extend the canvas, and a diagnostic warning describing which branch fired. </summary>
    private (byte[] bytes, int side, bool extended, string warning) ApplyDecisionTree(
        byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        // Dispatches on EdgeIntersects.Count, a literal count of touched edges (0-4) — the case
        // labels are that count, not tunable thresholds. 0 never reaches this class — the
        // no-intersect gate in ImageTransformer.SelectTransformer routes those to Tx_CenterAndStretch.
#pragma warning disable S109
        return intersects.Count switch {
            1 => this.OneEdge(sourceJpeg, bbox, imgW, imgH, intersects),
            2 when intersects.Top && intersects.Bottom => this.TwoOpposing(sourceJpeg, bbox, imgW, imgH, verticalPinned: true),
            2 when intersects.Left && intersects.Right => this.TwoOpposing(sourceJpeg, bbox, imgW, imgH, verticalPinned: false),
            2 => this.TwoAdjacent(sourceJpeg, bbox, imgW, imgH, intersects),
            3 => this.ThreeEdges(sourceJpeg, bbox, imgW, imgH, intersects),
            _ => this.FourEdges(sourceJpeg, bbox, imgW, imgH)
        };
#pragma warning restore S109
    }

    // 1 edge — touched edge anchors with margin on the far edge; the free (perpendicular) axis
    // centers on the bbox, shrinking or extending as needed to reach the same square side.
    private (byte[], int, bool, string) OneEdge(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        bool verticalTouch = intersects.Top || intersects.Bottom;
        string touchedEdge = intersects.Top ? "top" : intersects.Bottom ? "bottom" : intersects.Left ? "left" : "right";

        Axis touchedAxis = verticalTouch
            ? AnchoredAxis(bbox.Top, bbox.Bottom, flushAtStart: intersects.Top, this._crop.WhiteSpaceMargin)
            : AnchoredAxis(bbox.Left, bbox.Right, flushAtStart: intersects.Left, this._crop.WhiteSpaceMargin);

        int side = touchedAxis.Extent;
        Axis freeAxis = verticalTouch
            ? CenteredAxis(bbox.Left, bbox.Right, side)
            : CenteredAxis(bbox.Top, bbox.Bottom, side);

        Rect cropRect = verticalTouch
            ? new Rect(freeAxis.Start, touchedAxis.Start, freeAxis.Extent, touchedAxis.Extent)
            : new Rect(touchedAxis.Start, freeAxis.Start, touchedAxis.Extent, freeAxis.Extent);

        return this.CropThenExtendIfNeeded(sourceJpeg, cropRect, imgW, imgH, side,
            extendVertical: !verticalTouch, extendSymmetric: false, extendTowardStart: freeAxis.ExtendTowardStart,
            $"1-edge anchor on the {touchedEdge} edge, margin {this._crop.WhiteSpaceMargin:P1} on the far edge, free axis centered on the bbox.");
    }

    // 2 opposing edges — both edges on one axis touched (fully pinned, no margin); the other axis
    // is free and centers on the bbox, shrinking or extending symmetrically to reach a square.
    private (byte[], int, bool, string) TwoOpposing(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, bool verticalPinned) {
        int side = verticalPinned ? imgW : imgH;
        Axis freeAxis = verticalPinned
            ? CenteredAxis(bbox.Top, bbox.Bottom, side)
            : CenteredAxis(bbox.Left, bbox.Right, side);

        Rect cropRect = verticalPinned
            ? new Rect(0, freeAxis.Start, imgW, freeAxis.Extent)
            : new Rect(freeAxis.Start, 0, freeAxis.Extent, imgH);

        return this.CropThenExtendIfNeeded(sourceJpeg, cropRect, imgW, imgH, side,
            extendVertical: verticalPinned, extendSymmetric: true, extendTowardStart: true,
            "2-opposing: pinned axis at full extent, free axis centered on the bbox.");
    }

    // 2 adjacent edges — shared corner anchors both axes (no margin); each axis independently
    // shrinks toward the corner (bbox-preserving) or extends away from it on its own.
    private (byte[], int, bool, string) TwoAdjacent(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        int naturalW = bbox.Right - bbox.Left;
        int naturalH = bbox.Bottom - bbox.Top;
        int side = Math.Max(naturalW, naturalH);
        string corner = (intersects.Top ? "top" : "bottom") + "-" + (intersects.Left ? "left" : "right");

        Axis horizontal = AnchoredAxisToSide(intersects.Left ? bbox.Left : bbox.Right, intersects.Left, side);
        Axis vertical = AnchoredAxisToSide(intersects.Top ? bbox.Top : bbox.Bottom, intersects.Top, side);
        Rect cropRect = new(horizontal.Start, vertical.Start, side, side);

        if (naturalW == side && naturalH == side) {
            // Both axes are exactly the bbox's own extent here (side derived from the bbox, no
            // margin) and the bbox lies within the frame by construction, so no clamp is needed —
            // still assert via CenteredRect-style min() defensively against off-by-one rounding.
            Rect exactRect = new(Math.Max(0, cropRect.X), Math.Max(0, cropRect.Y), Math.Min(side, imgW - Math.Max(0, cropRect.X)), Math.Min(side, imgH - Math.Max(0, cropRect.Y)));
            return (CropLocal(sourceJpeg, exactRect), side, false, $"2-adjacent: {corner} anchor, both axes already square at {side}px.");
        }

        // Exactly one axis needed to grow (the other was already `side` — its AnchoredAxisToSide
        // call above is a no-op re-derivation of the same flush geometry).
        bool horizontalGrew = naturalW < side;
        return this.CropThenExtendIfNeeded(sourceJpeg, cropRect, imgW, imgH, side,
            extendVertical: !horizontalGrew, extendSymmetric: false,
            extendTowardStart: horizontalGrew ? horizontal.ExtendTowardStart : vertical.ExtendTowardStart,
            $"2-adjacent: {corner} anchor, {(horizontalGrew ? "width" : "height")} extended away from the corner to {side}px.");
    }

    // 3 edges — one axis fully pinned (both edges touched, fixed at full extent, never moved); the
    // other axis has one touched edge, which anchors it (no margin) same as the 1-edge case.
    private (byte[], int, bool, string) ThreeEdges(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        bool openIsVertical = !intersects.Top || !intersects.Bottom;
        int side = openIsVertical ? imgW : imgH;
        string openEdge = !intersects.Top ? "top" : !intersects.Bottom ? "bottom" : !intersects.Left ? "left" : "right";

        Axis openAxis = openIsVertical
            ? AnchoredAxisToSide(intersects.Top ? bbox.Top : bbox.Bottom, flushAtStart: intersects.Top, side)
            : AnchoredAxisToSide(intersects.Left ? bbox.Left : bbox.Right, flushAtStart: intersects.Left, side);

        Rect cropRect = openIsVertical
            ? new Rect(0, openAxis.Start, imgW, side)
            : new Rect(openAxis.Start, 0, side, imgH);

        return this.CropThenExtendIfNeeded(sourceJpeg, cropRect, imgW, imgH, side,
            extendVertical: openIsVertical, extendSymmetric: false, extendTowardStart: openAxis.ExtendTowardStart,
            $"3-edge: pinned axis fixed at {side}px, open {openEdge} edge anchored (no margin).");
    }

    // 4 edges — no free direction anywhere; always a local square crop, no extension.
    private (byte[], int, bool, string) FourEdges(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH) {
        int side = Math.Min(imgW, imgH);
        Rect cropRect = CenteredRect(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2, side, side, imgW, imgH);
        byte[] cropped = CropLocal(sourceJpeg, cropRect);
        return (cropped, side, false, "4-edge: no free direction; centered square crop at the smallest dimension, no extension.");
    }

    //  Shared axis geometry

    /// <summary>One axis's resolved crop window: start coordinate and extent (side length) within the frame.</summary>
    private readonly record struct Axis(int Start, int Extent, bool ExtendTowardStart);

    /// <summary>
    /// An axis anchored at a touched edge: the touched edge stays flush at its original coordinate;
    /// the far edge sits <paramref name="margin"/> beyond the bbox extent on that axis (0 for
    /// margin-free callers). Extent is not yet clamped to the frame — callers combine it with the
    /// other axis's extent (the square side) before cropping/extending.
    /// </summary>
    private static Axis AnchoredAxis(int bboxStart, int bboxEnd, bool flushAtStart, double margin) {
        int bboxExtent = bboxEnd - bboxStart;
        int targetExtent = (int)Math.Round(bboxExtent * (1.0 + margin));
        int start = flushAtStart ? bboxStart : bboxEnd - targetExtent;
        return new Axis(start, targetExtent, ExtendTowardStart: !flushAtStart);
    }

    /// <summary>Re-anchors an axis to an exact target <paramref name="side"/> instead of a margin-derived extent (used once the square side is already known, e.g. from the opposite pinned axis).</summary>
    private static Axis AnchoredAxisToSide(int flushCoordinate, bool flushAtStart, int side) {
        int start = flushAtStart ? flushCoordinate : flushCoordinate - side;
        return new Axis(start, side, ExtendTowardStart: !flushAtStart);
    }

    /// <summary>An axis centered on the bbox's midpoint at a given target extent, with no anchor edge.</summary>
    private static Axis CenteredAxis(int bboxStart, int bboxEnd, int targetExtent) {
        int center = (bboxStart + bboxEnd) / 2;
        int start = center - targetExtent / 2;
        return new Axis(start, targetExtent, ExtendTowardStart: true);
    }

    /// <summary>
    /// Crops <paramref name="cropRect"/>'s two axes independently against the frame using the
    /// bbox-preservation containment test: an axis that fits entirely within [0, frameExtent] at its
    /// computed position is cropped as-is; an axis that would run off the frame is cropped flush
    /// against whichever side of the frame it is pushed toward, then the remainder is filled by
    /// <see cref="Tx_util_BgStretch"/>. Only one axis (identified by <paramref name="extendVertical"/>)
    /// is ever allowed to extend — the other is assumed already frame-fitting by the caller's geometry.
    /// </summary>
    private (byte[], int, bool, string) CropThenExtendIfNeeded(
        byte[] sourceJpeg, Rect cropRect, int imgW, int imgH, int side,
        bool extendVertical, bool extendSymmetric, bool extendTowardStart, string baseWarning) {
        int freeStart = extendVertical ? cropRect.Y : cropRect.X;
        int freeExtent = extendVertical ? cropRect.Height : cropRect.Width;
        int frameExtent = extendVertical ? imgH : imgW;

        // The pinned axis (the one never extended) is clamped into frame bounds here too — its
        // caller-supplied Start/Extent is not guaranteed to already fit (e.g. OneEdge's margin-grown
        // touched axis can exceed the frame on its own).
        int pinnedStart = extendVertical ? cropRect.X : cropRect.Y;
        int pinnedExtent = extendVertical ? cropRect.Width : cropRect.Height;
        int pinnedFrameExtent = extendVertical ? imgW : imgH;
        int clampedPinnedStart = Math.Clamp(pinnedStart, 0, Math.Max(0, pinnedFrameExtent - pinnedExtent));
        int clampedPinnedExtent = Math.Min(pinnedExtent, pinnedFrameExtent - clampedPinnedStart);

        bool fitsInFrame = freeStart >= 0 && freeStart + freeExtent <= frameExtent;
        if (fitsInFrame) {
            Rect clamped = extendVertical
                ? new Rect(clampedPinnedStart, freeStart, clampedPinnedExtent, freeExtent)
                : new Rect(freeStart, clampedPinnedStart, freeExtent, clampedPinnedExtent);
            return (CropLocal(sourceJpeg, clamped), side, false, $"{baseWarning} Shrink only (bbox fits within the frame).");
        }

        // Extension needed: crop what is available on the in-frame side, then stretch the remainder.
        int availableStart = Math.Max(0, freeStart);
        int availableExtent = Math.Min(frameExtent, freeStart + freeExtent) - availableStart;
        Rect availableRect = extendVertical
            ? new Rect(clampedPinnedStart, availableStart, clampedPinnedExtent, availableExtent)
            : new Rect(availableStart, clampedPinnedStart, availableExtent, clampedPinnedExtent);
        byte[] cropped = CropLocal(sourceJpeg, availableRect);

        int srcOffset = extendSymmetric
            ? (side - availableExtent) / 2
            : (extendTowardStart ? side - availableExtent : 0);
        int srcX = extendVertical ? 0 : srcOffset;
        int srcY = extendVertical ? srcOffset : 0;
        int canvasW = extendVertical ? clampedPinnedExtent : side;
        int canvasH = extendVertical ? side : clampedPinnedExtent;
        byte[] extended = Tx_util_BgStretch.Stretch(cropped, canvasW, canvasH, srcX, srcY, this._bgStretch);
        return (extended, side, true, $"{baseWarning} Extended via Tx_util_BgStretch (bbox did not fit within the frame at the centered/anchored position).");
    }

    /// <summary>Builds a <paramref name="w"/>×<paramref name="h"/> rectangle centered on (<paramref name="cx"/>, <paramref name="cy"/>), clamped to stay within the frame.</summary>
    private static Rect CenteredRect(int cx, int cy, int w, int h, int frameW, int frameH) {
        int x = cx - w / 2;
        int y = cy - h / 2;
        x = Math.Clamp(x, 0, Math.Max(0, frameW - w));
        y = Math.Clamp(y, 0, Math.Max(0, frameH - h));
        return new Rect(x, y, Math.Min(w, frameW), Math.Min(h, frameH));
    }

    /// <summary>Crops <paramref name="sourceJpeg"/> to <paramref name="rect"/> using OpenCvSharp and re-encodes to JPEG.</summary>
    private static byte[] CropLocal(byte[] sourceJpeg, Rect rect) {
        using Mat src = Cv2.ImDecode(sourceJpeg, ImreadModes.Color);
        using Mat cropped = src.SubMat(rect);
        Cv2.ImEncode(".jpg", cropped, out byte[] result);
        return result;
    }

    private static BoundingBox FullFrameBounds(int w, int h) => new() {
        X = 0,
        Y = 0,
        Width = w,
        Height = h,
        Left = 0,
        Top = 0,
        Right = w,
        Bottom = h
    };
}
