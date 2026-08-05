using System.Text.Json;
using Xunit;

namespace PrismCoreTests.Classify;

/// <summary>
/// Unit tests for <see cref="PhenotypeRuleSet"/>: rule loading and first-match-wins evaluation.
/// Uses the real <c>ImageRoles.json</c> file; snapshots are built manually per test.
/// </summary>
public class PhenotypeRuleSetTests {
    private static readonly string ImageRolesPath = ResolveImageRolesPath();

    //  Load contract 

    [Fact]
    public void Load_ValidPath_DoesNotThrow_AndKnownPhenotypeIsReachable() {
        // Verify load succeeds by exercising a known phenotype.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "test");
        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Load_ValidPath_JsonContains18Phenotypes() {
        // 20 after T-4700 trimmed 6 unreachable ones, +1 for back-on-model-partial (T-4970:
        // a back view cut by a frame edge is 48% of a real catalogue set and had no rule),
        // -3 for ghost-front/back/side (T-5040: identical to their packshot counterparts once
        // clipping-path went, so provably unreachable; *-packshot now covers both cases).
        string json = File.ReadAllText(ImageRolesPath, System.Text.Encoding.UTF8);
        using var doc = JsonDocument.Parse(json);
        int count = doc.RootElement.GetProperty("phenotypes").GetArrayLength();
        Assert.Equal(18, count);
    }

    [Fact]
    public void Load_EverySlotPhenotypeInDetOrderRulesExists() {
        // T-5040 constraint: DetOrderRules may not name a phenotype the taxonomy does not define.
        // This is what would have caught the ghost-* entries left behind after a rule deletion.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        HashSet<string> defined = JsonDocument
            .Parse(File.ReadAllText(ImageRolesPath, System.Text.Encoding.UTF8))
            .RootElement.GetProperty("phenotypes")
            .EnumerateArray()
            .Select(p => p.GetProperty("id").GetString()!)
            .ToHashSet(StringComparer.Ordinal);

        string detOrderPath = Path.Combine(Path.GetDirectoryName(ImageRolesPath)!, "DetOrderRules.json");
        using var det = JsonDocument.Parse(File.ReadAllText(detOrderPath, System.Text.Encoding.UTF8));

        List<string> unknown = [];
        foreach (JsonProperty productType in det.RootElement.GetProperty("productTypes").EnumerateObject()) {
            foreach (JsonProperty slot in productType.Value.EnumerateObject()) {
                foreach (JsonElement id in slot.Value.GetProperty("phenotypes").EnumerateArray()) {
                    string name = id.GetString()!;
                    if (!defined.Contains(name))
                        unknown.Add($"{productType.Name}.{slot.Name} -> {name}");
                }
            }
        }

        Assert.Empty(unknown);
        Assert.NotNull(ruleSet);
    }

    [Fact]
    public void Load_MissingFile_ThrowsInvalidOperationException() {
        Assert.Throws<PrismConfigurationException>(() =>
            PhenotypeRuleSet.Load(@"C:\does\not\exist\ImageRoles.json"));
    }

    [Fact]
    public void Load_NullJson_ThrowsInvalidOperationException() {
        string tempPath = Path.GetTempFileName();
        try {
            // "null" deserializes to null reference, triggering the ?? throw branch.
            File.WriteAllText(tempPath, "null", System.Text.Encoding.UTF8);
            Assert.Throws<PrismConfigurationException>(() => PhenotypeRuleSet.Load(tempPath));
        }
        finally {
            File.Delete(tempPath);
        }
    }

    [Fact]
    public void Load_EmptyPhenotypes_AssignReturnsNull() {
        string tempPath = Path.GetTempFileName();
        try {
            File.WriteAllText(tempPath, """{"phenotypes": []}""", System.Text.Encoding.UTF8);
            var ruleSet = PhenotypeRuleSet.Load(tempPath);
            Assert.Null(ruleSet.Assign(AllUnknownSnapshot()));
        }
        finally {
            File.Delete(tempPath);
        }
    }

    //  UNKNOWN feature blocking 

    [Fact]
    public void Assign_AllUnknownFeatures_ReturnsNull() {
        // No features set → everything is UNKNOWN → no phenotype fires.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        Assert.Null(ruleSet.Assign(AllUnknownSnapshot()));
    }

    [Fact]
    public void Assign_UNKNOWNFeature_NeverSatisfiesEqualsCondition() {
        // hero-is-human is UNKNOWN → lifestyle-hero cannot fire even if lifestyle-background is set.
        // But lifestyle-hero doesn't require hero-is-human. Use a phenotype that does.
        // front-packshot requires hero-is-human=FALSE; with UNKNOWN it must not match.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        // Set all front-packshot conditions except hero-is-human (leave it UNKNOWN).
        snapshot.Set("hero-orientation", "FRONT", 1.0, "test");
        snapshot.Set("background-type", "SOLIDCOLOR", 1.0, "test");
        snapshot.Set("intersection-count", "0", 1.0, "test");

        Assert.Null(ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_UNKNOWNFeature_NeverSatisfiesMinCondition() {
        // closeup-image requires hero-is-human=FALSE and intersection-count >= 1.
        // With hero-is-human UNKNOWN it must not match even if intersection-count satisfies min.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("intersection-count", "3", 1.0, "test");
        // hero-is-human left UNKNOWN.

        Assert.Null(ruleSet.Assign(snapshot));
    }

    //  Condition types 

    [Fact]
    public void Assign_EqualsCondition_Matches() {
        // lifestyle-context requires only lifestyle-background=true (equals condition).
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "test");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MinCondition_MatchesNumericAboveThreshold() {
        // closeup-image: intersection-count min 1. Provide all required conditions.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "FALSE", 1.0, "test");
        snapshot.Set("intersection-count", "3", 1.0, "test"); // >= 1 ✓

        Assert.Equal("closeup-image", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_MinCondition_FailsBelowThreshold() {
        // closeup-image needs intersection-count >= 1. With 0 the rule fails.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "FALSE", 1.0, "test");
        snapshot.Set("intersection-count", "0", 1.0, "test"); // < 1 ✗

        Assert.NotEqual("closeup-image", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_AnyOfGroup_MatchesWhenOneChildMet() {
        // front-on-model-full-product has anyOf: head-visible=FULL OR PARTIAL.
        // Supply PARTIAL only — rule should still fire.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "TRUE", 1.0, "test");
        snapshot.Set("hero-orientation", "FRONT", 1.0, "test");
        snapshot.Set("head-visible", "PARTIAL", 1.0, "test"); // anyOf ✓
        snapshot.Set("body-visible", "full", 1.0, "test");
        snapshot.Set("intersection-count", "0", 1.0, "test");

        Assert.Equal("front-on-model-full-product", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_AnyOfGroup_FailsWhenNoChildMet() {
        // front-on-model-full-product anyOf: head-visible=FULL OR PARTIAL.
        // Supply NONE — the anyOf fails, rule does not fire.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "TRUE", 1.0, "test");
        snapshot.Set("hero-orientation", "FRONT", 1.0, "test");
        snapshot.Set("head-visible", "NONE", 1.0, "test"); // anyOf ✗
        snapshot.Set("body-visible", "full", 1.0, "test");
        snapshot.Set("intersection-count", "0", 1.0, "test");

        Assert.NotEqual("front-on-model-full-product", ruleSet.Assign(snapshot));
    }

    //  Priority / CPU-only reachability 

    [Fact]
    public void Assign_LifestyleContext_ReachableFromCpuOnlyFeatures() {
        // lifestyle-background is CPU-detectable. With all other features UNKNOWN,
        // lifestyle-hero fails (intersection-count UNKNOWN) and lifestyle-context fires.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_LifestyleHero_ReachableFromCpuOnlyFeatures() {
        // lifestyle-hero fires when lifestyle-background=true (CPU) AND the subject touches
        // at most one frame edge. Both are written by the CPU analyzers.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");
        snapshot.Set("intersection-count", "0", 0.85, "heuristic");

        Assert.Equal("lifestyle-hero", ruleSet.Assign(snapshot));
    }

    [Fact]
    public void Assign_LifestyleContext_WhenSubjectTouchesTooManyEdges() {
        // lifestyle-hero caps at intersection-count <= 1; three edges fails it and the
        // image falls through to lifestyle-context, which carries no intersection condition.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");
        snapshot.Set("intersection-count", "3", 0.85, "heuristic");

        Assert.Equal("lifestyle-context", ruleSet.Assign(snapshot));
    }


    //  the conditions that used to be ghost-front's now resolve to front-packshot

    [Fact]
    public void Assign_FormerGhostFrontConditions_ReturnsFrontPackshot() {
        // History, because this case is the reason the taxonomy shrank: ghost-front's only condition
        // front-packshot did not also carry was clipping-path, T-5030 deleted that outright, and the
        // two required blocks became character-for-character identical — so ghost-front could never
        // be assigned. T-5040 resolved it by merging: ghost-front/back/side are gone and *-packshot
        // now covers both the flat lay and the ghost-mannequin shot. This test pins the outcome —
        // an invisible-mannequin front shot on a solid background is a front-packshot. Re-separating
        // them needs a signal for whether the garment holds a worn 3D shape, which PRISM does not
        // measure; see imagePhenotypes.md's merged ghost entry.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("hero-is-human", "FALSE", 1.0, "test");
        snapshot.Set("hero-orientation", "FRONT", 1.0, "test");
        snapshot.Set("background-type", "SOLIDCOLOR", 1.0, "test");
        snapshot.Set("intersection-count", "0", 1.0, "test");

        Assert.Equal("front-packshot", ruleSet.Assign(snapshot));
    }

    //  EvaluateCandidates 

    [Fact]
    public void EvaluateCandidates_ReturnsAllMatchingPhenotypesInOrder() {
        // With lifestyle-background=true and the subject clear of every edge,
        // both lifestyle-hero AND lifestyle-context match.
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");
        snapshot.Set("intersection-count", "0", 0.85, "heuristic");

        string[] candidates = ruleSet.EvaluateCandidates(snapshot);

        Assert.Contains("lifestyle-hero", candidates);
        Assert.Contains("lifestyle-context", candidates);
        // lifestyle-hero must appear before lifestyle-context (rule order preserved).
        Assert.True(Array.IndexOf(candidates, "lifestyle-hero") <
                    Array.IndexOf(candidates, "lifestyle-context"));
    }

    [Fact]
    public void EvaluateCandidates_NoMatches_ReturnsEmptyArray() {
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        string[] candidates = ruleSet.EvaluateCandidates(AllUnknownSnapshot());
        Assert.Empty(candidates);
    }

    [Fact]
    public void EvaluateCandidates_FirstCandidateMatchesAssignResult() {
        var ruleSet = PhenotypeRuleSet.Load(ImageRolesPath);
        var snapshot = new ImageFeatureSnapshot();
        snapshot.Set("lifestyle-background", "true", 0.72, "heuristic");

        string[] candidates = ruleSet.EvaluateCandidates(snapshot);
        string? assigned = ruleSet.Assign(snapshot);

        Assert.NotEmpty(candidates);
        Assert.Equal(candidates[0], assigned);
    }

    //  Helpers 

    private static ImageFeatureSnapshot AllUnknownSnapshot() => new();

    private static string ResolveImageRolesPath() {
        var assemblyDir = new FileInfo(typeof(PhenotypeRuleSetTests).Assembly.Location).DirectoryName
            ?? throw new InvalidOperationException("Cannot determine assembly directory");

        var current = new DirectoryInfo(assemblyDir);
        while (current.Parent != null) {
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
