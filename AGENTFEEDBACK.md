# AGENTFEEDBACK.md

Static reload memory. Not a ticket board. Accepted knowledge lives in `jb/docs/`. Active tickets and milestone state live in `AGENT-TICKETS.md`.

## Project Constraints

- **ImageSharp**: locked at 3.1.12 (Apache 2.0). 4.0.0 rejected — commercial license.
- **Queue**: V1 = single-server in-process bounded queue. RabbitMQ deferred until durable recovery or distributed workers are needed.
- **Classification model**: `sentence-transformers/clip-ViT-B-32`. SHA-256: see `jb/docs/PRISM-classify.md`.
- **Model paths**: CLIP (`clip-vit-b32-uint8`) and Upscale (`Real-ESRGAN_x2plus.onnx`) model paths live in `Prism_Config.json`'s `Models` section — not hard-coded. See config table below. Upscale model inspiration (just for me, not for claude): https://github.com/upscayl/custom-models

## Config Locations

All runtime config JSON is centralized in `jb/src/core/config/` (copied to output via `Prism.Core.csproj` `Content`). This includes `Prism_Config.json` (with `Models`: CLIP/YOLO/Upscale paths), `ExcelConfig.json`, `MatchingConfig.json`, `TranslationDictionary.json`, `ImageNGP.json`, `ImageRoles.json`, `ClipPrompts.json`, `DetOrderRules.json`, `DetOrderKeywordStems.json`, `HostRules.json`, `analyzer_Config.json`, `ProductTypeMap.json`.

**Restructure (2026-07-08):** `jb/src/core/` split into `Services/` (`Prism.Services.*`: Matching/Transform/Generate/Upscale) and `lib/` (`Prism.Lib.*`: Excel/Ingress/Export/Zip/ImageNGP); contract types are `Prism.Contracts`; orchestrator + `Services/` glue stay `Prism.Core`. Model assets resolve via source-tree walk: CLIP `Services/Matching/Classify/ONNX/`, YOLO `Services/Matching/Analyzers/ONNX/`, Real-ESRGAN `Services/Upscale/Engine/ONNX/`.

## Behavioral Memory

Resolved decisions and gotchas agents should know before touching code:

- **ONNX singleton**: migration to application-scoped singleton is an *answered* decision — do not re-open. Implemented (done 2026-06-29, per M5 milestone gate): `MatchingService` owns one shared `ImageClassifier` (`_sharedClassifier`) for the job's lifetime, handed to every `ClassificationService`; a `_clipLock` serializes `InferenceSession.Run()` calls.
- **`illustration-technical-drawing` scope**: option (b) accepted — null/no-phenotype. Do not add a phenotype for it.
- **CLIP input_ids**: combined-input `Run()` is the fix for the input_ids inference bug. Already merged. Do not revert to separate runs.
- **Dual-interface contract**: all `Tx_*` processing-tool classes expose `Process(byte[] arr, int stride, float upscale_factor)`. Match this signature exactly.
- **WPF workbench deleted (2026-07-10)**: the web workbench (`jb/src/workbench/web/`) is the only workbench frontend. `Prism.Workbench.Wpf` was removed from `PRISM.sln` and `jb/src/workbench/wpf/` deleted. Do not re-add WPF parity language or web/WPF "allowed differences" — workbench docs are web-only.
- **Core co-deployment (2026-07-15, T-3600; refined by T-3300)**: Ingress + Matching + Export always run on one physical system sharing the job temp folder (the artifact bus). Do not propose shipping normalized image bytes over the wire for Matching; `NormalizedJpgPath` as a local absolute path is the deliberate contract. The four public services (Matching/Generate/Transform/Upscale) may run as their own `Prism.ServiceHost` processes — Matching only co-located on the same filesystem Ingest wrote. Ingest is never a service. See `PRISM-overview.md` "Core vs. Features" and `PRISM-io-import.md` "Co-Deployment Contract".
- **Batch cap is 10000 images / 250 MB per file (2026-07-17)**: `Input.Images.amount.max` in `Prism_Config.json` is 10000, `Input.Images.filesize.max` is 262144000 bytes (250 MB) — a single hard cap, not a two-tier "normal/ceiling" limit. Docs previously said 2500/5000 and 25 MB (stale since the cap was bumped in commit `57da823` without a doc update); `PRISM-overview.md`, `PRISM-information.md`, `PRISM-knowledge-base.md` now match the config. Read the config, not old doc numbers, if this value matters again.
- **Doc sweeps must glob `*.md` and `*.MD` separately**: `jb/docs/ImageNGP/PRODUCTTYPES.MD` uses an uppercase extension; case-sensitive `*.md` globs silently skip it. Confirmed while closing the docs-vocabulary jbtodo (2026-07-17).

## Reload Rules

1. Load `jb/docs/` first — owns accepted knowledge.
2. Use folder-local `jbtodo.md` for unresolved/pending decisions.
3. Accepted todo → move decision to `jb/docs/`, remove local block, delete `jbtodo.md` if empty.
4. Active tickets and milestone state → read `AGENT-TICKETS.md`, not this file.