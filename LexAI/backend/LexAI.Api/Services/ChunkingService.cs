using System.Text;

namespace LexAI.Api.Services;

public class ChunkingService : IChunkingService
{
    private static readonly string[] Separators = { "\n\n", "\n", ".", ";", ",", " " };

    public List<string> ChunkText(string text, int chunkSize = 1000, int overlap = 200)
    {
        if (string.IsNullOrWhiteSpace(text)) return new List<string>();
        text = text.Replace("\r\n", "\n").Trim();

        var pieces = SplitToPieces(text, Separators, chunkSize);
        return Merge(pieces, chunkSize, overlap);
    }

    private static List<string> SplitToPieces(string text, string[] separators, int chunkSize)
    {
        var result = new List<string>();
        if (text.Length == 0) return result;
        if (text.Length <= chunkSize)
        {
            result.Add(text);
            return result;
        }

        int sepIndex = -1;
        for (int i = 0; i < separators.Length; i++)
        {
            if (text.Contains(separators[i])) { sepIndex = i; break; }
        }

        if (sepIndex == -1)
            return HardSplit(text, chunkSize);

        var sep = separators[sepIndex];
        var remaining = separators.Skip(sepIndex + 1).ToArray();

        foreach (var part in SplitKeepSeparator(text, sep))
        {
            if (part.Length <= chunkSize)
                result.Add(part);
            else if (remaining.Length > 0)
                result.AddRange(SplitToPieces(part, remaining, chunkSize));
            else
                result.AddRange(HardSplit(part, chunkSize));
        }

        return result;
    }

    private static List<string> SplitKeepSeparator(string text, string separator)
    {
        var parts = new List<string>();
        int start = 0;
        int idx;
        while ((idx = text.IndexOf(separator, start, StringComparison.Ordinal)) >= 0)
        {
            parts.Add(text.Substring(start, idx - start + separator.Length));
            start = idx + separator.Length;
        }
        if (start < text.Length)
            parts.Add(text.Substring(start));
        return parts.Where(p => p.Length > 0).ToList();
    }

    private static List<string> HardSplit(string text, int chunkSize)
    {
        var parts = new List<string>();
        for (int i = 0; i < text.Length; i += chunkSize)
            parts.Add(text.Substring(i, Math.Min(chunkSize, text.Length - i)));
        return parts;
    }

    private static List<string> Merge(List<string> pieces, int chunkSize, int overlap)
    {
        var chunks = new List<string>();
        var sb = new StringBuilder();

        foreach (var piece in pieces)
        {
            if (sb.Length > 0 && sb.Length + piece.Length > chunkSize)
            {
                var chunk = sb.ToString().Trim();
                if (chunk.Length > 0) chunks.Add(chunk);

                var carry = chunk.Length > overlap
                    ? chunk.Substring(chunk.Length - overlap)
                    : chunk;

                sb.Clear();
                sb.Append(carry);
                if (carry.Length > 0 && !carry.EndsWith(" "))
                    sb.Append(' ');
            }

            sb.Append(piece);
        }

        var last = sb.ToString().Trim();
        if (last.Length > 0) chunks.Add(last);

        return chunks;
    }
}
