namespace Prism.Services.Matching;

/*
 Proposed workings (STUB — see Analyzer_IndoorOutdoor.md)
 -----------------
 Only meaningful for lifestyle shots: gated on lifestyle-background == true. CLIP scene
 prompts ("photographed indoors in a room", "photographed outdoors in nature / on the
 street") added to ClipPrompts.json write indoor/outdoor. Sky-blue + green-vegetation color
 mass in the top image half is a cheap supporting heuristic for outdoor.
*/

/// <summary>STUB: will set <c>indoor</c> and <c>outdoor</c>. Currently writes nothing.</summary>
internal static class Analyzer_IndoorOutdoor
{
    public static void Analyze(ImageFeatureSnapshot snapshot)
    {
        // Not implemented — indoor/outdoor stay UNKNOWN.
    }
}
