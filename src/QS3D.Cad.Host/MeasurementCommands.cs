using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public static class MeasurementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new DistanceCommand());
        registry.Register(new MeasureCommand());
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static double PropertyNumber(CadEntitySnapshot entity, string key)
    {
        if (!entity.Properties.TryGetValue(key, out var raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
            throw new FormatException($"Entity {entity.Handle} property {key} must be a finite number.");
        return value;
    }

    private static double Delta(double a, double b, string label)
    {
        var value = b - a;
        if (!double.IsFinite(value))
            throw new OverflowException($"{label} exceeds the finite numeric range.");
        return value;
    }

    private static double Hypot(double x, double y, string label)
    {
        var ax = Math.Abs(x);
        var ay = Math.Abs(y);
        var largest = Math.Max(ax, ay);
        if (largest == 0d) return 0d;
        var ratio = Math.Min(ax, ay) / largest;
        var value = largest * Math.Sqrt(1d + ratio * ratio);
        if (!double.IsFinite(value))
            throw new OverflowException($"{label} exceeds the finite numeric range.");
        return value;
    }

    private static double Product(double a, double b, string label)
    {
        var value = a * b;
        if (!double.IsFinite(value))
            throw new OverflowException($"{label} exceeds the finite numeric range.");
        return value;
    }

    private static string F(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private sealed class DistanceCommand : ICadCommand
    {
        public string Name => "DIST";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 4)
                return CommandResult.Failure("Usage: DIST x1 y1 x2 y2");
            try
            {
                var x1 = Number(context.Arguments[0], "x1");
                var y1 = Number(context.Arguments[1], "y1");
                var x2 = Number(context.Arguments[2], "x2");
                var y2 = Number(context.Arguments[3], "y2");
                var dx = Delta(x1, x2, "Distance dx");
                var dy = Delta(y1, y2, "Distance dy");
                var distance = Hypot(dx, dy, "Distance");
                var angleDegrees = Math.Atan2(dy, dx) * (180d / Math.PI);
                return CommandResult.Success($"Distance={F(distance)} dx={F(dx)} dy={F(dy)} angleDeg={F(angleDegrees)}.");
            }
            catch (Exception ex) when (ex is FormatException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class MeasureCommand : ICadCommand
    {
        public string Name => "MEASURE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0)
                return CommandResult.Failure("Usage: MEASURE handle...");
            try
            {
                var handles = context.Arguments.Select(static token => new QS3D.Platform.Domain.CadHandle(token)).Distinct().ToArray();
                using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
                var lines = new List<string>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null)
                        return CommandResult.Failure($"Entity {handle} does not exist.");
                    lines.Add(Describe(entity));
                }

                foreach (var line in lines)
                    context.Document.Editor.WriteMessage(line);
                return CommandResult.Success($"Measured {lines.Count} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private static string Describe(CadEntitySnapshot entity)
    {
        if (ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out var polygon))
            return DescribeRegularPolygon(entity, polygon);
        return entity.Kind switch
        {
            CadEntityKind.Line => DescribeLine(entity),
            CadEntityKind.Circle => DescribeCircle(entity),
            CadEntityKind.Arc when ReferencePrimitiveGeometry.TryGetArc(entity, out var arc) => DescribeArc(entity, arc),
            CadEntityKind.Point when ReferencePrimitiveGeometry.TryGetPoint(entity, out var point) => DescribePoint(entity, point),
            CadEntityKind.Polyline => DescribeReferenceRectangle(entity),
            _ => throw new InvalidOperationException($"MEASURE does not support the current {entity.Kind} reference schema.")
        };
    }

    private static string DescribeLine(CadEntitySnapshot entity)
    {
        var dx = Delta(PropertyNumber(entity, "x1"), PropertyNumber(entity, "x2"), "Line dx");
        var dy = Delta(PropertyNumber(entity, "y1"), PropertyNumber(entity, "y2"), "Line dy");
        var length = Hypot(dx, dy, "Line length");
        return $"MEASURE {entity.Handle} Line length={F(length)} dx={F(dx)} dy={F(dy)}.";
    }

    private static string DescribeCircle(CadEntitySnapshot entity)
    {
        var radius = PropertyNumber(entity, "radius");
        if (radius <= 0d)
            throw new InvalidOperationException($"Entity {entity.Handle} radius must be greater than zero.");
        var diameter = Product(radius, 2d, "Circle diameter");
        var circumference = Product(diameter, Math.PI, "Circle circumference");
        var area = Product(Product(radius, radius, "Circle radius squared"), Math.PI, "Circle area");
        return $"MEASURE {entity.Handle} Circle radius={F(radius)} diameter={F(diameter)} circumference={F(circumference)} area={F(area)}.";
    }

    private static string DescribeArc(CadEntitySnapshot entity, ReferenceArc arc)
    {
        var radians = Math.Abs(arc.SweepAngleDegrees) * Math.PI / 180d;
        var length = Product(arc.Radius, radians, "Arc length");
        return $"MEASURE {entity.Handle} Arc radius={F(arc.Radius)} startDeg={F(arc.StartAngleDegrees)} sweepDeg={F(arc.SweepAngleDegrees)} length={F(length)}.";
    }

    private static string DescribePoint(CadEntitySnapshot entity, QS3D.Platform.Geometry.Point3 point)
        => $"MEASURE {entity.Handle} Point x={F(point.X)} y={F(point.Y)}.";

    private static string DescribeRegularPolygon(CadEntitySnapshot entity, ReferenceRegularPolygon polygon)
    {
        var halfStep = Math.PI / polygon.Sides;
        var edge = Product(2d * polygon.Radius, Math.Sin(halfStep), "Polygon edge length");
        var perimeter = Product(edge, polygon.Sides, "Polygon perimeter");
        var radiusSquared = Product(polygon.Radius, polygon.Radius, "Polygon radius squared");
        var area = Product(.5d * polygon.Sides, Product(radiusSquared, Math.Sin(2d * Math.PI / polygon.Sides), "Polygon area sine term"), "Polygon area");
        return $"MEASURE {entity.Handle} Polyline(regular-polygon) sides={polygon.Sides} radius={F(polygon.Radius)} perimeter={F(perimeter)} area={F(area)}.";
    }

    private static string DescribeReferenceRectangle(CadEntitySnapshot entity)
    {
        var width = Math.Abs(Delta(PropertyNumber(entity, "x1"), PropertyNumber(entity, "x2"), "Rectangle width"));
        var height = Math.Abs(Delta(PropertyNumber(entity, "y1"), PropertyNumber(entity, "y2"), "Rectangle height"));
        var perimeter = Product(2d, width + height, "Rectangle perimeter");
        if (!double.IsFinite(perimeter))
            throw new OverflowException("Rectangle perimeter exceeds the finite numeric range.");
        var area = Product(width, height, "Rectangle area");
        return $"MEASURE {entity.Handle} Polyline(reference-rectangle) width={F(width)} height={F(height)} perimeter={F(perimeter)} area={F(area)}.";
    }
}
