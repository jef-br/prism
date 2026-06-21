namespace Prism.Core;

/// <summary>Outcome of the generation route decision for a family evaluated by the Generated stage.</summary>
public enum GenerationRouteState {
    /// <summary>Family has not yet been evaluated by the Generated stage.</summary>
    NotEvaluated,

    /// <summary>Family had more than the minimum image count — no generation needed.</summary>
    Skipped,

    /// <summary>Hero image dimensions were below the configured minimum — generation skipped.</summary>
    SkippedLowQuality,

    /// <summary>Generation would proceed but the backend is unavailable; record created with Gated status.</summary>
    Gated,

    /// <summary>Inference ran and generated child records were created successfully.</summary>
    Created,

    /// <summary>Inference ran but the quality check rejected all outputs.</summary>
    Failed
}
