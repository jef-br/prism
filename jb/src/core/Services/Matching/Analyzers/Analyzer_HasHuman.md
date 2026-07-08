# Analyzer_HasHuman

**Status:** implemented (reworked from HSV skin heuristic to YOLO) - **Wave:** 2 - **Writes:** `has-human`, `human-count`, `hero-is-human`

## How it works
YOLO person detections at or above `Yolo.HumanMinConfidence` set has-human true (confidence = best detection) and human-count. No person: false at `Yolo.AbsenceConfidence` (absence is weaker evidence than presence).

hero-is-human is derived from dominance: a person box covering at least `Yolo.HeroPersonMinArea` of the frame means the human wearing the product is the hero (TRUE); no person at all means the hero cannot be human (FALSE); a small person (scale reference, bystander) leaves it UNKNOWN. Stronger existing evidence (CLIP) is never overwritten. This feeds the on-model-before-packshot overflow rule in ImageOrderer and unblocks the hero-is-human conditions in packshot/closeup phenotype rules.

## History
The old HSV skin-ratio version ran from ImagePreProcessor at Transform time - too late for phenotype rules and fooled by skin-colored products. Retired; the two-orchestrator split is resolved (all analyzers now run via ImageFeatureAnalyzer).

## Open questions
- [ ] Validate person recall on partial bodies (legs-only apparel shots) against real batches.
- [ ] HeroPersonMinArea 0.15 is a first guess - calibrate against on-model vs scale-reference shots.
