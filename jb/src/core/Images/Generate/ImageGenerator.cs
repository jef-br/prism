using System.Text.Json;

namespace Prism.Core;

/// <summary>
/// Evaluates each FamilyID group and creates <see cref="ImageRecord_GENERATED"/> records
/// for families below the minimum image count threshold.
/// Actual inference is gated behind <see cref="GenerationBackendAvailable"/>; all records
/// produced today receive <see cref="GenerationStatus.Gated"/>.
/// </summary>
internal static class ImageGenerator
{
    /// <summary>
    /// Runs the generation decision over all non-KO images, enriching each hero LAMBDA in place and
    /// returning the new synthetic records it created.
    /// </summary>
    /// <param name="records">Matched LAMBDA records.</param>
    /// <param name="generationEnabled">Whether generation is enabled for this job.</param>
    /// <returns>The generated synthetic image records (empty when generation produced none).</returns>
    internal static IReadOnlyList<ImageRecord_GENERATED> Run(List<ImageRecord_LAMBDA> records, bool generationEnabled)
    {
        if (!generationEnabled)
        {
            foreach (ImageRecord_LAMBDA lambda in records)
            {
                if (!lambda.IsKo)
                    lambda.GenerationRouteState = GenerationRouteState.Skipped;
            }
            return [];
        }

        Generate_Config config = LoadConfig();
        List<ImageRecord_GENERATED> generatedImages = [];

        IEnumerable<IGrouping<string, ImageRecord_LAMBDA>> familyGroups =
            records.Where(r => !r.IsKo && !string.IsNullOrEmpty(r.Family))
                   .GroupBy(r => r.Family);

        foreach (IGrouping<string, ImageRecord_LAMBDA> group in familyGroups)
        {
            List<ImageRecord_LAMBDA> images = [.. group];

            if (images.Count > config.MinImagesPerFamily)
            {
                foreach (ImageRecord_LAMBDA img in images)
                    img.GenerationRouteState = GenerationRouteState.Skipped;

                continue;
            }

            ImageRecord_LAMBDA hero = SelectHero(images);

            if (!MeetsQuality(hero, config))
            {
                foreach (ImageRecord_LAMBDA img in images)
                    img.GenerationRouteState = GenerationRouteState.SkippedLowQuality;

                continue;
            }

            if (GenerationBackendAvailable())
            {
                // Future: run inference and create real generated records.
                continue;
            }

            ImageRecord_GENERATED generated = BuildGeneratedRecord(hero, GenerationMethod.DetailCrop);
            hero.GeneratedChildren      = [generated];
            hero.GenerationRouteState   = GenerationRouteState.Gated;
            generatedImages.Add(generated);

            foreach (ImageRecord_LAMBDA remaining in images)
            {
                if (!ReferenceEquals(remaining, hero))
                    remaining.GenerationRouteState = GenerationRouteState.Skipped;
            }
        }

        return generatedImages;
    }

    //  Private helpers 

    /// <summary>
    /// Returns the non-KO record with the lowest <see cref="ImageRecord_Base.DetOrder"/>.
    /// Ties are broken by <see cref="ImageRecord_Base.InitialFullName"/> ascending.
    /// </summary>
    private static ImageRecord_LAMBDA SelectHero(IReadOnlyList<ImageRecord_LAMBDA> group)
        => group
            .OrderBy(r => r.DetOrder)
            .ThenBy(r => r.InitialFullName, StringComparer.OrdinalIgnoreCase)
            .First();

    /// <summary>
    /// Returns <c>true</c> when the hero image meets minimum dimension requirements.
    /// Treats <c>Width == 0</c> or <c>Height == 0</c> as unknown and passes the check.
    /// </summary>
    private static bool MeetsQuality(ImageRecord_LAMBDA hero, Generate_Config config)
    {
        if (hero.Width > 0 && hero.Width < config.InputMinWidthPixels)
            return false;
        if (hero.Height > 0 && hero.Height < config.InputMinHeightPixels)
            return false;
        return true;
    }

    /// <summary>
    /// Returns <c>false</c>; no generation backend is deployed in the current environment.
    /// Replace with a real connectivity check when ComfyUI / SD infrastructure is available.
    /// </summary>
    private static bool GenerationBackendAvailable() => false;

    /// <summary>
    /// Constructs a <see cref="ImageRecord_GENERATED"/> for the given hero using the specified method.
    /// Status is always <see cref="GenerationStatus.Gated"/> until the backend is available.
    /// </summary>
    private static ImageRecord_GENERATED BuildGeneratedRecord(
        ImageRecord_LAMBDA hero,
        GenerationMethod method)
    {
        return new ImageRecord_GENERATED
        {
            SourceFamilyId      = hero.Family,
            SourceHeroImageName = hero.InitialFullName,
            Method              = method,
            Status              = GenerationStatus.Gated,
            Family              = hero.Family
        };
    }

    /// <summary>
    /// Loads generation thresholds from the <c>Generation</c> section of <c>Prism_Config.json</c>.
    /// Throws <see cref="InvalidOperationException"/> when the file is missing or the section is malformed.
    /// </summary>
    private static Generate_Config LoadConfig()
    {
        string configPath = PrismConfigLocator.FindPrismConfigPath()
            ?? throw new InvalidOperationException(
                "Prism_Config.json could not be located. Ensure the file is deployed next to the assembly.");

        string json = File.ReadAllText(configPath, System.Text.Encoding.UTF8);

        JsonDocument doc;
        try
        {
            doc = JsonDocument.Parse(json);
        }
        catch (JsonException ex)
        {
            throw new InvalidOperationException(
                $"Failed to parse Prism_Config.json at '{configPath}': {ex.Message}", ex);
        }

        using (doc)
        {
            if (!doc.RootElement.TryGetProperty("Generation", out JsonElement genEl))
                throw new InvalidOperationException(
                    "Prism_Config.json is missing required 'Generation' section.");

            int minFamily = genEl.TryGetProperty("MinImagesPerFamily", out JsonElement mipEl)
                ? mipEl.GetInt32()
                : throw new InvalidOperationException(
                    "Prism_Config.json Generation section is missing 'MinImagesPerFamily'.");

            int minWidth  = 0;
            int minHeight = 0;

            if (genEl.TryGetProperty("InputImages", out JsonElement inputEl) &&
                inputEl.TryGetProperty("MINIMUM_SIZE_IN_PIXELS", out JsonElement minSizeEl))
            {
                if (minSizeEl.TryGetProperty("width",  out JsonElement wEl)) minWidth  = wEl.GetInt32();
                if (minSizeEl.TryGetProperty("height", out JsonElement hEl)) minHeight = hEl.GetInt32();
            }

            return new Generate_Config
            {
                MinImagesPerFamily   = minFamily,
                InputMinWidthPixels  = minWidth,
                InputMinHeightPixels = minHeight
            };
        }
    }
}
