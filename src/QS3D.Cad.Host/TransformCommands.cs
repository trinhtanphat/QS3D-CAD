using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

public static class TransformCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RotateCommand());
        registry.Register(new MirrorCommand());
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

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
            result.Add(pair.Key, pair.Value);
        return result;
    }

    private static double Finite(double value, string label)
    {
        if (!double.IsFinite(value))
            throw new OverflowException($"{label} would overflow the finite numeric range.");
        return value;
    }

    private static Point3 RotatePoint(Point3 point, double baseX, double baseY, double cos, double sin)
    {
        var dx = Finite(point.X - baseX, "Rotation X delta");
        var dy = Finite(point.Y - baseY, "Rotation Y delta");
        var x = Finite(baseX + Finite(Finite(dx * cos, "Rotation X term") - Finite(dy * sin, "Rotation Y term"), "Rotation X offset"), "Rotated X");
        var y = Finite(baseY + Finite(Finite(dx * sin, "Rotation X term") + Finite(dy * cos, "Rotation Y term"), "Rotation Y offset"), "Rotated Y");
        return new Point3(x, y, point.Z);
    }

    private static Point3 MirrorPoint(Point3 point, double axisX, double axisY, double ux, double uy)
    {
        var vx = Finite(point.X - axisX, "Mirror X delta");
        var vy = Finite(point.Y - axisY, "Mirror Y delta");
        var dot = Finite(Finite(vx * ux, "Mirror X projection") + Finite(vy * uy, "Mirror Y projection"), "Mirror projection");
        var twiceDot = Finite(2d * dot, "Mirror doubled projection");
        var x = Finite(axisX + Finite(Finite(twiceDot * ux, "Mirror X projection") - vx, "Mirror X offset"), "Mirrored X");
        var y = Finite(axisY + Finite(Finite(twiceDot * uy, "Mirror Y projection") - vy, "Mirror Y offset"), "Mirrored Y");
        return new Point3(x, y, point.Z);
    }

    private static BoundingBox3 LineExtents(Point3 first, Point3 second)
        => BoundingBox3.FromPoints(first, second);

    private static BoundingBox3 CircleExtents(Point3 center, double radius)
    {
        var minX = Finite(center.X - radius, "Circle minimum X");
        var minY = Finite(center.Y - radius, "Circle minimum Y");
        var maxX = Finite(center.X + radius, "Circle maximum X");
        var maxY = Finite(center.Y + radius, "Circle maximum Y");
        return new BoundingBox3(new Point3(minX, minY, center.Z), new Point3(maxX, maxY, center.Z));
    }

    private static CadEntitySnapshot RotateEntity(CadEntitySnapshot entity, double baseX, double baseY, double cos, double sin)
    {
        var properties = CloneProperties(entity.Properties);
        switch (entity.Kind)
        {
            case CadEntityKind.Line:
            {
                var first = RotatePoint(new Point3(PropertyNumber(entity, "x1"), PropertyNumber(entity, "y1"), entity.Extents.Min.Z), baseX, baseY, cos, sin);
                var second = RotatePoint(new Point3(PropertyNumber(entity, "x2"), PropertyNumber(entity, "y2"), entity.Extents.Max.Z), baseX, baseY, cos, sin);
                properties["x1"] = first.X.ToString("R", CultureInfo.InvariantCulture);
                properties["y1"] = first.Y.ToString("R", CultureInfo.InvariantCulture);
                properties["x2"] = second.X.ToString("R", CultureInfo.InvariantCulture);
                properties["y2"] = second.Y.ToString("R", CultureInfo.InvariantCulture);
                return entity with { Extents = LineExtents(first, second), Properties = properties };
            }
            case CadEntityKind.Circle:
            {
                var radius = PropertyNumber(entity, "radius");
                if (radius <= 0d) throw new InvalidOperationException($"Entity {entity.Handle} radius must be greater than zero.");
                var center = RotatePoint(new Point3(PropertyNumber(entity, "cx"), PropertyNumber(entity, "cy"), entity.Extents.Min.Z), baseX, baseY, cos, sin);
                properties["cx"] = center.X.ToString("R", CultureInfo.InvariantCulture);
                properties["cy"] = center.Y.ToString("R", CultureInfo.InvariantCulture);
                return entity with { Extents = CircleExtents(center, radius), Properties = properties };
            }
            default:
                throw new InvalidOperationException($"ROTATE does not support {entity.Kind} in the standalone reference adapter because the current schema cannot guarantee lossless rotated geometry.");
        }
    }

    private static CadEntitySnapshot MirrorEntity(CadEntitySnapshot entity, double axisX, double axisY, double ux, double uy)
    {
        var properties = CloneProperties(entity.Properties);
        switch (entity.Kind)
        {
            case CadEntityKind.Line:
            {
                var first = MirrorPoint(new Point3(PropertyNumber(entity, "x1"), PropertyNumber(entity, "y1"), entity.Extents.Min.Z), axisX, axisY, ux, uy);
                var second = MirrorPoint(new Point3(PropertyNumber(entity, "x2"), PropertyNumber(entity, "y2"), entity.Extents.Max.Z), axisX, axisY, ux, uy);
                properties["x1"] = first.X.ToString("R", CultureInfo.InvariantCulture);
                properties["y1"] = first.Y.ToString("R", CultureInfo.InvariantCulture);
                properties["x2"] = second.X.ToString("R", CultureInfo.InvariantCulture);
                properties["y2"] = second.Y.ToString("R", CultureInfo.InvariantCulture);
                return entity with { Extents = LineExtents(first, second), Properties = properties };
            }
            case CadEntityKind.Circle:
            {
                var radius = PropertyNumber(entity, "radius");
                if (radius <= 0d) throw new InvalidOperationException($"Entity {entity.Handle} radius must be greater than zero.");
                var center = MirrorPoint(new Point3(PropertyNumber(entity, "cx"), PropertyNumber(entity, "cy"), entity.Extents.Min.Z), axisX, axisY, ux, uy);
                properties["cx"] = center.X.ToString("R", CultureInfo.InvariantCulture);
                properties["cy"] = center.Y.ToString("R", CultureInfo.InvariantCulture);
                return entity with { Extents = CircleExtents(center, radius), Properties = properties };
            }
            default:
                throw new InvalidOperationException($"MIRROR does not support {entity.Kind} in the standalone reference adapter because reflection cannot be represented losslessly by the current entity schema.");
        }
    }

    private sealed class RotateCommand : ICadCommand
    {
        public string Name => "ROTATE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 4)
                return CommandResult.Failure("Usage: ROTATE handle... baseX baseY angleDegrees");

            try
            {
                var handles = context.Arguments.Take(context.Arguments.Count - 3)
                    .Select(static token => new CadHandle(token))
                    .Distinct()
                    .ToArray();
                var baseX = Number(context.Arguments[^3], "baseX");
                var baseY = Number(context.Arguments[^2], "baseY");
                var degrees = Number(context.Arguments[^1], "angleDegrees");
                var normalizedDegrees = Math.IEEERemainder(degrees, 360d);
                var radians = Finite(normalizedDegrees * (Math.PI / 180d), "Rotation angle");
                var cos = Math.Cos(radians);
                var sin = Math.Sin(radians);

                using var tx = context.Document.Database.BeginTransaction();
                var transformed = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    transformed.Add(ReferencePrimitiveGeometry.TryRotate(entity, baseX, baseY, normalizedDegrees, out var primitive)
                        ? primitive
                        : RotateEntity(entity, baseX, baseY, cos, sin));
                }

                if (radians != 0d)
                {
                    foreach (var entity in transformed)
                        tx.Update(entity);
                    tx.Commit();
                }

                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success(radians == 0d
                    ? $"Rotation is zero; retained selection of {handles.Length} object(s)."
                    : $"Rotated {handles.Length} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class MirrorCommand : ICadCommand
    {
        public string Name => "MIRROR";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 5)
                return CommandResult.Failure("Usage: MIRROR handle... axisX1 axisY1 axisX2 axisY2");

            try
            {
                var handles = context.Arguments.Take(context.Arguments.Count - 4)
                    .Select(static token => new CadHandle(token))
                    .Distinct()
                    .ToArray();
                var axisX1 = Number(context.Arguments[^4], "axisX1");
                var axisY1 = Number(context.Arguments[^3], "axisY1");
                var axisX2 = Number(context.Arguments[^2], "axisX2");
                var axisY2 = Number(context.Arguments[^1], "axisY2");
                var dx = Finite(axisX2 - axisX1, "Mirror axis X delta");
                var dy = Finite(axisY2 - axisY1, "Mirror axis Y delta");
                var scale = Math.Max(Math.Abs(dx), Math.Abs(dy));
                if (scale == 0d)
                    return CommandResult.Failure("Mirror axis requires two distinct points.");
                var sx = dx / scale;
                var sy = dy / scale;
                var normalizedLength = Math.Sqrt(sx * sx + sy * sy);
                if (!double.IsFinite(normalizedLength) || normalizedLength <= 0d)
                    return CommandResult.Failure("Mirror axis could not be normalized safely.");
                var ux = sx / normalizedLength;
                var uy = sy / normalizedLength;

                using var tx = context.Document.Database.BeginTransaction();
                var transformed = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    transformed.Add(ReferencePrimitiveGeometry.TryMirror(entity, axisX1, axisY1, ux, uy, out var primitive)
                        ? primitive
                        : MirrorEntity(entity, axisX1, axisY1, ux, uy));
                }

                foreach (var entity in transformed)
                    tx.Update(entity);
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success($"Mirrored {handles.Length} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
