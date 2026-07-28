using OpenCvSharp;

namespace Prism.Services.Matching;

/// <summary>
/// Classical-CV subject isolation — the v1 <see cref="ISubjectDetector"/> producer, ported from the
/// reference prototype jb/docs/reference/process_images.py. Isolates the true product (minus shadow,
/// minus background) by keying on chroma + texture and never on lightness (a cast shadow is a near-pure
/// lightness change). White-on-white is caught by the texture signal; hard-shadow edges are stripped by
/// shape; the background colour is fitted as a plane over the border ring; a Canny border flood-fill
/// corroborates but never introduces a region on its own. No ONNX — runs upstream so the transforms stay
/// deterministic. Emits a <see cref="Prism.Contracts.SubjectDetection"/> (box + mask + intersects +
/// hard-shadow evidence).
/// </summary>
public sealed class SubjectDetector : ISubjectDetector {
    private readonly SubjectDetectorConfig cfg;

    public SubjectDetector(SubjectDetectorConfig cfg) { this.cfg = cfg; }

    public static SubjectDetector FromConfig() =>
        new(ConfigLoader.Section<SubjectDetectorConfig>("ClassifyConfig.json", "SubjectDetector"));

    public SubjectDetection Detect(Mat bgrImage) {
        int origW = bgrImage.Cols, origH = bgrImage.Rows;
        if (origW == 0 || origH == 0) return this.WholeFrameDetection(Math.Max(origW, 1), Math.Max(origH, 1), confidence: 0.0);

        double scale = Math.Min(1.0, (double)this.cfg.MaxAnalysisSize / Math.Max(origH, origW));
        using Mat small = this.ScaleForAnalysis(bgrImage, scale);

        using Mat mask = this.BuildForegroundMask(small, out bool hasHardShadow);
        Rect? box = this.SignificantComponentsBox(mask);
        (bool top, bool bottom, bool left, bool right) = this.CanvasContacts(mask);
        byte[] maskPng = this.EncodeMaskPng(mask, origW, origH);

        SubjectDetection detection = new() {
            Producer = "classical-cv",
            MaskPng = maskPng,
            IntersectsTop = top,
            IntersectsBottom = bottom,
            IntersectsLeft = left,
            IntersectsRight = right,
            HasHardShadowEvidence = hasHardShadow
        };

        if (box is null || this.IsWholeFrame(box.Value, small.Cols, small.Rows)) {
            detection.Box = FullBox(origW, origH);
            detection.Confidence = box is null ? 0.0 : 0.2;
            detection.IsWholeFrameFallback = true;
            return detection;
        }

        detection.Box = RescaleBox(box.Value, scale, origW, origH);
        detection.Confidence = this.BoxCoverageConfidence(mask, box.Value);
        detection.IsWholeFrameFallback = false;
        return detection;
    }

    // ---- Detection pipeline ----

    private Mat ScaleForAnalysis(Mat bgr, double scale) {
        if (scale >= 1.0) return bgr.Clone();
        int w = Math.Max(8, (int)(bgr.Cols * scale));
        int h = Math.Max(8, (int)(bgr.Rows * scale));
        Mat small = new();
        Cv2.Resize(bgr, small, new Size(w, h), interpolation: InterpolationFlags.Area);
        return small;
    }

