# AGENTFEEDBACK.md

Agent-owned reload memory. Not authoritative project docs. Accepted knowledge in `jb/docs/`. Abbreviations: `jb/docs/GLOSSARY.md`.

## Current Milestone

**M5 Classification Groundwork** — active gate. Blocks M6+.

Gate: All Classify `jbtodo.md` decisions answered; ONNX session migrated to application-scoped singleton.

## Active Tickets

| Ticket | Status | Description |
|---|---|---|
| T-1300 | Ready | `Fetch_HTTPS_DirectFile.cs` — stream download to temp, validate against `HostRules.json` |
| T-1400 | Blocked | `Fetch_DropBox.cs` — awaiting product scope decision (public-only vs. OAuth) |

## Project Constraints

- **ImageSharp**: locked at 3.1.12 (Apache 2.0). 4.0.0 rejected — commercial license.
- **Queue**: V1 = single-server in-process bounded queue. RabbitMQ deferred until durable recovery or distributed workers are needed.
- **CLIP model**: `sentence-transformers/clip-ViT-B-32`. SHA-256: see `jb/docs/PRISM-classify.md`.

## Open Work Summary

| Folder | Count | Topics |
|---|---|---|
| `Images/Classify/` | 7 | ONNX singleton (answered, not yet implemented), taxonomy list, illustration-technical-drawing scope, RecordUnknownFeatures() stub, production validation protocol |
| `Images/Match/` | 5 | 4 spec deviations (scoring formula, ME missing fields, Bracket-3 duplicate guard, original token text) + 1 user decision (cross-bracket tie resolution) |
| `Images/Order/` | 1 | det0 SIDE fallback — algorithm change vs. post-process promotion |
| `Images/Transform/` | 15 | 11 design decisions (crop/fill/resize/headcut/saliency/KO policy) + 4 impl stubs |
| `Images/Generate/` | 1 | wire real `GenerationBackendAvailable()`; recommend ComfyUI + Flux.1-schnell on-prem |
| `IO/` | 5 | WeTransfer (deferred), media-triage byte-header, SD-7 BatchManifest fields, SD-14 KO zip path, directory input |
| `api/` | 2 | `PrismApiModels.cs` split (one-type-per-file), SD-13 JSON output `images` shape |



## Reload Rules

1. Load `jb/docs/` first — owns accepted knowledge.
2. Use folder-local `jbtodo.md` for unresolved/pending decisions.
3. Accepted todo → move to `jb/docs/`, remove local block, delete `jbtodo.md` if empty.