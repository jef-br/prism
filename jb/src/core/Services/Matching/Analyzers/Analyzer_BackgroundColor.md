# Analyzer_BackgroundColor

**Status:** implemented - **Wave:** 3 - **Writes:** `background-color`

## How it works
Only when `background-type == SOLIDCOLOR`: mean color of the 5% border strips (same estimate as the edge detector) maps to the nearest palette name. REALLIFE/UNKNOWN backgrounds stay UNKNOWN - a mean over a scene names nothing.

## Open questions
- [ ] Gradient studio backgrounds (white to grey sweep) - is the border mean representative enough?
    - answer: the 5% border strip is too simple. We'll have to refine it because taking a 5% border strip will results in troubles given the desired whitespace margin surrounding products is 4.2%.
    - adapt the border size according to the bbox detected prior to launching this analyzer.
      - the borders dimensions always have one fixed and one variable dimension:
        - top & bottom borders adopt the images width, their height is variable
        - left & right borders adopt the images height, their width is variable
      - Scale the border strip relative to image size -> deduct 33% from the whitespace margin (loaded from the json prism config) with a minimum of 2 pixels per 300px (300px → 2px, 450px → 3px, ... , 2000px → 60px, 3000px → 90px) **cap the strip dynamically** → (maximum outputsize - 2 x whitespace margin)/2 - (maximum outputsize - 2 x whitespace margin)/2 *2*0.042** --> (2000px - 2*0.042 )/2-((2000px - 2*0.042 )/2)*2*0.042 = **840px**
        - reasoning behind this: a minimum strip size at all costs, greedy expansion up until almost half of the image, protection from any subject pixel not captured in the bounding box.
  
    - background color via border mean is not representative enough:
      - background color if solidcolor means 100% solid, no noise.
        - This is practically impossible but theoretically quiet clear, therefore take into account light jpg artefact noise and tiny dust clumps and near-same-color hard-edged blobs.
          - artefact noise is inevitable
          - tiny dust clumps might result from hasty retouching (leftover dust on the studio paper)
          - near-same-color hard-edged blobs are what result from improper image editing via clipping path.

## Refinements:
    - [ ] If histogram is heavily centered around one color, apply local-contrast adjustments and micro-contrast adjustments to the edge-areas of local-contrast-enhanced-areas prior to defining the border areas