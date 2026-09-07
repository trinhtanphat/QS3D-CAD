using System.Globalization;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

public readonly record struct ReferenceArc(Point3 Center, double Radius, double StartAngleDegrees, double SweepAngleDegrees)
{
    public Point3 StartPoint => ReferencePrimitiveGeometry.PolarPoint(Center, Radius, StartAngleDegrees);
    public Point3 EndPoint => ReferencePrimitiveGeometry.PolarPoint(Center, Radius, StartAngleDegrees + SweepAngleDegrees);
    public Point3 MidPoint => ReferencePrimitiveGeometry.PolarPoint(Center, Radius, StartAngleDegrees + SweepAngleDegrees * .5d);
}

public readonly record struct ReferenceRegularPolygon(Point3 Center, double Radius, int Sides, double RotationDegrees)
{
    public IReadOnlyList<Point3> Vertices => ReferencePrimitiveGeometry.PolygonVertices(this);
}

public static class ReferencePrimitiveGeometry
{
    public const string ArcStartAngleDegreesKey = "QS3D.Arc.StartAngleDegrees";
    public const string ArcSweepAngleDegreesKey = "QS3D.Arc.SweepAngleDegrees";
    public const string ReferenceShapeKey = "QS3D.ReferenceShape";
    public const string RegularPolygonShape = "RegularPolygon";
    public const string PolygonSidesKey = "QS3D.Polygon.Sides";
    public const string PolygonRotationDegreesKey = "QS3D.Polygon.RotationDegrees";

