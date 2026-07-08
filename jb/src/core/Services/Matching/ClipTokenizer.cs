using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Prism.Services.Matching;

/// <summary>
/// Minimal CLIP BPE tokenizer: loads vocab.json and merges.txt and converts
/// a text prompt to token IDs using OpenAI's CLIP byte-pair encoding.
/// Pads / truncates output to the CLIP context length of 77 tokens.
/// </summary>
internal sealed class ClipTokenizer
{
    private const int ContextLength = 77;
    private const long PadTokenId   = 0L;
    private const long StartTokenId = 49406L; // <|startoftext|>
    private const long EndTokenId   = 49407L; // <|endoftext|>

    // Matches CLIP's simple_tokenizer word pattern (before BPE).
    private static readonly Regex WordPattern = new(
        @"<\|startoftext\|>|<\|endoftext\|>|'s|'t|'re|'ve|'m|'ll|'d|[\p{L}]+|[\p{N}]|[^\s\p{L}\p{N}]+",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    private readonly Dictionary<string, int> vocab;
    private readonly Dictionary<(string A, string B), int> mergeRanks;

    // Maps each byte value 0-255 to a unicode character (CLIP byte_encoder).
    private readonly string[] byteEncoder = BuildByteEncoder();

    /// <summary>
    /// Loads vocab and merges from disk.
    /// </summary>
    public ClipTokenizer(string vocabPath, string mergesPath)
    {
        vocab      = LoadVocab(vocabPath);
        mergeRanks = LoadMerges(mergesPath);
    }

    /// <summary>
    /// Returns a 77-element int64 array of CLIP token IDs for the given text.
    /// </summary>
    public long[] Encode(string text)
    {
        text = CleanText(text);

        var tokens = new List<long>(ContextLength) { StartTokenId };

        foreach (Match match in WordPattern.Matches(text))
        {
            string word = match.Value;

            // Convert to byte-level unicode then apply BPE.
            byte[]   bytes     = Encoding.UTF8.GetBytes(word);
            string   bpeInput  = string.Concat(bytes.Select(b => byteEncoder[b]));
            string[] bpeTokens = BpeEncode(bpeInput);

            foreach (string t in bpeTokens)
            {
                if (vocab.TryGetValue(t, out int id))
                    tokens.Add(id);
            }
        }

        tokens.Add(EndTokenId);

        // Truncate preserving end token.
        if (tokens.Count > ContextLength)
        {
            tokens = [.. tokens.Take(ContextLength - 1), EndTokenId];
        }

        // Pad.
        while (tokens.Count < ContextLength)
            tokens.Add(PadTokenId);

        return [.. tokens];
    }

    //  BPE 

    private string[] BpeEncode(string word)
    {
        if (string.IsNullOrEmpty(word)) return [];

        // Start with characters; mark end of word on last character.
        var chars = new List<string>(word.Length);
        for (int i = 0; i < word.Length - 1; i++)
            chars.Add(word[i].ToString());
        chars.Add(word[^1] + "</w>");

        while (chars.Count >= 2)
        {
            int bestRank = int.MaxValue;
            int bestIdx  = -1;

            for (int i = 0; i < chars.Count - 1; i++)
            {
                if (mergeRanks.TryGetValue((chars[i], chars[i + 1]), out int rank) && rank < bestRank)
                {
                    bestRank = rank;
                    bestIdx  = i;
                }
            }

            if (bestIdx < 0) break;

            chars[bestIdx] = chars[bestIdx] + chars[bestIdx + 1];
            chars.RemoveAt(bestIdx + 1);
        }

        return [.. chars];
    }

    //  Loaders 

    private static Dictionary<string, int> LoadVocab(string path)
    {
        string json = File.ReadAllText(path, Encoding.UTF8);
        return JsonSerializer.Deserialize<Dictionary<string, int>>(json)
            ?? [];
    }

    private static Dictionary<(string, string), int> LoadMerges(string path)
    {
        var merges = new Dictionary<(string, string), int>();
        string[] lines = File.ReadAllLines(path, Encoding.UTF8);
        int rank = 0;

        foreach (string line in lines)
        {
            if (line.StartsWith('#')) continue;
            int space = line.IndexOf(' ');
            if (space < 1 || space >= line.Length - 1) continue;
            merges[(line[..space], line[(space + 1)..])] = rank++;
        }

        return merges;
    }

    //  Byte encoder 

    private static string[] BuildByteEncoder()
    {
        // Replicates OpenAI clip/simple_tokenizer.py bytes_to_unicode().
        // Printable ASCII 33–126, Latin supplement 161–172, 174–255 map to themselves.
        // All other bytes map to chr(256 + n) for n = 0, 1, 2, ...
        var bs = new List<int>();
        bs.AddRange(Enumerable.Range('!', '~' - '!' + 1));
        bs.AddRange(Enumerable.Range(0xA1, 0xAC - 0xA1 + 1));
        bs.AddRange(Enumerable.Range(0xAE, 0xFF - 0xAE + 1));

        var cs = new List<int>(bs);
        int n = 0;

        for (int b = 0; b < 256; b++)
        {
            if (!bs.Contains(b))
            {
                bs.Add(b);
                cs.Add(256 + n++);
            }
        }

        var encoder = new string[256];
        for (int i = 0; i < bs.Count; i++)
            encoder[bs[i]] = char.ConvertFromUtf32(cs[i]);

        return encoder;
    }

    //  Text normalisation 

    private static string CleanText(string text)
    {
        // Lowercase, collapse whitespace, trim.
        text = text.ToLowerInvariant().Trim();
        return Regex.Replace(text, @"\s+", " ");
    }
}
