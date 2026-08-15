using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class SelectionSetManagementModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        CommandMetadata();
        SetAlgebraAndValidation();
        StaleHandleRepair();
        DocumentIsolation();
    }

    private static void CommandMetadata()
    {
        var registry = new CommandRegistry();
        SelectionCommands.RegisterAll(registry);
        foreach (var name in new[] { "SELSTATUS", "SELHANDLES", "SELADD", "SELREMOVE", "SELTOGGLE", "SELHEALTH", "SELPRUNE" })
        {
            Require(registry.TryResolve(name, out var command) && command is not null, $"{name} must be registered");
            HasFlag(command!, CommandFlags.RequiresDocument, true);
            HasFlag(command!, CommandFlags.ReadOnly, true);
        }
    }

    private static void SetAlgebraAndValidation()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("selection-set-management");
        Succeeds(app.Execute("LINE 0 0 10 0"));
        Succeeds(app.Execute("CIRCLE 20 20 5"));
        Succeeds(app.Execute("RECTANG -2 -2 2 2"));
        var entities = Query(document).OrderBy(static entity => entity.Handle.Value, StringComparer.Ordinal).ToArray();
        Require(entities.Length == 3, "fixture must contain three entities");
        var first = entities[0].Handle;
        var second = entities[1].Handle;
        var third = entities[2].Handle;
        var revision = document.Database.Revision;

        Succeeds(app.Execute($"SELHANDLES {first.Value} {second.Value}"));
        SelectionEquals(document, first, second);
        SameRevision(document, revision, "SELHANDLES");

        Succeeds(app.Execute($"SELADD {third.Value} {first.Value}"));
        SelectionEquals(document, first, second, third);
        SameRevision(document, revision, "SELADD");

        Succeeds(app.Execute($"SELREMOVE {second.Value}"));
        SelectionEquals(document, first, third);
        SameRevision(document, revision, "SELREMOVE");

        Succeeds(app.Execute($"SELTOGGLE {first.Value} {second.Value}"));
        SelectionEquals(document, second, third);
        SameRevision(document, revision, "SELTOGGLE");

        Succeeds(app.Execute("SELSTATUS"));
        var health = app.Execute("SELHEALTH");
        Succeeds(health);
        Require(health.Message?.Contains("healthy", StringComparison.OrdinalIgnoreCase) == true, "live selection must be healthy");
        SelectionEquals(document, second, third);
        SameRevision(document, revision, "selection diagnostics");

        var before = document.Editor.Selection.Current.ToArray();
        Fails(app.Execute("SELHANDLES ZZZ"));
        SelectionEquals(document, before);
        Fails(app.Execute("SELADD BEEF"));
        SelectionEquals(document, before);
        Fails(app.Execute("SELTOGGLE CAFE"));
        SelectionEquals(document, before);
        Succeeds(app.Execute("SELREMOVE BEEF"));
        SelectionEquals(document, before);
        SameRevision(document, revision, "invalid/missing handle operations");
    }

    private static void StaleHandleRepair()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("selection-stale-repair");
        Succeeds(app.Execute("LINE 0 0 10 0"));
        var handle = Query(document).Single().Handle;
        Succeeds(app.Execute($"SELHANDLES {handle.Value}"));

        Succeeds(app.Execute("UNDO"));
        SelectionEquals(document, handle);
        Require(Query(document).Count == 0, "undo must remove fixture entity");
        var revisionAfterUndo = document.Database.Revision;

        var unhealthy = app.Execute("SELHEALTH");
        Succeeds(unhealthy);
        Require(unhealthy.Message?.Contains("unhealthy", StringComparison.OrdinalIgnoreCase) == true, "stale selection must be unhealthy");
        Succeeds(app.Execute("SELSTATUS"));
        SelectionEquals(document, handle);
        SameRevision(document, revisionAfterUndo, "stale diagnostics");

        Succeeds(app.Execute("SELPRUNE"));
        SelectionEquals(document);
        SameRevision(document, revisionAfterUndo, "SELPRUNE");
        var healthy = app.Execute("SELHEALTH");
        Succeeds(healthy);
        Require(healthy.Message?.Contains("healthy", StringComparison.OrdinalIgnoreCase) == true, "pruned selection must be healthy");

        var stale = new CadHandle("BEEF");
        document.Editor.Selection.Set(new[] { stale });
        Succeeds(app.Execute("SELREMOVE BEEF"));
        SelectionEquals(document);
        document.Editor.Selection.Set(new[] { stale });
        Succeeds(app.Execute("SELTOGGLE BEEF"));
        SelectionEquals(document);
        SameRevision(document, revisionAfterUndo, "stale removal/toggle-off");
    }

    private static void DocumentIsolation()
    {
        var app = new StandaloneCadApplication();
        var first = app.NewDocument("selection-first");
        Succeeds(app.Execute("LINE 0 0 1 1"));
        var firstHandle = Query(first).Single().Handle;
        Succeeds(app.Execute($"SELHANDLES {firstHandle.Value}"));
        SelectionEquals(first, firstHandle);

        var second = app.NewDocument("selection-second");
        SelectionEquals(second);
        app.Documents.Activate(first.Id);
        SelectionEquals(first, firstHandle);
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static void SelectionEquals(ICadDocument document, params CadHandle[] expected)
    {
        var actual = document.Editor.Selection.Current.ToHashSet();
        if (actual.Count != expected.Length || !actual.SetEquals(expected))
            throw new InvalidOperationException($"Selection mismatch. Expected {expected.Length}, got {actual.Count}.");
    }

    private static void SameRevision(ICadDocument document, long expected, string operation)
    {
        if (document.Database.Revision != expected)
            throw new InvalidOperationException($"{operation} must not create a drawing revision.");
    }

    private static void HasFlag(ICadCommand command, CommandFlags flag, bool expected)
    {
        var actual = (command.Flags & flag) != 0;
        if (actual != expected) throw new InvalidOperationException($"{command.Name} {flag} flag mismatch.");
    }

    private static void Succeeds(CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }

    private static void Fails(CommandResult result)
    {
        if (result.Succeeded) throw new InvalidOperationException("Expected command failure.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
