# Image Classification Todo

-------
- [ ] Define final ImageNGP taxonomy and feature combinations: list all possible ImageNGPs and the ImageFeature values required to derive each phenotype.
  - Answer: FROZEN: Taxonomy is captured in canonical files (ImageNGP.json, ImageRoles.json, imagePhenotypes.md, ImageFeatures.md). No reconciliation action needed at this time.

-------
- [ ] Phenotype production validation: define the protocol and acceptance criteria required before phenotype assignment can be trusted in production.
  - Issue: The 26 phenotypes in `imagePhenotypes.md` were defined from spec and taxonomy documentation without real-image testing. Production-quality assignment requires validation against a representative labeled image set. Currently most features are UNKNOWN (see per-feature Analyzer todos below), so phenotype assignment is unreliable for any image where CLIP evidence is needed.
  - What is needed: Once real analyzers cover enough features, collect ~200 labeled product images per major phenotype category, run the pipeline, compare output to ground truth, and measure accuracy. Acceptance: <5% misassignment rate across all 26 phenotypes with no systematic error on any single category.
  - Answer: FROZEN: Premature. Revisit after per-feature Analyzer stubs are substantially resolved and BypassPhenotypes flip is planned.

-------
- [ ] Implement Analyzer_HasHuman.cs — measure `has-human`, `human-count`, `hero-is-human`
  - Sets: `has-human` (boolean), `human-count` (integer), `hero-is-human` (enum: TRUE/FALSE/UNKNOWN)
  - `hero-is-human` is composite: TRUE when `has-human = true` AND `human-count = 1` (sole human is the primary subject).
  - Method: person/body detector — CLIP prompt or dedicated pose model. CLIP already active in `ImageClassifier.cs`; try prompts first.
  - Signature convention: `Analyzer_HasHuman.Analyze(Image<Rgba32> image, ImageFeatureSnapshot snapshot)`
  - Answer:

-------
- [ ] Implement Analyzer_HasFace.cs — measure `has-head`, `head-visible`, `has-face`, `face-visible`, `body-visible`
  - Sets: `has-head` (boolean), `head-visible` (enum: FULL/PARTIAL/NONE/UNKNOWN), `has-face` (boolean), `face-visible` (boolean), `body-visible` (enum: full/three-quarter/half/bust/none/unknown)
  - Method: HAAR cascade + human anatomical proportions + skin-tone color detection (lips are always darker than surrounding skin and red-ish). Detection area for head limited to top half of image; body extent inferred from skeleton proportions vs. image size. See PRISM-classify.md sections "Human Detection" and "Head Visibility Detection".
  - Prerequisite: `Analyzer_HasHuman.cs` must set `has-human = true` before this analyzer fires.
  - Answer:

-------
- [ ] Implement Analyzer_HeroOrientation.cs — measure `hero-orientation`
  - Sets: `hero-orientation` (enum: FRONT/DIAGONAL/SIDEON/BACK/TOP/BOTTOM/UNKNOWN)
  - Method: CLIP-based — add prompts to `ClipPrompts.json` for orientation labels. Correlate with existing `front-view`, `side-view`, `rear-view`, `top-view` flags already computed.
  - Answer:

-------
- [ ] Implement Analyzer_PoseType.cs — measure `pose-type`, `contains-mannequin`
  - Sets: `pose-type` (enum: standing/sitting/crouching/lying/unknown), `contains-mannequin` (boolean)
  - Method: pose estimation model (skeleton keypoints) or CLIP prompt for gross pose categories. Mannequin: heuristic from skin-tone absence + human-like geometry (CLIP or HAAR).
  - Prerequisite: `Analyzer_HasHuman.cs` for human presence gating.
  - Answer:

-------
- [ ] Implement Analyzer_ProductTypeLabel.cs — measure `product-type-label`, `packaging-visible`, `multiple-products`
  - Sets: `product-type-label` (string), `packaging-visible` (boolean), `multiple-products` (boolean)
  - Method: CLIP-based classification using prompts from `ClipPrompts.json`. `multiple-products` can also use object detection count.
  - Answer:

-------
- [ ] Implement Analyzer_SalientBbox.cs — measure `salient-bbox` and all derived spatial features
  - Sets: `salient-bbox` (string — format TBD, e.g. "x,y,w,h" in pixels), `product-coverage-ratio` (float), `image-occupancy` (float), `crop-tightness` (float), `product-aspect-ratio` (float), `vertical-centering` (float), `horizontal-centering` (float)
  - All six derived features are computed from the bbox + image dimensions in one pass.
  - Method: object saliency detection. Options: (a) CPU-only contrast isolation (foreground vs. near-uniform background region — similar approach to `AnalyzeBackground` which already detects SOLIDCOLOR backgrounds), (b) lightweight ONNX object detection model. Option (a) first.
  - Answer:

