using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

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

/// <summary>
/// Worksheet area scanned for a likely header row.
/// </summary>
public sealed record HeaderRowSearchSpace
{
    /// <summary>
    /// Zero-based first row to inspect.
    /// </summary>
    public int FirstRow { get; init; }

    /// <summary>
    /// Zero-based last row to inspect.
    /// </summary>
    public int LastRow { get; init; }

    /// <summary>
    /// Zero-based first column to inspect.
    /// </summary>
    public int FirstColumn { get; init; }

    /// <summary>
    /// Zero-based last column to inspect.
    /// </summary>
    public int LastColumn { get; init; }

    /// <summary>
    /// Validates the configured worksheet search bounds.
    /// </summary>
    public void Validate()
    {
        if (FirstRow < 0 || LastRow < FirstRow)
        {
            throw new InvalidOperationException("ExcelConfig.HeaderRowSearchSpace rows must be zero-based and ordered.");
        }

        if (FirstColumn < 0 || LastColumn < FirstColumn)
        {
            throw new InvalidOperationException("ExcelConfig.HeaderRowSearchSpace columns must be zero-based and ordered.");
        }
    }
}

/// <summary>
/// Validation rules for the configured FamilyID primary key.
/// </summary>
public sealed record FamilyIdProperties
{
    /// <summary>
    /// Indicates whether the primary key must contain digits only.
    /// </summary>
    public bool? IsNumeric { get; init; }

    /// <summary>
    /// Required primary-key length.
    /// </summary>
    public int Length { get; init; }

    /// <summary>
    /// Validates FamilyID requirements.
    /// </summary>
    public void Validate()
    {
        if (!IsNumeric.HasValue)
        {
            throw new InvalidOperationException("ExcelConfig.FamilyIDProperties.IsNumeric is required.");
        }

        if (Length <= 0)
        {
            throw new InvalidOperationException("ExcelConfig.FamilyIDProperties.Length must be greater than zero.");
        }
    }
}

/// <summary>
/// Header-detection thresholds from ExcelConfig.json.
/// </summary>
public sealed record HeaderDetectionConfig
{
    /// <summary>
    /// Minimum share of non-empty candidate cells that must match configured header indicators.
    /// </summary>
    public double MinimumMatchedColumnRatio { get; init; }

    /// <summary>
    /// Maximum edit-distance ratio accepted for an indicator match.
    /// </summary>
    public double MaximumEditDistanceRatio { get; init; }

    /// <summary>
    /// Confidence assigned to a one-character edit distance.
    /// </summary>
    public double EditDistanceOneConfidence { get; init; }

    /// <summary>
    /// Confidence assigned to a two-character edit distance.
    /// </summary>
    public double EditDistanceTwoConfidence { get; init; }

    /// <summary>
    /// Validates header-detection thresholds.
    /// </summary>
    public void Validate()
    {
        ValidateRatio(MinimumMatchedColumnRatio, "ExcelConfig.HeaderDetection.MinimumMatchedColumnRatio");
        ValidateRatio(MaximumEditDistanceRatio, "ExcelConfig.HeaderDetection.MaximumEditDistanceRatio");
        ValidateRatio(EditDistanceOneConfidence, "ExcelConfig.HeaderDetection.EditDistanceOneConfidence");
        ValidateRatio(EditDistanceTwoConfidence, "ExcelConfig.HeaderDetection.EditDistanceTwoConfidence");
    }

    private static void ValidateRatio(double value, string name)
    {
        if (value <= 0 || value > 1)
        {
            throw new InvalidOperationException($"{name} must be greater than zero and less than or equal to one.");
        }
    }
}

/// <summary>
/// Column validity thresholds from ExcelConfig.json.
/// </summary>
public sealed record ColumnValidityConfig
{
    /// <summary>
    /// Minimum share of data rows that must have a non-empty value for a column to survive.
    /// </summary>
    public double MinimumUsefulValueRatio { get; init; }

    /// <summary>
    /// Validates column validity thresholds.
    /// </summary>
    public void Validate()
    {
        if (MinimumUsefulValueRatio <= 0 || MinimumUsefulValueRatio > 1)
        {
            throw new InvalidOperationException("ExcelConfig.ColumnValidity.MinimumUsefulValueRatio must be greater than zero and less than or equal to one.");
        }
    }
}

/// <summary>
/// Duplicate column handling thresholds from ExcelConfig.json.
/// </summary>
public sealed record DuplicateColumnHandlingConfig
{
    /// <summary>
    /// Minimum cell-overlap ratio used to merge columns with different headers.
    /// </summary>
    public double OverlapRatioForMerge { get; init; }

    /// <summary>
    /// Validates duplicate column thresholds.
    /// </summary>
    public void Validate()
    {
        if (OverlapRatioForMerge <= 0 || OverlapRatioForMerge > 1)
        {
            throw new InvalidOperationException("ExcelConfig.DuplicateColumnHandling.OverlapRatioForMerge must be greater than zero and less than or equal to one.");
        }
    }
}

/// <summary>
/// Dynamic column classification thresholds from ExcelConfig.json.
/// </summary>
public sealed record ColumnClassificationConfig
{
    /// <summary>
    /// Maximum unique non-empty values for a string column to be considered categorical.
    /// </summary>
    public int CategoricalMaximumUniqueValues { get; init; }

    /// <summary>
    /// Maximum value length for a string column to be considered categorical.
    /// </summary>
    public int CategoricalMaximumValueLength { get; init; }

    /// <summary>
    /// Validates column classification thresholds.
    /// </summary>
    public void Validate()
    {
        if (CategoricalMaximumUniqueValues <= 0)
        {
            throw new InvalidOperationException("ExcelConfig.ColumnClassification.CategoricalMaximumUniqueValues must be greater than zero.");
        }

        if (CategoricalMaximumValueLength <= 0)
        {
            throw new InvalidOperationException("ExcelConfig.ColumnClassification.CategoricalMaximumValueLength must be greater than zero.");
        }
    }
}
