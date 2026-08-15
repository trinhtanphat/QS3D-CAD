using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class XrefManagementModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "qs3d-xref-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            LifecycleAndDiagnostics(root);
            DocumentIsolation(root);
        }
        finally
        {
            try { Directory.Delete(root, recursive: true); } catch { }
        }
    }

    private static void LifecycleAndDiagnostics(string root)
    {
        var existingPath = Path.Combine(root, "existing.dwg");
        var missingPath = Path.Combine(root, "missing.dwg");
        File.WriteAllText(existingPath, "reference-fixture");

        var app = new StandaloneCadApplication();
        var document = app.NewDocument("xref-management");
        foreach (var name in new[] { "XREFLIST", "XREFSTATUS", "XREFATTACH", "XREFRELOAD", "XREFUNLOAD", "XREFDETACH", "XREFHEALTH", "XREFRELOADALL", "XREFREF" })
            Require(app.Commands.Contains(name), $"{name} must be registered");

        var databaseRevision = document.Database.Revision;
        Succeeds(app.ExecuteCommand("XREFATTACH", new[] { "LoadedRef", existingPath, "Overlay" }));
        Succeeds(app.ExecuteCommand("XREFATTACH", new[] { "MissingRef", missingPath }));
        Equal(databaseRevision, document.Database.Revision, "reference-service lifecycle must not mutate drawing database revision");

        var service = Service(document);
        var loaded = Get(service, "LoadedRef");
        Equal(CadXrefKind.Overlay, loaded.Kind, "explicit overlay kind");
        Equal(CadXrefStatus.Loaded, loaded.Status, "existing path status");
        Equal(CadXrefStatus.Missing, Get(service, "MissingRef").Status, "missing path status");

        Succeeds(app.Execute("XREFLIST"));
        Succeeds(app.Execute("XREFSTATUS LoadedRef"));
        var health = app.Execute("XREFHEALTH");
        Succeeds(health);
        Require(health.Message?.Contains("1 problematic reference", StringComparison.Ordinal) == true, "health must report missing reference");

        Succeeds(app.Execute("XREFUNLOAD LoadedRef"));
        Equal(CadXrefStatus.Unloaded, Get(service, "LoadedRef").Status, "unload status");
        Succeeds(app.Execute("XREFRELOAD LoadedRef"));
        Equal(CadXrefStatus.Loaded, Get(service, "LoadedRef").Status, "reload existing status");

        Succeeds(app.Execute("XREFRELOADALL"));
        Equal(CadXrefStatus.Loaded, Get(service, "LoadedRef").Status, "reload-all existing status");
        Equal(CadXrefStatus.Missing, Get(service, "MissingRef").Status, "reload-all missing status");

        var countBeforeFailures = service.GetXrefs().Count;
        Fails(app.ExecuteCommand("XREFATTACH", new[] { "LoadedRef", existingPath }));
        Fails(app.Execute("XREFSTATUS UnknownRef"));
        Fails(app.Execute("XREFDETACH UnknownRef"));
        Fails(app.Execute("XREFATTACH BadKind nowhere.dwg NotAKind"));
        Equal(countBeforeFailures, service.GetXrefs().Count, "failed xref commands must preserve service membership");

        Succeeds(app.Execute("XREFDETACH MissingRef"));
        Equal(1, service.GetXrefs().Count, "detach must remove one reference");

        Succeeds(app.ExecuteCommand("XREFREF", new[] { "ATTACH", "LegacyRef", existingPath, "Attach" }));
        Equal(CadXrefStatus.Loaded, Get(service, "LegacyRef").Status, "legacy attach compatibility");
        Succeeds(app.Execute("XREFREF UNLOAD LegacyRef"));
        Equal(CadXrefStatus.Unloaded, Get(service, "LegacyRef").Status, "legacy unload compatibility");
        Succeeds(app.Execute("XREFREF RELOAD LegacyRef"));
        Equal(CadXrefStatus.Loaded, Get(service, "LegacyRef").Status, "legacy reload compatibility");
        Succeeds(app.Execute("XREFREF DETACH LegacyRef"));
        Require(service.GetXrefs().All(static item => !item.Name.Equals("LegacyRef", StringComparison.OrdinalIgnoreCase)), "legacy detach compatibility");
    }

    private static void DocumentIsolation(string root)
    {
        var path = Path.Combine(root, "isolated.dwg");
        File.WriteAllText(path, "isolated-fixture");

        var app = new StandaloneCadApplication();
        var first = app.NewDocument("xref-first");
        Succeeds(app.ExecuteCommand("XREFATTACH", new[] { "OnlyFirst", path }));
        Equal(1, Service(first).GetXrefs().Count, "first document xref count");

        var second = app.NewDocument("xref-second");
        Equal(0, Service(second).GetXrefs().Count, "second document must start with isolated xref service");
        Succeeds(app.Execute("XREFHEALTH"));
        Equal(0, Service(second).GetXrefs().Count, "read-only health must preserve second document service");

        app.Documents.Activate(first.Id);
        Equal(1, Service(first).GetXrefs().Count, "reactivating first document must preserve its xrefs");
        Equal("OnlyFirst", Service(first).GetXrefs().Single().Name, "first document xref identity");
    }

    private static InMemoryXrefService Service(ICadDocument document)
        => InMemoryAdvancedServicesRegistry.For((InMemoryCadDocument)document).Xrefs;

    private static CadXrefSnapshot Get(InMemoryXrefService service, string name)
        => service.GetXrefs().Single(item => item.Name.Equals(name, StringComparison.OrdinalIgnoreCase));

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command unexpectedly failed.");
    }

    private static void Fails(QS3D.Platform.Application.CommandResult result)
    {
        if (result.Succeeded) throw new InvalidOperationException("Command unexpectedly succeeded.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual, string label) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected} but got {actual}.");
    }
}
