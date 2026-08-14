using System.Globalization;
using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CopyMoveModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("copy-move-smoke");

        Succeeds(app.Execute("LINE 0 0 10 5"));
        var line = Query(document).Single(static entity => entity.Kind == CadEntityKind.Line);
        Succeeds(app.Execute($"BLOCK Unit {line.Handle}"));
        Succeeds(app.Execute("INSERT Unit 100 200"));
        var block = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);

        Succeeds(app.Execute($"MOVE {line.Handle} {block.Handle} 10 20"));
        var moved = Query(document);
        var movedLine = moved.Single(entity => entity.Handle == line.Handle);
        var movedBlock = moved.Single(entity => entity.Handle == block.Handle);
        Equal(10d, Number(movedLine, "x1"));
        Equal(20d, Number(movedLine, "y1"));
        Equal(20d, Number(movedLine, "x2"));
        Equal(25d, Number(movedLine, "y2"));
        Equal(110d, Number(movedBlock, CadBlockReferencePropertyNames.InsertionX));
        Equal(220d, Number(movedBlock, CadBlockReferencePropertyNames.InsertionY));

        var countBeforeCopy = moved.Count;
        Succeeds(app.Execute($"COPY {line.Handle} {block.Handle} -5 5"));
        var afterCopy = Query(document);
        if (afterCopy.Count != countBeforeCopy + 2) throw new InvalidOperationException("COPY must append exactly one entity per distinct source handle.");
        var selectedCopies = document.Editor.Selection.Current.ToHashSet();
        if (selectedCopies.Count != 2 || selectedCopies.Contains(line.Handle) || selectedCopies.Contains(block.Handle))
            throw new InvalidOperationException("COPY must select the newly created entities only.");
        var copiedLine = afterCopy.Single(entity => selectedCopies.Contains(entity.Handle) && entity.Kind == CadEntityKind.Line);
        var copiedBlock = afterCopy.Single(entity => selectedCopies.Contains(entity.Handle) && entity.Kind == CadEntityKind.BlockReference);
        Equal(5d, Number(copiedLine, "x1"));
        Equal(25d, Number(copiedLine, "y1"));
        Equal(15d, Number(copiedLine, "x2"));
        Equal(30d, Number(copiedLine, "y2"));
        Equal(105d, Number(copiedBlock, CadBlockReferencePropertyNames.InsertionX));
        Equal(225d, Number(copiedBlock, CadBlockReferencePropertyNames.InsertionY));
        if (!StringComparer.Ordinal.Equals(copiedLine.LayerName, movedLine.LayerName) || !StringComparer.Ordinal.Equals(copiedBlock.LayerName, movedBlock.LayerName))
            throw new InvalidOperationException("COPY must preserve source layers.");

        var revisionBeforeMissingCopy = document.Database.Revision;
        Fails(app.Execute($"COPY {line.Handle} FFFF 1 1"));
        if (document.Database.Revision != revisionBeforeMissingCopy || Query(document).Count != afterCopy.Count)
            throw new InvalidOperationException("COPY with a missing source must be all-or-nothing.");

        var lineBeforeMissingMove = Query(document).Single(entity => entity.Handle == line.Handle);
        var revisionBeforeMissingMove = document.Database.Revision;
        Fails(app.Execute($"MOVE {line.Handle} FFFF 1 1"));
        var lineAfterMissingMove = Query(document).Single(entity => entity.Handle == line.Handle);
        if (document.Database.Revision != revisionBeforeMissingMove)
            throw new InvalidOperationException("MOVE with a missing source must not commit a database revision.");
        SameExtents(lineBeforeMissingMove, lineAfterMissingMove);
        Equal(Number(lineBeforeMissingMove, "x1"), Number(lineAfterMissingMove, "x1"));
        Equal(Number(lineBeforeMissingMove, "y1"), Number(lineAfterMissingMove, "y1"));
        Equal(Number(lineBeforeMissingMove, "x2"), Number(lineAfterMissingMove, "x2"));
        Equal(Number(lineBeforeMissingMove, "y2"), Number(lineAfterMissingMove, "y2"));

        Succeeds(app.Execute("LINE 1E308 0 1E308 1"));
        var huge = Query(document).Single(static entity => entity.Kind == CadEntityKind.Line && entity.Extents.Min.X > 1E307);
        var revisionBeforeOverflow = document.Database.Revision;
        var countBeforeOverflow = Query(document).Count;
        Fails(app.Execute($"COPY {huge.Handle} 1E308 0"));
        if (document.Database.Revision != revisionBeforeOverflow || Query(document).Count != countBeforeOverflow)
            throw new InvalidOperationException("COPY coordinate overflow must roll back without appending an entity.");

        if (!app.Commands.Contains("COPY")) throw new InvalidOperationException("COPY command is not registered.");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static double Number(CadEntitySnapshot entity, string key)
    {
        if (!entity.Properties.TryGetValue(key, out var raw) || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new InvalidOperationException($"Expected finite numeric property '{key}' on {entity.Handle}.");
        return value;
    }

    private static void SameExtents(CadEntitySnapshot expected, CadEntitySnapshot actual)
    {
        Equal(expected.Extents.Min.X, actual.Extents.Min.X);
        Equal(expected.Extents.Min.Y, actual.Extents.Min.Y);
        Equal(expected.Extents.Min.Z, actual.Extents.Min.Z);
        Equal(expected.Extents.Max.X, actual.Extents.Max.X);
        Equal(expected.Extents.Max.Y, actual.Extents.Max.Y);
        Equal(expected.Extents.Max.Z, actual.Extents.Max.Z);
    }

    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 1E-9) throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}.");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }

    private static void Fails(QS3D.Platform.Application.CommandResult result)
    {
        if (result.Succeeded) throw new InvalidOperationException("Expected command failure.");
    }
}