    // Product = differs in colour from the sweep, or carries surface texture. Lightness is deliberately
    // never a criterion (that is how shadow is excluded). Returns an 8U 0/255 mask.
    private Mat BuildForegroundMask(Mat bgr, out bool hasHardShadow) {
        int w = bgr.Cols, h = bgr.Rows;
        (Mat chromaA, Mat chromaB, Mat texture) = this.BuildAnalysisLayers(bgr);
        List<Point> ring = this.RingCoords(w, h);

        using Mat backgroundA = this.EvaluatePlane(this.FitBackgroundPlane(chromaA, ring, w, h), w, h);
        using Mat backgroundB = this.EvaluatePlane(this.FitBackgroundPlane(chromaB, ring, w, h), w, h);

        using Mat deltaA = new(); using Mat deltaB = new();
        Cv2.Subtract(chromaA, backgroundA, deltaA);
        Cv2.Subtract(chromaB, backgroundB, deltaB);
        using Mat chromaDistance = new();
        Cv2.Magnitude(deltaA, deltaB, chromaDistance);

        float[] ringChroma = CollectRingValues(chromaDistance, ring);
        float[] ringTexture = CollectRingValues(texture, ring);
        double chromaLimit = Math.Max(this.cfg.ChromaFloor, this.cfg.OutlierSpreadMultiplier * RobustSpread(ringChroma));
        double textureLimit = Math.Max(this.cfg.TextureFloor, Median(ringTexture) + this.cfg.OutlierSpreadMultiplier * RobustSpread(ringTexture));

        using Mat chromaMask = ThresholdMask(chromaDistance, chromaLimit);
        using Mat textureMask = ThresholdMask(texture, textureLimit);
        chromaA.Dispose(); chromaB.Dispose(); texture.Dispose();

        // Texture with no chroma support is either real weave (a filled 2D area) or a hard shadow's thin
        // edge. A morphological open keeps the former and strips the latter; the stripped thin lines are
        // the candidate-hard-shadow evidence.
        using Mat notChroma = new();
        Cv2.BitwiseNot(chromaMask, notChroma);
        using Mat textureOnly = new();
        Cv2.BitwiseAnd(textureMask, notChroma, textureOnly);
        using Mat textureOnlyOpened = MorphOpen(textureOnly, this.cfg.ShadowEdgeKernel);
        hasHardShadow = this.StrippedFraction(textureOnly, textureOnlyOpened, w * h) >= this.cfg.HardShadowEvidenceFraction;

        Mat mask = new();
        Cv2.BitwiseOr(chromaMask, textureOnlyOpened, mask);

        // Canny border-flood corroboration: fold an enclosed region in only where it touches an
        // already-flagged pixel, so an isolated shadow silhouette can never sneak in on its own.
        using Mat enclosed = this.CannyEnclosedRegion(bgr);
        using Mat corroborated = CorroborateEnclosed(enclosed, mask);
        Cv2.BitwiseOr(mask, corroborated, mask);

        // Kill speckle, then bridge separately-detected parts (a print, a sleeve, a shaded fold) into one.
        int bridge = Math.Max(9, ((int)(0.02 * Math.Min(h, w))) | 1);
        using (Mat opened = MorphOpen(mask, 5)) opened.CopyTo(mask);
        using (Mat closed = MorphClose(mask, bridge, iterations: 2)) closed.CopyTo(mask);
        return mask;
    }

    // Returns (chroma_a, chroma_b, texture) as 32F mats. CLAHE lifts white-on-white weave clear of the
    // noise floor purely for detection; a high-pass then discards slow ramps (shadow penumbra) before the
    // local-std-dev texture measure.
    private (Mat chromaA, Mat chromaB, Mat texture) BuildAnalysisLayers(Mat bgr) {
        using Mat denoised = new();
        Cv2.BilateralFilter(bgr, denoised, 5, 40, 40);
        using Mat lab = new();
        Cv2.CvtColor(denoised, lab, ColorConversionCodes.BGR2Lab);
        Mat[] channels = Cv2.Split(lab);
        using Mat lightness = channels[0];
        using Mat aChannel = channels[1];
        using Mat bChannel = channels[2];

        using CLAHE clahe = Cv2.CreateCLAHE(this.cfg.ClaheClipLimit, new Size(this.cfg.ClaheTileSize, this.cfg.ClaheTileSize));
        using Mat equalized8 = new();
        clahe.Apply(lightness, equalized8);
        using Mat equalized = new();
        equalized8.ConvertTo(equalized, MatType.CV_32F);

        using Mat blurred = new();
        Cv2.GaussianBlur(equalized, blurred, new Size(0, 0), this.cfg.TextureDetailSigma);
        using Mat detail = new();
        Cv2.Subtract(equalized, blurred, detail);

        int win = this.cfg.TextureWindow;
        using Mat mean = new(); using Mat detailSq = new(); using Mat meanSq = new(); using Mat meanOfMeanSq = new(); using Mat variance = new();
        Cv2.BoxFilter(detail, mean, -1, new Size(win, win));
        Cv2.Multiply(detail, detail, detailSq);
        Cv2.BoxFilter(detailSq, meanSq, -1, new Size(win, win));
        Cv2.Multiply(mean, mean, meanOfMeanSq);
        Cv2.Subtract(meanSq, meanOfMeanSq, variance);
        Cv2.Max(variance, new Scalar(0), variance);
        Mat texture = new();
        Cv2.Sqrt(variance, texture);

        Mat chromaA = new(); Mat chromaB = new();
        using Mat aFloat = new(); using Mat bFloat = new();
        aChannel.ConvertTo(aFloat, MatType.CV_32F);
        bChannel.ConvertTo(bFloat, MatType.CV_32F);
        Cv2.Subtract(aFloat, new Scalar(128), chromaA);
        Cv2.Subtract(bFloat, new Scalar(128), chromaB);
        return (chromaA, chromaB, texture);
    }

