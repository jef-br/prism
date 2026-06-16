using System;

/// <summary>
/// Lightweight executable-style test cases for numeric noise filtering.
/// Move these cases into the project test runner once a test project exists.
/// </summary>
public static class NoiseFilterTestCases
{
    /// <summary>
    /// Verifies dimensions, dates, and unit-adjacent numbers are removed from matching text.
    /// </summary>
    public static void VerifyObviousNumericNoiseIsRemoved()
    {
        string cleaned = NoiseFilter.RemoveNumericNoiseForMatching("shirt 800x1200 2024-05-18 25cm 12345678");

        AssertFalse(cleaned.Contains("800x1200", StringComparison.OrdinalIgnoreCase), "Dimension should be removed.");
        AssertFalse(cleaned.Contains("2024-05-18", StringComparison.OrdinalIgnoreCase), "Date should be removed.");
        AssertFalse(cleaned.Contains("25cm", StringComparison.OrdinalIgnoreCase), "Unit value should be removed.");
        AssertTrue(cleaned.Contains("12345678", StringComparison.OrdinalIgnoreCase), "Unclassified numeric token should remain available.");
    }

    /// <summary>
    /// Verifies trusted identifier columns are never filtered as numeric noise.
    /// </summary>
    public static void VerifyTrustedIdentifierColumnsArePreserved()
    {
        string cleanedFamilyId = NoiseFilter.RemoveNumericNoiseForMatching("20240518", "FamilyID");
        string cleanedEan = NoiseFilter.RemoveNumericNoiseForMatching("8712345678901", "EAN");

        AssertTrue(cleanedFamilyId == "20240518", "FamilyID values should be preserved.");
        AssertTrue(cleanedEan == "8712345678901", "EAN values should be preserved.");
    }

    private static void AssertTrue(bool actual, string message)
    {
        if (!actual)
        {
            throw new InvalidOperationException(message);
        }
    }

    private static void AssertFalse(bool actual, string message)
    {
        if (actual)
        {
            throw new InvalidOperationException(message);
        }
    }
}
