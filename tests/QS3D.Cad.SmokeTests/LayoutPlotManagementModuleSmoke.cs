using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class LayoutPlotManagementModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        CommandMetadata();
        LifecycleAndDiagnostics();
        DocumentIsolation();
    }

    private static void CommandMetadata()
    {
        var registry = new CommandRegistry();
        LayoutReferenceCommands.RegisterAll(registry);
        PlotReferenceCommands.RegisterAll(registry);

        Equal(CommandFlags.RequiresDocument, Flags(registry, "LAYOUTREF"), "legacy layout flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ReadOnly, Flags(registry, "LAYOUTLIST"), "layout list flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ReadOnly, Flags(registry, "LAYOUTCURRENT"), "layout current flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing, Flags(registry, "LAYOUTCREATE"), "layout create flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing, Flags(registry, "LAYOUTSET"), "layout set flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing, Flags(registry, "LAYOUTDELETE"), "layout delete flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ReadOnly, Flags(registry, "LAYOUTHEALTH"), "layout health flags");

        Equal(CommandFlags.RequiresDocument, Flags(registry, "PLOTREF"), "legacy plot flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing, Flags(registry, "PLOTREQUEST"), "plot request flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ReadOnly, Flags(registry, "PLOTLIST"), "plot list flags");
        Equal(CommandFlags.RequiresDocument | CommandFlags.ReadOnly, Flags(registry, "PLOTHEALTH"), "plot health flags");
    }

    private static void LifecycleAndDiagnostics()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("layout-plot-management");
        foreach (var name in new[]
                 {
                     "LAYOUTREF", "LAYOUTLIST", "LAYOUTCURRENT", "LAYOUTCREATE", "LAYOUTSET", "LAYOUTDELETE", "LAYOUTHEALTH",
                     "PLOTREF", "PLOTREQUEST", "PLOTLIST", "PLOTHEALTH"
                 })
            Require(app.Commands.Contains(name), $"{name} must be registered");

        var services = Services(document);
        var databaseRevision = document.Database.Revision;
        Equal("Model", services.Layouts.CurrentLayoutName, "initial layout");
        Equal(1, services.Layouts.GetLayouts().Count, "initial layout count");
        Succeeds(app.Execute("LAYOUTLIST"));
        Succeeds(app.Execute("LAYOUTCURRENT"));
        Succeeds(app.Execute("LAYOUTHEALTH"));

        Succeeds(app.ExecuteCommand("LAYOUTCREATE", new[] { "Sheet A" }));
        var sheet = services.Layouts.GetLayouts().Single(item => item.Name == "Sheet A");
        Require(!sheet.IsModel, "created layout must be a paper layout");
        Equal(210d, sheet.PaperWidthMm, "default paper width");
        Equal(297d, sheet.PaperHeightMm, "default paper height");
        Fails(app.ExecuteCommand("LAYOUTCREATE", new[] { "sheet a" }));

        Succeeds(app.ExecuteCommand("LAYOUTSET", new[] { "Sheet A" }));
        Equal("Sheet A", services.Layouts.CurrentLayoutName, "current layout after set");
        Fails(app.ExecuteCommand("LAYOUTDELETE", new[] { "Sheet A" }));
        Fails(app.Execute("LAYOUTDELETE Model"));
        Fails(app.Execute("LAYOUTSET Missing"));

        Succeeds(app.ExecuteCommand("PLOTREQUEST", new[] { "Sheet A", "sheet-a.pdf" }));
        Succeeds(app.ExecuteCommand("PLOTREQUEST", new[] { "Sheet A", "OfficePrinter", "Printer" }));
        Equal(2, services.Plot.Requests.Count, "recorded plot request count");
        Equal(CadPlotTargetKind.Pdf, services.Plot.Requests[0].TargetKind, "default plot kind");
        Equal(CadPlotTargetKind.Printer, services.Plot.Requests[1].TargetKind, "explicit printer kind");
        Succeeds(app.Execute("PLOTLIST"));
        var healthy = app.Execute("PLOTHEALTH");
        Succeeds(healthy);
        Require(healthy.Message?.Contains("orphanedLayoutRequests=0", StringComparison.Ordinal) == true, "healthy plot queue must not report orphaned layouts");

        var countBeforeFailures = services.Plot.Requests.Count;
        Fails(app.ExecuteCommand("PLOTREQUEST", new[] { "Missing", "missing.pdf" }));
        Fails(app.ExecuteCommand("PLOTREQUEST", new[] { "Sheet A", "target", "NotAKind" }));
        Equal(countBeforeFailures, services.Plot.Requests.Count, "failed plot commands must not record requests");

        Succeeds(app.Execute("LAYOUTSET Model"));
        Succeeds(app.ExecuteCommand("LAYOUTDELETE", new[] { "Sheet A" }));
        var orphaned = app.Execute("PLOTHEALTH");
        Succeeds(orphaned);
        Require(orphaned.Message?.Contains("orphanedLayoutRequests=2", StringComparison.Ordinal) == true, "deleted layout must surface orphaned recorded requests");

        Succeeds(app.ExecuteCommand("LAYOUTREF", new[] { "CREATE", "LegacySheet" }));
        Succeeds(app.ExecuteCommand("LAYOUTREF", new[] { "SET", "LegacySheet" }));
        Equal("LegacySheet", services.Layouts.CurrentLayoutName, "legacy layout set compatibility");
        Succeeds(app.Execute("LAYOUTREF LIST"));
        Succeeds(app.Execute("LAYOUTREF SET Model"));
        Succeeds(app.ExecuteCommand("LAYOUTREF", new[] { "DELETE", "LegacySheet" }));
        Require(services.Layouts.GetLayouts().All(static item => !item.Name.Equals("LegacySheet", StringComparison.OrdinalIgnoreCase)), "legacy layout delete compatibility");

        var beforeLegacyPlot = services.Plot.Requests.Count;
        Succeeds(app.Execute("PLOTREF Model legacy.pdf"));
        Equal(beforeLegacyPlot + 1, services.Plot.Requests.Count, "legacy plot compatibility");
        Equal(databaseRevision, document.Database.Revision, "reference layout and plot service state must not mutate drawing database revision");
    }

    private static void DocumentIsolation()
    {
        var app = new StandaloneCadApplication();
        var first = app.NewDocument("layout-first");
        Succeeds(app.ExecuteCommand("LAYOUTCREATE", new[] { "FirstOnly" }));
        Succeeds(app.ExecuteCommand("PLOTREQUEST", new[] { "FirstOnly", "first.pdf" }));
        Equal(2, Services(first).Layouts.GetLayouts().Count, "first document layout count");
        Equal(1, Services(first).Plot.Requests.Count, "first document plot count");

        var second = app.NewDocument("layout-second");
        Equal(1, Services(second).Layouts.GetLayouts().Count, "second document must start with Model only");
        Equal("Model", Services(second).Layouts.CurrentLayoutName, "second document current layout");
        Equal(0, Services(second).Plot.Requests.Count, "second document plot queue must be isolated");
        Succeeds(app.Execute("LAYOUTHEALTH"));
        Succeeds(app.Execute("PLOTHEALTH"));

        app.Documents.Activate(first.Id);
        Equal(2, Services(first).Layouts.GetLayouts().Count, "reactivating first document must preserve layouts");
        Equal(1, Services(first).Plot.Requests.Count, "reactivating first document must preserve plot requests");
    }

    private static InMemoryAdvancedServices Services(ICadDocument document)
        => InMemoryAdvancedServicesRegistry.For((InMemoryCadDocument)document);

    private static CommandFlags Flags(CommandRegistry registry, string name)
    {
        Require(registry.TryResolve(name, out var command) && command is not null, $"{name} must resolve");
        return command!.Flags;
    }

    private static void Succeeds(CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command unexpectedly failed.");
    }

    private static void Fails(CommandResult result)
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
