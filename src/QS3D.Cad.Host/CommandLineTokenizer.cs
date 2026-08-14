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
        var tokenStarted = false;

        foreach (var c in commandLine)
        {
            if (escape)
            {
                current.Append(c);
                tokenStarted = true;
                escape = false;
                continue;
            }

            if (c == '\\' && quoted)
            {
                escape = true;
                tokenStarted = true;
                continue;
            }

            if (c == '"')
            {
                quoted = !quoted;
                tokenStarted = true;
                continue;
            }

            if (char.IsWhiteSpace(c) && !quoted)
            {
                Flush(tokens, current, ref tokenStarted);
                continue;
            }

            current.Append(c);
            tokenStarted = true;
        }

        if (escape || quoted)
            throw new FormatException("Unterminated quoted command argument.");
        Flush(tokens, current, ref tokenStarted);
        return tokens;
    }

    private static void Flush(List<string> tokens, StringBuilder current, ref bool tokenStarted)
    {
        if (!tokenStarted) return;
        tokens.Add(current.ToString());
        current.Clear();
        tokenStarted = false;
    }
}
