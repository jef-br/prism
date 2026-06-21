# Order Stage Open Decisions
- [ ] Default det0 SIDE fallback: spec says FRONT → SIDE → DIAGONAL but current default det0 only lists FRONT and DIAGONAL phenotypes, skipping SIDE.
  - File: `jb/src/core/Images/Order/DetOrderRules.json`.
  - Issue: SIDE is absent.
    - Adding SIDE phenotypes to the det0 list alone does not fix the fallback behavior because the ordering algorithm assigns each image to its best-ranked slot.
      -  a SIDE image qualifies for det2 (rank 0) over det0 (rank 2) and will always prefer det2.
      -  The fallback cannot work purely through phenotype list ordering.
  - Fix options: (a) Algorithm change: add a det0-priority pass that fills det0 first, then assigns remaining images to their best non-det0 slot. (b) Post-processing: if det0 is unfilled after primary assignment, promote the det2 image to det0 and leave det2 empty. Requires user decision on which approach to use.
dot
  - Answer: Similar to b.
    - Add a boolean configurable parameter to `prism_config.json` "DET-ORDER-GAPS-ALLOWED