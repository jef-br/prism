# Image Transform Todo

- [ ] Define transform-facing ImageFeature and ImageNGP output: say which ImageFeatures and selected ImageNGP phenotypes feed crop and center logic, including what happens when transform-critical features are unknown.
  - Impact:
    - Project progress: High - Transform rules depend on selected ImageNGP phenotype plus features such as type-of-shot, orientation, background, human/head evidence, edge intersections, object bounds, and detail/whole-product evidence.
    - Effect on other TODOs: Blocks - It links ImageFeature output, selected ImageNGP phenotype, the open classification todo, crop behavior, fill policy, and fallback rules.
  - Industry standard:
    Transform engines consume bounded classification/analyzer features and selected image phenotypes rather than re-inferring product semantics inside crop code.
  - Recommended solution:
    Pass transform-relevant ImageFeatures and the selected ImageNGP phenotype with confidence from classification into transform decisions, and define a safe fallback for unavailable or below-threshold feature evidence.
  - Answer:

- [ ] Define transform failure, fallback, and fill-KO policy: list which transform problems become KO, which eligible fill failures can still export, and what fallback path is used after border-intersection no-reposition cases are excluded.
  - Impact:
    - Project progress: High - Failure and fallback policy decides when visual quality is too risky to export while preventing transform exceptions from becoming full-batch failures.
    - Effect on other TODOs: Blocks - It affects KO reasons, `ImageTransformationResult`, manifest projection, JSON/zip status, background fill, center-and-stretch behavior, and output quality warnings.
  - Industry standard:
    Batch image processors distinguish recoverable quality warnings from unrecoverable transform failures, use deterministic fallback paths for low-confidence geometry, and record per-item KO instead of crashing the batch.
  - Recommended solution:
    KO images when decode/normalized input is invalid, object bounds are unusable for required transforms, required output size or margins cannot be met, or fill/crop artifacts exceed configured quality thresholds. Export with warnings only when fallback resize/crop/fill output remains acceptable.
  - Answer:

- [ ] Define crop decision output for eligible images: say how crop coordinates, anchors, confidence, and non-repositionable border-intersection decisions are represented.
  - Impact:
    - Project progress: High - Crop decisions are the main transform artifact that diagnostics and output generation need.
    - Effect on other TODOs: Unblocks - It feeds `ImageTransformationResult`, detail crop behavior, center-and-stretch fallback, border-intersection handling, and workbench snapshots.
  - Industry standard:
    Transform stages record requested crop, clamped crop, anchor rules, confidence, and warnings so outputs can be audited.
  - Recommended solution:
    Store crop rectangle, anchor edges, rule name, confidence, clamping/fill requirements, warnings, and the explicit no-reposition state for border-intersecting images.
  - Answer:

- [ ] Define background fill policy for eligible crop and center operations: choose allowed methods for images that are not blocked by border-intersection no-reposition rules.
  - Impact:
    - Project progress: High - Fill policy controls visual quality, compute cost, and whether external dependencies are needed.
    - Effect on other TODOs: Blocks - It gates detail crop fill, center-and-stretch extension, cleanup, KO rejection, and no-fill handling for border-intersecting images.
  - Industry standard:
    Product-image pipelines use deterministic cheap fills first, then controlled inpainting/generation only when allowed and traceable.
  - Recommended solution:
    Allow edge extension, local blur/clone, solid fill, and optional local inpainting for eligible images; do not rely on external SaaS generation for core transforms.
  - Answer:

- [ ] Define resize decision output: say how preprocessor reports upscale, downscale, or no-resize decisions.
  - Impact:
    - Project progress: Medium - Resize metadata supports quality control and manifest diagnostics.
    - Effect on other TODOs: Influences - It affects `ImageTransformationResult`, output dimensions, config limits, and workbench display.
  - Industry standard:
    Image transforms record input size, target size, scale factor, interpolation method, and whether quality limits were exceeded.
  - Recommended solution:
    Emit resize mode, scale factor, input/output dimensions, interpolation method, and warning when configured upscale/downscale limits are approached.
  - Answer:

