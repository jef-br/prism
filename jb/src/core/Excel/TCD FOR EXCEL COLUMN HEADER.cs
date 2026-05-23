class Program
{
    /*
    Tokenized Concatenation Distance (TCD)
    */
    static double ComputeTCD(string[] tokens, string target, double w = 1.0)
    {
        // --- Parameters ---
        // tokens : input token array — the candidate substrings to match against target
        // target : the string being matched
        // w      : reordering weight — multiplier on the Kendall tau penalty (default 1.0)
        //          raise to penalise out-of-order tokens more aggressively than fragmentation

        // --- Local helpers ---

        IEnumerable<int[]> Permutations(int[] arr)
        {
            if (arr.Length <= 1) { yield return arr.ToArray(); yield break; }
            for (int i = 0; i < arr.Length; i++)
            {
                var rest = arr.Where((_, idx) => idx != i).ToArray();
                foreach (var sub in Permutations(rest))
                    yield return new[] { arr[i] }.Concat(sub).ToArray();
            }
        }

        int KendallTau(int[] perm)
        {
            int inv = 0;
            for (int i = 0; i < perm.Length - 1; i++)
                for (int j = i + 1; j < perm.Length; j++)
                    if (perm[i] > perm[j]) inv++;
            return inv;
        }

        // --- Single-token shortcut ---
        int n = tokens.Length;
        if (n == 1)
            return tokens[0] == target ? 0.0 : double.PositiveInfinity;

        // --- Find all valid permutations; keep the one with minimum Kendall tau ---
        int minK = int.MaxValue;
        bool anyValid = false;

        foreach (var perm in Permutations(Enumerable.Range(0, n).ToArray()))
        {
            if (string.Concat(perm.Select(i => tokens[i])) == target)
            {
                anyValid = true;
                int k = KendallTau(perm);
                if (k < minK) minK = k;
            }
        }

        if (!anyValid)
            return double.PositiveInfinity;

        // --- Imbalance I: 1 − normalised entropy of token-length proportions ---
        // I = 0 when all tokens are equal length (best case)
        // I → 1 when one token dominates (worst case)
        double L = target.Length;
        double entropy = tokens.Sum(t => {
            double p = t.Length / L;
            return p > 0 ? -p * Math.Log(p) : 0.0;   // natural log; base cancels in ratio
        });
        double maxEntropy = Math.Log(n);
        double I = 1.0 - entropy / maxEntropy;

        // --- TCD = (n − 1)(1 + I) + w · K ---
        // Fragmentation cost: between (n−1) [balanced] and 2(n−1) [maximally skewed]
        // Reorder cost: Kendall tau of the best valid permutation, scaled by w
        return (n - 1) * (1.0 + I) + w * minK;
    }
    static double TcdToConfidence(double tcd, double lambda = 0.5)
    {
        if (double.IsPositiveInfinity(tcd)) return 0.0;
        return 100.0 * Math.Exp(-lambda * tcd);
    }


    // Below is example code to illustrate how TCD works
    // static void Main()
    // {
    //     var tests = new (string[] tokens, string target, string label, string expected)[]
    //     {



    //         // --- Single token ---
    //         (new[]{"12345"},                 "12345", "single exact match",              "0.000"),
    //         (new[]{"12346"},                 "12345", "single wrong token",              "∞"),

    //         // --- Target "12345" ---
    //         (new[]{"123","45"},              "12345", "123 + 45",                        "~1.03"),
    //         (new[]{"12","345"},              "12345", "12 + 345",                        "~1.03"),
    //         (new[]{"345","12"},              "12345", "345 + 12  [reordered]",           "~2.03"),
    //         (new[]{"1","2","3","4","5"},     "12345", "1+2+3+4+5",                       "4.000"),
    //         (new[]{"1","5","2","34"},        "12345", "1+5+2+34  [reordered]",           "~5.12"),
    //         (new[]{"1","6","2","34"},        "12345", "1+6+2+34  [invalid]",             "∞"),

    //         // --- Target "1234567890": balance sensitivity ---
    //         (new[]{"12345","67890"},         "1234567890", "12345+67890  [balanced]",    "1.000"),
    //         (new[]{"1234","567890"},         "1234567890", "1234+567890  [slight imbal]","~1.03"),
    //         (new[]{"123","456","7890"},      "1234567890", "123+456+7890",               "~2.02"),
    //         (new[]{"1","234567890"},         "1234567890", "1+234567890  [unbalanced]",  "~1.53"),
    //     };

    //     Console.WriteLine($"\n{"Label",-42} {"Expected",15} {"TCD",10}");
    //     Console.WriteLine(new string('-', 70));

    //     foreach (var (tokens, target, label, expected) in tests)
    //     {
    //         double tcd = ComputeTCD(tokens, target);
    //         string result = double.IsPositiveInfinity(tcd) ? "∞" : tcd.ToString("F3");
    //         Console.WriteLine($"{label,-42} {expected,15} {result,10}");

    //         double confidence = TcdToConfidence(tcd);          // e.g. 59.8%
            
    //     }
    // }
}