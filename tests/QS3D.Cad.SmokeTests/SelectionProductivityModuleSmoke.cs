using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class SelectionProductivityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("selection-productivity-smoke");

        Succeeds(app.Execute("LAYER NEW A"));
        Succeeds(app.Execute("LAYER SET A"));
        Succeeds(app.Execute("LINE 0 0 10 0"));
        Succeeds(app.Execute("CIRCLE 20 20 5"));
        Succeeds(app.Execute("LAYER SET 0"));
        Succeeds(app.Execute("RECTANG -2 -2 2 2"));

        var entities = Query(document);
        var line = entities.Single(static entity => entity.Kind == CadEntityKind.Line);
        var circle = entities.Single(static entity => entity.Kind == CadEntityKind.Circle);
        var rectangle = entities.Single(static entity => entity.Kind == CadEntityKind.Polyline);
        var revision = document.Database.Revision;

        Succeeds(app.Execute("SELALL"));
        SelectionEquals(document, line.Handle, circle.Handle, rectangle.Handle);
        SameRevision(document, revision, "SELALL");

        Succeeds(app.Execute("SELKIND line"));
        SelectionEquals(document, line.Handle);
        SameRevision(document, revision, "SELKIND");

        Succeeds(app.Execute("SELLAYER A"));
        SelectionEquals(document, line.Handle, circle.Handle);
        SameRevision(document, revision, "SELLAYER");

        Succeeds(app.Execute("SELPROP radius 5"));
        SelectionEquals(document, circle.Handle);
        SameRevision(document, revision, "SELPROP");

        var beforeInvalid = document.Editor.Selection.Current.ToHashSet();
        Fails(app.Execute("SELKIND DefinitelyNotAnEntityKind"));
        SelectionEquals(document, beforeInvalid.ToArray());
        Fails(app.Execute("SELLAYER MissingLayer"));
        SelectionEquals(document, beforeInvalid.ToArray());
        SameRevision(document, revision, "invalid selectors");

        Succeeds(app.Execute("SELBOX Window -3 -3 3 3"));
        SelectionEquals(document, rectangle.Handle);
        SameRevision(document, revision, "SELBOX Window");

        Succeeds(app.Execute("SELBOX Crossing 9 -1 16 1"));
        SelectionEquals(document, line.Handle);
        SameRevision(document, revision, "SELBOX Crossing");

        Succeeds(app.Execute("SELINVERT"));
        SelectionEquals(document, circle.Handle, rectangle.Handle);
        SameRevision(document, revision, "SELINVERT");

        Succeeds(app.Execute("SELNONE"));
        SelectionEquals(document);
        SameRevision(document, revision, "SELNONE");

        foreach (var name in new[] { "SELALL", "SELNONE", "SELINVERT", "SELKIND", "SELLAYER", "SELPROP", "SELBOX" })
        {
            if (!app.Commands.Contains(name))
                throw new InvalidOperationException($"Selection command {name} is not registered.");
        }
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
            throw new InvalidOperationException($"{operation} must be read-only and must not create a drawing revision.");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }

    private static void Fails(QS3D.Platform.Application.CommandResult result)
    {
        if (result.Succeeded)
            throw new InvalidOperationException("Expected command failure.");
    }
}
