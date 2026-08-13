using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

var tests = new (string Name, Action Run)[]
{
    ("tokenizer", Tokenizer),
    ("drawing commands and history", DrawingCommandsAndHistory),
    ("failed erase rolls back", FailedEraseRollsBack),
    ("semantic workspace commands", SemanticWorkspaceCommands),
    ("global undo orders CAD and semantic changes", GlobalUndoOrdersCadAndSemanticChanges),
    ("external mutation invalidates app undo", ExternalMutationInvalidatesAppUndo),
    ("bootstrap save load CAD and semantic round trip", BootstrapSaveLoadRoundTrip)
};

foreach (var test in tests)
{
    test.Run();
    Console.WriteLine($"PASS {test.Name}");
}

static void Tokenizer()
{
    var tokens = CommandLineTokenizer.Tokenize("LINE 0 0 \"10\" 20");
    Equal(5, tokens.Count);
    Equal("10", tokens[3]);
    Throws<FormatException>(() => CommandLineTokenizer.Tokenize("LINE \"0"));
}

static void DrawingCommandsAndHistory()
{
    var app = new StandaloneCadApplication();
    var document = app.NewDocument("Smoke");
    Success(app.Execute("LINE 0 0 10 0"));
    Success(app.Execute("CIRCLE 5 5 2"));
    Success(app.Execute("RECTANG -1 -1 1 1"));
    Equal(3, Count(document.Database));

    Success(app.Execute("MOVE 1 5 2"));
    using (var read = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
    {
        var line = read.Get(new CadHandle("1")) ?? throw new InvalidOperationException("LINE handle missing.");
        Equal(5d, line.Extents.Min.X);
        Equal(2d, line.Extents.Min.Y);
    }

    Success(app.Execute("UNDO"));
    using (var read = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
    {
        var line = read.Get(new CadHandle("1")) ?? throw new InvalidOperationException("LINE handle missing after undo.");
        Equal(0d, line.Extents.Min.X);
    }
    Success(app.Execute("REDO"));
    Success(app.Execute("SELECT 1 2"));
    Equal(2, document.Editor.Selection.Current.Count);
}

static void FailedEraseRollsBack()
{
    var app = new StandaloneCadApplication();
    var document = app.NewDocument("Rollback");
    Success(app.Execute("LINE 0 0 1 1"));
    var result = app.Execute("ERASE 1 FFFF");
    Require(!result.Succeeded, "erase should fail when any requested handle is absent");
    Equal(1, Count(document.Database));
}

static void SemanticWorkspaceCommands()
{
    var app = new StandaloneCadApplication();
    var document = app.NewDocument("Semantic");
    Success(app.Execute("LINE 0 0 4 0"));
    Success(app.Execute("QSTAG 1 Wall \"Exterior Wall\""));
    var project = app.Projects.Get(document);
    Equal(1, project.Elements.Count);
    var element = project.Elements.Single();
    Equal(SemanticElementKind.Wall, element.Kind);
    Equal("Exterior Wall", element.Name);
    Require(element.SourceReference.HasValue && element.SourceReference.Value.Handle == new CadHandle("1"), "source handle must be retained");
    Require(!app.Execute("QSTAG 1 Wall duplicate").Succeeded, "one source CAD entity must not be tagged twice");
    Success(app.Execute("QSCOUNT Wall"));
    Success(app.Execute("QSHEALTH"));
}

static void GlobalUndoOrdersCadAndSemanticChanges()
{
    var app = new StandaloneCadApplication();
    var document = app.NewDocument("History");
    Success(app.Execute("LINE 0 0 4 0"));
    Success(app.Execute("QSTAG 1 Wall W1"));
    Equal(1, Count(document.Database));
    Equal(1, app.Projects.Get(document).Elements.Count);

    Success(app.Execute("UNDO"));
    Equal(1, Count(document.Database));
    Equal(0, app.Projects.Get(document).Elements.Count);

    Success(app.Execute("UNDO"));
    Equal(0, Count(document.Database));
    Success(app.Execute("REDO"));
    Equal(1, Count(document.Database));
    Success(app.Execute("REDO"));
    Equal(1, app.Projects.Get(document).Elements.Count);
}

static void ExternalMutationInvalidatesAppUndo()
{
    var app = new StandaloneCadApplication();
    var document = app.NewDocument("StaleHistory");
    Success(app.Execute("LINE 0 0 1 0"));
    using (var external = document.Database.BeginTransaction())
    {
        external.Append(new CadEntityDraft(CadEntityKind.Point, new QS3D.Platform.Geometry.BoundingBox3(new QS3D.Platform.Geometry.Point3(2, 2), new QS3D.Platform.Geometry.Point3(2, 2))));
        external.Commit();
    }
    var result = app.Execute("UNDO");
    Require(!result.Succeeded && result.Message!.Contains("stale", StringComparison.OrdinalIgnoreCase), "external mutation must fail closed instead of undoing the wrong command");
    Equal(2, Count(document.Database));
}

static void BootstrapSaveLoadRoundTrip()
{
    var app = new StandaloneCadApplication();
    var source = app.NewDocument("Persistence");
    Success(app.Execute("LINE 1 2 3 4"));
    Success(app.Execute("CIRCLE 5 6 7"));
    Success(app.Execute("QSTAG 1 Wall \"Persisted Wall\""));
    var sourceProjectId = app.Projects.Get(source).Id;
    var path = Path.Combine(Path.GetTempPath(), $"qs3d-{Guid.NewGuid():N}.qs3d-bootstrap.json");
    try
    {
        app.SaveBootstrap(path);
        var second = new StandaloneCadApplication();
        var loaded = second.OpenBootstrap(path);
        Equal(source.Id, loaded.Id);
        Equal(2, Count(loaded.Database));
        using (var read = loaded.Database.BeginTransaction(CadTransactionMode.ReadOnly))
            Equal(CadEntityKind.Line, read.Get(new CadHandle("1"))!.Kind);
        var loadedProject = second.Projects.Get(loaded);
        Equal(sourceProjectId, loadedProject.Id);
        Equal(1, loadedProject.Elements.Count);
        var loadedElement = loadedProject.Elements.Single();
        Equal("Persisted Wall", loadedElement.Name);
        Require(loadedElement.SourceReference.HasValue && loadedElement.SourceReference.Value.Handle == new CadHandle("1"), "semantic source handle must survive bootstrap round trip");
        Success(second.Execute("QSHEALTH"));
    }
    finally
    {
        if (File.Exists(path)) File.Delete(path);
    }
}

static int Count(ICadDatabase database)
{
    using var read = database.BeginTransaction(CadTransactionMode.ReadOnly);
    return read.Query().Count;
}

static void Success(QS3D.Platform.Application.CommandResult result)
{
    if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
}

static void Require(bool condition, string message)
{
    if (!condition) throw new InvalidOperationException(message);
}

static void Equal<T>(T expected, T actual) where T : notnull
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
        throw new InvalidOperationException($"Expected {expected} but got {actual}.");
}

static void Throws<T>(Action action) where T : Exception
{
    try { action(); }
    catch (T) { return; }
    throw new InvalidOperationException($"Expected {typeof(T).Name}.");
}
