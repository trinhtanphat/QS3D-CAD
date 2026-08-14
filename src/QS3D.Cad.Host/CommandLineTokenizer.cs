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
        var tokenStarted = false;

        for (var index = 0; index < commandLine.Length; index++)
        {
            var c = commandLine[index];
            if (c == '"')
            {
                if (quoted && index + 1 < commandLine.Length && commandLine[index + 1] == '"')
                {
                    current.Append('"');
                    tokenStarted = true;
                    index++;
                    continue;
                }

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

        if (quoted)
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
