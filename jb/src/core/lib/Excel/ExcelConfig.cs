using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace Prism.Lib.Excel;

/// <summary>
/// Runtime configuration for building the Internal Excel Model.
/// </summary>
public sealed record ExcelConfig
{
    /// <summary>
    /// Canonical primary-key column header expected in a detected header row.
    /// </summary>
    public string RecordPrimaryKey { get; init; } = string.Empty;

    /// <summary>
    /// Header words used to decide whether a worksheet row is the column-header row.
    /// </summary>
    public IReadOnlyList<string> HeaderRowIndicators { get; init; } = [];

    /// <summary>
    /// Search window used when scanning for a worksheet header row.
    /// </summary>
    public HeaderRowSearchSpace HeaderRowSearchSpace { get; init; } = new();

    /// <summary>
    /// Validation rules for FamilyID values.
    /// </summary>
    public FamilyIdProperties FamilyIDProperties { get; init; } = new();

    /// <summary>
    /// Tunable thresholds for header detection.
    /// </summary>
    public HeaderDetectionConfig HeaderDetection { get; init; } = new();

    /// <summary>
    /// Tunable thresholds for deciding which columns contain useful data.
    /// </summary>
    public ColumnValidityConfig ColumnValidity { get; init; } = new();

    /// <summary>
    /// Tunable thresholds for duplicate column detection and merging.
    /// </summary>
    public DuplicateColumnHandlingConfig DuplicateColumnHandling { get; init; } = new();

    /// <summary>
    /// Tunable thresholds for classifying dynamic Excel columns.
    /// </summary>
    public ColumnClassificationConfig ColumnClassification { get; init; } = new();

    /// <summary>
    /// Configured regex definitions retained for the noise filter configuration surface.
    /// </summary>
    public IReadOnlyList<Dictionary<string, string>> NoiseFilterPatterns { get; init; } = [];

    /// <summary>
    /// Loads and validates ExcelConfig.json.
    /// </summary>
    /// <param name="configPath">Path to ExcelConfig.json.</param>
    /// <returns>A validated Excel configuration object.</returns>
    public static ExcelConfig Load(string configPath)
    {
        if (string.IsNullOrWhiteSpace(configPath))
        {
            throw new ArgumentException("Excel config path is required.", nameof(configPath));
        }

        if (!File.Exists(configPath))
        {
            throw new FileNotFoundException("Excel config file was not found.", configPath);
        }

        string json = File.ReadAllText(configPath);
        ExcelConfig? config = JsonSerializer.Deserialize<ExcelConfig>(json, JsonOptions);

        if (config is null)
        {
            throw new InvalidOperationException("Excel config could not be parsed.");
        }

        config.Validate();

        return config;
    }

    /// <summary>
    /// Validates all required configuration fields before processing any workbook.
    /// </summary>
    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(RecordPrimaryKey))
        {
            throw new InvalidOperationException("ExcelConfig.RecordPrimaryKey is required.");
        }

        if (HeaderRowIndicators.Count == 0 || HeaderRowIndicators.Any(string.IsNullOrWhiteSpace))
        {
            throw new InvalidOperationException("ExcelConfig.HeaderRowIndicators must contain at least one non-empty value.");
        }

        HeaderRowSearchSpace.Validate();
        FamilyIDProperties.Validate();
        HeaderDetection.Validate();
        ColumnValidity.Validate();
        DuplicateColumnHandling.Validate();
        ColumnClassification.Validate();
    }

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        ReadCommentHandling = JsonCommentHandling.Skip,
        AllowTrailingCommas = true
    };
}
