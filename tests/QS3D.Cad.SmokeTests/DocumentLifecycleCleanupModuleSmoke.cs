using System.Runtime.CompilerServices;
using QS3D.Cad.Host;

namespace QS3D.Cad.SmokeTests;

internal static class DocumentLifecycleCleanupModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var doc = app.NewDocument("Lifecycle");
        if (!app.CloseDocument(doc.Id)) throw new InvalidOperationException("Close failed.");
        if (app.CloseDocument(doc.Id)) throw new InvalidOperationException("Duplicate close succeeded.");
        try { _ = app.Projects.Get(doc); }
        catch (InvalidOperationException) { return; }
        throw new InvalidOperationException("Closed document retained semantic state.");
    }
}
