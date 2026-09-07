using System.Globalization;
using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class ReferencePrimitiveCommandsModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("reference-primitives-smoke");

        Succeeds(app.Execute("ARC 0 0 10 0 90"));
        Succeeds(app.Execute("POINT 5 5"));
        Succeeds(app.Execute("POLYGON 4 20 0 5 45"));
        Succeeds(app.Execute("A 40 0 4 180 -90"));
        Succeeds(app.Execute("PO 50 5"));
        Succeeds(app.Execute("POL 3 60 0 4 90"));

        var entities = Query(document);
        var arc = entities.First(static entity => entity.Kind == CadEntityKind.Arc);
        var point = entities.First(static entity => entity.Kind == CadEntityKind.Point);
        var polygon = entities.First(entity => ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out var candidate) && candidate.Sides == 4);

        if (!ReferencePrimitiveGeometry.TryGetArc(arc, out var arcGeometry))
            throw new InvalidOperationException("ARC did not persist the expected reference schema.");
        Equal(10d, arcGeometry.Radius);
        Equal(0d, arcGeometry.StartAngleDegrees);
        Equal(90d, arcGeometry.SweepAngleDegrees);
        Equal(0d, arc.Extents.Min.X, 1e-9);
        Equal(0d, arc.Extents.Min.Y, 1e-9);
        Equal(10d, arc.Extents.Max.X, 1e-9);
        Equal(10d, arc.Extents.Max.Y, 1e-9);

        if (!ReferencePrimitiveGeometry.TryGetPoint(point, out var pointGeometry))
            throw new InvalidOperationException("POINT did not persist the expected reference schema.");
        Equal(5d, pointGeometry.X);
        Equal(5d, pointGeometry.Y);
        Equal(point.Extents.Min.X, point.Extents.Max.X);
        Equal(point.Extents.Min.Y, point.Extents.Max.Y);

        if (!ReferencePrimitiveGeometry.TryGetRegularPolygon(polygon, out var polygonGeometry))
            throw new InvalidOperationException("POLYGON did not persist the expected regular polygon schema.");
        if (polygonGeometry.Sides != 4) throw new InvalidOperationException("Expected a four-sided regular polygon.");
        Equal(5d, polygonGeometry.Radius);
        if (polygonGeometry.Vertices.Count != 4) throw new InvalidOperationException("Polygon vertex count mismatch.");

        var revisionBeforeInvalid = document.Database.Revision;
        Fails(app.Execute("ARC 0 0 10 0 360"));
        Fails(app.Execute("POLYGON 2 0 0 10 0"));
        Fails(app.Execute("POINT NaN 0"));
        if (document.Database.Revision != revisionBeforeInvalid)
            throw new InvalidOperationException("Invalid primitive creation must not mutate the drawing.");

        Fails(app.Execute($"SETPROP {arc.Handle} {ReferencePrimitiveGeometry.ArcStartAngleDegreesKey} 20"));
        Fails(app.Execute($"SETPROP {polygon.Handle} {ReferencePrimitiveGeometry.PolygonSidesKey} 7"));
        if (document.Database.Revision != revisionBeforeInvalid)
            throw new InvalidOperationException("Reserved primitive structural property edits must not mutate the drawing.");

        var measured = app.Execute($"MEASURE {arc.Handle} {point.Handle} {polygon.Handle}");
        Succeeds(measured);
        var editor = document.Editor as InMemoryEditor ?? throw new InvalidOperationException("Expected in-memory editor.");
        if (!editor.Messages.Any(message => message.Contains($"MEASURE {arc.Handle} Arc", StringComparison.Ordinal)))
            throw new InvalidOperationException("Arc measurement output missing.");
        if (!editor.Messages.Any(message => message.Contains($"MEASURE {point.Handle} Point", StringComparison.Ordinal)))
            throw new InvalidOperationException("Point measurement output missing.");
        if (!editor.Messages.Any(message => message.Contains($"MEASURE {polygon.Handle} Polyline(regular-polygon)", StringComparison.Ordinal)))
            throw new InvalidOperationException("Polygon measurement output missing.");

        var arcEndpoint = Resolve(document, new Point3(9.8, .1), 1d, CadSnapKind.Endpoint);
        SnapEquals(arcEndpoint, arc.Handle, CadSnapKind.Endpoint, 10d, 0d);
        var arcMid = Resolve(document, new Point3(7, 7), 1d, CadSnapKind.Midpoint);
        if (arcMid.Snap?.Handle != arc.Handle || arcMid.Snap.Kind != CadSnapKind.Midpoint)
            throw new InvalidOperationException("Arc midpoint snap missing.");
        var pointSnap = Resolve(document, new Point3(5.1, 5.1), 1d, CadSnapKind.Endpoint);
        SnapEquals(pointSnap, point.Handle, CadSnapKind.Endpoint, 5d, 5d);
        var polygonVertex = polygonGeometry.Vertices[0];
        var polygonSnap = Resolve(document, new Point3(polygonVertex.X + .1, polygonVertex.Y + .1), 1d, CadSnapKind.Endpoint);
        SnapEquals(polygonSnap, polygon.Handle, CadSnapKind.Endpoint, polygonVertex.X, polygonVertex.Y);

        Succeeds(app.Execute($"MOVE {arc.Handle} {point.Handle} {polygon.Handle} 10 20"));
        (arc, point, polygon) = GetThree(document, arc.Handle, point.Handle, polygon.Handle);
        ReferencePrimitiveGeometry.TryGetArc(arc, out arcGeometry);
        ReferencePrimitiveGeometry.TryGetPoint(point, out pointGeometry);
        ReferencePrimitiveGeometry.TryGetRegularPolygon(polygon, out polygonGeometry);
        Equal(10d, arcGeometry.Center.X); Equal(20d, arcGeometry.Center.Y);
        Equal(15d, pointGeometry.X); Equal(25d, pointGeometry.Y);
        Equal(30d, polygonGeometry.Center.X); Equal(20d, polygonGeometry.Center.Y);

        Succeeds(app.Execute($"SCALE {arc.Handle} {point.Handle} {polygon.Handle} 0 0 2"));
        (arc, point, polygon) = GetThree(document, arc.Handle, point.Handle, polygon.Handle);
        ReferencePrimitiveGeometry.TryGetArc(arc, out arcGeometry);
        ReferencePrimitiveGeometry.TryGetPoint(point, out pointGeometry);
        ReferencePrimitiveGeometry.TryGetRegularPolygon(polygon, out polygonGeometry);
        Equal(20d, arcGeometry.Radius);
        Equal(30d, pointGeometry.X); Equal(50d, pointGeometry.Y);
        Equal(10d, polygonGeometry.Radius);

        Succeeds(app.Execute($"ROTATE {arc.Handle} {point.Handle} {polygon.Handle} 0 0 90"));
        (arc, point, polygon) = GetThree(document, arc.Handle, point.Handle, polygon.Handle);
        ReferencePrimitiveGeometry.TryGetArc(arc, out arcGeometry);
        ReferencePrimitiveGeometry.TryGetPoint(point, out pointGeometry);
        ReferencePrimitiveGeometry.TryGetRegularPolygon(polygon, out polygonGeometry);
        Equal(90d, arcGeometry.StartAngleDegrees);
        Equal(-50d, pointGeometry.X, 1e-8); Equal(30d, pointGeometry.Y, 1e-8);
        Equal(135d, polygonGeometry.RotationDegrees);

        var beforeMirror = Query(document).ToDictionary(static entity => entity.Handle);
        Succeeds(app.Execute($"MIRROR {arc.Handle} {point.Handle} {polygon.Handle} 0 0 10 0"));
        var afterMirror = Query(document).ToDictionary(static entity => entity.Handle);
        ReferencePrimitiveGeometry.TryGetArc(afterMirror[arc.Handle], out var mirroredArc);
        ReferencePrimitiveGeometry.TryGetPoint(afterMirror[point.Handle], out var mirroredPoint);
        ReferencePrimitiveGeometry.TryGetRegularPolygon(afterMirror[polygon.Handle], out var mirroredPolygon);
        Equal(-90d, mirroredArc.SweepAngleDegrees);
        Equal(-30d, mirroredPoint.Y, 1e-8);
        if (mirroredPolygon.Sides != 4) throw new InvalidOperationException("Polygon mirror lost its schema.");

        Succeeds(app.Execute("UNDO"));
        SamePrimitive(beforeMirror[arc.Handle], Query(document).Single(entity => entity.Handle == arc.Handle));
        Succeeds(app.Execute("REDO"));
        SamePrimitive(afterMirror[arc.Handle], Query(document).Single(entity => entity.Handle == arc.Handle));

        var countBeforeCopy = Query(document).Count;
        Succeeds(app.Execute($"COPY {arc.Handle} {point.Handle} {polygon.Handle} 3 4"));
        if (Query(document).Count != countBeforeCopy + 3)
            throw new InvalidOperationException("COPY did not clone all reference primitives.");
        var selectedCopies = document.Editor.Selection.Current.ToArray();
        if (selectedCopies.Length != 3) throw new InvalidOperationException("COPY must select the three new primitives.");
        var copiedEntities = Query(document).Where(entity => selectedCopies.Contains(entity.Handle)).ToArray();
        if (!copiedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetArc(entity, out _))
            || !copiedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetPoint(entity, out _))
            || !copiedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out _)))
            throw new InvalidOperationException("COPY lost a reference primitive schema.");

        var path = Path.Combine(Path.GetTempPath(), $"qs3d-reference-primitives-{Guid.NewGuid():N}.json");
        try
        {
            app.SaveBootstrap(path);
            var reopened = new StandaloneCadApplication();
            var loaded = reopened.OpenBootstrap(path);
            var loadedEntities = Query(loaded);
            if (!loadedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetArc(entity, out _))
                || !loadedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetPoint(entity, out _))
                || !loadedEntities.Any(entity => ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out _)))
                throw new InvalidOperationException("Bootstrap round-trip lost delivered reference primitive schemas.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        foreach (var command in new[] { "ARC", "POINT", "POLYGON" })
        {
            if (!app.Commands.Contains(command))
                throw new InvalidOperationException($"Primitive command {command} is not registered.");
        }
    }

    private static ReferencePrecisionResult Resolve(ICadDocument document, Point3 point, double aperture, CadSnapKind kinds)
        => ReferencePrecisionInput.Resolve(document, point, null, aperture, new ReferencePrecisionSettings(true, false, false, 100d, kinds));

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static (CadEntitySnapshot Arc, CadEntitySnapshot Point, CadEntitySnapshot Polygon) GetThree(
        ICadDocument document,
        QS3D.Platform.Domain.CadHandle arc,
        QS3D.Platform.Domain.CadHandle point,
        QS3D.Platform.Domain.CadHandle polygon)
    {
        var entities = Query(document);
        return (
            entities.Single(entity => entity.Handle == arc),
            entities.Single(entity => entity.Handle == point),
            entities.Single(entity => entity.Handle == polygon));
    }

    private static void SnapEquals(ReferencePrecisionResult result, QS3D.Platform.Domain.CadHandle handle, CadSnapKind kind, double x, double y)
    {
        if (result.Snap is null || result.Snap.Handle != handle || result.Snap.Kind != kind)
            throw new InvalidOperationException($"Expected {kind} snap on {handle}.");
        Equal(x, result.Point.X, 1e-8);
        Equal(y, result.Point.Y, 1e-8);
    }

    private static void SamePrimitive(CadEntitySnapshot expected, CadEntitySnapshot actual)
    {
        if (expected.Kind != actual.Kind || expected.Properties.Count != actual.Properties.Count)
            throw new InvalidOperationException("Primitive identity/schema changed unexpectedly.");
        Equal(expected.Extents.Min.X, actual.Extents.Min.X, 1e-8);
        Equal(expected.Extents.Min.Y, actual.Extents.Min.Y, 1e-8);
        Equal(expected.Extents.Max.X, actual.Extents.Max.X, 1e-8);
        Equal(expected.Extents.Max.Y, actual.Extents.Max.Y, 1e-8);
        foreach (var pair in expected.Properties)
        {
            if (!actual.Properties.TryGetValue(pair.Key, out var value) || !StringComparer.Ordinal.Equals(pair.Value, value))
                throw new InvalidOperationException($"Primitive property '{pair.Key}' changed unexpectedly.");
        }
    }

    private static void Equal(double expected, double actual, double tolerance = 1e-10)
    {
        if (!double.IsFinite(actual) || Math.Abs(expected - actual) > tolerance)
            throw new InvalidOperationException($"Expected {expected.ToString("R", CultureInfo.InvariantCulture)}, got {actual.ToString("R", CultureInfo.InvariantCulture)}.");
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
