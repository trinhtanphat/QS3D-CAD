using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class StandaloneCadApplication
{
    private readonly Dictionary<DrawingId, Stack<HistoryEntry>> _undo = new();
    private readonly Dictionary<DrawingId, Stack<HistoryEntry>> _redo = new();
    private readonly CommandRegistry _commands;

    public StandaloneCadApplication()
    {
        Projects = new StandaloneSemanticWorkspace();
        Documents = new StandaloneDocumentManager(OnDocumentOpened, OnDocumentClosed);
        _commands = new CommandRegistry();
        Commands = new StandaloneCommandCatalog(_commands);
        Store = new BootstrapDrawingStore();
        BuiltInCommands.RegisterAll(_commands);
        LayerCommands.RegisterAll(_commands);
        BlockCommands.RegisterAll(_commands);
        SemanticCommands.RegisterAll(_commands, Projects);
        AdvancedReferenceCommands.RegisterAll(_commands);
        XrefReferenceCommands.RegisterAll(_commands);
        LayoutReferenceCommands.RegisterAll(_commands);
        PlotReferenceCommands.RegisterAll(_commands);
    }

    public StandaloneDocumentManager Documents { get; }
    public StandaloneSemanticWorkspace Projects { get; }
    public StandaloneCommandCatalog Commands { get; }
    public BootstrapDrawingStore Store { get; }

    public ICadDocument NewDocument(string name) => Documents.CreateNew(name);

    public ICadDocument OpenBootstrap(string path)
    {
        var loaded = Store.LoadWithProject(path);
        Documents.Open(loaded.Document);
        try
        {
            if (loaded.Project is not null) Projects.Attach(loaded.Document, loaded.Project);
            return loaded.Document;
        }
        catch
        {
            Documents.Close(loaded.Document.Id);
            throw;
        }
    }

    public bool CloseDocument(DrawingId drawingId) => Documents.Close(drawingId);

    public void SaveBootstrap(string path)
    {
        var document = Documents.ActiveDocument as InMemoryCadDocument
            ?? throw new InvalidOperationException("No bootstrap document is active.");
        Store.Save(document, Projects.Get(document), path);
    }

    public CommandResult Execute(string commandLine, CancellationToken cancellationToken = default)
    {
        IReadOnlyList<string> tokens;
        try { tokens = CommandLineTokenizer.Tokenize(commandLine); }
        catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        return ExecuteTokens(tokens, cancellationToken);
    }

    public CommandResult ExecuteCommand(string commandName, IEnumerable<string>? arguments = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(commandName)) return CommandResult.Failure("Command name is empty.");
        var normalizedName = commandName.Trim();
        if (normalizedName.Any(char.IsWhiteSpace)) return CommandResult.Failure("Command name must be a single token.");

        var tokens = new List<string> { normalizedName };
        if (arguments is not null)
        {
            foreach (var argument in arguments)
            {
                if (argument is null) return CommandResult.Failure("Command arguments must not contain null values.");
                tokens.Add(argument);
            }
        }
        return ExecuteTokens(tokens, cancellationToken);
    }

    private CommandResult ExecuteTokens(IReadOnlyList<string> tokens, CancellationToken cancellationToken)
    {
        if (tokens.Count == 0) return CommandResult.Failure("Command line is empty.");
        var document = Documents.ActiveDocument;
        if (document is null) return CommandResult.Failure("No active drawing.");

        cancellationToken.ThrowIfCancellationRequested();
        if (tokens[0].Equals("UNDO", StringComparison.OrdinalIgnoreCase))
            return Undo(document);
        if (tokens[0].Equals("REDO", StringComparison.OrdinalIgnoreCase))
            return Redo(document);

        var databaseRevisionBefore = document.Database.Revision;
        var semanticRevisionBefore = Projects.Revision(document);
        CommandResult result;
        try
        {
            result = _commands.Execute(tokens[0], new CommandContext(document, tokens.Skip(1), cancellationToken));
        }
        catch
        {
            RecordMutation(document, databaseRevisionBefore, semanticRevisionBefore);
            throw;
        }

        RecordMutation(document, databaseRevisionBefore, semanticRevisionBefore);
        if (!string.IsNullOrWhiteSpace(result.Message)) document.Editor.WriteMessage(result.Message!);
        return result;
    }

    private void OnDocumentOpened(ICadDocument document)
    {
        Projects.Ensure(document);
        EnsureHistory(document.Id);
    }

    private void OnDocumentClosed(DrawingId drawingId)
    {
        Projects.Detach(drawingId);
        _undo.Remove(drawingId);
        _redo.Remove(drawingId);
    }

    private void RecordMutation(ICadDocument document, long databaseRevisionBefore, long semanticRevisionBefore)
    {
        var databaseChanged = document.Database.Revision != databaseRevisionBefore;
        var semanticChanged = Projects.Revision(document) != semanticRevisionBefore;
        if (!databaseChanged && !semanticChanged) return;

        var (undo, redo) = EnsureHistory(document.Id);
        undo.Push(new HistoryEntry(databaseChanged, semanticChanged, document.Database.Revision, Projects.Revision(document)));
        redo.Clear();
    }

    private CommandResult Undo(ICadDocument document)
    {
        var (undo, redo) = EnsureHistory(document.Id);
        if (undo.Count == 0) return CommandResult.Failure("Nothing to undo.");
        var entry = undo.Peek();
        if (IsStale(document, entry))
            return CommandResult.Failure("Undo history is stale because a changed domain was mutated outside the application command journal.");
        if (entry.DatabaseChanged && !document.Database.History.CanUndo)
            return CommandResult.Failure("Drawing history cannot satisfy the requested undo.");
        if (entry.SemanticChanged && !Projects.CanUndo(document))
            return CommandResult.Failure("Semantic history cannot satisfy the requested undo.");

        undo.Pop();
        if (entry.SemanticChanged) Projects.Undo(document);
        if (entry.DatabaseChanged) document.Database.History.Undo();
        redo.Push(new HistoryEntry(entry.DatabaseChanged, entry.SemanticChanged, document.Database.Revision, Projects.Revision(document)));
        var result = CommandResult.Success("Undo complete.");
        document.Editor.WriteMessage(result.Message!);
        return result;
    }

    private CommandResult Redo(ICadDocument document)
    {
        var (undo, redo) = EnsureHistory(document.Id);
        if (redo.Count == 0) return CommandResult.Failure("Nothing to redo.");
        var entry = redo.Peek();
        if (IsStale(document, entry))
            return CommandResult.Failure("Redo history is stale because a changed domain was mutated outside the application command journal.");
        if (entry.DatabaseChanged && !document.Database.History.CanRedo)
            return CommandResult.Failure("Drawing history cannot satisfy the requested redo.");
        if (entry.SemanticChanged && !Projects.CanRedo(document))
            return CommandResult.Failure("Semantic history cannot satisfy the requested redo.");

        redo.Pop();
        if (entry.DatabaseChanged) document.Database.History.Redo();
        if (entry.SemanticChanged) Projects.Redo(document);
        undo.Push(new HistoryEntry(entry.DatabaseChanged, entry.SemanticChanged, document.Database.Revision, Projects.Revision(document)));
        var result = CommandResult.Success("Redo complete.");
        document.Editor.WriteMessage(result.Message!);
        return result;
    }

    private bool IsStale(ICadDocument document, HistoryEntry entry)
    {
        return (entry.DatabaseChanged && document.Database.Revision != entry.DatabaseRevision)
            || (entry.SemanticChanged && Projects.Revision(document) != entry.SemanticRevision);
    }

    private (Stack<HistoryEntry> Undo, Stack<HistoryEntry> Redo) EnsureHistory(DrawingId drawingId)
    {
        if (!_undo.TryGetValue(drawingId, out var undo))
        {
            undo = new Stack<HistoryEntry>();
            _undo.Add(drawingId, undo);
        }
        if (!_redo.TryGetValue(drawingId, out var redo))
        {
            redo = new Stack<HistoryEntry>();
            _redo.Add(drawingId, redo);
        }
        return (undo, redo);
    }

    private sealed record HistoryEntry(bool DatabaseChanged, bool SemanticChanged, long DatabaseRevision, long SemanticRevision);
}
