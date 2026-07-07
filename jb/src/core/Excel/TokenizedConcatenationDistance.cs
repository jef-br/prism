using System;
using System.Collections.Generic;
using System.Linq;

namespace Prism.Core;

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

        // No permutation can concatenate to the target unless the total token length matches —
        // cheap guard that rejects almost all candidates before any search.
        if (normalizedTokens.Sum(token => token.Length) != normalizedTarget.Length)
        {
            return double.PositiveInfinity;
        }

        // Beyond this the 2^N mask table stops being reasonable; the previous N! enumeration never
        // completed for such inputs anyway, so treating them as no-match loses nothing.
        if (normalizedTokens.Length > MaxTokensForPermutationSearch)
        {
            return double.PositiveInfinity;
        }

        int minimumKendallTau = ComputeMinimumKendallTau(normalizedTokens, normalizedTarget);

        if (minimumKendallTau < 0)
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

    private const int MaxTokensForPermutationSearch = 16;

    /// <summary>
    /// Minimum Kendall tau (inversion count) over all token permutations whose concatenation equals
    /// the target, or -1 when no permutation matches. Dynamic program over token-usage masks: a
    /// mask's position in the target is the total length of its used tokens, and appending token
    /// <c>i</c> adds one inversion for every already-used token with a higher original index —
    /// replacing the former N! permutation enumeration with O(2^N × N).
    /// </summary>
    private static int ComputeMinimumKendallTau(string[] tokens, string target)
    {
        int tokenCount = tokens.Length;
        int fullMask = (1 << tokenCount) - 1;

        int[] bestInversions = new int[fullMask + 1];
        Array.Fill(bestInversions, int.MaxValue);
        bestInversions[0] = 0;

        // positionByMask[mask] = total length of the tokens used in mask (the target prefix covered).
        int[] positionByMask = new int[fullMask + 1];
        for (int mask = 1; mask <= fullMask; mask++)
        {
            int lowestBitIndex = System.Numerics.BitOperations.TrailingZeroCount(mask);
            positionByMask[mask] = positionByMask[mask & (mask - 1)] + tokens[lowestBitIndex].Length;
        }

        for (int mask = 0; mask < fullMask; mask++)
        {
            if (bestInversions[mask] == int.MaxValue)
            {
                continue;
            }

            int position = positionByMask[mask];

            for (int index = 0; index < tokenCount; index++)
            {
                if ((mask >> index & 1) != 0)
                {
                    continue;
                }

                string token = tokens[index];

                if (position + token.Length > target.Length ||
                    !target.AsSpan(position, token.Length).SequenceEqual(token))
                {
                    continue;
                }

                int added = System.Numerics.BitOperations.PopCount((uint)(mask >> (index + 1)));
                int candidate = bestInversions[mask] + added;
                int nextMask = mask | (1 << index);

                if (candidate < bestInversions[nextMask])
                {
                    bestInversions[nextMask] = candidate;
                }
            }
        }

        return bestInversions[fullMask] == int.MaxValue ? -1 : bestInversions[fullMask];
    }
}
