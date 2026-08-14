using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class DerivedCoordinateOverflowModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("Overflow guards");

        var circle = app.Execute("CIRCLE 1e308 0 1e308");
        Require(!circle.Succeeded, "circle whose derived extents overflow must fail as a command result");
        Equal(0, EntityCount(document.Database));

        Success(app.Execute("LINE 1e308 0 1e308 1"));
        var revision = document.Database.Revision;
        var move = app.Execute("MOVE 1 1e308 0");
        Require(!move.Succeeded, "move whose derived coordinates overflow must fail as a command result");
        Equal(revision, document.Database.Revision);
        Equal(1, EntityCount(document.Database));

        using (var read = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
        {
            var entity = read.Get(new QS3D.Platform.Domain.CadHandle("1")) ?? throw new InvalidOperationException("line disappeared after failed move");
            Equal(1e308, entity.Extents.Min.X);
            Equal(1e308, entity.Extents.Max.X);
        }

        Success(app.Execute("UNDO"));
        Equal(0, EntityCount(document.Database));
        Console.WriteLine("PASS derived coordinate overflow fails without mutation");
    }

    private static int EntityCount(ICadDatabase database)
    {
        using var transaction = database.BeginTransaction(CadTransactionMode.ReadOnly);
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
}
