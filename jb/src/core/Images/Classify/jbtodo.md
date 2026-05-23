# Image Classification Todo

- [ ] Define canonical image type classification values: confirm whether `ImageNGP.TypeOfShot` is the canonical type list and whether it needs an `UNKNOWN` value.
  - Impact:
    - Project progress: High - Image type controls matching evidence, transform behavior, ordering, and output quality rules.
    - Effect on other TODOs: Blocks - It gates transform-facing type output, ordering rules, `ImageNGP.cs` fields, `ImageRecord_LAMBDA.cs` fields, and unknown-state policy.
  - Industry standard:
    Vision pipelines use bounded enumerations for item type with confidence and unknown states so downstream stages can make deterministic decisions.
  - Recommended solution:
    Treat the current `ImageNGP.TypeOfShot` values (`PACKSHOT`, `ONMODEL`, `DETAIL`, `LIFESTYLE`, `GHOST`, `FLAT`, `STILLIFE`) as the current draft taxonomy, then decide whether an explicit `UNKNOWN` value or separate unavailable state is required.
  - Answer: **FROZEN TODO**
