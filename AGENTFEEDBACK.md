# AGENTFEEDBACK.md

Static reload memory. Not a ticket board. Accepted knowledge lives in `jb/docs/`. Active tickets and milestone state live in `AGENT-TICKETS.md`.

## Project Constraints

- **ImageSharp**: locked at 3.1.12 (Apache 2.0). 4.0.0 rejected — commercial license.
- **Queue**: V1 = single-server in-process bounded queue. RabbitMQ deferred until durable recovery or distributed workers are needed.
- **CLIP model**: `sentence-transformers/clip-ViT-B-32`. SHA-256: see `jb/docs/PRISM-classify.md`.

## Config Locations

| File | Path |
|---|---|
| `Prism_Config.json` | `jb/src/core/` |
| `ExcelConfig.json` | `jb/src/core/Excel/` |
| `MatchingConfig.json` | `jb/src/core/Images/Match/` |
| `ImageNGP.json` / `ImageRoles.json` | `jb/src/core/ImageNGP/` |
| `ClipPrompts.json` | `jb/src/core/Images/Classify/` |
| `DetOrderRules.json` | `jb/src/core/Images/Order/` |
| `HostRules.json` | `jb/src/core/IO/cfg/` |
| `TranslationConfig.json` | `jb/src/core/Images/Match/Translate/` |

## Behavioral Memory

Resolved decisions and gotchas agents should know before touching code:

- **ONNX singleton**: migration to application-scoped singleton is an *answered* decision — do not re-open. Not yet implemented; tracked in T-2600.
- **`illustration-technical-drawing` scope**: option (b) accepted — null/no-phenotype. Do not add a phenotype for it.
- **CLIP input_ids**: combined-input `Run()` is the fix for the input_ids inference bug. Already merged. Do not revert to separate runs.
- **Dual-interface contract**: all `Tx_*` processing-tool classes expose `Process(byte[] arr, int stride, float upscale_factor)`. Match this signature exactly.

## Reload Rules

1. Load `jb/docs/` first — owns accepted knowledge.
2. Use folder-local `jbtodo.md` for unresolved/pending decisions.
3. Accepted todo → move decision to `jb/docs/`, remove local block, delete `jbtodo.md` if empty.
4. Active tickets and milestone state → read `AGENT-TICKETS.md`, not this file.