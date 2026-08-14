using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.SmokeTests;

internal static class CommandJournalFailureModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("Journal failure");
        app.Commands.Register(new MutateThenFailCommand());
        app.Commands.Register(new MutateThenThrowCommand());

        var failed = app.Execute("MUTATEFAIL");
        Require(!failed.Succeeded, "mutate-then-fail command must report failure");
        Equal(1, EntityCount(document.Database));
        Success(app.Execute("UNDO"));
        Equal(0, EntityCount(document.Database));

        Throws<InvalidOperationException>(() => app.Execute("MUTATETHROW"));
        Equal(1, EntityCount(document.Database));
        Success(app.Execute("UNDO"));
        Equal(0, EntityCount(document.Database));

        Console.WriteLine("PASS command journal captures committed mutations across failure and exception paths");
    }

    private static int EntityCount(ICadDatabase database)
    {
        using var transaction = database.BeginTransaction(CadTransactionMode.ReadOnly);
        return transaction.Query().Count;
    }

    private sealed class MutateThenFailCommand : ICadCommand
    {
        public string Name => "MUTATEFAIL";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            using var transaction = context.Document.Database.BeginTransaction();
            transaction.Append(new CadEntityDraft(CadEntityKind.Point, new BoundingBox3(new Point3(1, 1), new Point3(1, 1))));
            transaction.Commit();
            return CommandResult.Failure("intentional failure after commit");
        }
    }

    private sealed class MutateThenThrowCommand : ICadCommand
    {
        public string Name => "MUTATETHROW";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            using var transaction = context.Document.Database.BeginTransaction();
            transaction.Append(new CadEntityDraft(CadEntityKind.Point, new BoundingBox3(new Point3(2, 2), new Point3(2, 2))));
            transaction.Commit();
            throw new InvalidOperationException("intentional exception after commit");
        }
    }

    private static void Success(CommandResult result)
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
