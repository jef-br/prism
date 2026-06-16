# Image Classification Todo

- [ ] Define final ImageNGP taxonomy and feature combinations: list all possible ImageNGPs and the ImageFeature values required to derive each phenotype.
  - Impact:
    - Project progress: High - ImageFeatures and ImageNGPs control matching evidence, transform behavior, DetOrder assignment, and output quality rules.
    - Effect on other TODOs: Blocks - It gates ordering rules, `ImageNGP.cs` fields, `ImageRecord_LAMBDA.cs` fields, transform-facing phenotype use, and unknown-state handling for derived phenotypes.
  - Industry standard:
    Vision pipelines keep measured attributes separate from derived image phenotypes, then document the feature combinations that produce each phenotype so downstream stages can make deterministic decisions.
  - Recommended solution:
    Use the accepted `jb/docs/PRISM-classify.md` decision as the baseline: ImageFeatures are measured attributes with source/confidence/unknown state, and `ImageNGP` is a phenotype derived from combinations of ImageFeatures rather than a single `TypeOfShot` list. Complete this todo by listing the concrete ImageNGP values and their required feature combinations.
  - Answer:
