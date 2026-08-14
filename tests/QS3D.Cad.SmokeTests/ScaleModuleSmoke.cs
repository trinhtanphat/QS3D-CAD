using System.Globalization;
using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class ScaleModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("scale-smoke");

        Succeeds(app.Execute("LINE 1 2 3 4"));
        Succeeds(app.Execute("CIRCLE 5 6 2"));
        var initial = Query(document);
        var line = initial.Single(static entity => entity.Kind == CadEntityKind.Line);
        var circle = initial.Single(static entity => entity.Kind == CadEntityKind.Circle);

        Succeeds(app.Execute($"BLOCK Unit {line.Handle}"));
        Succeeds(app.Execute("INSERT Unit 10 20 2 30"));
        var block = Query(document).Single(static entity => entity.Kind == CadEntityKind.BlockReference);

        Succeeds(app.Execute($"SCALE {line.Handle} {circle.Handle} {block.Handle} {line.Handle} 1 2 2"));
        var scaled = Query(document);
        var scaledLine = scaled.Single(entity => entity.Handle == line.Handle);
        var scaledCircle = scaled.Single(entity => entity.Handle == circle.Handle);
        var scaledBlock = scaled.Single(entity => entity.Handle == block.Handle);

        Equal(1d, Number(scaledLine, "x1"));
        Equal(2d, Number(scaledLine, "y1"));
        Equal(5d, Number(scaledLine, "x2"));
        Equal(6d, Number(scaledLine, "y2"));
        Equal(1d, scaledLine.Extents.Min.X);
        Equal(2d, scaledLine.Extents.Min.Y);
        Equal(5d, scaledLine.Extents.Max.X);
        Equal(6d, scaledLine.Extents.Max.Y);

        Equal(9d, Number(scaledCircle, "cx"));
        Equal(10d, Number(scaledCircle, "cy"));
        Equal(4d, Number(scaledCircle, "radius"));
        Equal(5d, scaledCircle.Extents.Min.X);
        Equal(6d, scaledCircle.Extents.Min.Y);
        Equal(13d, scaledCircle.Extents.Max.X);
        Equal(14d, scaledCircle.Extents.Max.Y);

        Equal(19d, Number(scaledBlock, CadBlockReferencePropertyNames.InsertionX));
        Equal(38d, Number(scaledBlock, CadBlockReferencePropertyNames.InsertionY));
        Equal(4d, Number(scaledBlock, CadBlockReferencePropertyNames.UniformScale));
        Equal(Math.PI / 6d, Number(scaledBlock, CadBlockReferencePropertyNames.RotationRadians));

        var selection = document.Editor.Selection.Current.ToHashSet();
        if (selection.Count != 3 || !selection.SetEquals(new[] { line.Handle, circle.Handle, block.Handle }))
            throw new InvalidOperationException("SCALE must retain a distinct selection of the scaled source handles.");

        var revisionBeforeMissing = document.Database.Revision;
        var lineBeforeMissing = Query(document).Single(entity => entity.Handle == line.Handle);
        Fails(app.Execute($"SCALE {line.Handle} FFFF 0 0 2"));
        var lineAfterMissing = Query(document).Single(entity => entity.Handle == line.Handle);
        if (document.Database.Revision != revisionBeforeMissing)
            throw new InvalidOperationException("SCALE with a missing source must not commit a database revision.");
        SameEntityGeometry(lineBeforeMissing, lineAfterMissing);

        var revisionBeforeInvalidFactor = document.Database.Revision;
        Fails(app.Execute($"SCALE {line.Handle} 0 0 0"));
        Fails(app.Execute($"SCALE {line.Handle} 0 0 -1"));
        if (document.Database.Revision != revisionBeforeInvalidFactor)
            throw new InvalidOperationException("SCALE with a non-positive factor must not mutate the drawing.");

        Succeeds(app.Execute("LINE 1E308 0 1E308 1"));
        var huge = Query(document).Single(static entity => entity.Kind == CadEntityKind.Line && entity.Extents.Min.X > 1E307);
        var revisionBeforeOverflow = document.Database.Revision;
        var hugeBeforeOverflow = Query(document).Single(entity => entity.Handle == huge.Handle);
        Fails(app.Execute($"SCALE {huge.Handle} 0 0 2"));
        var hugeAfterOverflow = Query(document).Single(entity => entity.Handle == huge.Handle);
        if (document.Database.Revision != revisionBeforeOverflow)
            throw new InvalidOperationException("SCALE coordinate overflow must roll back without a database revision.");
        SameEntityGeometry(hugeBeforeOverflow, hugeAfterOverflow);

        if (!app.Commands.Contains("SCALE"))
            throw new InvalidOperationException("SCALE command is not registered.");
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

    private static void SameEntityGeometry(CadEntitySnapshot expected, CadEntitySnapshot actual)
    {
        Equal(expected.Extents.Min.X, actual.Extents.Min.X);
        Equal(expected.Extents.Min.Y, actual.Extents.Min.Y);
        Equal(expected.Extents.Min.Z, actual.Extents.Min.Z);
        Equal(expected.Extents.Max.X, actual.Extents.Max.X);
        Equal(expected.Extents.Max.Y, actual.Extents.Max.Y);
        Equal(expected.Extents.Max.Z, actual.Extents.Max.Z);
        if (expected.Properties.Count != actual.Properties.Count)
            throw new InvalidOperationException("Entity property count changed after failed SCALE.");
        foreach (var pair in expected.Properties)
        {
            if (!actual.Properties.TryGetValue(pair.Key, out var value) || !StringComparer.Ordinal.Equals(pair.Value, value))
                throw new InvalidOperationException($"Entity property '{pair.Key}' changed after failed SCALE.");
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
