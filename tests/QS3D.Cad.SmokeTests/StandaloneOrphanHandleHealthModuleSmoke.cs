using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class StandaloneOrphanHandleHealthModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = (InMemoryCadDocument)app.NewDocument("Orphan health");

        Success(app.Execute("LINE 0 0 10 0"));
        Success(app.Execute("QSTAG 1 Wall TaggedWall"));
        Success(app.Execute("QSHEALTH"));

        Success(app.Execute("ERASE 1"));
        var blocked = app.Execute("QSHEALTH");
        Require(!blocked.Succeeded, "health must block when a tagged source handle is erased");
        Require(document.Editor.Messages.Any(static message => message.Contains("ORPHAN_HANDLE", StringComparison.Ordinal)),
            "health output must identify the missing live CAD reference as ORPHAN_HANDLE");

        Success(app.Execute("UNDO"));
        Success(app.Execute("QSHEALTH"));

        Console.WriteLine("PASS standalone orphan-handle health and undo recovery");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