    // Pixels an edge boundary walls off from the frame border — candidate product. Corroboration only.
    private Mat CannyEnclosedRegion(Mat bgr) {
        using Mat gray = new();
        Cv2.CvtColor(bgr, gray, ColorConversionCodes.BGR2GRAY);
        using Mat denoised = new();
        Cv2.BilateralFilter(gray, denoised, 5, 40, 40);
        double median = MedianOf8U(denoised);
        int low = (int)Math.Max(0, (1.0 - this.cfg.CannySigma) * median);
        int high = (int)Math.Min(255, (1.0 + this.cfg.CannySigma) * median);
        using Mat edges = new();
        Cv2.Canny(denoised, edges, low, high);
        using Mat closedEdges = MorphClose(edges, this.cfg.CannyCloseKernel, iterations: 1);

        using Mat free = new();
        Cv2.Compare(closedEdges, new Scalar(0), free, CmpType.EQ);   // free space = non-edge, 0/255
        using Mat labels = new();
        Cv2.ConnectedComponents(free, labels, PixelConnectivity.Connectivity8, MatType.CV_32S);

        labels.GetArray(out int[] labelData);
        int w = labels.Cols, h = labels.Rows;
        HashSet<int> borderLabels = BorderLabels(labelData, w, h);

        byte[] enclosedData = new byte[labelData.Length];
        for (int i = 0; i < labelData.Length; i++)
            enclosedData[i] = borderLabels.Contains(labelData[i]) ? (byte)0 : (byte)255;   // ~background
        Mat enclosed = new(h, w, MatType.CV_8U);
        enclosed.SetArray(enclosedData);
        return enclosed;
    }

    // Keep only enclosed components that touch an already-flagged mask pixel.
    private static Mat CorroborateEnclosed(Mat enclosed, Mat mask) {
        using Mat labels = new();
        Cv2.ConnectedComponents(enclosed, labels, PixelConnectivity.Connectivity8, MatType.CV_32S);
        labels.GetArray(out int[] labelData);
        mask.GetArray(out byte[] maskData);

        HashSet<int> touching = [];
        for (int i = 0; i < labelData.Length; i++)
            if (maskData[i] > 0 && labelData[i] != 0) touching.Add(labelData[i]);

        byte[] outData = new byte[labelData.Length];
        for (int i = 0; i < labelData.Length; i++)
            outData[i] = touching.Contains(labelData[i]) ? (byte)255 : (byte)0;
        Mat corroborated = new(mask.Rows, mask.Cols, MatType.CV_8U);
        corroborated.SetArray(outData);
        return corroborated;
    }

    // Bounding box over every blob big enough to belong to the product.
    private Rect? SignificantComponentsBox(Mat mask) {
        ConnectedComponents cc = Cv2.ConnectedComponentsEx(mask, PixelConnectivity.Connectivity8);
        if (cc.Blobs.Count <= 1) return null;

        double largest = 0;
        for (int i = 1; i < cc.Blobs.Count; i++) largest = Math.Max(largest, cc.Blobs[i].Area);

        double threshold = Math.Max(
            this.cfg.MinComponentAreaFraction * mask.Cols * mask.Rows,
            Math.Max(this.cfg.MinComponentAreaRatio * largest, this.cfg.MinComponentAreaPixels));

        int x0 = int.MaxValue, y0 = int.MaxValue, x1 = 0, y1 = 0;
        bool any = false;
        for (int i = 1; i < cc.Blobs.Count; i++) {
            ConnectedComponents.Blob blob = cc.Blobs[i];
            if (blob.Area < threshold) continue;
            any = true;
            x0 = Math.Min(x0, blob.Left);
            y0 = Math.Min(y0, blob.Top);
            x1 = Math.Max(x1, blob.Left + blob.Width);
            y1 = Math.Max(y1, blob.Top + blob.Height);
        }
        return any ? new Rect(x0, y0, x1 - x0, y1 - y0) : null;
    }

    // How many canvas edges the product runs off, ignoring incidental touches.
    private (bool top, bool bottom, bool left, bool right) CanvasContacts(Mat mask) {
        int w = mask.Cols, h = mask.Rows;
        bool top = EdgeCoverage(mask.Row(0)) >= this.cfg.BleedContact;
        bool bottom = EdgeCoverage(mask.Row(h - 1)) >= this.cfg.BleedContact;
        bool left = EdgeCoverage(mask.Col(0)) >= this.cfg.BleedContact;
        bool right = EdgeCoverage(mask.Col(w - 1)) >= this.cfg.BleedContact;
        return (top, bottom, left, right);
    }

    // ---- Background-plane fit ----

