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
    }
}
