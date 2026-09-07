using System.Runtime.CompilerServices;
using QS3D.Cad.Cli;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CliBatchAutomationModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        StdinSuccessIgnoresCommentsAndBlankLines();
        FileBatchExecutesDeterministically();
        DefaultBatchFailsFast();
        ContinueOnErrorRunsRemainingCommands();
        InvalidBatchOptionsFailClosed();
        MissingBatchFileFailsClosed();
        LegacyArgvDoesNotEnterBatchMode();
    }

    private static void StdinSuccessIgnoresCommentsAndBlankLines()
    {
        var app = CreateApp(out var document);
        var exit = Execute(
            app,
            new[] { "--stdin" },
            "# comment\n\nLINE 0 0 10 0\nCIRCLE 5 5 2\n",
            out var stdout,
            out var stderr);

        Equal(0, exit, "stdin success exit code");
        Equal(2, EntityCount(document), "stdin success entity count");
        Contains(stdout, "OK line=3", "stdin first physical line");
        Contains(stdout, "OK line=4", "stdin second physical line");
        Contains(stdout, "SUMMARY commands=2 succeeded=2 failed=0 stoppedEarly=false", "stdin success summary");
        Equal(string.Empty, stderr, "stdin success stderr");
    }

    private static void FileBatchExecutesDeterministically()
    {
        var app = CreateApp(out var document);
        var path = Path.Combine(Path.GetTempPath(), $"qs3d-cli-batch-{Guid.NewGuid():N}.txt");
        try
        {
            File.WriteAllText(path, "# file batch\nLINE 1 2 3 4\n");
            var exit = Execute(app, new[] { "--batch", path }, string.Empty, out var stdout, out var stderr);
            Equal(0, exit, "file batch exit code");
            Equal(1, EntityCount(document), "file batch entity count");
            Contains(stdout, "OK line=2", "file batch physical line");
            Contains(stdout, "SUMMARY commands=1 succeeded=1 failed=0 stoppedEarly=false", "file batch summary");
            Equal(string.Empty, stderr, "file batch stderr");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void DefaultBatchFailsFast()
    {
        var app = CreateApp(out var document);
        var exit = Execute(
            app,
            new[] { "--stdin" },
            "LINE 0 0 1 0\nNO_SUCH_COMMAND\nLINE 0 1 1 1\n",
            out var stdout,
            out var stderr);

        Equal(1, exit, "fail-fast exit code");
        Equal(1, EntityCount(document), "fail-fast entity count");
        Contains(stdout, "ERROR line=2", "fail-fast error line");
        Contains(stdout, "SUMMARY commands=2 succeeded=1 failed=1 stoppedEarly=true", "fail-fast summary");
        Equal(string.Empty, stderr, "fail-fast stderr");
    }

    private static void ContinueOnErrorRunsRemainingCommands()
    {
        var app = CreateApp(out var document);
        var exit = Execute(
            app,
            new[] { "--stdin", "--continue-on-error" },
            "LINE 0 0 1 0\nNO_SUCH_COMMAND\nLINE 0 1 1 1\n",
            out var stdout,
            out var stderr);

        Equal(1, exit, "continue-on-error exit code");
        Equal(2, EntityCount(document), "continue-on-error entity count");
        Contains(stdout, "ERROR line=2", "continue-on-error error line");
        Contains(stdout, "OK line=3", "continue-on-error trailing command");
        Contains(stdout, "SUMMARY commands=3 succeeded=2 failed=1 stoppedEarly=false", "continue-on-error summary");
        Equal(string.Empty, stderr, "continue-on-error stderr");
    }

    private static void InvalidBatchOptionsFailClosed()
    {
        var app = CreateApp(out var document);
        var exit = Execute(
            app,
            new[] { "--stdin", "--batch", "commands.txt" },
            string.Empty,
            out var stdout,
            out var stderr);

        Equal(2, exit, "invalid options exit code");
        Equal(0, EntityCount(document), "invalid options entity count");
        Equal(string.Empty, stdout, "invalid options stdout");
        Contains(stderr, "ERROR usage", "invalid options error prefix");
        Contains(stderr, "Specify exactly one input source", "invalid options detail");

        exit = Execute(app, new[] { "--continue-on-error" }, string.Empty, out stdout, out stderr);
        Equal(2, exit, "orphan continue-on-error exit code");
        Equal(string.Empty, stdout, "orphan continue-on-error stdout");
        Contains(stderr, "Specify exactly one input source", "orphan continue-on-error detail");
    }

    private static void MissingBatchFileFailsClosed()
    {
        var app = CreateApp(out var document);
        var path = Path.Combine(Path.GetTempPath(), $"qs3d-cli-missing-{Guid.NewGuid():N}.txt");
        var exit = Execute(app, new[] { "--batch", path }, string.Empty, out var stdout, out var stderr);
        Equal(2, exit, "missing file exit code");
        Equal(0, EntityCount(document), "missing file entity count");
        Equal(string.Empty, stdout, "missing file stdout");
        Contains(stderr, "ERROR input", "missing file error prefix");
    }

    private static void LegacyArgvDoesNotEnterBatchMode()
    {
        if (CliBatchMode.IsRequested(new[] { "LINE", "0", "0", "1", "1" }))
            throw new InvalidOperationException("Legacy single-command argv must not be routed into batch mode.");
        if (!CliBatchMode.IsRequested(new[] { "--stdin" }))
            throw new InvalidOperationException("--stdin must route into batch mode.");
    }

    private static StandaloneCadApplication CreateApp(out ICadDocument document)
    {
        var app = new StandaloneCadApplication();
        document = app.NewDocument("cli-batch-smoke");
        return app;
    }

    private static int Execute(
        StandaloneCadApplication app,
        IReadOnlyList<string> args,
        string stdin,
        out string stdout,
        out string stderr)
    {
        using var input = new StringReader(stdin);
        using var output = new StringWriter();
        using var error = new StringWriter();
        var exit = CliBatchMode.Execute(app, args, input, output, error);
        stdout = output.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        stderr = error.ToString().Replace("\r\n", "\n", StringComparison.Ordinal);
        return exit;
    }

    private static int EntityCount(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().Count;
    }

    private static void Equal<T>(T expected, T actual, string operation)
        where T : IEquatable<T>
    {
        if (!actual.Equals(expected))
            throw new InvalidOperationException($"{operation}: expected '{expected}', got '{actual}'.");
    }

    private static void Contains(string actual, string expected, string operation)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"{operation}: missing '{expected}' in '{actual}'.");
    }
}
