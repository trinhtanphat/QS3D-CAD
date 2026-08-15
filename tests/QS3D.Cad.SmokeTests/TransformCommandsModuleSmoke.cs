using System.Globalization;
using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class TransformCommandsModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("transform-smoke");

        Succeeds(app.Execute("LINE 1 0 3 0"));
        Succeeds(app.Execute("CIRCLE 2 2 1"));
        var initial = Query(document);
        var line = initial.Single(static entity => entity.Kind == CadEntityKind.Line);
        var circle = initial.Single(static entity => entity.Kind == CadEntityKind.Circle);
        var initialLine = line;
        var initialCircle = circle;

        var revisionBeforeRotate = document.Database.Revision;
        Succeeds(app.Execute($"ROTATE {line.Handle} {circle.Handle} {line.Handle} 0 0 90"));
        if (document.Database.Revision == revisionBeforeRotate)
            throw new InvalidOperationException("ROTATE must commit a drawing revision for a non-zero angle.");

        var rotated = Query(document);
        var rotatedLine = rotated.Single(entity => entity.Handle == line.Handle);
        var rotatedCircle = rotated.Single(entity => entity.Handle == circle.Handle);
        Equal(0d, Number(rotatedLine, "x1"));
        Equal(1d, Number(rotatedLine, "y1"));
        Equal(0d, Number(rotatedLine, "x2"));
        Equal(3d, Number(rotatedLine, "y2"));
        Equal(-2d, Number(rotatedCircle, "cx"));
        Equal(2d, Number(rotatedCircle, "cy"));
        Equal(1d, Number(rotatedCircle, "radius"));
        Equal(-3d, rotatedCircle.Extents.Min.X);
        Equal(1d, rotatedCircle.Extents.Min.Y);
        Equal(-1d, rotatedCircle.Extents.Max.X);
        Equal(3d, rotatedCircle.Extents.Max.Y);
        SelectionEquals(document, line.Handle, circle.Handle);

        Succeeds(app.Execute("UNDO"));
        var afterUndo = Query(document);
        SameEntity(initialLine, afterUndo.Single(entity => entity.Handle == line.Handle));
        SameEntity(initialCircle, afterUndo.Single(entity => entity.Handle == circle.Handle));

        Succeeds(app.Execute("REDO"));
        var afterRedo = Query(document);
        SameEntity(rotatedLine, afterRedo.Single(entity => entity.Handle == line.Handle));
        SameEntity(rotatedCircle, afterRedo.Single(entity => entity.Handle == circle.Handle));

        var revisionBeforeMirror = document.Database.Revision;
        Succeeds(app.Execute($"MIRROR {line.Handle} {circle.Handle} 0 0 10 0"));
        if (document.Database.Revision == revisionBeforeMirror)
            throw new InvalidOperationException("MIRROR must commit a drawing revision.");
        var mirrored = Query(document);
        var mirroredLine = mirrored.Single(entity => entity.Handle == line.Handle);
        var mirroredCircle = mirrored.Single(entity => entity.Handle == circle.Handle);
        Equal(0d, Number(mirroredLine, "x1"));
        Equal(-1d, Number(mirroredLine, "y1"));
        Equal(0d, Number(mirroredLine, "x2"));
        Equal(-3d, Number(mirroredLine, "y2"));
        Equal(-2d, Number(mirroredCircle, "cx"));
        Equal(-2d, Number(mirroredCircle, "cy"));
        Equal(1d, Number(mirroredCircle, "radius"));
        SelectionEquals(document, line.Handle, circle.Handle);

        Succeeds(app.Execute("RECTANG 0 0 2 1"));
        var rectangle = Query(document).Single(static entity => entity.Kind == CadEntityKind.Polyline);
        var lineBeforeUnsupported = Query(document).Single(entity => entity.Handle == line.Handle);
        var revisionBeforeUnsupported = document.Database.Revision;
        Fails(app.Execute($"ROTATE {line.Handle} {rectangle.Handle} 0 0 45"));
        if (document.Database.Revision != revisionBeforeUnsupported)
            throw new InvalidOperationException("ROTATE with an unsupported entity must roll back the entire multi-object operation.");
        SameEntity(lineBeforeUnsupported, Query(document).Single(entity => entity.Handle == line.Handle));

        revisionBeforeUnsupported = document.Database.Revision;
        Fails(app.Execute($"MIRROR {line.Handle} {rectangle.Handle} 0 0 10 0"));
        if (document.Database.Revision != revisionBeforeUnsupported)
            throw new InvalidOperationException("MIRROR with an unsupported entity must roll back the entire multi-object operation.");
        SameEntity(lineBeforeUnsupported, Query(document).Single(entity => entity.Handle == line.Handle));

        var revisionBeforeMissing = document.Database.Revision;
        Fails(app.Execute($"ROTATE {line.Handle} FFFF 0 0 15"));
        Fails(app.Execute($"MIRROR {line.Handle} FFFF 0 0 10 0"));
        if (document.Database.Revision != revisionBeforeMissing)
            throw new InvalidOperationException("Missing transform sources must not mutate the drawing.");

        var revisionBeforeDegenerateAxis = document.Database.Revision;
        Fails(app.Execute($"MIRROR {line.Handle} 5 5 5 5"));
        if (document.Database.Revision != revisionBeforeDegenerateAxis)
            throw new InvalidOperationException("A degenerate mirror axis must not mutate the drawing.");

        var revisionBeforeZeroRotate = document.Database.Revision;
        Succeeds(app.Execute($"ROTATE {line.Handle} 0 0 360"));
        if (document.Database.Revision != revisionBeforeZeroRotate)
            throw new InvalidOperationException("A normalized zero ROTATE must not create a drawing revision.");
        SelectionEquals(document, line.Handle);

        Succeeds(app.Execute("LINE 1E308 0 1E308 1"));
        var huge = Query(document).Single(static entity => entity.Kind == CadEntityKind.Line && entity.Extents.Min.X > 1E307);
        var hugeBefore = huge;
        var revisionBeforeOverflow = document.Database.Revision;
        Fails(app.Execute($"ROTATE {huge.Handle} -1E308 0 90"));
        if (document.Database.Revision != revisionBeforeOverflow)
            throw new InvalidOperationException("ROTATE overflow must roll back without a drawing revision.");
        SameEntity(hugeBefore, Query(document).Single(entity => entity.Handle == huge.Handle));

        if (!app.Commands.Contains("ROTATE") || !app.Commands.Contains("MIRROR"))
            throw new InvalidOperationException("Transform commands are not registered.");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static void SelectionEquals(ICadDocument document, params QS3D.Platform.Domain.CadHandle[] expected)
    {
        var actual = document.Editor.Selection.Current.ToHashSet();
        if (actual.Count != expected.Length || !actual.SetEquals(expected))
            throw new InvalidOperationException("Transform selection did not match the distinct transformed source handles.");
    }

    private static double Number(CadEntitySnapshot entity, string key)
    {
        if (!entity.Properties.TryGetValue(key, out var raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
            throw new InvalidOperationException($"Expected finite numeric property '{key}' on {entity.Handle}.");
        return value;
    }

    private static void SameEntity(CadEntitySnapshot expected, CadEntitySnapshot actual)
    {
        if (expected.Kind != actual.Kind || !StringComparer.Ordinal.Equals(expected.LayerName, actual.LayerName))
            throw new InvalidOperationException("Entity identity metadata changed unexpectedly.");
        Equal(expected.Extents.Min.X, actual.Extents.Min.X);
        Equal(expected.Extents.Min.Y, actual.Extents.Min.Y);
        Equal(expected.Extents.Min.Z, actual.Extents.Min.Z);
        Equal(expected.Extents.Max.X, actual.Extents.Max.X);
        Equal(expected.Extents.Max.Y, actual.Extents.Max.Y);
        Equal(expected.Extents.Max.Z, actual.Extents.Max.Z);
        if (expected.Properties.Count != actual.Properties.Count)
            throw new InvalidOperationException("Entity property count changed unexpectedly.");
        foreach (var pair in expected.Properties)
        {
            if (!actual.Properties.TryGetValue(pair.Key, out var value) || !StringComparer.Ordinal.Equals(pair.Value, value))
                throw new InvalidOperationException($"Entity property '{pair.Key}' changed unexpectedly.");
        }
    }

    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 1E-9)
            throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}.");
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