-------
- [ ] Implement Analyzer_BackgroundColor.cs — measure `background-color`, `dominant-colors`, `product-color`
  - Sets: `background-color` (string hex or name), `dominant-colors` (string — comma-separated hex), `product-color` (string)
  - Method: topology + histogram.
    - Background detection: identify the continuous near-monotone region touching all four image edges that encloses a higher-contrast subject region. Use `AnalyzeBackground()` corner sampling as a starting point; extend to flood-fill for non-corner-adjacent backgrounds.
    - Dominant colors: build color histogram over entire image (excluding background), quantize to major clusters, output top N colors as hex.
    - Product color: dominant non-background color cluster.
  - Easy case already partially solved: `AnalyzeBackground()` in `ImageFeatureAnalyzer.cs` already detects near-white background via corner sampling.
  - Answer:

-------
- [ ] Implement Analyzer_TopView.cs — measure `top-view`, `camera-angle`
  - Sets: `top-view` (boolean), `camera-angle` (enum: eye-level/low-angle/high-angle/overhead/unknown)
  - Note: `top-view` is already used in `flatlay-front` and `flatlay-styled` phenotype rules.
  - Method: CLIP prompts for camera angle labels. `top-view = true` when `camera-angle = overhead`.
  - Answer:

-------
- [ ] Implement Analyzer_Indoor.cs — measure `indoor`, `outdoor`
  - Sets: `indoor` (boolean), `outdoor` (boolean)
  - Method: CLIP-based or derived from `background-type` (REALLIFE background + color histogram indicating natural/built environment).
  - Answer:

-------
- [ ] Implement Analyzer_SymmetryScore.cs — measure `symmetry-score`
  - Sets: `symmetry-score` (float, 0–1)
  - Method: CPU-only. Mirror the image horizontally, compute per-pixel grayscale difference, normalize by image area. High score = symmetric product (packshot candidate).
  - Answer:

-------
- [ ] Implement Analyzer_LogoPresent.cs — measure `logo-present`
  - Sets: `logo-present` (boolean)
  - Method: CLIP prompt ("product with visible brand logo / text on label") or text-region detection. Can share infrastructure with Analyzer_TextPresent.
  - Answer:

-------
- [ ] Implement Analyzer_TextPresent.cs — measure `text-present`
  - Sets: `text-present` (boolean)
  - Method: topology — detect high-density clusters of thin, high-contrast vertical/horizontal strokes in a regular grid (text character geometry). CLIP fallback.
  - Answer:

-------
- [ ] Implement Analyzer_ShadowPresent.cs — measure `shadow-present`
  - Sets: `shadow-present` (boolean)
  - Method: topology — look for a near-elliptical or elongated dark gradient region at the base of the subject on a light background. Darker than background by threshold, smoothly fading outward.
  - Answer:

-------
- [ ] Implement Analyzer_ReflectionPresent.cs — measure `reflection-present`
  - Sets: `reflection-present` (boolean)
  - Method: topology — detect a vertically symmetric dim or mirrored region below the subject's bottom edge on a reflective surface. Mirror the bottom N pixels and compare to the subject's bottom region.
  - Answer:

-------
- [ ] Implement Analyzer_MaterialTextureVisible.cs — measure `material-texture-visible`
  - Sets: `material-texture-visible` (boolean)
  - Method: CLIP-based ("visible fabric texture", "leather grain", "wood grain") or local variance analysis in product region (high local variance at medium frequency = visible texture).
  - Answer:

-------
- [ ] Implement Analyzer_Lighting.cs — measure `lighting`, `lighting-detail`
  - Sets: `lighting` (enum: EASY/HARD/UNKNOWN), `lighting-detail` (enum: flat/directional/high-key/low-key/mixed/unknown)
  - Method: histogram-based. EASY = narrow luminance range (flat/high-key studio). HARD = bimodal (bright highlight + deep shadow). CLIP secondary signal.
  - Answer:

-------
- [ ] Implement Analyzer_OverlapCount.cs — measure `overlap-count`
  - Sets: `overlap-count` (integer)
  - Method: detect distinct foreground object blobs — count of visible separable product silhouettes. Requires salient-bbox to be set first (use as seed region; count disconnected high-contrast sub-regions).
  - Prerequisite: `Analyzer_SalientBbox.cs`
  - Answer:

-------
- [ ] Implement Analyzer_ScaleReferencePresent.cs — measure `scale-reference-present`
  - Sets: `scale-reference-present` (boolean)
  - Method: CLIP prompt ("product photographed next to a hand / coin / ruler for scale"). Low priority.
  - Answer:
