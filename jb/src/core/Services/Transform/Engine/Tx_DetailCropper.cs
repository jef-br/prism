using OpenCvSharp;

namespace Prism.Services.Transform;

/// <summary>
/// Crops the image to a square anchored at the edges touched by the salient-object bounding box.
/// Applied to close-up and detail images where the bounding box intersects one or more image edges.
/// Handles every intersection pattern locally (0 through 4 touched edges) — extension always goes
/// through <see cref="Tx_util_BgStretch"/>, and cases where repositioning is impossible fall back to
/// a local square crop rather than delegating to <see cref="Tx_CropSquare"/>.
/// Supports optional headcut placement via <see cref="Tx_util_HeadCutter"/>.
/// </summary>
public class Tx_DetailCropper : IImageTransformation {
    private readonly double _coverage;
    private readonly double _extensionOneSided;
    private readonly double _extensionBiDirectional;
    private readonly bool _headcut;
    private readonly Mat? _colorMat;
    private readonly DetailCropperConfig _detailCropper;
    private readonly BgStretchConfig _bgStretch;
    private readonly HeadCutterConfig _headCutter;

    /// <summary>Creates the transformer with crop-sizing budgets, headcut flag, pre-decoded BGR Mat, and config sections.</summary>
    public Tx_DetailCropper(double coverage, double extensionOneSided, double extensionBiDirectional, bool headcut, Mat? colorMat, DetailCropperConfig detailCropper, BgStretchConfig bgStretch, HeadCutterConfig headCutter) {
        this._coverage = coverage;
        this._extensionOneSided = extensionOneSided;
        this._extensionBiDirectional = extensionBiDirectional;
        this._headcut = headcut;
        this._colorMat = colorMat;
        this._detailCropper = detailCropper;
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
        // labels are that count, not tunable thresholds.
#pragma warning disable S109
        return intersects.Count switch {
            0 => this.ZeroEdges(sourceJpeg, bbox, imgW, imgH),
            1 => this.OneEdge(sourceJpeg, bbox, imgW, imgH, intersects),
            2 when intersects.Top && intersects.Bottom => this.TwoOpposing(sourceJpeg, bbox, imgW, imgH, verticalPinned: true),
            2 when intersects.Left && intersects.Right => this.TwoOpposing(sourceJpeg, bbox, imgW, imgH, verticalPinned: false),
            2 => this.TwoAdjacent(sourceJpeg, bbox, imgW, imgH, intersects),
            3 => this.ThreeEdges(sourceJpeg, bbox, imgW, imgH, intersects),
            _ => this.FourEdges(sourceJpeg, bbox, imgW, imgH)
        };
#pragma warning restore S109
    }

    // 0 edges — greedy-crop toward the bbox, never below the Coverage floor.
    private (byte[], int, bool, string) ZeroEdges(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH) {
        int side = this.ComputeIdealSide(bbox, imgW, imgH);
        side = Math.Min(side, Math.Min(imgW, imgH));

        int cx = bbox.X + bbox.Width / 2;
        int cy = bbox.Y + bbox.Height / 2;
        Rect cropRect = CenteredRect(cx, cy, side, side, imgW, imgH);

        byte[] cropped = CropLocal(sourceJpeg, cropRect);
        return (cropped, side, false, "0-edge greedy crop centered on bounding box.");
    }