    public static CadEntityDraft CreateArcDraft(double cx, double cy, double radius, double startAngleDegrees, double sweepAngleDegrees)
    {
        RequireFinite(cx, nameof(cx));
        RequireFinite(cy, nameof(cy));
        RequireFinite(radius, nameof(radius));
        RequireFinite(startAngleDegrees, nameof(startAngleDegrees));
        RequireFinite(sweepAngleDegrees, nameof(sweepAngleDegrees));
        if (radius <= 0d) throw new ArgumentOutOfRangeException(nameof(radius), "Arc radius must be greater than zero.");
        if (Math.Abs(sweepAngleDegrees) <= 1e-12d || Math.Abs(sweepAngleDegrees) >= 360d)
            throw new ArgumentOutOfRangeException(nameof(sweepAngleDegrees), "Arc sweep must be non-zero and strictly less than 360 degrees; use CIRCLE for a full revolution.");
        var arc = new ReferenceArc(new Point3(cx, cy), radius, NormalizeAngle(startAngleDegrees), sweepAngleDegrees);
        return new CadEntityDraft(CadEntityKind.Arc, ArcExtents(arc), new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["cx"] = F(cx),
            ["cy"] = F(cy),
            ["radius"] = F(radius),
            [ArcStartAngleDegreesKey] = F(arc.StartAngleDegrees),
            [ArcSweepAngleDegreesKey] = F(sweepAngleDegrees)
        });
    }

    public static CadEntityDraft CreatePointDraft(double x, double y)
    {
        RequireFinite(x, nameof(x));
        RequireFinite(y, nameof(y));
        var point = new Point3(x, y);
        return new CadEntityDraft(CadEntityKind.Point, BoundingBox3.FromPoints(point, point), new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["x1"] = F(x),
            ["y1"] = F(y)
        });
    }

    public static CadEntityDraft CreateRegularPolygonDraft(int sides, double cx, double cy, double radius, double rotationDegrees)
    {
        if (sides < 3 || sides > 1024) throw new ArgumentOutOfRangeException(nameof(sides), "Polygon sides must be between 3 and 1024.");
        RequireFinite(cx, nameof(cx));
        RequireFinite(cy, nameof(cy));
        RequireFinite(radius, nameof(radius));
        RequireFinite(rotationDegrees, nameof(rotationDegrees));
        if (radius <= 0d) throw new ArgumentOutOfRangeException(nameof(radius), "Polygon radius must be greater than zero.");
        var polygon = new ReferenceRegularPolygon(new Point3(cx, cy), radius, sides, NormalizeAngle(rotationDegrees));
        return new CadEntityDraft(CadEntityKind.Polyline, PolygonExtents(polygon), new Dictionary<string, string>(StringComparer.Ordinal)
        {
            [ReferenceShapeKey] = RegularPolygonShape,
            ["cx"] = F(cx),
            ["cy"] = F(cy),
            ["radius"] = F(radius),
            [PolygonSidesKey] = sides.ToString(CultureInfo.InvariantCulture),
            [PolygonRotationDegreesKey] = F(polygon.RotationDegrees)
        });
    }

    public static bool TryGetArc(CadEntitySnapshot entity, out ReferenceArc arc)
    {
        arc = default;
        if (entity.Kind != CadEntityKind.Arc
            || !TryNumber(entity, "cx", out var cx)
            || !TryNumber(entity, "cy", out var cy)
            || !TryNumber(entity, "radius", out var radius)
            || !TryNumber(entity, ArcStartAngleDegreesKey, out var start)
            || !TryNumber(entity, ArcSweepAngleDegreesKey, out var sweep)
            || radius <= 0d
            || Math.Abs(sweep) <= 1e-12d
            || Math.Abs(sweep) >= 360d)
            return false;
        arc = new ReferenceArc(new Point3(cx, cy), radius, NormalizeAngle(start), sweep);
        return true;
    }

    public static bool TryGetPoint(CadEntitySnapshot entity, out Point3 point)
    {
        point = default;
        if (entity.Kind != CadEntityKind.Point
            || !TryNumber(entity, "x1", out var x)
            || !TryNumber(entity, "y1", out var y))
            return false;
        point = new Point3(x, y);
        return true;
    }

    public static bool TryGetRegularPolygon(CadEntitySnapshot entity, out ReferenceRegularPolygon polygon)
    {
        polygon = default;
        if (entity.Kind != CadEntityKind.Polyline
            || !entity.Properties.TryGetValue(ReferenceShapeKey, out var shape)
            || !StringComparer.Ordinal.Equals(shape, RegularPolygonShape)
            || !TryNumber(entity, "cx", out var cx)
            || !TryNumber(entity, "cy", out var cy)
            || !TryNumber(entity, "radius", out var radius)
            || !TryInteger(entity, PolygonSidesKey, out var sides)
            || !TryNumber(entity, PolygonRotationDegreesKey, out var rotation)
            || radius <= 0d
            || sides < 3
            || sides > 1024)
            return false;
        polygon = new ReferenceRegularPolygon(new Point3(cx, cy), radius, sides, NormalizeAngle(rotation));
        return true;
    }

    public static IReadOnlyList<Point3> PolygonVertices(ReferenceRegularPolygon polygon)
    {
        var vertices = new Point3[polygon.Sides];
        var step = 360d / polygon.Sides;
        for (var index = 0; index < vertices.Length; index++)
            vertices[index] = PolarPoint(polygon.Center, polygon.Radius, polygon.RotationDegrees + step * index);
        return vertices;
    }

    public static Point3 PolarPoint(Point3 center, double radius, double angleDegrees)
    {
        RequireFinite(center.X, nameof(center));
        RequireFinite(center.Y, nameof(center));
        RequireFinite(radius, nameof(radius));
        RequireFinite(angleDegrees, nameof(angleDegrees));
        var radians = angleDegrees * Math.PI / 180d;
        var x = center.X + radius * Math.Cos(radians);
        var y = center.Y + radius * Math.Sin(radians);
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new OverflowException("Primitive coordinate exceeds the finite numeric range.");
        return new Point3(x, y, center.Z);
    }

    public static BoundingBox3 ArcExtents(ReferenceArc arc)
    {
        var points = new List<Point3> { arc.StartPoint, arc.EndPoint };
        foreach (var cardinal in new[] { 0d, 90d, 180d, 270d })
        {
            if (AngleOnSweep(cardinal, arc.StartAngleDegrees, arc.SweepAngleDegrees))
                points.Add(PolarPoint(arc.Center, arc.Radius, cardinal));
        }
        return Bounds(points);
    }

    public static BoundingBox3 PolygonExtents(ReferenceRegularPolygon polygon) => Bounds(PolygonVertices(polygon));

    public static bool AngleOnSweep(double angleDegrees, double startAngleDegrees, double sweepAngleDegrees)
    {
        var start = NormalizeAngle(startAngleDegrees);
        var target = NormalizeAngle(angleDegrees);
        if (sweepAngleDegrees > 0d)
            return NormalizeAngle(target - start) <= sweepAngleDegrees + 1e-10d;
        return NormalizeAngle(start - target) <= -sweepAngleDegrees + 1e-10d;
    }

    public static Point3 ClosestPointOnArc(ReferenceArc arc, Point3 raw)
    {
        RequireFinite(raw.X, nameof(raw));
        RequireFinite(raw.Y, nameof(raw));
        var dx = raw.X - arc.Center.X;
        var dy = raw.Y - arc.Center.Y;
        if (!double.IsFinite(dx) || !double.IsFinite(dy)) throw new OverflowException("Arc nearest-point delta exceeds the finite numeric range.");
        if (dx == 0d && dy == 0d) return arc.StartPoint;
        var angle = Math.Atan2(dy, dx) * 180d / Math.PI;
        if (AngleOnSweep(angle, arc.StartAngleDegrees, arc.SweepAngleDegrees))
            return PolarPoint(arc.Center, arc.Radius, angle);
        var start = arc.StartPoint;
        var end = arc.EndPoint;
        return Distance2D(raw, start) <= Distance2D(raw, end) ? start : end;
    }

    public static bool TryScale(CadEntitySnapshot entity, double baseX, double baseY, double factor, out CadEntitySnapshot scaled)
    {
        scaled = entity;
        if (TryGetArc(entity, out var arc))
        {
            var center = ScalePoint(arc.Center, baseX, baseY, factor);
            var radius = Finite(arc.Radius * factor, "Arc scaled radius");
            scaled = Snapshot(entity, CreateArcDraft(center.X, center.Y, radius, arc.StartAngleDegrees, arc.SweepAngleDegrees));
            return true;
        }
        if (TryGetPoint(entity, out var point))
        {
            var result = ScalePoint(point, baseX, baseY, factor);
            scaled = Snapshot(entity, CreatePointDraft(result.X, result.Y));
            return true;
        }
        if (TryGetRegularPolygon(entity, out var polygon))
        {
            var center = ScalePoint(polygon.Center, baseX, baseY, factor);
            var radius = Finite(polygon.Radius * factor, "Polygon scaled radius");
            scaled = Snapshot(entity, CreateRegularPolygonDraft(polygon.Sides, center.X, center.Y, radius, polygon.RotationDegrees));
            return true;
        }
        return false;
    }

    public static bool TryRotate(CadEntitySnapshot entity, double baseX, double baseY, double angleDegrees, out CadEntitySnapshot rotated)
    {
        rotated = entity;
        if (TryGetArc(entity, out var arc))
        {
            var center = RotatePoint(arc.Center, baseX, baseY, angleDegrees);
            rotated = Snapshot(entity, CreateArcDraft(center.X, center.Y, arc.Radius, arc.StartAngleDegrees + angleDegrees, arc.SweepAngleDegrees));
            return true;
        }
        if (TryGetPoint(entity, out var point))
        {
            var result = RotatePoint(point, baseX, baseY, angleDegrees);
            rotated = Snapshot(entity, CreatePointDraft(result.X, result.Y));
            return true;
        }
        if (TryGetRegularPolygon(entity, out var polygon))
        {
            var center = RotatePoint(polygon.Center, baseX, baseY, angleDegrees);
            rotated = Snapshot(entity, CreateRegularPolygonDraft(polygon.Sides, center.X, center.Y, polygon.Radius, polygon.RotationDegrees + angleDegrees));
            return true;
        }
        return false;
    }

    public static bool TryMirror(CadEntitySnapshot entity, double axisX, double axisY, double ux, double uy, out CadEntitySnapshot mirrored)
    {
        mirrored = entity;
        if (TryGetArc(entity, out var arc))
        {
            var center = MirrorPoint(arc.Center, axisX, axisY, ux, uy);
            var start = MirrorPoint(arc.StartPoint, axisX, axisY, ux, uy);
            var startAngle = Math.Atan2(start.Y - center.Y, start.X - center.X) * 180d / Math.PI;
            mirrored = Snapshot(entity, CreateArcDraft(center.X, center.Y, arc.Radius, startAngle, -arc.SweepAngleDegrees));
            return true;
        }
        if (TryGetPoint(entity, out var point))
        {
            var result = MirrorPoint(point, axisX, axisY, ux, uy);
            mirrored = Snapshot(entity, CreatePointDraft(result.X, result.Y));
            return true;
        }
        if (TryGetRegularPolygon(entity, out var polygon))
        {
            var center = MirrorPoint(polygon.Center, axisX, axisY, ux, uy);
            var first = MirrorPoint(PolygonVertices(polygon)[0], axisX, axisY, ux, uy);
            var rotation = Math.Atan2(first.Y - center.Y, first.X - center.X) * 180d / Math.PI;
            mirrored = Snapshot(entity, CreateRegularPolygonDraft(polygon.Sides, center.X, center.Y, polygon.Radius, rotation));
            return true;
        }
        return false;
    }

    public static double Distance2D(Point3 first, Point3 second)
    {
        var scale = Math.Max(Math.Max(Math.Abs(first.X), Math.Abs(first.Y)), Math.Max(Math.Abs(second.X), Math.Abs(second.Y)));
        if (scale == 0d) return 0d;
        var dx = second.X / scale - first.X / scale;
        var dy = second.Y / scale - first.Y / scale;
        return scale * Math.Sqrt(dx * dx + dy * dy);
    }

    public static double NormalizeAngle(double degrees)
    {
        RequireFinite(degrees, nameof(degrees));
        var result = degrees % 360d;
        if (result < 0d) result += 360d;
        return result == 360d ? 0d : result;
    }

    private static CadEntitySnapshot Snapshot(CadEntitySnapshot original, CadEntityDraft draft)
        => original with
        {
            Extents = draft.Extents,
            Properties = draft.Properties ?? new Dictionary<string, string>(),
            LayerName = original.LayerName
        };

    private static Point3 ScalePoint(Point3 point, double baseX, double baseY, double factor)
    {
        var x = baseX + (point.X - baseX) * factor;
        var y = baseY + (point.Y - baseY) * factor;
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new OverflowException("Scaled primitive point exceeds the finite numeric range.");
        return new Point3(x, y, point.Z);
    }

    private static Point3 RotatePoint(Point3 point, double baseX, double baseY, double degrees)
    {
        var radians = degrees * Math.PI / 180d;
        var cos = Math.Cos(radians);
        var sin = Math.Sin(radians);
        var dx = point.X - baseX;
        var dy = point.Y - baseY;
        var x = baseX + dx * cos - dy * sin;
        var y = baseY + dx * sin + dy * cos;
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new OverflowException("Rotated primitive point exceeds the finite numeric range.");
        return new Point3(x, y, point.Z);
    }

    private static Point3 MirrorPoint(Point3 point, double axisX, double axisY, double ux, double uy)
    {
        var vx = point.X - axisX;
        var vy = point.Y - axisY;
        var dot = vx * ux + vy * uy;
        var x = axisX + 2d * dot * ux - vx;
        var y = axisY + 2d * dot * uy - vy;
        if (!double.IsFinite(x) || !double.IsFinite(y)) throw new OverflowException("Mirrored primitive point exceeds the finite numeric range.");
        return new Point3(x, y, point.Z);
    }

    private static BoundingBox3 Bounds(IEnumerable<Point3> points)
    {
        var array = points.ToArray();
        if (array.Length == 0) throw new ArgumentException("At least one primitive point is required.", nameof(points));
        var minX = array.Min(static point => point.X);
        var minY = array.Min(static point => point.Y);
        var minZ = array.Min(static point => point.Z);
        var maxX = array.Max(static point => point.X);
        var maxY = array.Max(static point => point.Y);
        var maxZ = array.Max(static point => point.Z);
        return new BoundingBox3(new Point3(minX, minY, minZ), new Point3(maxX, maxY, maxZ));
    }

    private static bool TryNumber(CadEntitySnapshot entity, string key, out double value)
    {
        value = default;
        return entity.Properties.TryGetValue(key, out var raw)
            && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }

    private static bool TryInteger(CadEntitySnapshot entity, string key, out int value)
    {
        value = default;
        return entity.Properties.TryGetValue(key, out var raw)
            && int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out value);
    }

    private static double Finite(double value, string label)
    {
        if (!double.IsFinite(value)) throw new OverflowException($"{label} exceeds the finite numeric range.");
        return value;
    }

    private static void RequireFinite(double value, string label)
    {
        if (!double.IsFinite(value)) throw new ArgumentOutOfRangeException(label, "Primitive numeric value must be finite.");
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);
}
