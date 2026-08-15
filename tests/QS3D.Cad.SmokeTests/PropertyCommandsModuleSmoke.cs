using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class PropertyCommandsModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        BatchEditingAndSafety();
        JournalRoundTrips();
    }

    private static void BatchEditingAndSafety()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("property-layer-smoke");

        Succeeds(app.Execute("LAYER NEW A"));
        Succeeds(app.Execute("LAYER NEW B"));
        Succeeds(app.Execute("LAYER NEW FrozenTarget"));
        Succeeds(app.Execute("LAYER NEW LockedTarget"));
        Succeeds(app.Execute("LAYER SET A"));
        Succeeds(app.Execute("LINE 0 0 10 0"));
        Succeeds(app.Execute("CIRCLE 20 20 5"));
        Succeeds(app.Execute("LAYER SET 0"));
        Succeeds(app.Execute("LAYER FREEZE FrozenTarget"));
        Succeeds(app.Execute("LAYER LOCK LockedTarget"));

        var initial = Query(document);
        var line = initial.Single(static entity => entity.Kind == CadEntityKind.Line);
        var circle = initial.Single(static entity => entity.Kind == CadEntityKind.Circle);

        var revisionBeforeMove = document.Database.Revision;
        Succeeds(app.Execute($"CHLAYER {line.Handle} {line.Handle} {circle.Handle} B"));
        if (document.Database.Revision != revisionBeforeMove + 1)
            throw new InvalidOperationException("CHLAYER batch must create exactly one drawing revision.");
        LayerEquals(document, line.Handle, "B");
        LayerEquals(document, circle.Handle, "B");
        SelectionEquals(document, line.Handle, circle.Handle);

        Succeeds(app.Execute("UNDO"));
        LayerEquals(document, line.Handle, "A");
        LayerEquals(document, circle.Handle, "A");
        Succeeds(app.Execute("REDO"));
        LayerEquals(document, line.Handle, "B");
        LayerEquals(document, circle.Handle, "B");

        var revisionBeforeSet = document.Database.Revision;
        Succeeds(app.Execute($"SETPROP {line.Handle} {circle.Handle} User.Tag Alpha"));
        if (document.Database.Revision != revisionBeforeSet + 1)
            throw new InvalidOperationException("SETPROP batch must create exactly one drawing revision.");
        PropertyEquals(document, line.Handle, "User.Tag", "Alpha");
        PropertyEquals(document, circle.Handle, "User.Tag", "Alpha");
        SelectionEquals(document, line.Handle, circle.Handle);

        var revisionBeforeNoOpSet = document.Database.Revision;
        Succeeds(app.Execute($"SETPROP {line.Handle} {circle.Handle} User.Tag Alpha"));
        SameRevision(document, revisionBeforeNoOpSet, "no-op SETPROP");

        Succeeds(app.Execute($"SETPROP {line.Handle} user.tag Beta"));
        var updatedLine = Get(document, line.Handle);
        var caseMatches = updatedLine.Properties.Keys.Where(static key => StringComparer.OrdinalIgnoreCase.Equals(key, "User.Tag")).ToArray();
        if (caseMatches.Length != 1 || !StringComparer.Ordinal.Equals(caseMatches[0], "User.Tag"))
            throw new InvalidOperationException("SETPROP must preserve the existing metadata key casing without creating a case-duplicate.");
        PropertyEquals(document, line.Handle, "User.Tag", "Beta");
        SelectionEquals(document, line.Handle);

        Succeeds(app.Execute($"DELPROP {circle.Handle} USER.TAG"));
        PropertyAbsent(document, circle.Handle, "User.Tag");
        SelectionEquals(document, circle.Handle);

        var revisionBeforeNoOpDelete = document.Database.Revision;
        Succeeds(app.Execute($"DELPROP {circle.Handle} user.tag"));
        SameRevision(document, revisionBeforeNoOpDelete, "no-op DELPROP");

        var originalX1 = Get(document, line.Handle).Properties["x1"];
        var revisionBeforeReserved = document.Database.Revision;
        Fails(app.Execute($"SETPROP {line.Handle} x1 999"));
        SameRevision(document, revisionBeforeReserved, "reserved geometry SETPROP");
        PropertyEquals(document, line.Handle, "x1", originalX1);
        Fails(app.Execute($"DELPROP {line.Handle} QS3D.BlockName"));
        SameRevision(document, revisionBeforeReserved, "reserved QS3D DELPROP");

        var revisionBeforeMissing = document.Database.Revision;
        Fails(app.Execute($"SETPROP {line.Handle} FFFF User.Stage Draft"));
        SameRevision(document, revisionBeforeMissing, "missing-handle SETPROP");
        PropertyAbsent(document, line.Handle, "User.Stage");
        Fails(app.Execute($"CHLAYER {line.Handle} FFFF 0"));
        SameRevision(document, revisionBeforeMissing, "missing-handle CHLAYER");
        LayerEquals(document, line.Handle, "B");

        var revisionBeforeFrozenTarget = document.Database.Revision;
        Fails(app.Execute($"CHLAYER {line.Handle} FrozenTarget"));
        SameRevision(document, revisionBeforeFrozenTarget, "frozen-target CHLAYER");
        LayerEquals(document, line.Handle, "B");

        var revisionBeforeLockedTarget = document.Database.Revision;
        Fails(app.Execute($"CHLAYER {line.Handle} LockedTarget"));
        SameRevision(document, revisionBeforeLockedTarget, "locked-target CHLAYER");
        LayerEquals(document, line.Handle, "B");

        Succeeds(app.Execute("LAYER LOCK B"));
        var revisionAfterSourceLock = document.Database.Revision;
        Fails(app.Execute($"CHLAYER {line.Handle} 0"));
        Fails(app.Execute($"SETPROP {line.Handle} User.Stage Draft"));
        SameRevision(document, revisionAfterSourceLock, "locked-source edits");
        LayerEquals(document, line.Handle, "B");
        PropertyAbsent(document, line.Handle, "User.Stage");
        Succeeds(app.Execute("LAYER UNLOCK B"));

        Succeeds(app.Execute("LAYER FREEZE B"));
        var revisionAfterSourceFreeze = document.Database.Revision;
        Fails(app.Execute($"CHLAYER {line.Handle} 0"));
        Fails(app.Execute($"SETPROP {line.Handle} User.Stage Draft"));
        SameRevision(document, revisionAfterSourceFreeze, "frozen-source edits");
        Succeeds(app.Execute("LAYER THAW B"));

        foreach (var command in new[] { "CHLAYER", "SETPROP", "DELPROP" })
        {
            if (!app.Commands.Contains(command))
                throw new InvalidOperationException($"Property command {command} is not registered.");
        }
    }

    private static void JournalRoundTrips()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("property-journal-smoke");
        Succeeds(app.Execute("LINE 0 0 10 0"));
        Succeeds(app.Execute("LAYER NEW B"));
        var line = Query(document).Single();

        Succeeds(app.Execute($"SETPROP {line.Handle} User.Code A-01"));
        PropertyEquals(document, line.Handle, "User.Code", "A-01");
        Succeeds(app.Execute("UNDO"));
        PropertyAbsent(document, line.Handle, "User.Code");
        Succeeds(app.Execute("REDO"));
        PropertyEquals(document, line.Handle, "User.Code", "A-01");

        Succeeds(app.Execute($"CHLAYER {line.Handle} B"));
        LayerEquals(document, line.Handle, "B");
        Succeeds(app.Execute("UNDO"));
        LayerEquals(document, line.Handle, "0");
        Succeeds(app.Execute("REDO"));
        LayerEquals(document, line.Handle, "B");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static CadEntitySnapshot Get(ICadDocument document, CadHandle handle)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Get(handle) ?? throw new InvalidOperationException($"Entity {handle} is missing.");
    }

    private static void LayerEquals(ICadDocument document, CadHandle handle, string expected)
    {
        var actual = Get(document, handle).LayerName;
        if (!StringComparer.OrdinalIgnoreCase.Equals(actual, expected))
            throw new InvalidOperationException($"Expected layer '{expected}', got '{actual}'.");
    }

    private static void PropertyEquals(ICadDocument document, CadHandle handle, string key, string expected)
    {
        var entity = Get(document, handle);
        var matches = entity.Properties.Where(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Key, key)).ToArray();
        if (matches.Length != 1 || !StringComparer.Ordinal.Equals(matches[0].Value, expected))
            throw new InvalidOperationException($"Expected metadata property '{key}'='{expected}' on {handle}.");
    }

    private static void PropertyAbsent(ICadDocument document, CadHandle handle, string key)
    {
        var entity = Get(document, handle);
        if (entity.Properties.Keys.Any(candidate => StringComparer.OrdinalIgnoreCase.Equals(candidate, key)))
            throw new InvalidOperationException($"Expected metadata property '{key}' to be absent on {handle}.");
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
