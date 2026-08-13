using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class StandaloneCadApplication
{
    public StandaloneCadApplication()
    {
        Documents = new InMemoryDocumentManager();
        Commands = new CommandRegistry();
        Store = new BootstrapDrawingStore();
        BuiltInCommands.RegisterAll(Commands);
    }

    public InMemoryDocumentManager Documents { get; }
    public CommandRegistry Commands { get; }
    public BootstrapDrawingStore Store { get; }

    public ICadDocument NewDocument(string name) => Documents.CreateNew(name);

    public ICadDocument OpenBootstrap(string path)
    {
        var document = Store.Load(path);
        Documents.Open(document);
        return document;
    }

    public void SaveBootstrap(string path)
    {
        var document = Documents.ActiveDocument as InMemoryCadDocument
            ?? throw new InvalidOperationException("No bootstrap document is active.");
        Store.Save(document, path);
    }

    public CommandResult Execute(string commandLine, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> tokens;
        try { tokens = CommandLineTokenizer.Tokenize(commandLine); }
        catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        if (tokens.Count == 0) return CommandResult.Failure("Command line is empty.");
        var document = Documents.ActiveDocument;
        if (document is null) return CommandResult.Failure("No active drawing.");
        var result = Commands.Execute(tokens[0], new CommandContext(document, tokens.Skip(1), cancellationToken));
        if (!string.IsNullOrWhiteSpace(result.Message)) document.Editor.WriteMessage(result.Message!);
        return result;
    }
}
