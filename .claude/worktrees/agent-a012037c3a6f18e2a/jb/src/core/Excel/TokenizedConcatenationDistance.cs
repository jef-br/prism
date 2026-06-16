using System;
using System.Collections.Generic;
using System.Linq;

/// <summary>
/// Computes Tokenized Concatenation Distance for Excel header matching.
/// </summary>
public static class TokenizedConcatenationDistance
{
    /// <summary>
    /// Computes the tokenized concatenation distance between candidate tokens and a canonical target.
    /// </summary>
    /// <param name="tokens">Candidate header tokens.</param>
    /// <param name="target">Canonical target header without separators.</param>
    /// <param name="reorderingWeight">Penalty applied to token reordering.</param>
    /// <returns>A finite distance when the tokens can concatenate into the target; otherwise positive infinity.</returns>
    public static double Compute(IReadOnlyList<string> tokens, string target, double reorderingWeight = 1.0)
    {
        if (tokens.Count == 0 || string.IsNullOrWhiteSpace(target))
        {
            return double.PositiveInfinity;
        }

        string normalizedTarget = target.Trim();
        string[] normalizedTokens = tokens
            .Where(token => !string.IsNullOrWhiteSpace(token))
            .Select(token => token.Trim())
            .ToArray();

        if (normalizedTokens.Length == 0)
        {
            return double.PositiveInfinity;
        }

        if (normalizedTokens.Length == 1)
        {
            return normalizedTokens[0] == normalizedTarget ? 0.0 : double.PositiveInfinity;
        }

        int minimumKendallTau = int.MaxValue;
        bool foundValidPermutation = false;

        foreach (int[] permutation in BuildPermutations(Enumerable.Range(0, normalizedTokens.Length).ToArray()))
        {
            string concatenatedCandidate = string.Concat(permutation.Select(index => normalizedTokens[index]));

            if (concatenatedCandidate != normalizedTarget)
            {
                continue;
            }

            foundValidPermutation = true;
            int kendallTau = ComputeKendallTau(permutation);

            if (kendallTau < minimumKendallTau)
            {
                minimumKendallTau = kendallTau;
            }
        }

        if (!foundValidPermutation)
        {
            return double.PositiveInfinity;
        }

        double targetLength = normalizedTarget.Length;
        double entropy = normalizedTokens.Sum(token =>
        {
            double tokenShare = token.Length / targetLength;
            return tokenShare > 0 ? -tokenShare * Math.Log(tokenShare) : 0.0;
        });

        double maximumEntropy = Math.Log(normalizedTokens.Length);
        double imbalance = 1.0 - entropy / maximumEntropy;

        return (normalizedTokens.Length - 1) * (1.0 + imbalance) + reorderingWeight * minimumKendallTau;
    }

    /// <summary>
    /// Converts a TCD value into a percentage confidence.
    /// </summary>
    /// <param name="tcd">Tokenized concatenation distance.</param>
    /// <param name="lambda">Decay rate used to map distance to confidence.</param>
    /// <returns>A confidence percentage between zero and one hundred.</returns>
    public static double ConvertDistanceToConfidence(double tcd, double lambda = 0.5)
    {
        if (double.IsPositiveInfinity(tcd))
        {
            return 0.0;
        }

        return 100.0 * Math.Exp(-lambda * tcd);
    }

    private static IEnumerable<int[]> BuildPermutations(int[] values)
    {
        if (values.Length <= 1)
        {
            yield return values.ToArray();
            yield break;
        }

        for (int index = 0; index < values.Length; index++)
        {
            int[] remainingValues = values.Where((_, remainingIndex) => remainingIndex != index).ToArray();

            foreach (int[] subPermutation in BuildPermutations(remainingValues))
            {
                yield return new[] { values[index] }.Concat(subPermutation).ToArray();
            }
        }
    }

    private static int ComputeKendallTau(IReadOnlyList<int> permutation)
    {
        int inversions = 0;

        for (int left = 0; left < permutation.Count - 1; left++)
        {
            for (int right = left + 1; right < permutation.Count; right++)
            {
                if (permutation[left] > permutation[right])
                {
                    inversions++;
                }
            }
        }

        return inversions;
    }
}
