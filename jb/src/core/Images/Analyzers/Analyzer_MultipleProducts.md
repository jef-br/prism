# Analyzer_MultipleProducts

**Status:** implemented - **Wave:** 3 - **Writes:** `multiple-products`, `overlap-count`

## How it works
Non-person YOLO detections: count > 1 means multiple-products true; pairs with IoU > `MultipleProducts.OverlapIou` increment overlap-count. No detections: UNKNOWN (many PRISM products are outside the 80 COCO classes; absence is not evidence of a single product).

## Open questions
- [ ] Multi-piece single products (a pair of shoes = two detections) will read as multiple-products - needs product-type-aware handling or a same-class-pair exemption. Validate on footwear batches.