- [ ] Define transform result for border-intersecting detail crops: say how the no-reposition decision is recorded and whether the image exports unchanged or becomes KO.
  - Impact:
    - Project progress: Medium - Border-intersecting detail crops need a deterministic output status after classification has marked them as non-repositionable.
    - Effect on other TODOs: Influences - It depends on border intersection detection and feeds crop decision output, `ImageTransformationResult` status, and manifest projection.
  - Industry standard:
    Crop systems preserve edge contact when content is intentionally or inherently clipped at the source boundary and record when transforms are skipped.
  - Recommended solution:
    Record the border-intersection no-reposition decision, then define whether the unmodified normalized image can be exported with a warning or must become KO.
  - Answer:

- [ ] Define detail crop saliency map behavior for eligible images: say how the most important object region influences square crop placement when no border intersection blocks repositioning.
  - Impact:
    - Project progress: Medium - Saliency improves detail crops but depends on object bounds and crop policy.
    - Effect on other TODOs: Influences - It affects crop anchors, greedy crop behavior, and transform diagnostics.
  - Industry standard:
    Detail crop algorithms use saliency or attention maps to keep the highest-value region inside the target aspect ratio.
  - Recommended solution:
    Center the square crop on the dominant saliency region while respecting edge anchors and minimum content retention.
  - Answer:

- [ ] Define detail crop headcut thresholds and placement: say which human/head confidence thresholds enable headcut and how top crop placement changes for eligible non-intersecting images.
  - Impact:
    - Project progress: Medium - Headcut rules affect fashion/clothing outputs and must be predictable.
    - Effect on other TODOs: Influences - It depends on human/head traits, top-edge intersection, border-intersection no-reposition handling, and crop anchors.
  - Industry standard:
    Apparel pipelines make person-specific crop rules explicit and gated by confidence to avoid accidental face/head removal.
  - Recommended solution:
    Apply headcut only when configured and the answered human/head detection signals meet explicit thresholds; otherwise preserve the detected head region.
  - Answer:

- [ ] Define detail crop greedy crop behavior for eligible images: say how much original content to keep when no headcut is requested and no border intersection blocks repositioning.
  - Impact:
    - Project progress: Medium - Greedy crop behavior controls balance between detail focus and preserving context.
    - Effect on other TODOs: Influences - It uses saliency, crop decision output, and fill policy.
  - Industry standard:
    Crop systems define minimum content retention and padding rules so outputs are consistent across image sets.
  - Recommended solution:
    Keep as much original content as possible while meeting target aspect ratio and configured margin, using fill only when needed.
  - Answer:

- [ ] Define detail crop fill policy for eligible images: say how missing pixels are created when the crop extends beyond the original image and border-intersection rules do not block manipulation.
  - Impact:
    - Project progress: Medium - Detail crop fill must align with global background fill quality rules.
    - Effect on other TODOs: Influences - It depends on background fill policy and KO rejection rules.
  - Industry standard:
    Missing pixels outside a crop are generated with the least destructive background-compatible method and recorded as a transform operation.
  - Recommended solution:
    Use the global fill policy in priority order and record fill method, confidence, and warnings in `ImageTransformationResult`.
  - Answer:

- [ ] Define center-and-stretch cleanup method: choose the cleanup technique used after stretching or filling background.
  - Impact:
    - Project progress: Medium - Cleanup improves visual quality but follows fill method selection.
    - Effect on other TODOs: Influences - It affects background extension diagnostics and KO policy.
  - Industry standard:
    Post-fill cleanup uses local smoothing, feathering, or inpainting only near seams while preserving subject pixels.
  - Recommended solution:
    Use feathering and local smoothing by default, with optional inpainting for artifacts above a configured threshold.
  - Answer:

