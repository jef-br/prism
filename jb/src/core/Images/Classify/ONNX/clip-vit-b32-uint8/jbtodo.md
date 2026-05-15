# clip-vit-b32-uint8 Todo

- [ ] Define model license: record whether the model can be redistributed inside this repo or must be downloaded separately.
  - Impact:
    - Project progress: High - License status determines whether the model can ship with the project at all.
    - Effect on other TODOs: Blocks - It gates provenance, download instructions, rebuild instructions, and repository packaging.
  - Industry standard:
    ML pipelines document model license and redistribution rights before committing weights, because model artifacts often have different obligations than source code.
  - Recommended solution:
    Record the upstream license, redistribution permission, and whether `model_uint8.onnx` must be downloaded outside git.
  - Answer:

- [ ] Define model provenance: record where `model_uint8.onnx` came from and who produced or converted it.
  - Impact:
    - Project progress: High - Provenance is required for trust, reproducibility, and model replacement.
    - Effect on other TODOs: Blocks - It supports version, checksum, license, rebuild, and diagnostic policy.
  - Industry standard:
    Production ML assets include source repository/model ID, converter, conversion command, date, and responsible owner.
  - Recommended solution:
    Document upstream model identifier, source URL, converter/toolchain, conversion owner, conversion date, and local artifact path.
  - Answer:

- [ ] Define model version: record the exact upstream model, conversion date, and quantization variant.
  - Impact:
    - Project progress: High - Versioning makes inference behavior reproducible across machines and future upgrades.
    - Effect on other TODOs: Blocks - It affects tokenizer compatibility, tensor definitions, normalization values, and expected outputs.
  - Industry standard:
    ML deployments pin exact model versions and conversion variants instead of relying on generic model family names.
  - Recommended solution:
    Record the exact CLIP variant, upstream revision, ONNX opset, quantization method, and conversion date.
  - Answer:

- [ ] Define model checksum: record a hash used to verify the local `.onnx` file before inference.
  - Impact:
    - Project progress: High - Checksum validation prevents silent model corruption or accidental replacement.
    - Effect on other TODOs: Unblocks - It feeds ONNX model ownership, health checks, and model download verification.
  - Industry standard:
    Model artifacts are verified by cryptographic hash during startup/readiness, especially when downloaded outside the repository.
  - Recommended solution:
    Store a SHA-256 checksum for `model_uint8.onnx` and verify it before creating an inference session.
  - Answer:

- [ ] Define input tensor names: list the exact ONNX input names used by the inference code.
  - Impact:
    - Project progress: High - Tensor names are required before inference code can bind inputs reliably.
    - Effect on other TODOs: Blocks - It gates tensor shapes, dtypes, normalization, and runtime diagnostics.
  - Industry standard:
    ONNX integrations pin input/output names and validate them at model load rather than discovering failures per item.
  - Recommended solution:
    Record exact image and text input tensor names from the loaded ONNX graph and validate them in the model provider.
  - Answer:

- [ ] Define output tensor names: list the exact ONNX output names used by the inference code.
  - Impact:
    - Project progress: High - Output names define how scores and embeddings are read from inference.
    - Effect on other TODOs: Blocks - It affects emitted labels, thresholds, diagnostics, and expected output tests.
  - Industry standard:
    Inference wrappers validate expected output tensors at startup so model swaps fail fast when contracts differ.
  - Recommended solution:
    Record the exact output tensor names for logits/embeddings and assert them during model initialization.
  - Answer:

- [ ] Define tensor shapes: list expected image and text tensor dimensions.
  - Impact:
    - Project progress: High - Shapes define preprocessing and batching behavior.
    - Effect on other TODOs: Blocks - It gates normalization values, tokenizer compatibility, and inference input construction.
  - Industry standard:
    ML pipelines document static and dynamic tensor dimensions so preprocessing can be validated before runtime.
  - Recommended solution:
    Record batch dimension behavior, image tensor dimensions, token sequence length, and any dynamic axes.
  - Answer:

- [ ] Define tensor dtypes: list expected numeric types for each input and output tensor.
  - Impact:
    - Project progress: High - Dtypes must match the quantized model contract.
    - Effect on other TODOs: Blocks - It affects preprocessing, tokenization, inference binding, and diagnostics.
  - Industry standard:
    Quantized models require explicit dtype documentation because image, token, and output tensors may use different numeric types.
  - Recommended solution:
    Record dtype for image input, text/token input, masks if any, logits, and embedding outputs.
  - Answer:

