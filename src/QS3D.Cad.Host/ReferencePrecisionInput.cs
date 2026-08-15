using System.Globalization;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

public sealed record ReferencePrecisionSettings(
    bool ObjectSnapEnabled = true,
    bool OrthoEnabled = false,
    bool GridSnapEnabled = false,
    double GridSpacing = 100d,
    CadSnapKind SnapKinds = ReferencePrecisionInput.DefaultSnapKinds);

public sealed record ReferencePrecisionSnap(
    CadHandle Handle,
    CadSnapKind Kind,
    Point3 Point,
    double DistanceWorld);

public sealed record ReferencePrecisionResult(
    Point3 Point,
    ReferencePrecisionSnap? Snap,
    bool GridApplied,
    bool OrthoApplied);

public static class ReferencePrecisionInput
{
    public const CadSnapKind DefaultSnapKinds = CadSnapKind.Endpoint
        | CadSnapKind.Midpoint
        | CadSnapKind.Center
        | CadSnapKind.Intersection
        | CadSnapKind.Nearest
        | CadSnapKind.Quadrant;

    private const CadSnapKind SupportedSnapKinds = DefaultSnapKinds;

    public static ReferencePrecisionResult Resolve(
        ICadDocument document,
        Point3 rawPoint,
        Point3? anchor,
        double apertureWorldUnits,
        ReferencePrecisionSettings? settings = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        settings ??= new ReferencePrecisionSettings();
        RequireFinite(rawPoint, nameof(rawPoint));
        if (!double.IsFinite(apertureWorldUnits) || apertureWorldUnits < 0d)
            throw new ArgumentOutOfRangeException(nameof(apertureWorldUnits), "Snap aperture must be finite and non-negative.");
        if ((settings.SnapKinds & ~SupportedSnapKinds) != 0)
            throw new ArgumentOutOfRangeException(nameof(settings), settings.SnapKinds, "Reference precision input contains unsupported snap kinds.");
        if (anchor is not null)
            RequireFinite(anchor.Value, nameof(anchor));

        if (settings.ObjectSnapEnabled && settings.SnapKinds != CadSnapKind.None)
        {
            var snap = FindSnap(document, rawPoint, apertureWorldUnits, settings.SnapKinds);
            if (snap is not null)
                return new ReferencePrecisionResult(snap.Point, snap, GridApplied: false, OrthoApplied: false);
        }

        var point = rawPoint;
        var gridApplied = false;
        if (settings.GridSnapEnabled)
        {
            point = SnapToGrid(point, settings.GridSpacing);
            gridApplied = true;
        }

        var orthoApplied = false;
        if (settings.OrthoEnabled && anchor is not null)
        {
            point = ConstrainOrtho(anchor.Value, point);
            orthoApplied = true;
        }

        return new ReferencePrecisionResult(point, null, gridApplied, orthoApplied);
    }

    public static double Distance2D(Point3 first, Point3 second)
    {
        RequireFinite(first, nameof(first));
        RequireFinite(second, nameof(second));
        var scale = Math.Max(
            Math.Max(Math.Abs(first.X), Math.Abs(first.Y)),
            Math.Max(Math.Abs(second.X), Math.Abs(second.Y)));
        if (scale == 0d) return 0d;
        var dx = (second.X / scale) - (first.X / scale);
        var dy = (second.Y / scale) - (first.Y / scale);
        return scale * Math.Sqrt((dx * dx) + (dy * dy));
    }

    private static ReferencePrecisionSnap? FindSnap(
        ICadDocument document,
        Point3 rawPoint,
        double aperture,
        CadSnapKind enabledKinds)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var entities = tx.Query().ToArray();
        var candidates = new List<ReferencePrecisionSnap>();

        foreach (var entity in entities)
            AddEntityCandidates(candidates, entity, rawPoint, aperture, enabledKinds);

