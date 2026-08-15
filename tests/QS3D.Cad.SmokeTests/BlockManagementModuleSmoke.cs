using System.Globalization;
using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class BlockManagementModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        DefinitionAndInstanceLifecycle();
        DependencyAwarePurgeAndDeleteSafety();
        ValidationAndLockedLayerSafety();
    }

    private static void DefinitionAndInstanceLifecycle()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("block-management");
        Succeeds(app.Execute("LINE 0 0 10 0"));
        Succeeds(app.Execute("CIRCLE 5 5 2"));

        var beforeDefinition = document.Database.Revision;
        Succeeds(app.Execute("BLOCKBASE Widget 2 3 1 2"));
        Equal(beforeDefinition + 1, document.Database.Revision, "BLOCKBASE must create one revision");
        var widget = GetBlock(document, "Widget");
        Equal(2d, widget.BasePoint.X, "explicit base X");
        Equal(3d, widget.BasePoint.Y, "explicit base Y");
        Equal(2, widget.Entities.Count, "block member count");

        Succeeds(app.Execute("INSERT Widget 100 200 2 30"));
        var reference = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);
        SelectionEquals(document, reference.Handle);
        ReferenceEquals(reference, "Widget", 100d, 200d, 2d, 30d);

        var readRevision = document.Database.Revision;
        Succeeds(app.Execute("BLOCKINFO Widget"));
        Succeeds(app.Execute("BLOCKREFS Widget"));
        Equal(readRevision, document.Database.Revision, "BLOCKINFO/BLOCKREFS must stay read-only");
        SelectionEquals(document, reference.Handle);

        var originalExtents = reference.Extents;
        var beforeSet = document.Database.Revision;
        Succeeds(app.Execute($"BLOCKSET {reference.Handle} Widget 110 210 1.5 45"));
        Equal(beforeSet + 1, document.Database.Revision, "BLOCKSET must create one revision");
        var updated = Get(document, reference.Handle);
        ReferenceEquals(updated, "Widget", 110d, 210d, 1.5d, 45d);
        Require(!updated.Extents.Equals(originalExtents), "BLOCKSET must recompute transformed extents");
        SelectionEquals(document, reference.Handle);

        Succeeds(app.Execute("UNDO"));
        ReferenceEquals(Get(document, reference.Handle), "Widget", 100d, 200d, 2d, 30d);
        Succeeds(app.Execute("REDO"));
        ReferenceEquals(Get(document, reference.Handle), "Widget", 110d, 210d, 1.5d, 45d);

        var beforeNoOp = document.Database.Revision;
        Succeeds(app.Execute($"BLOCKSET {reference.Handle} Widget 110 210 1.5 45"));
        Equal(beforeNoOp, document.Database.Revision, "no-op BLOCKSET must not create a revision");

        Succeeds(app.Execute("BLOCKCLONE Widget WidgetCopy"));
        var copy = GetBlock(document, "WidgetCopy");
        Equal(widget.BasePoint, copy.BasePoint, "BLOCKCLONE base point");
        Equal(widget.Entities.Count, copy.Entities.Count, "BLOCKCLONE member count");

        var beforeReplace = document.Database.Revision;
        Succeeds(app.Execute($"BLOCKSET {reference.Handle} WidgetCopy 110 210 1.5 45"));
        Equal(beforeReplace + 1, document.Database.Revision, "definition replacement must create one revision");
        ReferenceEquals(Get(document, reference.Handle), "WidgetCopy", 110d, 210d, 1.5d, 45d);
        Succeeds(app.Execute("UNDO"));
        ReferenceEquals(Get(document, reference.Handle), "Widget", 110d, 210d, 1.5d, 45d);
        Succeeds(app.Execute("REDO"));
        ReferenceEquals(Get(document, reference.Handle), "WidgetCopy", 110d, 210d, 1.5d, 45d);
    }

    private static void DependencyAwarePurgeAndDeleteSafety()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("block-purge");
        Succeeds(app.Execute("LINE 0 0 4 0"));
        Succeeds(app.Execute("BLOCKBASE Leaf 0 0 1"));
        Succeeds(app.Execute("INSERT Leaf 10 10"));
        var leafReference = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);
        Succeeds(app.Execute($"BLOCKBASE Parent 0 0 {leafReference.Handle}"));
        Succeeds(app.Execute($"ERASE {leafReference.Handle}"));

        var beforeRejectedDelete = document.Database.Revision;
        Fails(app.Execute("BLOCKDELETE Leaf"));
        Equal(beforeRejectedDelete, document.Database.Revision, "nested-definition BLOCKDELETE rejection must be atomic");
        Require(HasBlock(document, "Leaf") && HasBlock(document, "Parent"), "nested dependency must survive rejected delete");

        var beforePurge = document.Database.Revision;
        Succeeds(app.Execute("BLOCKPURGE"));
        Equal(beforePurge + 1, document.Database.Revision, "unreachable block graph purge must create one revision");
        Equal(0, Blocks(document).Count, "unreachable parent and leaf definitions must both purge");

        var beforeNoOpPurge = document.Database.Revision;
        Succeeds(app.Execute("BLOCKPURGE"));
        Equal(beforeNoOpPurge, document.Database.Revision, "empty BLOCKPURGE must not create a revision");

        Succeeds(app.Execute("BLOCKBASE Leaf 0 0 1"));
        Succeeds(app.Execute("INSERT Leaf 20 20"));
        leafReference = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);
        Succeeds(app.Execute($"BLOCKBASE Parent 0 0 {leafReference.Handle}"));
        Succeeds(app.Execute($"ERASE {leafReference.Handle}"));
        Succeeds(app.Execute("INSERT Parent 40 40"));
        var parentReference = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);
        Succeeds(app.Execute("BLOCKCLONE Leaf Orphan"));

        var beforeReachablePurge = document.Database.Revision;
        Succeeds(app.Execute("BLOCKPURGE"));
        Equal(beforeReachablePurge + 1, document.Database.Revision, "purging one orphan definition must create one revision");
        Require(HasBlock(document, "Leaf") && HasBlock(document, "Parent"), "reachable dependency graph must be retained");
        Require(!HasBlock(document, "Orphan"), "unreachable clone must be purged");

        var beforeStablePurge = document.Database.Revision;
        Succeeds(app.Execute("BLOCKPURGE"));
        Equal(beforeStablePurge, document.Database.Revision, "fully reachable graph purge must be a no-op");
        Succeeds(app.Execute("BLOCKREFS Parent"));
        SelectionEquals(document, parentReference.Handle);
    }

    private static void ValidationAndLockedLayerSafety()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("block-safety");
        Succeeds(app.Execute("LINE 0 0 1 0"));

        var beforeMissing = document.Database.Revision;
        Fails(app.Execute("BLOCKBASE Broken 0 0 FFFF"));
        Equal(beforeMissing, document.Database.Revision, "BLOCKBASE missing handle must not mutate");
        Require(!HasBlock(document, "Broken"), "failed BLOCKBASE must not leave a definition");

        Succeeds(app.Execute("BLOCKBASE Good 0 0 1"));
        var beforeWrongKind = document.Database.Revision;
        Fails(app.Execute("BLOCKSET 1 Good 1 1 1 0"));
        Equal(beforeWrongKind, document.Database.Revision, "BLOCKSET on non-reference must not mutate");

        var beforeMissingTarget = document.Database.Revision;
        Succeeds(app.Execute("INSERT Good 5 5"));
        var reference = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);
        var snapshot = reference;
        beforeMissingTarget = document.Database.Revision;
        Fails(app.Execute($"BLOCKSET {reference.Handle} Missing 6 6 1 0"));
        Equal(beforeMissingTarget, document.Database.Revision, "BLOCKSET missing target definition must not mutate");
        Equal(snapshot, Get(document, reference.Handle), "failed BLOCKSET must preserve reference snapshot");

        var beforeBadScale = document.Database.Revision;
        Fails(app.Execute($"BLOCKSET {reference.Handle} Good 6 6 0 0"));
        Equal(beforeBadScale, document.Database.Revision, "BLOCKSET zero scale must not mutate");

        Succeeds(app.Execute("LAYER NEW LockedRefs"));
        Succeeds(app.Execute("LAYER SET LockedRefs"));
        Succeeds(app.Execute("INSERT Good 8 8"));
        var lockedReference = Query(document)
            .Single(entity => entity.Kind == CadEntityKind.BlockReference && entity.LayerName == "LockedRefs");
        Succeeds(app.Execute("LAYER SET 0"));
        Succeeds(app.Execute("LAYER LOCK LockedRefs"));
        var beforeLocked = document.Database.Revision;
        Fails(app.Execute($"BLOCKSET {lockedReference.Handle} Good 9 9 2 15"));
        Equal(beforeLocked, document.Database.Revision, "locked-layer BLOCKSET must not mutate");
        ReferenceEquals(Get(document, lockedReference.Handle), "Good", 8d, 8d, 1d, 0d);

        var beforeDuplicateClone = document.Database.Revision;
        Fails(app.Execute("BLOCKCLONE Good Good"));
        Equal(beforeDuplicateClone, document.Database.Revision, "duplicate BLOCKCLONE must not mutate");

        var beforeBadPurgeSyntax = document.Database.Revision;
        Fails(app.Execute("BLOCKPURGE extra"));
        Equal(beforeBadPurgeSyntax, document.Database.Revision, "invalid BLOCKPURGE syntax must not mutate");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query();
    }

    private static CadEntitySnapshot Get(ICadDocument document, CadHandle handle)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Get(handle) ?? throw new InvalidOperationException($"Entity {handle} was not found.");
    }

    private static IReadOnlyList<CadBlockDefinitionSnapshot> Blocks(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.GetBlocks();
    }

    private static CadBlockDefinitionSnapshot GetBlock(ICadDocument document, string name)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.GetBlock(name) ?? throw new InvalidOperationException($"Block '{name}' was not found.");
    }

    private static bool HasBlock(ICadDocument document, string name)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.GetBlock(name) is not null;
    }

    private static void ReferenceEquals(CadEntitySnapshot entity, string blockName, double x, double y, double scale, double degrees)
    {
        Equal(CadEntityKind.BlockReference, entity.Kind, "reference kind");
        Equal(blockName, entity.Properties[CadBlockReferencePropertyNames.BlockName], "reference block name");
        Near(x, Parse(entity, CadBlockReferencePropertyNames.InsertionX), "reference insertion X");
        Near(y, Parse(entity, CadBlockReferencePropertyNames.InsertionY), "reference insertion Y");
        Near(scale, Parse(entity, CadBlockReferencePropertyNames.UniformScale), "reference scale");
        Near(degrees * Math.PI / 180d, Parse(entity, CadBlockReferencePropertyNames.RotationRadians), "reference rotation");
    }

    private static double Parse(CadEntitySnapshot entity, string key)
        => double.Parse(entity.Properties[key], NumberStyles.Float, CultureInfo.InvariantCulture);

    private static void SelectionEquals(ICadDocument document, params CadHandle[] expected)
    {
        var actual = document.Editor.Selection.Current.OrderBy(static handle => handle.Value, StringComparer.Ordinal).ToArray();
        var wanted = expected.OrderBy(static handle => handle.Value, StringComparer.Ordinal).ToArray();
        Equal(wanted.Length, actual.Length, "selection count");
        for (var index = 0; index < wanted.Length; index++) Equal(wanted[index], actual[index], $"selection[{index}]");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command unexpectedly failed.");
    }

    private static void Fails(QS3D.Platform.Application.CommandResult result)
    {
        if (result.Succeeded) throw new InvalidOperationException("Command unexpectedly succeeded.");
    }

    private static void Near(double expected, double actual, string label)
    {
        if (Math.Abs(expected - actual) > 1e-9) throw new InvalidOperationException($"{label}: expected {expected:R} but got {actual:R}.");
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
