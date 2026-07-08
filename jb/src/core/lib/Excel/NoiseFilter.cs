using System;
using System.Text.RegularExpressions;

namespace Prism.Lib.Excel;

/// <summary>
/// Removes numeric values that look like measurements, dates, or other non-identity
/// numbers before text is used as matching evidence.
/// </summary>
public static class NoiseFilter
{
    private static readonly Regex DimensionPattern = new(
        @"(?<![A-Za-z0-9])\d+(?:[.,]\d+)?\s*[xX]\s*\d+(?:[.,]\d+)?(?:\s*[xX]\s*\d+(?:[.,]\d+)?)?(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    private static readonly Regex DatePattern = new(
        @"(?<![A-Za-z0-9])(?:\d{4}[-_./]\d{1,2}[-_./]\d{1,2}|\d{1,2}[-_./]\d{1,2}[-_./]\d{2,4})(?![A-Za-z0-9])",
        RegexOptions.Compiled);

    private static readonly Regex UnitAdjacentNumberPattern = new(
        @"(?<![A-Za-z0-9])\d+(?:[.,]\d+)?\s*(?:%|mm|cm|m|meter|meters|g|gr|kg|kilo|l|ml|oz|lb|lbs|inch|inches|in|ft)(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex DateWordAdjacentNumberPattern = new(
        @"(?<![A-Za-z0-9])(?:date|dated|day|month|year|week|season|expiry|expires|expiration|valid|from|until|to)\s*[-_:]?\s*\d{1,4}(?![A-Za-z0-9])",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private static readonly Regex ExtraWhitespacePattern = new(
        @"\s+",
        RegexOptions.Compiled);

    /// <summary>
    /// Returns text with obvious numeric noise removed while preserving trusted identifier columns.
    /// </summary>
    /// <param name="sourceText">The text that may contain numeric noise.</param>
    /// <param name="sourceColumnName">The Excel column name when the text came from Excel.</param>
    /// <returns>Clean text that can be used as matching evidence.</returns>
    public static string RemoveNumericNoiseForMatching(string? sourceText, string? sourceColumnName = null)
    {
        if (string.IsNullOrWhiteSpace(sourceText))
        {
            return string.Empty;
        }

        if (IsTrustedIdentifierColumn(sourceColumnName))
        {
            return sourceText.Trim();
        }

        string withoutDimensions = DimensionPattern.Replace(sourceText, " ");
        string withoutDates = DatePattern.Replace(withoutDimensions, " ");
        string withoutUnitValues = UnitAdjacentNumberPattern.Replace(withoutDates, " ");
        string withoutDateWordValues = DateWordAdjacentNumberPattern.Replace(withoutUnitValues, " ");

        return ExtraWhitespacePattern.Replace(withoutDateWordValues, " ").Trim();
    }

    /// <summary>
    /// Indicates whether a token should be excluded from FamilyID numeric matching evidence.
    /// </summary>
    /// <param name="token">The token being evaluated.</param>
    /// <param name="sourceColumnName">The Excel column name when the token came from Excel.</param>
    /// <returns>True when the token is numeric noise and should not match FamilyID.</returns>
    public static bool IsNumericNoiseForFamilyMatching(string? token, string? sourceColumnName = null)
    {
        if (string.IsNullOrWhiteSpace(token))
        {
            return true;
        }

        if (IsTrustedIdentifierColumn(sourceColumnName))
        {
            return false;
        }

        string cleanedToken = RemoveNumericNoiseForMatching(token, sourceColumnName);
        return cleanedToken.Length == 0;
    }

    /// <summary>
    /// Identifies Excel columns where numeric values are trusted identifiers instead of noise.
    /// </summary>
    /// <param name="sourceColumnName">The Excel column name.</param>
    /// <returns>True for configured identity-like columns such as FamilyID and EAN.</returns>
    public static bool IsTrustedIdentifierColumn(string? sourceColumnName)
    {
        if (string.IsNullOrWhiteSpace(sourceColumnName))
        {
            return false;
        }

        string normalizedColumnName = sourceColumnName.Trim().Replace("_", string.Empty).Replace("-", string.Empty);

        return normalizedColumnName.Equals("familyid", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("famid", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("ean", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("sku", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("ref", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("refco", StringComparison.OrdinalIgnoreCase)
            || normalizedColumnName.Equals("reference", StringComparison.OrdinalIgnoreCase);
    }
}
