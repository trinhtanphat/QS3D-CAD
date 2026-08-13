using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

var tests = new (string Name, Action Run)[]
{
    ("tokenizer", Tokenizer),
    ("drawing commands and history", DrawingCommandsAndHistory),
    ("failed erase rolls back", FailedEraseRollsBack),
    ("bootstrap save load round trip", BootstrapSaveLoadRoundTrip)
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

static void BootstrapSaveLoadRoundTrip()
{
    var app = new StandaloneCadApplication();
    var source = app.NewDocument("Persistence");
    Success(app.Execute("LINE 1 2 3 4"));
    Success(app.Execute("CIRCLE 5 6 7"));
    var path = Path.Combine(Path.GetTempPath(), $"qs3d-{Guid.NewGuid():N}.qs3d-bootstrap.json");
    try
    {
        app.SaveBootstrap(path);
        var second = new StandaloneCadApplication();
        var loaded = second.OpenBootstrap(path);
        Equal(source.Id, loaded.Id);
        Equal(2, Count(loaded.Database));
        using var read = loaded.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        Equal(CadEntityKind.Line, read.Get(new CadHandle("1"))!.Kind);
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