    // 1 edge — the touched edge pins the crop; the perpendicular axis may extend; the far edge
    // crops toward the bbox subject to the same Coverage floor as the 0-edge case.
    private (byte[], int, bool, string) OneEdge(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        int side = this.ComputeIdealSide(bbox, imgW, imgH);
        bool verticalTouch = intersects.Top || intersects.Bottom;

        // Pinned axis: bounded by the original extent — it can only shrink toward the bbox
        // from the free (far) end, never extend past the touched edge or past the original frame.
        int pinnedOriginalExtent = verticalTouch ? imgH : imgW;
        int pinnedSide = Math.Min(side, pinnedOriginalExtent);

        // The pinned axis' fixed start is 0 when the touched edge is Top/Left (crop grows away
        // from the origin); otherwise it is anchored at the far end (crop grows toward the origin).
        int pinnedFixedStart = (intersects.Top || intersects.Left) ? 0 : pinnedOriginalExtent - pinnedSide;

        Rect preCropRect = verticalTouch
            ? new Rect(0, pinnedFixedStart, imgW, pinnedSide)
            : new Rect(pinnedFixedStart, 0, pinnedSide, imgH);

        byte[] preCropped = CropLocal(sourceJpeg, preCropRect);

        // Free (perpendicular) axis: centered on the bbox. The pre-crop above only ever narrows
        // the PINNED axis's range (rows for a vertical touch, columns for a horizontal touch) and
        // always keeps the free axis's own range at its full original extent (see preCropRect:
        // the non-pinned dimension is always passed through unchanged as imgW/imgH), so the free
        // axis's coordinate space is never shifted by the pre-crop — no adjustment needed here.
        int freeOriginalExtent = verticalTouch ? imgW : imgH;
        int freeCenter = verticalTouch
            ? bbox.X + bbox.Width / 2
            : bbox.Y + bbox.Height / 2;

        if (side <= freeOriginalExtent) {
            // Free axis needs no extension — crop it down instead, centered on the bbox.
            Rect finalRect = verticalTouch
                ? CenteredRect(freeCenter, pinnedSide / 2, side, pinnedSide, preCropRect.Width, preCropRect.Height)
                : CenteredRect(pinnedSide / 2, freeCenter, pinnedSide, side, preCropRect.Width, preCropRect.Height);
            byte[] finalBytes = CropLocal(preCropped, finalRect);
            return (finalBytes, side, false, $"1-edge crop, far edge trimmed to Coverage floor ({this._coverage:P0}).");
        }

        // Extend the free axis via Tx_util_BgStretch to reach `side`. Clamp to the valid placement
        // range [0, side - freeOriginalExtent] the same way CenteredRect does above — an off-center
        // bbox can otherwise push the ideal (uncapped) offset negative, since centering the bbox
        // exactly can demand placing the source further than the canvas actually allows.
        int idealOffset = side / 2 - freeCenter;
        int maxOffset = side - freeOriginalExtent;
        int clampedOffset = Math.Clamp(idealOffset, 0, maxOffset);
        int srcX = verticalTouch ? clampedOffset : 0;
        int srcY = verticalTouch ? 0 : clampedOffset;
        byte[] extendedBytes = Tx_util_BgStretch.Stretch(preCropped, side, side, srcX, srcY, this._bgStretch);
        string touchedEdge = intersects.Top ? "top" : intersects.Bottom ? "bottom" : intersects.Left ? "left" : "right";
        return (extendedBytes, side, true, $"1-edge extension applied to the free axis opposite the pinned {touchedEdge} edge (uncapped, not explicitly specified).");
    }

    // 2 opposing edges — stuck axis pinned at full original extent; free axis extends up to
    // BiDirectional, otherwise crops toward the bbox, otherwise falls back to a local square crop.
    private (byte[], int, bool, string) TwoOpposing(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, bool verticalPinned) {
        int pinnedSide = verticalPinned ? imgW : imgH;
        int currentFreeSide = verticalPinned ? imgH : imgW;
        int delta = pinnedSide - currentFreeSide;

        if (delta <= 0) {
            // Free axis already >= pinned axis — crop it down to pinnedSide, centered on bbox.
            int freeCenter = verticalPinned ? bbox.Y + bbox.Height / 2 : bbox.X + bbox.Width / 2;
            Rect cropRect = verticalPinned
                ? CenteredRect(imgW / 2, freeCenter, pinnedSide, pinnedSide, imgW, imgH)
                : CenteredRect(freeCenter, imgH / 2, pinnedSide, pinnedSide, imgW, imgH);
            byte[] cropped = CropLocal(sourceJpeg, cropRect);
            return (cropped, pinnedSide, false, "2-opposing crop: free axis trimmed to match the pinned axis.");
        }

        if ((double)delta / currentFreeSide <= this._extensionBiDirectional) {
            // Extend the free axis symmetrically to pinnedSide.
            int offset = (pinnedSide - currentFreeSide) / 2;
            int srcX = verticalPinned ? 0 : offset;
            int srcY = verticalPinned ? offset : 0;
            byte[] extendedBytes = Tx_util_BgStretch.Stretch(sourceJpeg, pinnedSide, pinnedSide, srcX, srcY, this._bgStretch);
            return (extendedBytes, pinnedSide, true, $"2-opposing bidirectional extension applied ({(double)delta / currentFreeSide:P1} of {this._extensionBiDirectional:P0} budget).");
        }

        // Exceeds the BiDirectional budget — local square crop centered on the bbox.
        int side = Math.Min(imgW, imgH);
        Rect fallbackRect = CenteredRect(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2, side, side, imgW, imgH);
        byte[] fallback = CropLocal(sourceJpeg, fallbackRect);
        return (fallback, side, false, $"2-opposing fallback: required extension {(double)delta / currentFreeSide:P1} exceeds {this._extensionBiDirectional:P0} budget; local square crop applied.");
    }

