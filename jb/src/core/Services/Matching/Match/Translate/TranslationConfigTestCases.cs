using System;

namespace Prism.Core;

/// <summary>
/// Lightweight executable-style test cases for translation matching behavior.
/// Move these cases into the project test runner once a test project exists.
/// </summary>
public static class TranslationConfigTestCases
{
    /// <summary>
    /// Verifies exact token matching and configured synonym matching.
    /// </summary>
    public static void VerifySynonymMatching()
    {
        TranslationConfig config = new()
        {
            SynonymGroups =
            [
                new SynonymGroup
                {
                    Id = "color-blue",
                    Domain = "color",
                    Terms = [ "blue", "bleu", "blauw" ]
                }
            ]
        };

        AssertTrue(config.AreMatchingTokens("blue", "blue"), "Exact token match should pass.");
        AssertTrue(config.AreMatchingTokens("blue", "bleu"), "Configured synonym match should pass.");
        AssertFalse(config.AreMatchingTokens("blue", "red"), "Unrelated token match should fail.");
    }

    /// <summary>
    /// Verifies configured stop words are ignored.
    /// </summary>
    public static void VerifyStopWordMatching()
    {
        TranslationConfig config = new()
        {
            StopWords = new StopWordConfig
            {
                General = [ "the", "and" ],
                Domain = [ "product", "image" ]
            }
        };

        AssertTrue(config.IsStopWord("the"), "General stop word should be ignored.");
        AssertTrue(config.IsStopWord("product"), "Domain stop word should be ignored.");
        AssertFalse(config.IsStopWord("cotton"), "Meaningful product token should not be ignored.");
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
