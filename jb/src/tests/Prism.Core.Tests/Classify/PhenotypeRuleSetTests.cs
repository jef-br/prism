using System.Text.Json;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for <see cref="PhenotypeRuleSet"/>: rule loading and first-match-wins evaluation.
/// Uses the real <c>ImageRoles.json</c> file; snapshots are built manually per test.
/// </summary>
public class PhenotypeRuleSetTests
{
    private static readonly string ImageRolesPath = ResolveImageRolesPath();

    //  Load contract 

    [Fact]
    public void Load_ValidPath_DoesNotThrow_AndKnownPhenotypeIsReachable()
    {
        // Verify load succeeds by exercising a known phenotype.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "test");
        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Load_ValidPath_JsonContains26Phenotypes()
    {
        // Verify the configuration file itself carries the expected 26 phenotypes.
        string json = File.ReadAllText(ImageRolesPath, System.Text.Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        int count = doc.RootElement.GetProperty("phenotypes").GetArrayLength();
        Assert.Equal(26, count);
    }

    [Fact]
    public void Load_MissingFile_ThrowsInvalidOperationException()
    {
        Assert.Throws<InvalidOperationException>(() =>
            PhenotypeRuleSet.Load(@"C:\does\not\exist\ImageRoles.json"));
    }

    [Fact]
    public void Load_NullJson_ThrowsInvalidOperationException()
    {
        string tempPath = Path.GetTempFileName();
        try
        {
            // "null" deserializes to null reference, triggering the ?? throw branch.
            File.WriteAllText(tempPath, "null", System.Text.Encoding.UTF8);
            Assert.Throws<InvalidOperationException>(() => PhenotypeRuleSet.Load(tempPath));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_EmptyPhenotypes_AssignReturnsNull()
    {
        string tempPath = Path.GetTempFileName();
        try
        {
            File.WriteAllText(tempPath, """{"phenotypes": []}""", System.Text.Encoding.UTF8);
            var ruleSet = PhenotypeRuleSet.Load(tempPath);
            Assert.Null(ruleSet.Assign(AllUnknownSnapshot()));
        }
        finally
        {
            File.Delete(tempPath);
        }
    }

    //  UNKNOWN feature blocking 

    [Fact]
    public void Assign_AllUnknownFeatures_ReturnsNull()
    {
        // No features set → everything is UNKNOWN → no phenotype fires.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        Assert.Null(ruleSet.Assign(AllUnknownSnapshot()));
    }

    [Fact]
    public void Assign_UNKNOWNFeature_NeverSatisfiesEqualsCondition()
    {
        // hero-is-human is UNKNOWN → lifestyle-hero cannot fire even if lifestyle-background is set.
        // But lifestyle-hero doesn't require hero-is-human. Use a phenotype that does.
        // front-packshot requires hero-is-human=FALSE; with UNKNOWN it must not match.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        // Set all front-packshot conditions except hero-is-human (leave it UNKNOWN).
        snapshot.Set("hero-orientation",  "FRONT",      1.0, "test");
        snapshot.Set("background-type",   "SOLIDCOLOR", 1.0, "test");
        snapshot.Set("occlusion-level",   "full-product", 1.0, "test");
        snapshot.Set("intersection-count", "0",         1.0, "test");

        Assert.Null(ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_UNKNOWNFeature_NeverSatisfiesMinCondition()
    {
        // closeup-image requires hero-is-human=FALSE and intersection-count >= 1.
        // With hero-is-human UNKNOWN it must not match even if intersection-count satisfies min.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("intersection-count", "3",       1.0, "test");
        snapshot.Set("occlusion-level",    "closeup", 1.0, "test");
        // hero-is-human left UNKNOWN.

        Assert.Null(ruleSet.Assign(snapshot));
    }

    //  Condition types 

    [Fact]
    public void Assign_EqualsCondition_Matches()
    {
        // lifestyle-context requires only lifestyle-background=true (equals condition).
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "test");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MinCondition_MatchesNumericAboveThreshold()
    {
        // closeup-image: intersection-count min 1. Provide all required conditions.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human",      "FALSE",   1.0, "test");
        snapshot.Set("intersection-count", "3",       1.0, "test"); // >= 1 ✓
        snapshot.Set("occlusion-level",    "closeup", 1.0, "test");

        Assert.Equal("closeup-image", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MinCondition_FailsBelowThreshold()
    {
        // closeup-image needs intersection-count >= 1. With 0 the rule fails.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human",      "FALSE",   1.0, "test");
        snapshot.Set("intersection-count", "0",       1.0, "test"); // < 1 ✗
        snapshot.Set("occlusion-level",    "closeup", 1.0, "test");

        Assert.NotEqual("closeup-image", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MaxCondition_MatchesNumericBelowThreshold()
    {
        // size-chart: product-coverage-ratio max 0.30, image-occupancy min 0.60, text-present=true.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("text-present",           "true", 1.0, "test");
        snapshot.Set("product-coverage-ratio", "0.20", 1.0, "test"); // <= 0.30 ✓
        snapshot.Set("image-occupancy",        "0.65", 1.0, "test"); // >= 0.60 ✓

        Assert.Equal("size-chart", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MaxCondition_FailsAboveThreshold()
    {
        // size-chart: product-coverage-ratio max 0.30. With 0.50 the rule fails.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("text-present",           "true", 1.0, "test");
        snapshot.Set("product-coverage-ratio", "0.50", 1.0, "test"); // > 0.30 ✗
        snapshot.Set("image-occupancy",        "0.65", 1.0, "test");

        Assert.NotEqual("size-chart", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_AnyOfGroup_MatchesWhenOneChildMet()
    {
        // front-on-model-full-product has anyOf: head-visible=FULL OR PARTIAL.
        // Supply PARTIAL only — rule should still fire.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human",      "TRUE",         1.0, "test");
        snapshot.Set("hero-orientation",   "FRONT",        1.0, "test");
        snapshot.Set("head-visible",       "PARTIAL",      1.0, "test"); // anyOf ✓
        snapshot.Set("body-visible",       "full",         1.0, "test");
        snapshot.Set("intersection-count", "0",            1.0, "test");

        Assert.Equal("front-on-model-full-product", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_AnyOfGroup_FailsWhenNoChildMet()
    {
        // front-on-model-full-product anyOf: head-visible=FULL OR PARTIAL.
        // Supply NONE — the anyOf fails, rule does not fire.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human",      "TRUE",  1.0, "test");
        snapshot.Set("hero-orientation",   "FRONT", 1.0, "test");
        snapshot.Set("head-visible",       "NONE",  1.0, "test"); // anyOf ✗
        snapshot.Set("body-visible",       "full",  1.0, "test");
        snapshot.Set("intersection-count", "0",     1.0, "test");

        Assert.NotEqual("front-on-model-full-product", ruleSet.Assign(snapshot));
    }

    //  Priority / CPU-only reachability 

    [Fact]
    public void Assign_LifestyleContext_ReachableFromCpuOnlyFeatures()
    {
        // lifestyle-background is CPU-detectable. With all other features UNKNOWN,
        // lifestyle-hero fails (occlusion-level UNKNOWN) and lifestyle-context fires.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_LifestyleHero_ReachableWhenOcclusionIsDerivedByAnalyzer()
    {
        // lifestyle-hero fires when lifestyle-background=true (CPU) AND
        // occlusion-level=full-product (CPU-derived from intersection-count=0).
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true",         0.72, "heuristic");
        snapshot.Set("occlusion-level",      "full-product", 0.68, "heuristic");

        Assert.Equal("lifestyle-hero", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_LifestyleContext_WhenOcclusionIsCloseup()
    {
        // lifestyle-background=true + occlusion=closeup → lifestyle-hero fails on occlusion,
        // falls through to lifestyle-context.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true",    0.72, "heuristic");
        snapshot.Set("occlusion-level",      "closeup", 0.68, "heuristic");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_LifestyleContext_WhenOcclusionIsUnknown()
    {
        // lifestyle-background=true + occlusion=UNKNOWN → lifestyle-hero fails (UNKNOWN),
        // lifestyle-context fires because it has no occlusion condition.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true",    0.72, "heuristic");
        // occlusion-level not set → UNKNOWN

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    //  Ordering bug: ghost-front unreachable 

    [Fact]
    public void Assign_GhostFront_OrderingBug_CurrentlyReturnsFrontPackshot()
    {
        // BUG: ghost-front must precede front-packshot in ImageRoles.json.
        // ghost-front has the same 5 conditions as front-packshot PLUS contains-mannequin=false.
        // Because front-packshot appears first, it always wins — ghost-front is unreachable.
        // This test documents the CURRENT (broken) behavior. When the ordering is fixed,
        // this test should be updated to assert "ghost-front".
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human",      "FALSE",      1.0, "test");
        snapshot.Set("hero-orientation",   "FRONT",      1.0, "test");
        snapshot.Set("background-type",    "SOLIDCOLOR", 1.0, "test");
        snapshot.Set("occlusion-level",    "full-product", 1.0, "test");
        snapshot.Set("intersection-count", "0",          1.0, "test");
        snapshot.Set("contains-mannequin", "false",      1.0, "test");

        // Expected correct result: "ghost-front"
        // Actual (buggy) result: "front-packshot" due to wrong rule ordering.
        Assert.Equal("front-packshot", ruleSet.Assign(snapshot));
    }

    //  EvaluateCandidates 

    [Fact]
    public void EvaluateCandidates_ReturnsAllMatchingPhenotypesInOrder()
    {
        // With lifestyle-background=true + occlusion=full-product,
        // both lifestyle-hero AND lifestyle-context match.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true",         0.72, "heuristic");
        snapshot.Set("occlusion-level",      "full-product", 0.68, "heuristic");

        string[] candidates = ruleSet.EvaluateCandidates(snapshot);

        Assert.Contains("lifestyle-hero",    candidates);
        Assert.Contains("lifestyle-context", candidates);
        // lifestyle-hero must appear before lifestyle-context (rule order preserved).
        Assert.True(Array.IndexOf(candidates, "lifestyle-hero") <
                    Array.IndexOf(candidates, "lifestyle-context"));
    }

    [Fact]
    public void EvaluateCandidates_NoMatches_ReturnsEmptyArray()
    {
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        string[] candidates = ruleSet.EvaluateCandidates(AllUnknownSnapshot());
        Assert.Empty(candidates);
    }

    [Fact]
    public void EvaluateCandidates_FirstCandidateMatchesAssignResult()
    {
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true",         0.72, "heuristic");
        snapshot.Set("occlusion-level",      "full-product", 0.68, "heuristic");

        string[] candidates = ruleSet.EvaluateCandidates(snapshot);
        string? assigned    = ruleSet.Assign(snapshot);

        Assert.NotEmpty(candidates);
        Assert.Equal(candidates[0], assigned);
    }

    //  Helpers 

    private static ImageFeatureSnapshot AllUnknownSnapshot() => new();

    private static string ResolveImageRolesPath()
    {
        var assemblyDir = new FileInfo(typeof(PhenotypeRuleSetTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null)
        {
            var candidate = Path.Combine(current.FullName, "jb", "src", "core", "config", "ImageRoles.json");
            if (File.Exists(candidate))
                return candidate;
            current = current.Parent;
        }

        var fallback = @"c:\Users\JefB\Documents\JBGITROOT\prism\jb\src\core\config\ImageRoles.json";
        if (File.Exists(fallback))
            return fallback;

        throw new FileNotFoundException("ImageRoles.json not found when walking up from assembly directory.");
    }
}