    private (double c0, double c1, double c2) FitBackgroundPlane(Mat channel, List<Point> ring, int w, int h) {
        if (ring.Count < 500) {
            float[] values = CollectRingValues(channel, ring);
            return (Median(values), 0.0, 0.0);
        }

        double n = 0, sx = 0, sy = 0, sxx = 0, sxy = 0, syy = 0, sv = 0, sxv = 0, syv = 0;
        double halfW = w / 2.0, halfH = h / 2.0;
        foreach (Point p in ring) {
            double xn = (p.X - halfW) / halfW;
            double yn = (p.Y - halfH) / halfH;
            double v = channel.At<float>(p.Y, p.X);
            n++; sx += xn; sy += yn; sxx += xn * xn; sxy += xn * yn; syy += yn * yn;
            sv += v; sxv += xn * v; syv += yn * v;
        }
        return Solve3x3(n, sx, sy, sxx, sxy, syy, sv, sxv, syv);
    }

    private Mat EvaluatePlane((double c0, double c1, double c2) plane, int w, int h) {
        using Mat xRow = new(1, w, MatType.CV_32F);
        using Mat yCol = new(h, 1, MatType.CV_32F);
        double halfW = w / 2.0, halfH = h / 2.0;
        for (int x = 0; x < w; x++) xRow.Set(0, x, (float)((x - halfW) / halfW));
        for (int y = 0; y < h; y++) yCol.Set(y, 0, (float)((y - halfH) / halfH));

        using Mat xGrid = new(); using Mat yGrid = new();
        Cv2.Repeat(xRow, h, 1, xGrid);
        Cv2.Repeat(yCol, 1, w, yGrid);
        Mat background = new();
        Cv2.AddWeighted(xGrid, plane.c1, yGrid, plane.c2, plane.c0, background);
        return background;
    }

    // ---- Small helpers ----

    private List<Point> RingCoords(int w, int h) {
        int bandY = Math.Max(2, (int)(h * this.cfg.BorderRingFraction));
        int bandX = Math.Max(2, (int)(w * this.cfg.BorderRingFraction));
        List<Point> coords = [];
        for (int y = 0; y < bandY; y++) for (int x = 0; x < w; x++) coords.Add(new Point(x, y));
        for (int y = h - bandY; y < h; y++) for (int x = 0; x < w; x++) coords.Add(new Point(x, y));
        for (int y = bandY; y < h - bandY; y++) {
            for (int x = 0; x < bandX; x++) coords.Add(new Point(x, y));
            for (int x = w - bandX; x < w; x++) coords.Add(new Point(x, y));
        }
        return coords;
    }

    private double StrippedFraction(Mat before, Mat after, int area) {
        using Mat notAfter = new();
        Cv2.BitwiseNot(after, notAfter);
        using Mat stripped = new();
        Cv2.BitwiseAnd(before, notAfter, stripped);
        return area <= 0 ? 0.0 : (double)Cv2.CountNonZero(stripped) / area;
    }

    private double BoxCoverageConfidence(Mat mask, Rect box) {
        using Mat region = new(mask, box);
        double coverage = (double)Cv2.CountNonZero(region) / Math.Max(1, box.Width * box.Height);
        return Math.Clamp(coverage, 0.1, 1.0);
    }

    private byte[] EncodeMaskPng(Mat mask, int origW, int origH) {
        using Mat full = new();
        Cv2.Resize(mask, full, new Size(origW, origH), interpolation: InterpolationFlags.Nearest);
        Cv2.ImEncode(".png", full, out byte[] png);
        return png;
    }

    private bool IsWholeFrame(Rect box, int w, int h) =>
        (long)box.Width * box.Height >= this.cfg.WholeFrameFraction * w * h;

    private SubjectDetection WholeFrameDetection(int w, int h, double confidence) => new() {
        Producer = "classical-cv",
        Box = FullBox(w, h),
        Confidence = confidence,
        IntersectsTop = true, IntersectsBottom = true, IntersectsLeft = true, IntersectsRight = true
    };

    private static double EdgeCoverage(Mat edge) => (double)Cv2.CountNonZero(edge) / Math.Max(1, edge.Rows * edge.Cols);

    private static Mat ThresholdMask(Mat src32F, double limit) {
        using Mat binary = new();
        Cv2.Threshold(src32F, binary, limit, 255, ThresholdTypes.Binary);
        Mat mask = new();
        binary.ConvertTo(mask, MatType.CV_8U);
        return mask;
    }

    private static Mat MorphOpen(Mat src, int kernelSize, int iterations = 1) {
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        Mat dst = new();
        Cv2.MorphologyEx(src, dst, MorphTypes.Open, kernel, iterations: iterations);
        return dst;
    }

