# Prism Feedback Reload

This file is up to date with the current repo notes and the expanded `jbtodo.md` files under `jb/src`.

The detailed todo source of truth is now the local `jbtodo.md` file in each folder. This file should stay as the overview and reload point, not as a duplicate copy of every local todo.

## Current Status

- [x] Accepted media types are collated and deduplicated as jpg/jpeg, png, tif/tiff, pdf, webp, bmp, and gif.
- [x] Batch size wording is resolved as daily average versus design limit versus configured cap.
- [x] API route casing is standardized to `/PRISM/`.
- [x] External resources are allowed only before pipeline entry, except the approved upscaling API.
- [x] Multipage PDF/TIFF failure behavior tries first-page jpg fallback before KO.
- [x] Zip max size now agrees with `Prism_Config.json`.
- [x] API request size wording points to runtime config loaded by `Prism.cs`.
- [x] API route parameter syntax now uses query syntax close to the intended behavior.
- [x] Core model placeholder files exist in `core/Models`.
- [x] All folder-local `jbtodo.md` files now use concrete one-line todo descriptions.

## Open Work Index

- [ ] Root source organization: finish [jbtodo.md](jbtodo.md) decisions for source tree ownership, shared docs, fixtures, and README synchronization.
- [ ] API shape: finish [api/jbtodo.md](api/jbtodo.md) decisions for multipart fields, request/response models, errors, health, config, URL validation, request-size validation, and progress streaming.
- [ ] Core pipeline shape: finish [core/jbtodo.md](core/jbtodo.md) decisions for `Prism.Process`, config loading, resource lifecycle, stage order, progress events, failure policy, and config ownership.
- [ ] Excel behavior: finish [core/Excel/jbtodo.md](core/Excel/jbtodo.md) open decisions for exact-match header scoring, conflicting duplicate rows, conflicting duplicate columns, and invalid primary-key rows.
- [ ] IO behavior: finish [core/IO/jbtodo.md](core/IO/jbtodo.md) decisions for paths, streams, directories, multipart inputs, links, job folders, flat jpg conversion, metadata handling, KO reasons, and JSON export fields.
- [ ] Zip behavior: finish [core/IO/Zip/jbtodo.md](core/IO/Zip/jbtodo.md) decisions for KO entries, duplicate output filenames, layout configurability, and zip/JSON parity.
- [ ] Image records and filenames: finish [core/Images/jbtodo.md](core/Images/jbtodo.md) decisions for imported image state, token metadata, filename stems, suffixes, collisions, unmatched images, visual duplicates, invalid characters, and extensions.
- [ ] Matching rules: finish [core/Images/Match/jbtodo.md](core/Images/Match/jbtodo.md) decisions for numeric matching, string normalization, language/stop words, image labels, score aggregation, tie-breaking, evidence, and thresholds.
- [ ] Image ordering: finish [core/Images/Order/jbtodo.md](core/Images/Order/jbtodo.md) decisions for front/back/detail/ambiance/unknown ranking, tie-breakers, `_det` assignment, filename hints, and vision hints.
- [ ] Image classification: finish [core/Images/Classify/jbtodo.md](core/Images/Classify/jbtodo.md) decisions for human/head/border detection, orientation values, image type values, confidence values, and unknown states.
- [ ] ONNX runtime: finish [core/Images/Classify/ONNX/jbtodo.md](core/Images/Classify/ONNX/jbtodo.md) decisions for model ownership, runtime provider, session lifetime, diagnostics, and CPU fallback.
- [ ] CLIP model metadata: finish [core/Images/Classify/ONNX/clip-vit-b32-uint8/jbtodo.md](core/Images/Classify/ONNX/clip-vit-b32-uint8/jbtodo.md) decisions for provenance, license, version, checksum, rebuild/download instructions, tensors, normalization, tokenizer compatibility, prompts, thresholds, and expected outputs.
- [ ] Transform behavior: finish [core/Images/Transform/jbtodo.md](core/Images/Transform/jbtodo.md) decisions for preprocessor outputs, transform failures, detail crop behavior, center/stretch behavior, background fill, and KO rejection.
- [ ] Core model fields: finish [core/Models/jbtodo.md](core/Models/jbtodo.md) field decisions for product records, source images, processed images, matcher results, evidence, traits, transform results, output images, generated images, KO reasons, manifest, and progress events.
- [ ] Workbench parity: finish [workbench/jbtodo.md](workbench/jbtodo.md) decisions for shared web/WPF behavior, allowed differences, progress subscription, diagnostic snapshots, and raw pipeline visibility.
- [ ] Web workbench: finish [workbench/web/jbtodo.md](workbench/web/jbtodo.md) decisions for Next.js layout, upload behavior, API client, progress views, section data, CSS files, drag/drop errors, and validation states.
- [ ] WPF workbench: finish [workbench/wpf/jbtodo.md](workbench/wpf/jbtodo.md) decisions for WPF layout, direct core invocation, local file selection, progress display, diagnostics, and web parity.

## Immediate Priority

- [ ] Define the core model fields first because they are shared by API, core, matching, transform, export, manifest, and workbench.
- [ ] Define `Prism.Process` input and output types after core model fields are chosen.
- [ ] Define manifest and progress event shapes before implementing API or workbench behavior.
- [ ] Define matcher result and evidence fields before implementing numeric, string, or image-label matching.
- [ ] Define transform result fields before implementing preprocessing, detail cropping, or center-and-stretch.

## Local Todo Files Checked

- [x] [jbtodo.md](jbtodo.md)
- [x] [api/jbtodo.md](api/jbtodo.md)
- [x] [core/jbtodo.md](core/jbtodo.md)
- [x] [core/Excel/jbtodo.md](core/Excel/jbtodo.md)
- [x] [core/IO/jbtodo.md](core/IO/jbtodo.md)
- [x] [core/IO/Zip/jbtodo.md](core/IO/Zip/jbtodo.md)
- [x] [core/Images/jbtodo.md](core/Images/jbtodo.md)
- [x] [core/Images/Match/jbtodo.md](core/Images/Match/jbtodo.md)
- [x] [core/Images/Order/jbtodo.md](core/Images/Order/jbtodo.md)
- [x] [core/Images/Transform/jbtodo.md](core/Images/Transform/jbtodo.md)
- [x] [core/Images/Classify/jbtodo.md](core/Images/Classify/jbtodo.md)
- [x] [core/Images/Classify/ONNX/jbtodo.md](core/Images/Classify/ONNX/jbtodo.md)
- [x] [core/Images/Classify/ONNX/clip-vit-b32-uint8/jbtodo.md](core/Images/Classify/ONNX/clip-vit-b32-uint8/jbtodo.md)
- [x] [core/Models/jbtodo.md](core/Models/jbtodo.md)
- [x] [workbench/jbtodo.md](workbench/jbtodo.md)
- [x] [workbench/web/jbtodo.md](workbench/web/jbtodo.md)
- [x] [workbench/wpf/jbtodo.md](workbench/wpf/jbtodo.md)

## Reload Notes

- `AGENTFEEDBACK.md` should be reloaded again after a group of local `jbtodo.md` items is completed.
- Do not use this file for fine-grained implementation detail when a folder-local `jbtodo.md` already owns that detail.
- If a local todo is solved in its folder, update that local todo first and then update this overview.
