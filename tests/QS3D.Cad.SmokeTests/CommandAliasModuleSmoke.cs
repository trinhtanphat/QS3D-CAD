using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CommandAliasModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("alias-smoke");

        Succeeds(app.Execute("l 0 0 10 0"));
        Succeeds(app.Execute("c 20 20 5"));
        Succeeds(app.Execute("rec -2 -2 2 2"));
        var entities = Query(document);
        if (entities.Count != 3)
            throw new InvalidOperationException("Creation aliases did not create the expected three entities.");

        var line = entities.Single(static entity => entity.Kind == CadEntityKind.Line);
        var beforeMove = line;
        Succeeds(app.Execute($"m {line.Handle} 5 0"));
        var moved = Query(document).Single(entity => entity.Handle == line.Handle);
        if (moved.Extents.Min.X != beforeMove.Extents.Min.X + 5d)
            throw new InvalidOperationException("MOVE alias did not dispatch to MOVE.");

        Succeeds(app.Execute("u"));
        var undone = Query(document).Single(entity => entity.Handle == line.Handle);
        if (undone.Extents.Min.X != beforeMove.Extents.Min.X)
            throw new InvalidOperationException("U alias did not dispatch through application UNDO.");

        Succeeds(app.Execute("re"));
        var redone = Query(document).Single(entity => entity.Handle == line.Handle);
        if (redone.Extents.Min.X != moved.Extents.Min.X)
            throw new InvalidOperationException("RE alias did not dispatch through application REDO.");

        var distance = app.Execute("di 0 0 3 4");
        Succeeds(distance);
        if (distance.Message is null || !distance.Message.Contains("Distance=5", StringComparison.Ordinal))
            throw new InvalidOperationException("DI alias did not dispatch to DIST.");

        var custom = new StandaloneCadApplication();
        custom.NewDocument("alias-override-smoke");
        custom.Commands.Register(new CustomLCommand());
        var customResult = custom.Execute("L");
        Succeeds(customResult);
        if (!StringComparer.Ordinal.Equals(customResult.Message, "custom-L"))
            throw new InvalidOperationException("An explicitly registered exact command must take precedence over a fallback alias.");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private sealed class CustomLCommand : ICadCommand
    {
        public string Name => "L";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context) => CommandResult.Success("custom-L");
    }

    private static void Succeeds(CommandResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }
}