    // 2 adjacent edges — the shared corner is a directional anchor: crop the larger dimension
    // toward the smaller (capped at 14%), then stretch the smaller dimension to square, then
    // fall back to a corner-anchored local square crop if still not square.
    private (byte[], int, bool, string) TwoAdjacent(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        double adjacentCropCap = this._detailCropper.AdjacentCropCap;
        bool anchorTop = intersects.Top;
        bool anchorLeft = intersects.Left;

        int largerDim = Math.Max(imgW, imgH);
        int smallerDim = Math.Min(imgW, imgH);

        // Step 1: crop the larger dimension toward the smaller, capped at 14% of the larger dimension,
        // trimming away from the anchor corner (the anchor corner never moves).
        int maxReduction = (int)(largerDim * adjacentCropCap);
        int target = Math.Max(smallerDim, largerDim - maxReduction);

        bool cropWidth = imgW >= imgH;
        Rect step1Rect = cropWidth
            ? (anchorLeft ? new Rect(0, 0, target, imgH) : new Rect(imgW - target, 0, target, imgH))
            : (anchorTop ? new Rect(0, 0, imgW, target) : new Rect(0, imgH - target, imgW, target));
        byte[] step1Bytes = CropLocal(sourceJpeg, step1Rect);
        int curW = step1Rect.Width, curH = step1Rect.Height;

        if (curW == curH)
            return (step1Bytes, curW, false, "2-adjacent: step-1 crop alone reached a square.");

        // Step 2: stretch the smaller dimension's background until square, extending away from
        // the anchor corner.
        int side = Math.Max(curW, curH);
        int srcX = anchorLeft ? 0 : side - curW;
        int srcY = anchorTop ? 0 : side - curH;
        byte[] step2Bytes = Tx_util_BgStretch.Stretch(step1Bytes, side, side, srcX, srcY, this._bgStretch);

        using Mat verify = Cv2.ImDecode(step2Bytes, ImreadModes.Color);
        if (verify.Cols == verify.Rows)
            return (step2Bytes, side, true, "2-adjacent: step-1 crop (capped 14%) + step-2 background stretch reached a square.");

        // Step 3 (defensive — should rarely trigger): local square crop anchored at the same corner.
        int fallbackSide = Math.Min(verify.Cols, verify.Rows);
        int fx = anchorLeft ? 0 : verify.Cols - fallbackSide;
        int fy = anchorTop ? 0 : verify.Rows - fallbackSide;
        byte[] fallback = CropLocal(step2Bytes, new Rect(fx, fy, fallbackSide, fallbackSide));
        return (fallback, fallbackSide, true, "2-adjacent fallback: still not square after crop+stretch; corner-anchored local square crop applied.");
    }

