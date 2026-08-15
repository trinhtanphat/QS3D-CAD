using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.SmokeTests;

internal static class ReferencePrecisionInputModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("precision-input-smoke");

        Succeeds(app.Execute("LINE 0 100 100 0"));
        var diagonal = Query(document).Single();
        var originalRevision = document.Database.Revision;
        document.Editor.Selection.Set(new[] { diagonal.Handle });

        var endpoint = Resolve(document, new Point3(2, 98), 10d, CadSnapKind.Endpoint);
        SnapEquals(endpoint, diagonal.Handle, CadSnapKind.Endpoint, 0d, 100d);

        var midpoint = Resolve(document, new Point3(52, 48), 10d, CadSnapKind.Midpoint);
        SnapEquals(midpoint, diagonal.Handle, CadSnapKind.Midpoint, 50d, 50d);

        Succeeds(app.Execute("CIRCLE 300 200 50"));
        var circle = Query(document).Single(static entity => entity.Kind == CadEntityKind.Circle);
        var center = Resolve(document, new Point3(303, 198), 10d, CadSnapKind.Center);
        SnapEquals(center, circle.Handle, CadSnapKind.Center, 300d, 200d);
        var quadrant = Resolve(document, new Point3(348, 201), 10d, CadSnapKind.Quadrant);
        SnapEquals(quadrant, circle.Handle, CadSnapKind.Quadrant, 350d, 200d);

        Succeeds(app.Execute("RECTANG 400 400 500 460"));
        var rectangle = Query(document).Single(static entity => entity.Kind == CadEntityKind.Polyline);
        var rectangleCorner = Resolve(document, new Point3(402, 458), 10d, CadSnapKind.Endpoint);
        SnapEquals(rectangleCorner, rectangle.Handle, CadSnapKind.Endpoint, 400d, 460d);

        Succeeds(app.Execute("LINE 0 0 100 0"));
        Succeeds(app.Execute("LINE 25 -100 25 100"));
        var intersection = Resolve(document, new Point3(26, 1), 10d, CadSnapKind.Intersection);
        if (intersection.Snap?.Kind != CadSnapKind.Intersection)
            throw new InvalidOperationException("Expected a line-line intersection snap.");
        Equal(25d, intersection.Point.X);
        Equal(0d, intersection.Point.Y);

        var horizontal = Query(document).Single(entity => entity.Kind == CadEntityKind.Line
            && Number(entity, "x1") == 0d && Number(entity, "y1") == 0d
            && Number(entity, "x2") == 100d && Number(entity, "y2") == 0d);
        var nearest = Resolve(document, new Point3(40, 7), 10d, CadSnapKind.Nearest);
        SnapEquals(nearest, horizontal.Handle, CadSnapKind.Nearest, 40d, 0d);

        var priority = ReferencePrecisionInput.Resolve(
            document,
            new Point3(2, 98),
            new Point3(50, 50),
            10d,
            new ReferencePrecisionSettings(
                ObjectSnapEnabled: true,
                OrthoEnabled: true,
                GridSnapEnabled: true,
                GridSpacing: 25d,
                SnapKinds: CadSnapKind.Endpoint));
        SnapEquals(priority, diagonal.Handle, CadSnapKind.Endpoint, 0d, 100d);
        if (priority.GridApplied || priority.OrthoApplied)
            throw new InvalidOperationException("Exact object snap must win over grid and ORTHO constraints.");

        var gridOrtho = ReferencePrecisionInput.Resolve(
            document,
            new Point3(133, 176),
            new Point3(100, 100),
            0d,
            new ReferencePrecisionSettings(
                ObjectSnapEnabled: false,
                OrthoEnabled: true,
                GridSnapEnabled: true,
                GridSpacing: 50d,
                SnapKinds: CadSnapKind.None));
        Equal(100d, gridOrtho.Point.X);
        Equal(200d, gridOrtho.Point.Y);
        if (!gridOrtho.GridApplied || !gridOrtho.OrthoApplied || gridOrtho.Snap is not null)
            throw new InvalidOperationException("Grid+ORTHO resolution flags are incorrect.");

        var horizontalOrtho = ReferencePrecisionInput.Resolve(
            document,
            new Point3(180, 120),
            new Point3(100, 100),
            0d,
            new ReferencePrecisionSettings(false, true, false, 50d, CadSnapKind.None));
        Equal(180d, horizontalOrtho.Point.X);
        Equal(100d, horizontalOrtho.Point.Y);

        Throws<ArgumentOutOfRangeException>(() => ReferencePrecisionInput.Resolve(
            document,
            new Point3(1, 1),
            null,
            1d,
            new ReferencePrecisionSettings(false, false, true, 0d, CadSnapKind.None)));
        Throws<ArgumentOutOfRangeException>(() => Resolve(document, new Point3(1, 1), 1d, CadSnapKind.Tangent));
        Throws<OverflowException>(() => ReferencePrecisionInput.Resolve(
            document,
            new Point3(1e308, 1e308),
            null,
            0d,
            new ReferencePrecisionSettings(false, false, true, 1e-308, CadSnapKind.None)));

        if (document.Database.Revision != originalRevision + 4)
            throw new InvalidOperationException("Precision resolution must not mutate the drawing revision.");
        var selection = document.Editor.Selection.Current.ToArray();
        if (selection.Length != 1 || selection[0] != diagonal.Handle)
            throw new InvalidOperationException("Precision resolution must not mutate editor selection.");
    }

    private static ReferencePrecisionResult Resolve(ICadDocument document, Point3 point, double aperture, CadSnapKind kinds)
        => ReferencePrecisionInput.Resolve(
            document,
            point,
            null,
            aperture,
            new ReferencePrecisionSettings(true, false, false, 100d, kinds));

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static double Number(CadEntitySnapshot entity, string key)
    {
        if (!entity.Properties.TryGetValue(key, out var raw)
            || !double.TryParse(raw, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out var value))
            throw new InvalidOperationException($"Missing numeric property {key}.");
        return value;
    }

    private static void SnapEquals(ReferencePrecisionResult result, QS3D.Platform.Domain.CadHandle handle, CadSnapKind kind, double x, double y)
    {
        if (result.Snap is null || result.Snap.Handle != handle || result.Snap.Kind != kind)
            throw new InvalidOperationException($"Expected {kind} snap on {handle}.");
        Equal(x, result.Point.X);
        Equal(y, result.Point.Y);
    }

    private static void Equal(double expected, double actual)
    {
        if (Math.Abs(expected - actual) > 1e-9)
            throw new InvalidOperationException($"Expected {expected:R}, got {actual:R}.");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }

    private static void Throws<TException>(Action action) where TException : Exception
    {
        try
        {
            action();
        }
        catch (TException)
        {
            return;
        }
        throw new InvalidOperationException($"Expected {typeof(TException).Name}.");
    }
}
