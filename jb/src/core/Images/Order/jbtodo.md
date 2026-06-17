# Order Stage Open Decisions
- [ ] Default det0 SIDE fallback: spec says FRONT → SIDE → DIAGONAL but current default det0 only lists FRONT and DIAGONAL phenotypes, skipping SIDE.
  - File: `jb/src/core/Images/Order/DetOrderRules.json`.
  - Issue: AGENTFEEDBACK.md (det0 orientation 2026-06-15) specifies the fallback chain for det0 as FRONT → SIDE → DIAGONAL. The default product type det0 has ["front-packshot", "front-on-model-full-product", "diagonal-packshot"] — SIDE is absent. Adding SIDE phenotypes to the det0 list alone does not fix the fallback behavior because the ordering algorithm assigns each image to its best-ranked slot: a SIDE image qualifies for det2 (rank 0) over det0 (rank 2) and will always prefer det2. The fallback cannot work purely through phenotype list ordering.
  - Fix options: (a) Algorithm change: add a det0-priority pass that fills det0 first, then assigns remaining images to their best non-det0 slot. (b) Post-processing: if det0 is unfilled after primary assignment, promote the det2 image to det0 and leave det2 empty. Requires user decision on which approach to use.

- [ ] No code-level guard ensuring illustration-technical-drawing is always the last configured det slot.
  - File: `jb/src/core/Images/Order/ImageOrderer.cs`.
  - Issue: The spec says illustration-technical-drawing always gets the last configured det slot. This is currently enforced only through the JSON config (it appears in det7 for all product types). If a config edit places it in an earlier slot, the code will honor it silently. A runtime assertion or validation step would catch mis-configured rules.
  - Fix: Add validation in DetOrderConfig that verifies illustration-technical-drawing only appears in the last det slot index for each product type.

- [ ] OrderEvidence missing full qualifying candidate set.
  - File: `jb/src/core/Images/Order/ImageOrderer.cs`, `jb/src/core/Images/Order/OrderEvidence.cs`.
  - Issue: PRISM-models.md requires "DetOrder assignment evidence must record which ImageNGP/DetOrder combinations qualified." Only the winning combination is stored. The losing qualifying combinations (e.g., image qualified for det2 and det4 but was placed at det2) are discarded.
  - Fix: Add a field to OrderEvidence storing the full list of qualifying slot indices and their phenotype ranks.
