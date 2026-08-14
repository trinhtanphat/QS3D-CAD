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
            if (!app.Commands.Contains(name)) throw new InvalidOperationException(name);

        foreach (var journalCommand in new[] { "UNDO", "REDO" })
        {
            if (app.Commands.Contains(journalCommand))
                throw new InvalidOperationException($"{journalCommand} must be owned by StandaloneCadApplication journal, not the public command catalog.");
            Throws<InvalidOperationException>(() => app.Commands.Register(new TestCommand(journalCommand)));
        }

        Throws<ArgumentException>(() => app.Commands.Register(new TestCommand("")));
        Throws<ArgumentException>(() => app.Commands.Register(new TestCommand("   ")));
        Throws<ArgumentException>(() => app.Commands.Register(new TestCommand(null)));

        var forbidden = typeof(StandaloneCommandCatalog)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .Where(static method => method.Name is "Execute" or "TryResolve")
            .Select(static method => method.Name)
            .ToArray();
        if (forbidden.Length != 0)
            throw new InvalidOperationException("StandaloneCommandCatalog must not expose raw command resolution/execution outside the application journal: " + string.Join(", ", forbidden));
    }

    private sealed class TestCommand : ICadCommand
    {
        public TestCommand(string? name) => Name = name!;
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
