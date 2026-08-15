using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Cli;

public sealed record CliBatchResult(int Commands, int Succeeded, int Failed, bool StoppedEarly)
{
    public int ExitCode => Failed == 0 ? 0 : 1;
}

public static class CliBatchMode
{
    public static bool IsRequested(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        return args.Any(static arg =>
            arg.Equals("--batch", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--stdin", StringComparison.OrdinalIgnoreCase)
            || arg.Equals("--continue-on-error", StringComparison.OrdinalIgnoreCase));
    }

    public static int Execute(
        StandaloneCadApplication app,
        IReadOnlyList<string> args,
        TextReader stdin,
        TextWriter output,
        TextWriter error)
    {
        ArgumentNullException.ThrowIfNull(app);
        ArgumentNullException.ThrowIfNull(args);
        ArgumentNullException.ThrowIfNull(stdin);
        ArgumentNullException.ThrowIfNull(output);
        ArgumentNullException.ThrowIfNull(error);

        if (!TryParse(args, out var options, out var parseError))
        {
            error.WriteLine("ERROR usage " + parseError);
            return 2;
        }

        try
        {
            var lines = options!.UseStdin ? ReadLines(stdin) : File.ReadLines(options.FilePath!);
            return new CliBatchRunner(app).Run(lines, output, options.ContinueOnError).ExitCode;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or ArgumentException or NotSupportedException)
        {
            error.WriteLine("ERROR input " + OneLine(ex.Message));
            return 2;
        }
    }

    private static bool TryParse(IReadOnlyList<string> args, out BatchOptions? options, out string error)
    {
        options = null;
        error = string.Empty;
        string? filePath = null;
        var useStdin = false;
        var continueOnError = false;

        for (var index = 0; index < args.Count; index++)
        {
            var token = args[index];
            if (token.Equals("--batch", StringComparison.OrdinalIgnoreCase))
            {
                if (filePath is not null)
                {
                    error = "--batch may be specified only once.";
                    return false;
                }
                if (index + 1 >= args.Count || string.IsNullOrWhiteSpace(args[index + 1]))
                {
                    error = "--batch requires a file path.";
                    return false;
                }
                filePath = args[++index];
                continue;
            }

            if (token.Equals("--stdin", StringComparison.OrdinalIgnoreCase))
            {
                if (useStdin)
                {
                    error = "--stdin may be specified only once.";
                    return false;
                }
                useStdin = true;
                continue;
            }

            if (token.Equals("--continue-on-error", StringComparison.OrdinalIgnoreCase))
            {
                if (continueOnError)
                {
                    error = "--continue-on-error may be specified only once.";
                    return false;
                }
                continueOnError = true;
                continue;
            }

            error = $"Unknown batch option '{token}'.";
            return false;
        }

        if ((filePath is null) == !useStdin)
        {
            error = "Specify exactly one input source: --batch <file> or --stdin.";
            return false;
        }

        options = new BatchOptions(filePath, useStdin, continueOnError);
        return true;
    }

    private static IEnumerable<string> ReadLines(TextReader reader)
    {
        while (reader.ReadLine() is { } line)
            yield return line;
    }

    private static string OneLine(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "Input failed."
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();

    private sealed record BatchOptions(string? FilePath, bool UseStdin, bool ContinueOnError);
}

public sealed class CliBatchRunner
{
    private readonly StandaloneCadApplication _app;
    private InMemoryEditor? _lastEditor;
    private int _messageCursor;

    public CliBatchRunner(StandaloneCadApplication app)
        => _app = app ?? throw new ArgumentNullException(nameof(app));

    public CliBatchResult Run(IEnumerable<string> lines, TextWriter output, bool continueOnError = false)
    {
        ArgumentNullException.ThrowIfNull(lines);
        ArgumentNullException.ThrowIfNull(output);

        var physicalLine = 0;
        var commands = 0;
        var succeeded = 0;
        var failed = 0;
        var stoppedEarly = false;

        foreach (var rawLine in lines)
        {
            physicalLine++;
            var command = rawLine?.Trim() ?? string.Empty;
            if (command.Length == 0 || command.StartsWith('#'))
                continue;

            commands++;
            try
            {
                var result = _app.Execute(command);
                if (result.Succeeded)
                {
                    succeeded++;
                    output.WriteLine($"OK line={physicalLine}");
                }
                else
                {
                    failed++;
                    output.WriteLine($"ERROR line={physicalLine} {OneLine(result.Message)}");
                }
            }
            catch (Exception ex)
            {
                failed++;
                output.WriteLine($"ERROR line={physicalLine} {OneLine(ex.Message)}");
            }

            DumpMessages(output);
            if (failed != 0 && !continueOnError)
            {
                stoppedEarly = true;
                break;
            }
        }

        output.WriteLine(
            $"SUMMARY commands={commands} succeeded={succeeded} failed={failed} stoppedEarly={(stoppedEarly ? "true" : "false")}");
        return new CliBatchResult(commands, succeeded, failed, stoppedEarly);
    }

    private void DumpMessages(TextWriter output)
    {
        if (_app.Documents.ActiveDocument?.Editor is not InMemoryEditor editor)
            return;

        if (!ReferenceEquals(editor, _lastEditor))
        {
            _lastEditor = editor;
            _messageCursor = 0;
        }
        if (_messageCursor < 0 || _messageCursor > editor.Messages.Count)
            _messageCursor = 0;

        for (var index = _messageCursor; index < editor.Messages.Count; index++)
            output.WriteLine(editor.Messages[index]);
        _messageCursor = editor.Messages.Count;
    }

    private static string OneLine(string? value)
        => string.IsNullOrWhiteSpace(value)
            ? "Command failed."
            : value.Replace('\r', ' ').Replace('\n', ' ').Trim();
}
