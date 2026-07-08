# Image Classification Todo

-------
- [ ] Define final ImageNGP taxonomy and feature combinations: list all possible ImageNGPs and the ImageFeature values required to derive each phenotype.
  - Answer: FROZEN: Taxonomy is captured in canonical files (ImageNGP.json, ImageRoles.json, imagePhenotypes.md, ImageFeatures.md). No reconciliation action needed at this time.

-------
- [ ] Phenotype production validation: define the protocol and acceptance criteria required before phenotype assignment can be trusted in production.
  - Issue: The 26 phenotypes in `imagePhenotypes.md` were defined from spec and taxonomy documentation without real-image testing. Production-quality assignment requires validation against a representative labeled image set. Currently most features are UNKNOWN (see per-feature Analyzer todos in `jb/src/core/Images/Analyzers/jbtodo.md`), so phenotype assignment is unreliable for any image where CLIP evidence is needed.
  - What is needed: Once real analyzers cover enough features, collect ~200 labeled product images per major phenotype category, run the pipeline, compare output to ground truth, and measure accuracy. Acceptance: <5% misassignment rate across all 26 phenotypes with no systematic error on any single category.
  - Answer: FROZEN: Premature. Revisit after per-feature Analyzer stubs are substantially resolved and BypassPhenotypes flip is planned.
