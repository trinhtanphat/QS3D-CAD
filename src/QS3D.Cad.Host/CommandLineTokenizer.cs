using System.Text;

namespace QS3D.Cad.Host;

public static class CommandLineTokenizer
{
    public static IReadOnlyList<string> Tokenize(string commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine)) return Array.Empty<string>();
        var tokens = new List<string>();
        var current = new StringBuilder();
        var quoted = false;
        var escape = false;

        foreach (var c in commandLine)
        {
            if (escape)
            {
                current.Append(c);
                escape = false;
                continue;
            }

            if (c == '\\' && quoted)
            {
                escape = true;
                continue;
            }

            if (c == '"')
            {
                quoted = !quoted;
                continue;
            }

            if (char.IsWhiteSpace(c) && !quoted)
            {
                Flush(tokens, current);
                continue;
            }

            current.Append(c);
        }

        if (escape || quoted)
            throw new FormatException("Unterminated quoted command argument.");
        Flush(tokens, current);
        return tokens;
    }

    private static void Flush(List<string> tokens, StringBuilder current)
    {
        if (current.Length == 0) return;
        tokens.Add(current.ToString());
        current.Clear();
    }
}
