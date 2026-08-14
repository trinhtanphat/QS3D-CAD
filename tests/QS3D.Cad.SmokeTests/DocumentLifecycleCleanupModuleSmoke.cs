using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class DocumentLifecycleCleanupModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("Lifecycle");
        Success(app.Execute("LINE 0 0 1 0"));
        Success(app.Execute("QSTAG 1 Wall LifecycleWall"));
        Equal(1, app.Projects.Get(document).Elements.Count);

        if (!app.Documents.Close(document.Id)) throw new InvalidOperationException("Direct manager close failed.");
        if (app.Documents.Close(document.Id)) throw new InvalidOperationException("Duplicate direct manager close succeeded.");
        Throws<InvalidOperationException>(() => app.Projects.Get(document));

        var reopened = new InMemoryCadDocument(document.Id, "Lifecycle reopened", new InMemoryCadDatabase());
        app.Documents.Open(reopened);
        Equal(0, app.Projects.Get(reopened).Elements.Count);
        Require(!app.Execute("UNDO").Succeeded, "reopened drawing reused stale application journal");

        Success(app.Execute("LINE 0 0 2 0"));
        Success(app.Execute("UNDO"));
        Equal(0, EntityCount(reopened));

        if (!app.CloseDocument(reopened.Id)) throw new InvalidOperationException("Application close failed.");
        Throws<InvalidOperationException>(() => app.Projects.Get(reopened));
        Console.WriteLine("PASS standalone document lifecycle owns semantic and journal cleanup");
    }

    private static int EntityCount(InMemoryCadDocument document)
    {
        using var transaction = document.Database.BeginTransaction(QS3D.Platform.Cad.Abstractions.CadTransactionMode.ReadOnly);
        return transaction.Query().Count;
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
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
