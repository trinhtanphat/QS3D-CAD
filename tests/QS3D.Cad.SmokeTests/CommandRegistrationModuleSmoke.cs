using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;

namespace QS3D.Cad.SmokeTests;

internal static class CommandRegistrationModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        foreach (var name in new[] { "QSTAG", "QSFLOOR", "QSZONE", "QSPROP", "QSLOC", "QSQTY", "QSSCHEDULE" })
            if (!app.Commands.TryResolve(name, out _)) throw new InvalidOperationException(name);

        foreach (var journalCommand in new[] { "UNDO", "REDO" })
        {
            if (app.Commands.TryResolve(journalCommand, out _))
                throw new InvalidOperationException($"{journalCommand} must be owned by StandaloneCadApplication journal, not the public command registry.");
            Throws<InvalidOperationException>(() => app.Commands.Register(new ReservedCommand(journalCommand)));
        }

        var publicExecute = typeof(StandaloneCommandCatalog)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(static method => StringComparer.Ordinal.Equals(method.Name, "Execute"));
        if (publicExecute is not null)
            throw new InvalidOperationException("StandaloneCommandCatalog must not expose raw command execution outside the application journal.");
    }

    private sealed class ReservedCommand : ICadCommand
    {
        public ReservedCommand(string name) => Name = name;
        public string Name { get; }
        public CommandFlags Flags => CommandFlags.None;
        public CommandResult Execute(CommandContext context) => CommandResult.Success();
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