        if ((enabledKinds & CadSnapKind.Intersection) != 0)
        {
            var lines = entities
                .Where(static entity => entity.Kind == CadEntityKind.Line)
                .Where(entity => IsNearBounds(entity.Extents, rawPoint, aperture))
                .Select(static entity => TryLine(entity, out var line)
                    ? (Entity: entity, Line: line, Valid: true)
                    : (Entity: entity, Line: default(LineGeometry), Valid: false))
                .Where(static item => item.Valid)
                .ToArray();
            for (var left = 0; left < lines.Length; left++)
            {
                for (var right = left + 1; right < lines.Length; right++)
                {
                    if (!TryIntersection(lines[left].Line, lines[right].Line, out var point)) continue;
                    AddCandidate(candidates, lines[left].Entity.Handle, CadSnapKind.Intersection, point, rawPoint, aperture);
                }
            }
        }

        return candidates
            .OrderBy(static candidate => candidate.DistanceWorld)
            .ThenBy(static candidate => SnapPriority(candidate.Kind))
            .ThenBy(static candidate => candidate.Handle.Value, StringComparer.Ordinal)
            .ThenBy(static candidate => candidate.Point.X)
            .ThenBy(static candidate => candidate.Point.Y)
            .FirstOrDefault();
    }

    private static void AddEntityCandidates(
        ICollection<ReferencePrecisionSnap> output,
        CadEntitySnapshot entity,
        Point3 rawPoint,
        double aperture,
        CadSnapKind enabledKinds)
    {
        if (!IsNearBounds(entity.Extents, rawPoint, aperture)) return;

        if (entity.Kind == CadEntityKind.Line && TryLine(entity, out var line))
        {
            if ((enabledKinds & CadSnapKind.Endpoint) != 0)
            {
                AddCandidate(output, entity.Handle, CadSnapKind.Endpoint, line.Start, rawPoint, aperture);
                AddCandidate(output, entity.Handle, CadSnapKind.Endpoint, line.End, rawPoint, aperture);
            }
            if ((enabledKinds & CadSnapKind.Midpoint) != 0)
                AddCandidate(output, entity.Handle, CadSnapKind.Midpoint, Midpoint(line.Start, line.End), rawPoint, aperture);
            if ((enabledKinds & CadSnapKind.Nearest) != 0 && TryClosestPointOnSegment(line.Start, line.End, rawPoint, out var nearest))
                AddCandidate(output, entity.Handle, CadSnapKind.Nearest, nearest, rawPoint, aperture);
            return;
        }

        if (entity.Kind == CadEntityKind.Circle && TryCircle(entity, out var circle))
        {
            if ((enabledKinds & CadSnapKind.Center) != 0)
                AddCandidate(output, entity.Handle, CadSnapKind.Center, circle.Center, rawPoint, aperture);
            if ((enabledKinds & CadSnapKind.Quadrant) != 0)
            {
                if (TryOffsetPoint(circle.Center, circle.Radius, 0d, out var right))
                    AddCandidate(output, entity.Handle, CadSnapKind.Quadrant, right, rawPoint, aperture);
                if (TryOffsetPoint(circle.Center, -circle.Radius, 0d, out var left))
                    AddCandidate(output, entity.Handle, CadSnapKind.Quadrant, left, rawPoint, aperture);
                if (TryOffsetPoint(circle.Center, 0d, circle.Radius, out var top))
                    AddCandidate(output, entity.Handle, CadSnapKind.Quadrant, top, rawPoint, aperture);
                if (TryOffsetPoint(circle.Center, 0d, -circle.Radius, out var bottom))
                    AddCandidate(output, entity.Handle, CadSnapKind.Quadrant, bottom, rawPoint, aperture);
            }
            if ((enabledKinds & CadSnapKind.Nearest) != 0 && TryClosestPointOnCircle(circle, rawPoint, out var nearest))
                AddCandidate(output, entity.Handle, CadSnapKind.Nearest, nearest, rawPoint, aperture);
            return;
        }

        if (entity.Kind == CadEntityKind.Polyline && TryRectangle(entity, out var rectangle))
        {
            var corners = rectangle.Corners;
            if ((enabledKinds & CadSnapKind.Endpoint) != 0)
            {
                foreach (var corner in corners)
                    AddCandidate(output, entity.Handle, CadSnapKind.Endpoint, corner, rawPoint, aperture);
            }
            if ((enabledKinds & CadSnapKind.Midpoint) != 0)
            {
                for (var index = 0; index < corners.Length; index++)
                    AddCandidate(output, entity.Handle, CadSnapKind.Midpoint, Midpoint(corners[index], corners[(index + 1) % corners.Length]), rawPoint, aperture);
            }
            if ((enabledKinds & CadSnapKind.Nearest) != 0)
            {
                for (var index = 0; index < corners.Length; index++)
                {
                    if (TryClosestPointOnSegment(corners[index], corners[(index + 1) % corners.Length], rawPoint, out var nearest))
                        AddCandidate(output, entity.Handle, CadSnapKind.Nearest, nearest, rawPoint, aperture);
                }
            }
        }
    }

    private static void AddCandidate(
        ICollection<ReferencePrecisionSnap> output,
        CadHandle handle,
        CadSnapKind kind,
        Point3 point,
        Point3 rawPoint,
        double aperture)
    {
        if (!IsFinite(point)) return;
        var distance = Distance2D(rawPoint, point);
        if (double.IsFinite(distance) && distance <= aperture)
            output.Add(new ReferencePrecisionSnap(handle, kind, point, distance));
    }

    private static bool IsNearBounds(BoundingBox3 bounds, Point3 point, double aperture)
    {
        if (!IsFinite(bounds.Min) || !IsFinite(bounds.Max)) return false;
        var nearest = new Point3(
            Math.Max(bounds.Min.X, Math.Min(bounds.Max.X, point.X)),
            Math.Max(bounds.Min.Y, Math.Min(bounds.Max.Y, point.Y)),
            Math.Max(bounds.Min.Z, Math.Min(bounds.Max.Z, point.Z)));
        var distance = Distance2D(point, nearest);
        return double.IsFinite(distance) && distance <= aperture;
    }

    private static Point3 SnapToGrid(Point3 point, double spacing)
    {
        if (!double.IsFinite(spacing) || spacing <= 0d)
            throw new ArgumentOutOfRangeException(nameof(spacing), "Grid spacing must be positive and finite.");
        return new Point3(Quantize(point.X, spacing, "X"), Quantize(point.Y, spacing, "Y"), point.Z);
    }

    private static double Quantize(double value, double spacing, string axis)
    {
        var quotient = value / spacing;
        if (!double.IsFinite(quotient))
            throw new OverflowException($"Grid snap {axis} quotient exceeds the finite numeric range.");
        var snapped = Math.Round(quotient, MidpointRounding.AwayFromZero) * spacing;
        if (!double.IsFinite(snapped))
            throw new OverflowException($"Grid snap {axis} coordinate exceeds the finite numeric range.");
        return snapped;
    }

    private static Point3 ConstrainOrtho(Point3 anchor, Point3 candidate)
    {
        var scale = Math.Max(
            Math.Max(Math.Abs(anchor.X), Math.Abs(anchor.Y)),
            Math.Max(Math.Abs(candidate.X), Math.Abs(candidate.Y)));
        if (scale == 0d) return candidate;
        var dx = (candidate.X / scale) - (anchor.X / scale);
        var dy = (candidate.Y / scale) - (anchor.Y / scale);
        if (!double.IsFinite(dx) || !double.IsFinite(dy))
            throw new OverflowException("ORTHO delta exceeds the finite numeric range.");
        return Math.Abs(dx) >= Math.Abs(dy)
            ? new Point3(candidate.X, anchor.Y, candidate.Z)
            : new Point3(anchor.X, candidate.Y, candidate.Z);
    }

    private static bool TryLine(CadEntitySnapshot entity, out LineGeometry line)
    {
        if (TryProperty(entity, "x1", out var x1)
            && TryProperty(entity, "y1", out var y1)
            && TryProperty(entity, "x2", out var x2)
            && TryProperty(entity, "y2", out var y2))
        {
            line = new LineGeometry(new Point3(x1, y1), new Point3(x2, y2));
            return true;
        }
        line = default;
        return false;
    }

    private static bool TryCircle(CadEntitySnapshot entity, out CircleGeometry circle)
    {
        if (TryProperty(entity, "cx", out var cx)
            && TryProperty(entity, "cy", out var cy)
            && TryProperty(entity, "radius", out var radius)
            && radius > 0d)
        {
            circle = new CircleGeometry(new Point3(cx, cy), radius);
            return true;
        }
        circle = default;
        return false;
    }

    private static bool TryRectangle(CadEntitySnapshot entity, out RectangleGeometry rectangle)
    {
        if (TryProperty(entity, "x1", out var x1)
            && TryProperty(entity, "y1", out var y1)
            && TryProperty(entity, "x2", out var x2)
            && TryProperty(entity, "y2", out var y2))
        {
            rectangle = RectangleGeometry.Create(x1, y1, x2, y2);
            return true;
        }
        rectangle = default;
        return false;
    }

    private static bool TryProperty(CadEntitySnapshot entity, string key, out double value)
    {
        value = default;
        return entity.Properties.TryGetValue(key, out var raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    private static bool TryClosestPointOnSegment(Point3 start, Point3 end, Point3 point, out Point3 nearest)
    {
        var scale = MaxAbs(start.X, start.Y, end.X, end.Y, point.X, point.Y);
        if (!double.IsFinite(scale))
        {
            nearest = default;
            return false;
        }
        if (scale == 0d)
        {
            nearest = start;
            return true;
        }
        var ax = start.X / scale;
        var ay = start.Y / scale;
        var bx = end.X / scale;
        var by = end.Y / scale;
        var px = point.X / scale;
        var py = point.Y / scale;
        var dx = bx - ax;
        var dy = by - ay;
        var lengthSquared = (dx * dx) + (dy * dy);
        if (!double.IsFinite(lengthSquared) || lengthSquared <= 0d)
        {
            nearest = start;
            return true;
        }
        var t = (((px - ax) * dx) + ((py - ay) * dy)) / lengthSquared;
        t = Math.Max(0d, Math.Min(1d, t));
        var x = (ax + (t * dx)) * scale;
        var y = (ay + (t * dy)) * scale;
        nearest = new Point3(x, y, Midpoint(start.Z, end.Z));
        return IsFinite(nearest);
    }

    private static bool TryClosestPointOnCircle(CircleGeometry circle, Point3 point, out Point3 nearest)
    {
        var scale = MaxAbs(circle.Center.X, circle.Center.Y, point.X, point.Y, circle.Radius);
        if (!double.IsFinite(scale) || scale == 0d)
        {
            nearest = default;
            return false;
        }
        var dx = (point.X / scale) - (circle.Center.X / scale);
        var dy = (point.Y / scale) - (circle.Center.Y / scale);
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (!double.IsFinite(length))
        {
            nearest = default;
            return false;
        }
        if (length <= 1e-15)
            return TryOffsetPoint(circle.Center, circle.Radius, 0d, out nearest);
        var radius = circle.Radius / scale;
        var x = (circle.Center.X / scale + (dx / length * radius)) * scale;
        var y = (circle.Center.Y / scale + (dy / length * radius)) * scale;
        nearest = new Point3(x, y, circle.Center.Z);
        return IsFinite(nearest);
    }

    private static bool TryIntersection(LineGeometry first, LineGeometry second, out Point3 point)
    {
        var scale = MaxAbs(
            first.Start.X, first.Start.Y, first.End.X, first.End.Y,
            second.Start.X, second.Start.Y, second.End.X, second.End.Y);
        if (!double.IsFinite(scale) || scale == 0d)
        {
            point = default;
            return false;
        }

        var px = first.Start.X / scale;
        var py = first.Start.Y / scale;
        var rx = (first.End.X / scale) - px;
        var ry = (first.End.Y / scale) - py;
        var qx = second.Start.X / scale;
        var qy = second.Start.Y / scale;
        var sx = (second.End.X / scale) - qx;
        var sy = (second.End.Y / scale) - qy;
        var denominator = Cross(rx, ry, sx, sy);
        if (!double.IsFinite(denominator) || Math.Abs(denominator) <= 1e-15)
        {
            point = default;
            return false;
        }
        var qpx = qx - px;
        var qpy = qy - py;
        var t = Cross(qpx, qpy, sx, sy) / denominator;
        var u = Cross(qpx, qpy, rx, ry) / denominator;
        const double tolerance = 1e-12;
        if (t < -tolerance || t > 1d + tolerance || u < -tolerance || u > 1d + tolerance)
        {
            point = default;
            return false;
        }
        point = new Point3((px + (t * rx)) * scale, (py + (t * ry)) * scale, 0d);
        return IsFinite(point);
    }

    private static bool TryOffsetPoint(Point3 origin, double dx, double dy, out Point3 point)
    {
        var x = origin.X + dx;
        var y = origin.Y + dy;
        if (!double.IsFinite(x) || !double.IsFinite(y))
        {
            point = default;
            return false;
        }
        point = new Point3(x, y, origin.Z);
        return IsFinite(point);
    }

    private static Point3 Midpoint(Point3 first, Point3 second)
        => new(Midpoint(first.X, second.X), Midpoint(first.Y, second.Y), Midpoint(first.Z, second.Z));

    private static double Midpoint(double first, double second)
        => (first * 0.5d) + (second * 0.5d);

    private static double Cross(double ax, double ay, double bx, double by)
        => (ax * by) - (ay * bx);

    private static int SnapPriority(CadSnapKind kind) => kind switch
    {
        CadSnapKind.Endpoint => 0,
        CadSnapKind.Intersection => 1,
        CadSnapKind.Midpoint => 2,
        CadSnapKind.Center => 3,
        CadSnapKind.Quadrant => 4,
        CadSnapKind.Nearest => 5,
        _ => 99
    };

    private static double MaxAbs(params double[] values)
    {
        var result = 0d;
        foreach (var value in values)
        {
            if (!double.IsFinite(value)) return double.PositiveInfinity;
            result = Math.Max(result, Math.Abs(value));
        }
        return result;
    }

    private static bool IsFinite(Point3 point)
        => double.IsFinite(point.X) && double.IsFinite(point.Y) && double.IsFinite(point.Z);

    private static void RequireFinite(Point3 point, string parameterName)
    {
        if (!IsFinite(point))
            throw new ArgumentOutOfRangeException(parameterName, "Precision point coordinates must be finite.");
    }

    private readonly record struct LineGeometry(Point3 Start, Point3 End);
    private readonly record struct CircleGeometry(Point3 Center, double Radius);
    private readonly record struct RectangleGeometry(Point3[] Corners)
    {
        public static RectangleGeometry Create(double x1, double y1, double x2, double y2)
        {
            var minX = Math.Min(x1, x2);
            var maxX = Math.Max(x1, x2);
            var minY = Math.Min(y1, y2);
            var maxY = Math.Max(y1, y2);
            return new RectangleGeometry(new[]
            {
                new Point3(minX, minY),
                new Point3(maxX, minY),
                new Point3(maxX, maxY),
                new Point3(minX, maxY)
            });
        }
    }
}
