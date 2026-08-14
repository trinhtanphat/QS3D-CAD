using System.Runtime.CompilerServices;
using QS3D.Cad.Host;

namespace QS3D.Cad.SmokeTests;

internal static class CommandLineTokenizerModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var tokens = CommandLineTokenizer.Tokenize("QSPROP 1 Note \"\"");
        Equal(4, tokens.Count);
        Equal("QSPROP", tokens[0]);
        Equal("1", tokens[1]);
        Equal("Note", tokens[2]);
        Equal(string.Empty, tokens[3]);

        var escaped = CommandLineTokenizer.Tokenize("QSPROP 1 Note \"a\\\"b\"");
        Equal("a\"b", escaped[3]);
        Throws<FormatException>(() => CommandLineTokenizer.Tokenize("QSPROP 1 Note \"unterminated"));

        var app = new StandaloneCadApplication();
        app.NewDocument("Empty property");
        Success(app.Execute("LINE 0 0 1 0"));
        Success(app.Execute("QSTAG 1 Wall EmptyPropertyWall"));
        Success(app.Execute("QSPROP 1 Note \"\""));
        var element = app.Projects.GetElementBySource(app.Documents.ActiveDocument!, new QS3D.Platform.Domain.CadHandle("1"));
        Equal(string.Empty, element.Properties["Note"]);

        Console.WriteLine("PASS command tokenizer preserves empty quoted arguments");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected} but got {actual}.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
