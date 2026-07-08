namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_Packaging.md)
 -----------------
 packaging-visible: CLIP prompts ("product in its retail box/blister/bottle" vs "product
 without packaging") added to ClipPrompts.json; YOLO's bottle/box-adjacent classes are
 supporting evidence for FMCG types. scale-reference-present: CLIP prompts for a hand, coin,
 or ruler next to the product; a person detection whose box is small relative to the subject
 box is supporting evidence for a held product.
*/

/// <summary>STUB: will set <c>packaging-visible</c> and <c>scale-reference-present</c>. Currently writes nothing.</summary>
internal static class Analyzer_Packaging
{
    public static void Analyze(ImageFeatureSnapshot snapshot)
    {
        // Not implemented — packaging-visible/scale-reference-present stay UNKNOWN.
    }
}
