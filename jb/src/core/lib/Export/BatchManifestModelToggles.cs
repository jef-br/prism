namespace Prism.Contracts;

/// <summary>
/// Which AI models were enabled for this job, mirroring the Models.&lt;section&gt;.UseIt toggles in
/// Prism_Config.json. Recorded so a manifest read months later still says whether a feature was
/// UNKNOWN because the model measured nothing or because the model never ran.
/// </summary>
public sealed record BatchManifestModelToggles {
    /// <summary>CLIP zero-shot classification (Models.classification.UseIt).</summary>
    public bool Classification { get; init; }

    /// <summary>YOLO26 object detection (Models.Detection.UseIt).</summary>
    public bool Detection { get; init; }

    /// <summary>Real-ESRGAN upscaling (Models.Upscaling.UseIt).</summary>
    public bool Upscale { get; init; }

    /// <summary>Synthetic image generation backend (Models.Generation.UseIt).</summary>
    public bool Generation { get; init; }
}