    private static Mat MorphClose(Mat src, int kernelSize, int iterations = 1) {
        using Mat kernel = Cv2.GetStructuringElement(MorphShapes.Ellipse, new Size(kernelSize, kernelSize));
        Mat dst = new();
        Cv2.MorphologyEx(src, dst, MorphTypes.Close, kernel, iterations: iterations);
        return dst;
    }

    private static HashSet<int> BorderLabels(int[] labelData, int w, int h) {
        HashSet<int> labels = [];
        for (int x = 0; x < w; x++) { labels.Add(labelData[x]); labels.Add(labelData[(h - 1) * w + x]); }
        for (int y = 0; y < h; y++) { labels.Add(labelData[y * w]); labels.Add(labelData[y * w + (w - 1)]); }
        labels.Remove(0);
        return labels;
    }

    private static float[] CollectRingValues(Mat m, List<Point> ring) {
        float[] values = new float[ring.Count];
        for (int i = 0; i < ring.Count; i++) values[i] = m.At<float>(ring[i].Y, ring[i].X);
        return values;
    }

    private static double Median(float[] values) {
        if (values.Length == 0) return 0.0;
        float[] copy = (float[])values.Clone();
        Array.Sort(copy);
        return copy[copy.Length / 2];
    }

    // Median absolute deviation, scaled to compare with a standard deviation.
    private static double RobustSpread(float[] values) {
        if (values.Length == 0) return 0.0;
        double median = Median(values);
        float[] deviations = new float[values.Length];
        for (int i = 0; i < values.Length; i++) deviations[i] = (float)Math.Abs(values[i] - median);
        return 1.4826 * Median(deviations);
    }

    private static double MedianOf8U(Mat gray) {
        using Mat hist = new();
        Cv2.CalcHist([gray], [0], null, hist, 1, [256], [new Rangef(0, 256)]);
        long total = (long)gray.Rows * gray.Cols;
        long cumulative = 0;
        for (int bin = 0; bin < 256; bin++) {
            cumulative += (long)hist.At<float>(bin, 0);
            if (cumulative * 2 >= total) return bin;
        }
        return 128;
    }

    private static (double, double, double) Solve3x3(double n, double sx, double sy, double sxx, double sxy, double syy, double sv, double sxv, double syv) {
        // Normal equations: [[n,sx,sy],[sx,sxx,sxy],[sy,sxy,syy]] * c = [sv,sxv,syv], solved by Cramer's rule.
        double det = n * (sxx * syy - sxy * sxy) - sx * (sx * syy - sxy * sy) + sy * (sx * sxy - sxx * sy);
        if (Math.Abs(det) < 1e-9) return (sv / Math.Max(1.0, n), 0.0, 0.0);
        double d0 = sv * (sxx * syy - sxy * sxy) - sx * (sxv * syy - sxy * syv) + sy * (sxv * sxy - sxx * syv);
        double d1 = n * (sxv * syy - syv * sxy) - sv * (sx * syy - sy * sxy) + sy * (sx * syv - sxv * sy);
        double d2 = n * (sxx * syv - sxv * sxy) - sx * (sx * syv - sv * sxy) + sv * (sx * sxy - sxx * sy);
        return (d0 / det, d1 / det, d2 / det);
    }

    private static BoundingBox FullBox(int w, int h) => new() {
        X = 0, Y = 0, Width = w, Height = h, Left = 0, Top = 0, Right = w, Bottom = h
    };

    private static BoundingBox RescaleBox(Rect box, double scale, int origW, int origH) {
        if (scale >= 1.0) return RectToBox(box, origW, origH);
        int x = Math.Max(0, (int)Math.Floor(box.X / scale) - 1);
        int y = Math.Max(0, (int)Math.Floor(box.Y / scale) - 1);
        int width = Math.Min(origW - x, (int)Math.Ceiling(box.Width / scale) + 2);
        int height = Math.Min(origH - y, (int)Math.Ceiling(box.Height / scale) + 2);
        return RectToBox(new Rect(x, y, width, height), origW, origH);
    }

    private static BoundingBox RectToBox(Rect box, int origW, int origH) {
        int x = Math.Clamp(box.X, 0, origW);
        int y = Math.Clamp(box.Y, 0, origH);
        int right = Math.Clamp(box.X + box.Width, 0, origW);
        int bottom = Math.Clamp(box.Y + box.Height, 0, origH);
        return new BoundingBox { X = x, Y = y, Width = right - x, Height = bottom - y, Left = x, Top = y, Right = right, Bottom = bottom };
    }
}
