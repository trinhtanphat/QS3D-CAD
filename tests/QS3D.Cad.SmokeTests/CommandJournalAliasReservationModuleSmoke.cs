using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CommandJournalAliasReservationModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        app.NewDocument("JournalAliasReservation");

        Throws<InvalidOperationException>(() => app.Commands.Register(new ProbeCommand("UNDO")));
        Throws<InvalidOperationException>(() => app.Commands.Register(new ProbeCommand("redo")));
        Throws<InvalidOperationException>(() => app.Commands.Register(new ProbeCommand("u")));
        Throws<InvalidOperationException>(() => app.Commands.Register(new ProbeCommand("RE")));

        var probe = new ProbeCommand("EXTENSIONPROBE");
        app.Commands.Register(probe);
        Success(app.Execute("EXTENSIONPROBE"));
        Require(probe.ExecutionCount == 1, "ordinary extension command must remain registerable and executable");

        Success(app.Execute("LINE 0 0 10 0"));
        Require(EntityCount(app) == 1, "LINE fixture must create one entity");
        Success(app.Execute("U"));
        Require(EntityCount(app) == 0, "U must remain owned by the application undo journal");
        Success(app.Execute("RE"));
        Require(EntityCount(app) == 1, "RE must remain owned by the application redo journal");

        Console.WriteLine("PASS application journal aliases cannot be shadowed by extension commands");
    }

    private static int EntityCount(StandaloneCadApplication app)
    {
        var document = app.Documents.ActiveDocument ?? throw new InvalidOperationException("No active document.");
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().Count;
    }

    private static void Success(CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private sealed class ProbeCommand : ICadCommand
    {
        public ProbeCommand(string name) => Name = name;
        public string Name { get; }
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public int ExecutionCount { get; private set; }

        public CommandResult Execute(CommandContext context)
        {
            ExecutionCount++;
            return CommandResult.Success("probe executed");
        }
    }
}
