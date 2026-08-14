using System.Runtime.CompilerServices;
using QS3D.Cad.Host;

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
        }

        var publicExecute = typeof(StandaloneCommandCatalog)
            .GetMethods(System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public)
            .FirstOrDefault(static method => StringComparer.Ordinal.Equals(method.Name, "Execute"));
        if (publicExecute is not null)
            throw new InvalidOperationException("StandaloneCommandCatalog must not expose raw command execution outside the application journal.");
    }
}