- [ ] Define normalization values: list image resize, channel order, mean, standard deviation, and scaling rules.
  - Impact:
    - Project progress: High - Normalization determines whether model scores are meaningful.
    - Effect on other TODOs: Blocks - It affects emitted labels, thresholds, expected outputs, and classification reliability.
  - Industry standard:
    Vision model deployments pin preprocessing exactly to the training model, including resize method, crop, channel order, scaling, mean, and standard deviation.
  - Recommended solution:
    Document CLIP-compatible resize/crop, RGB channel order, scaling, mean/std values, and quantization-specific requirements.
  - Answer:

- [ ] Define tokenizer compatibility for `vocab.json`: say which model version this vocabulary must match.
  - Impact:
    - Project progress: High - Vocabulary mismatch can silently corrupt text embeddings and label scores.
    - Effect on other TODOs: Blocks - It affects prompts, thresholds, expected outputs, and model versioning.
  - Industry standard:
    Text-image models pin tokenizer assets to the exact upstream model revision and validate asset hashes.
  - Recommended solution:
    Record the upstream tokenizer revision for `vocab.json` and verify it against the model version/checksum metadata.
  - Answer:

- [ ] Define tokenizer compatibility for `merges.txt`: say which model version these BPE merges must match.
  - Impact:
    - Project progress: High - BPE merge mismatch changes token IDs and invalidates prompt scores.
    - Effect on other TODOs: Blocks - It supports prompt definitions, thresholds, and expected output verification.
  - Industry standard:
    Tokenizer merge files are treated as model artifacts with the same version and checksum discipline as weights.
  - Recommended solution:
    Record the exact upstream revision for `merges.txt` and verify it together with `vocab.json`.
  - Answer:

- [ ] Define image-label prompts: list the prompt strings used to classify product traits and visual concepts.
  - Impact:
    - Project progress: High - Prompt strings define the label taxonomy emitted by CLIP.
    - Effect on other TODOs: Blocks - It feeds emitted image labels, classification traits, ordering hints, and thresholds.
  - Industry standard:
    Prompt-based classifiers use versioned prompt sets with stable labels, prompt text, category grouping, and evaluation examples.
  - Recommended solution:
    Store prompts in a classification-local config grouped by trait, with stable label IDs and prompt text.
  - Answer:

- [ ] Define image-label thresholds: say which scores become accepted labels and which remain uncertain.
  - Impact:
    - Project progress: High - Thresholds determine when vision evidence is trusted.
    - Effect on other TODOs: Blocks - It affects emitted labels, unknown classification states, matcher aggregation, and ordering.
  - Industry standard:
    ML labelers use calibrated thresholds per label group and expose uncertain results rather than forcing weak labels.
  - Recommended solution:
    Define per-category acceptance and uncertainty thresholds, with below-threshold results emitted as unknown/uncertain.
  - Answer:

- [ ] Define expected image-label outputs: give sample tags for a known product image so implementation can be checked.
  - Impact:
    - Project progress: Medium - Expected outputs provide a smoke test for model wiring and prompt behavior.
    - Effect on other TODOs: Unblocks - It supports normalization validation, thresholds, prompts, and diagnostic tests.
  - Industry standard:
    Inference integrations keep golden examples that verify preprocessing, tokenizer assets, model outputs, and threshold logic.
  - Recommended solution:
    Add one or more sanitized golden product images with expected accepted/uncertain labels and score tolerances.
  - Answer:

- [ ] Define model download instructions: document where to obtain the model if it cannot be stored in git.
  - Impact:
    - Project progress: Medium - Download instructions help setup once license and artifact policy are known.
    - Effect on other TODOs: Influences - It depends on license, provenance, version, and checksum decisions.
  - Industry standard:
    Repositories avoid committing large or restricted model binaries and provide repeatable download steps with checksum verification.
  - Recommended solution:
    Document the approved download source, destination path, expected filename, and SHA-256 verification command.
  - Answer:

- [ ] Define model rebuild instructions: document the commands or workflow needed to recreate the `.onnx` file.
  - Impact:
    - Project progress: Medium - Rebuild instructions make model updates reproducible but follow provenance/version decisions.
    - Effect on other TODOs: Influences - It supports versioning, quantization details, and model checksum refresh.
  - Industry standard:
    ML projects treat conversion as reproducible build work with pinned tools, commands, opset, quantization settings, and validation steps.
  - Recommended solution:
    Document the exact conversion environment, commands, quantization settings, validation command, and checksum generation step.
  - Answer:
