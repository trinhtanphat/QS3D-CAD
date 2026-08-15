using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class ViewportNavigationModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        CommandMetadata();
        NavigationAndLegacyZoom();
        ValidationSafety();
        DocumentIsolation();
    }

    private static void CommandMetadata()
    {
        var registry = new CommandRegistry();
        AdvancedReferenceCommands.RegisterAll(registry);

        foreach (var name in new[] { "VIEW", "ZOOMEXTENTS", "ZOOMWINDOW", "VIEWSTATUS", "VIEWSET", "VIEWCENTER", "VIEWPAN", "VIEWZOOM", "VIEWRESET", "VIEWHEALTH" })
            Require(registry.TryResolve(name, out _), $"{name} must be registered");

        HasFlag(Resolve(registry, "VIEW"), CommandFlags.ReadOnly, true);
        HasFlag(Resolve(registry, "VIEWSTATUS"), CommandFlags.ReadOnly, true);
        HasFlag(Resolve(registry, "VIEWHEALTH"), CommandFlags.ReadOnly, true);
        HasFlag(Resolve(registry, "ZOOMEXTENTS"), CommandFlags.ReadOnly, false);
        HasFlag(Resolve(registry, "ZOOMWINDOW"), CommandFlags.ReadOnly, false);
        foreach (var name in new[] { "VIEWSET", "VIEWCENTER", "VIEWPAN", "VIEWZOOM", "VIEWRESET" })
            HasFlag(Resolve(registry, name), CommandFlags.ReadOnly, false);
        foreach (var name in new[] { "ZOOMEXTENTS", "ZOOMWINDOW", "VIEWSTATUS", "VIEWSET", "VIEWCENTER", "VIEWPAN", "VIEWZOOM", "VIEWRESET", "VIEWHEALTH" })
            HasFlag(Resolve(registry, name), CommandFlags.RequiresDocument, true);
    }

    private static void NavigationAndLegacyZoom()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("viewport-navigation");
        var initialRevision = document.Database.Revision;

        Succeeds(app.Execute("VIEWSTATUS"));
        Succeeds(app.Execute("VIEWHEALTH"));
        Equal(initialRevision, document.Database.Revision, "read-only viewport diagnostics revision");

        Succeeds(app.Execute("VIEWSET 10 20 30 0 0 -1 0 1 0 200 100 Perspective"));
        var view = Service(document).CurrentView;
        Equal(10d, view.Target.X, "viewset target x");
        Equal(20d, view.Target.Y, "viewset target y");
        Equal(30d, view.Target.Z, "viewset target z");
        Equal(200d, view.Width, "viewset width");
        Equal(100d, view.Height, "viewset height");
        Equal(CadViewProjection.Perspective, view.Projection, "viewset projection");
        Equal(initialRevision, document.Database.Revision, "viewset must not mutate database revision");

        Succeeds(app.Execute("VIEWCENTER 5 6"));
        view = Service(document).CurrentView;
        Equal(5d, view.Target.X, "viewcenter x");
        Equal(6d, view.Target.Y, "viewcenter y");
        Equal(30d, view.Target.Z, "viewcenter preserves z");

        Succeeds(app.Execute("VIEWPAN 2 -3 4"));
        view = Service(document).CurrentView;
        Equal(7d, view.Target.X, "viewpan x");
        Equal(3d, view.Target.Y, "viewpan y");
        Equal(34d, view.Target.Z, "viewpan z");

        Succeeds(app.Execute("VIEWZOOM 2"));
        view = Service(document).CurrentView;
        Equal(100d, view.Width, "viewzoom width");
        Equal(50d, view.Height, "viewzoom height");
        Equal(initialRevision, document.Database.Revision, "viewport navigation remains non-database state");

        var undo = app.Execute("UNDO");
        Fails(undo);
        Require(undo.Message?.Contains("Nothing to undo", StringComparison.OrdinalIgnoreCase) == true, "viewport-only state must not enter drawing undo journal");

        Succeeds(app.Execute("ZOOMWINDOW 0 0 10 20"));
        view = Service(document).CurrentView;
        Equal(5d, view.Target.X, "zoomwindow target x");
        Equal(10d, view.Target.Y, "zoomwindow target y");
        Equal(0d, view.Target.Z, "zoomwindow target z");
        Equal(10.5d, view.Width, "zoomwindow width");
        Equal(21d, view.Height, "zoomwindow height");
        Equal(initialRevision, document.Database.Revision, "legacy zoomwindow database revision");

        Succeeds(app.Execute("LINE 0 0 20 10"));
        var drawingRevision = document.Database.Revision;
        Succeeds(app.Execute("ZOOMEXTENTS"));
        view = Service(document).CurrentView;
        Equal(10d, view.Target.X, "zoomextents target x");
        Equal(5d, view.Target.Y, "zoomextents target y");
        Equal(21d, view.Width, "zoomextents width");
        Equal(10.5d, view.Height, "zoomextents height");
        Equal(drawingRevision, document.Database.Revision, "legacy zoomextents database revision");

        Succeeds(app.Execute("VIEWRESET"));
        view = Service(document).CurrentView;
        Equal(0d, view.Target.X, "reset target x");
        Equal(0d, view.Target.Y, "reset target y");
        Equal(0d, view.Target.Z, "reset target z");
        Equal(100d, view.Width, "reset width");
        Equal(100d, view.Height, "reset height");
        Equal(CadViewProjection.Orthographic, view.Projection, "reset projection");
    }

    private static void ValidationSafety()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("viewport-validation");
        Succeeds(app.Execute("VIEWSET 1 2 3 0 0 -1 0 1 0 80 40 Orthographic"));
        var before = Service(document).CurrentView;
        var revision = document.Database.Revision;

        Fails(app.Execute("VIEWSET 0 0 0 0 0 0 0 1 0 100 100"));
        Fails(app.Execute("VIEWSET 0 0 0 0 0 -1 0 0 2 100 100"));
        Fails(app.Execute("VIEWSET 0 0 0 0 0 -1 0 1 0 0 100"));
        Fails(app.Execute("VIEWSET 0 0 0 0 0 -1 0 1 0 100 100 FishEye"));
        Fails(app.Execute("VIEWZOOM 0"));
        Fails(app.Execute("VIEWZOOM -2"));
        Fails(app.Execute("VIEWCENTER NaN 0"));
        Fails(app.Execute("VIEWPAN 1e309 0"));

        Equal(before, Service(document).CurrentView, "failed viewport commands must preserve state");
        Equal(revision, document.Database.Revision, "failed viewport commands must preserve database revision");

        Succeeds(app.Execute("VIEWSET 1e308 2 3 0 0 -1 0 1 0 80 40 Orthographic"));
        var huge = Service(document).CurrentView;
        Fails(app.Execute("VIEWPAN 1e308 0"));
        Equal(huge, Service(document).CurrentView, "pan overflow must fail closed instead of storing an infinite target");
        Equal(revision, document.Database.Revision, "pan overflow must preserve database revision");
        Succeeds(app.Execute("VIEWHEALTH"));
    }

    private static void DocumentIsolation()
    {
        var app = new StandaloneCadApplication();
        var first = app.NewDocument("viewport-first");
        Succeeds(app.Execute("VIEWCENTER 11 22 33"));
        var firstView = Service(first).CurrentView;

        var second = app.NewDocument("viewport-second");
        var secondView = Service(second).CurrentView;
        Equal(0d, secondView.Target.X, "second document default target x");
        Equal(0d, secondView.Target.Y, "second document default target y");
        Equal(0d, secondView.Target.Z, "second document default target z");

        app.Documents.Activate(first.Id);
        Equal(firstView, Service(first).CurrentView, "first document viewport must survive reactivation");
        Require(!Equals(firstView, secondView), "documents must not share viewport state");
    }

    private static InMemoryViewportService Service(ICadDocument document)
        => InMemoryAdvancedServicesRegistry.For((InMemoryCadDocument)document).Viewport;

    private static ICadCommand Resolve(CommandRegistry registry, string name)
    {
        Require(registry.TryResolve(name, out var command) && command is not null, $"{name} must resolve");
        return command!;
    }

    private static void HasFlag(ICadCommand command, CommandFlags flag, bool expected)
    {
        var actual = (command.Flags & flag) != 0;
        Equal(expected, actual, $"{command.Name} {flag} flag");
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