    // 3 edges — one open side carries all extension, capped at OneSided; otherwise crop the open
    // side down, otherwise fall back to a local square crop centered on the bbox.
    private (byte[], int, bool, string) ThreeEdges(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH, EdgeIntersects intersects) {
        // Exactly one of the four booleans is false — that is the open side.
        bool openTop = !intersects.Top, openBottom = !intersects.Bottom;
        bool openLeft = !intersects.Left, openRight = !intersects.Right;
        bool openIsVertical = openTop || openBottom;

        int pinnedSide = openIsVertical ? imgW : imgH;       // fully-pinned axis, original extent
        int currentOpenSide = openIsVertical ? imgH : imgW;  // partially-open axis, original extent
        int delta = pinnedSide - currentOpenSide;

        if (delta <= 0) {
            // Crop the open side down to pinnedSide, trimming only from the open end.
            Rect cropRect = openTop ? new Rect(0, 0, imgW, pinnedSide)
                          : openBottom ? new Rect(0, imgH - pinnedSide, imgW, pinnedSide)
                          : openLeft ? new Rect(0, 0, pinnedSide, imgH)
                                       : new Rect(imgW - pinnedSide, 0, pinnedSide, imgH);
            byte[] cropped = CropLocal(sourceJpeg, cropRect);
            return (cropped, pinnedSide, false, "3-edge crop: open side trimmed to match the pinned axis.");
        }

        if ((double)delta / currentOpenSide <= this._extensionOneSided) {
            // Extend on the open side only — the source sits flush against the three pinned
            // edges, and new pixels appear only on the open side.
            int srcX = openLeft ? pinnedSide - currentOpenSide : 0;   // Left open → push right, grow left
            int srcY = openTop ? pinnedSide - currentOpenSide : 0;    // Top open → push down, grow up
            byte[] extendedBytes = Tx_util_BgStretch.Stretch(sourceJpeg, pinnedSide, pinnedSide, srcX, srcY, this._bgStretch);
            string openEdgeName = openTop ? "top" : openBottom ? "bottom" : openLeft ? "left" : "right";
            return (extendedBytes, pinnedSide, true, $"3-edge one-sided extension applied to the {openEdgeName} edge ({(double)delta / currentOpenSide:P1} of {this._extensionOneSided:P0} budget).");
        }

        // Exceeds the OneSided budget — local square crop centered on the bbox.
        int side = Math.Min(imgW, imgH);
        Rect fallbackRect = CenteredRect(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2, side, side, imgW, imgH);
        byte[] fallback = CropLocal(sourceJpeg, fallbackRect);
        return (fallback, side, false, $"3-edge fallback: required extension {(double)delta / currentOpenSide:P1} exceeds {this._extensionOneSided:P0} budget; local square crop applied.");
    }

    // 4 edges — no open side exists; always a local square crop centered on the bbox.
    private (byte[], int, bool, string) FourEdges(byte[] sourceJpeg, BoundingBox bbox, int imgW, int imgH) {
        int side = Math.Min(imgW, imgH);
        Rect cropRect = CenteredRect(bbox.X + bbox.Width / 2, bbox.Y + bbox.Height / 2, side, side, imgW, imgH);
        byte[] cropped = CropLocal(sourceJpeg, cropRect);
        return (cropped, side, false, "4-edge fallback: no direction available for repositioning; centered square crop at smallest dimension.");
    }

    //  Shared geometry helpers

    /// <summary>
    /// Computes the Coverage-respecting ideal square side for a bbox-driven crop: the tight bbox
    /// square when it already retains at least <see cref="_coverage"/> of the image area, otherwise
    /// the largest square that retains exactly that fraction.
    /// </summary>
    private int ComputeIdealSide(BoundingBox bbox, int imgW, int imgH) {
        long imgArea = (long)imgW * imgH;
        int idealSide = Math.Max(bbox.Width, bbox.Height);
        long tightRemovedArea = imgArea - (long)idealSide * idealSide;

        if (tightRemovedArea <= imgArea * (1.0 - this._coverage))
            return idealSide;

        return (int)Math.Ceiling(Math.Sqrt(imgArea * this._coverage));
    }

    /// <summary>Builds a <paramref name="w"/>×<paramref name="h"/> rectangle centered on (<paramref name="cx"/>, <paramref name="cy"/>), clamped to stay within the frame.</summary>
    private static Rect CenteredRect(int cx, int cy, int w, int h, int frameW, int frameH) {
        int x = cx - w / 2;
        int y = cy - h / 2;
        x = Math.Clamp(x, 0, Math.Max(0, frameW - w));
        y = Math.Clamp(y, 0, Math.Max(0, frameH - h));
        return new Rect(x, y, Math.Min(w, frameW), Math.Min(h, frameH));
    }

    /// <summary>Crops <paramref name="sourceJpeg"/> to <paramref name="rect"/> using OpenCvSharp and re-encodes to JPEG. Local to this class — never delegates to <see cref="Tx_CropSquare"/>.</summary>
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
