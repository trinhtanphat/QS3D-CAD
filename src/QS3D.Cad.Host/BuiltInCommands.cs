using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

public static class BuiltInCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        registry.Register(new LineCommand());
        registry.Register(new CircleCommand());
        registry.Register(new RectangleCommand());
        registry.Register(new MoveCommand());
        registry.Register(new CopyCommand());
        registry.Register(new ScaleCommand());
        registry.Register(new SelectCommand());
        registry.Register(new EraseCommand());
        registry.Register(new ListCommand());
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static Dictionary<string, string> Properties(params (string Key, double Value)[] values)
        => values.ToDictionary(static x => x.Key, static x => x.Value.ToString("R", CultureInfo.InvariantCulture), StringComparer.Ordinal);

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
            result.Add(pair.Key, pair.Value);
        return result;
    }

    private static BoundingBox3 TranslateExtents(BoundingBox3 extents, double dx, double dy)
        => new(
            new Point3(AddFinite(extents.Min.X, dx, "minimum X"), AddFinite(extents.Min.Y, dy, "minimum Y"), extents.Min.Z),
            new Point3(AddFinite(extents.Max.X, dx, "maximum X"), AddFinite(extents.Max.Y, dy, "maximum Y"), extents.Max.Z));

    private static IReadOnlyDictionary<string, string> TranslateProperties(IReadOnlyDictionary<string, string> source, double dx, double dy)
    {
        var result = CloneProperties(source);
        Shift(result, "x1", dx); Shift(result, "x2", dx); Shift(result, "cx", dx); Shift(result, CadBlockReferencePropertyNames.InsertionX, dx);
        Shift(result, "y1", dy); Shift(result, "y2", dy); Shift(result, "cy", dy); Shift(result, CadBlockReferencePropertyNames.InsertionY, dy);
        return result;
    }

    private static BoundingBox3 ScaleExtents(BoundingBox3 extents, double baseX, double baseY, double factor)
        => new(
            new Point3(
                ScaleCoordinate(extents.Min.X, baseX, factor, "minimum X"),
                ScaleCoordinate(extents.Min.Y, baseY, factor, "minimum Y"),
                extents.Min.Z),
            new Point3(
                ScaleCoordinate(extents.Max.X, baseX, factor, "maximum X"),
                ScaleCoordinate(extents.Max.Y, baseY, factor, "maximum Y"),
                extents.Max.Z));

    private static IReadOnlyDictionary<string, string> ScaleProperties(CadEntitySnapshot entity, double baseX, double baseY, double factor)
    {
        var result = CloneProperties(entity.Properties);
        ScaleCoordinateProperty(result, "x1", baseX, factor);
        ScaleCoordinateProperty(result, "x2", baseX, factor);
        ScaleCoordinateProperty(result, "cx", baseX, factor);
        ScaleCoordinateProperty(result, CadBlockReferencePropertyNames.InsertionX, baseX, factor);
        ScaleCoordinateProperty(result, "y1", baseY, factor);
        ScaleCoordinateProperty(result, "y2", baseY, factor);
        ScaleCoordinateProperty(result, "cy", baseY, factor);
        ScaleCoordinateProperty(result, CadBlockReferencePropertyNames.InsertionY, baseY, factor);
        if (entity.Kind is CadEntityKind.Circle or CadEntityKind.Arc || ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out _))
            ScaleLengthProperty(result, "radius", factor);
        if (entity.Kind == CadEntityKind.BlockReference)
            ScaleLengthProperty(result, CadBlockReferencePropertyNames.UniformScale, factor);
        return result;
    }

    private static bool SupportsReferenceScale(CadEntityKind kind)
        => kind is CadEntityKind.Line or CadEntityKind.Polyline or CadEntityKind.Circle or CadEntityKind.Arc or CadEntityKind.Point or CadEntityKind.BlockReference;

    private static double AddFinite(double current, double delta, string label)
    {
        var shifted = current + delta;
        if (!double.IsFinite(shifted))
            throw new OverflowException($"Entity {label} would overflow the finite coordinate range.");
        return shifted;
    }

    private static double ScaleCoordinate(double current, double origin, double factor, string label)
    {
        var delta = current - origin;
        if (!double.IsFinite(delta))
            throw new OverflowException($"Entity {label} delta would overflow the finite coordinate range.");
        var scaledDelta = delta * factor;
        if (!double.IsFinite(scaledDelta))
            throw new OverflowException($"Entity {label} scale would overflow the finite coordinate range.");
        return AddFinite(origin, scaledDelta, label);
    }

    private static double MultiplyFinite(double current, double factor, string label)
    {
        var scaled = current * factor;
        if (!double.IsFinite(scaled))
            throw new OverflowException($"Entity {label} would overflow the finite numeric range.");
        return scaled;
    }

    private static void Shift(Dictionary<string, string> properties, string key, double delta)
    {
        if (!properties.TryGetValue(key, out var raw)) return;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var current) || !double.IsFinite(current))
            throw new FormatException($"Entity property {key} is not a finite number.");
        properties[key] = AddFinite(current, delta, $"property {key}").ToString("R", CultureInfo.InvariantCulture);
    }

    private static void ScaleCoordinateProperty(Dictionary<string, string> properties, string key, double origin, double factor)
    {
        if (!properties.TryGetValue(key, out var raw)) return;
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var current) || !double.IsFinite(current))
            throw new FormatException($"Entity property {key} is not a finite number.");
        properties[key] = ScaleCoordinate(current, origin, factor, $"property {key}").ToString("R", CultureInfo.InvariantCulture);
    }

    private static void ScaleLengthProperty(Dictionary<string, string> properties, string key, double factor)
    {
        if (!properties.TryGetValue(key, out var raw))
            throw new FormatException($"Entity property {key} is required for scaling.");
        if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var current) || !double.IsFinite(current))
            throw new FormatException($"Entity property {key} is not a finite number.");
        var scaled = MultiplyFinite(current, factor, $"property {key}");
        if (scaled <= 0d)
            throw new InvalidOperationException($"Entity property {key} must remain greater than zero after scaling.");
        properties[key] = scaled.ToString("R", CultureInfo.InvariantCulture);
    }

    private static CommandResult Usage(string text) => CommandResult.Failure($"Usage: {text}");

    private sealed class LineCommand : ICadCommand
    {
        public string Name => "LINE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 4) return Usage("LINE x1 y1 x2 y2");
            try
            {
                var p1 = new Point3(Number(context.Arguments[0], "x1"), Number(context.Arguments[1], "y1"));
                var p2 = new Point3(Number(context.Arguments[2], "x2"), Number(context.Arguments[3], "y2"));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(new CadEntityDraft(CadEntityKind.Line, BoundingBox3.FromPoints(p1, p2), Properties(("x1", p1.X), ("y1", p1.Y), ("x2", p2.X), ("y2", p2.Y))));
                tx.Commit();
                return CommandResult.Success($"Created LINE {handle}.");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class CircleCommand : ICadCommand
    {
        public string Name => "CIRCLE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 3) return Usage("CIRCLE cx cy radius");
            try
            {
                var cx = Number(context.Arguments[0], "cx");
                var cy = Number(context.Arguments[1], "cy");
                var radius = Number(context.Arguments[2], "radius");
                if (radius <= 0d) return CommandResult.Failure("radius must be greater than zero.");
                var extents = new BoundingBox3(new Point3(cx - radius, cy - radius), new Point3(cx + radius, cy + radius));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(new CadEntityDraft(CadEntityKind.Circle, extents, Properties(("cx", cx), ("cy", cy), ("radius", radius))));
                tx.Commit();
                return CommandResult.Success($"Created CIRCLE {handle}.");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class RectangleCommand : ICadCommand
    {
        public string Name => "RECTANG";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 4) return Usage("RECTANG x1 y1 x2 y2");
            try
            {
                var p1 = new Point3(Number(context.Arguments[0], "x1"), Number(context.Arguments[1], "y1"));
                var p2 = new Point3(Number(context.Arguments[2], "x2"), Number(context.Arguments[3], "y2"));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(new CadEntityDraft(CadEntityKind.Polyline, BoundingBox3.FromPoints(p1, p2), Properties(("x1", p1.X), ("y1", p1.Y), ("x2", p2.X), ("y2", p2.Y))));
                tx.Commit();
                return CommandResult.Success($"Created RECTANG {handle}.");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class MoveCommand : ICadCommand
    {
        public string Name => "MOVE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 3) return Usage("MOVE handle... dx dy");
            try
            {
                var handles = context.Arguments.Take(context.Arguments.Count - 2).Select(static token => new CadHandle(token)).Distinct().ToArray();
                var dx = Number(context.Arguments[^2], "dx");
                var dy = Number(context.Arguments[^1], "dy");
                using var tx = context.Document.Database.BeginTransaction();
                var entities = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    entities.Add(entity);
                }
                foreach (var entity in entities)
                    tx.Update(entity with { Extents = TranslateExtents(entity.Extents, dx, dy), Properties = TranslateProperties(entity.Properties, dx, dy) });
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success($"Moved {handles.Length} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class CopyCommand : ICadCommand
    {
        public string Name => "COPY";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 3) return Usage("COPY handle... dx dy");
            try
            {
                var sourceHandles = context.Arguments.Take(context.Arguments.Count - 2).Select(static token => new CadHandle(token)).Distinct().ToArray();
                var dx = Number(context.Arguments[^2], "dx");
                var dy = Number(context.Arguments[^1], "dy");
                using var tx = context.Document.Database.BeginTransaction();
                var sources = new List<CadEntitySnapshot>(sourceHandles.Length);
                foreach (var handle in sourceHandles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    sources.Add(entity);
                }

                var copiedHandles = new List<CadHandle>(sources.Count);
                foreach (var entity in sources)
                {
                    var draft = new CadEntityDraft(
                        entity.Kind,
                        TranslateExtents(entity.Extents, dx, dy),
                        TranslateProperties(entity.Properties, dx, dy),
                        entity.LayerName);
                    copiedHandles.Add(tx.Append(draft));
                }
                tx.Commit();
                context.Document.Editor.Selection.Set(copiedHandles);
                return CommandResult.Success($"Copied {copiedHandles.Count} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class ScaleCommand : ICadCommand
    {
        public string Name => "SCALE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 4) return Usage("SCALE handle... baseX baseY factor");
            try
            {
                var handles = context.Arguments.Take(context.Arguments.Count - 3).Select(static token => new CadHandle(token)).Distinct().ToArray();
                var baseX = Number(context.Arguments[^3], "baseX");
                var baseY = Number(context.Arguments[^2], "baseY");
                var factor = Number(context.Arguments[^1], "factor");
                if (factor <= 0d) return CommandResult.Failure("factor must be greater than zero.");

                using var tx = context.Document.Database.BeginTransaction();
                var scaledEntities = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    if (!SupportsReferenceScale(entity.Kind))
                        return CommandResult.Failure($"SCALE does not support {entity.Kind} in the standalone reference adapter.");
                    scaledEntities.Add(entity with
                    {
                        Extents = ScaleExtents(entity.Extents, baseX, baseY, factor),
                        Properties = ScaleProperties(entity, baseX, baseY, factor)
                    });
                }

                foreach (var entity in scaledEntities)
                    tx.Update(entity);
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success($"Scaled {handles.Length} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SelectCommand : ICadCommand
    {
        public string Name => "SELECT";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0) return Usage("SELECT handle...");
            try
            {
                var handles = context.Arguments.Select(static x => new CadHandle(x)).ToArray();
                using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
                foreach (var handle in handles)
                {
                    if (tx.Get(handle) is null)
                        return CommandResult.Failure($"Entity {handle} does not exist.");
                }
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success($"Selected {handles.Length} object(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class EraseCommand : ICadCommand
    {
        public string Name => "ERASE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0) return Usage("ERASE handle...");
            try
            {
                var handles = context.Arguments.Select(static x => new CadHandle(x)).Distinct().ToArray();
                using var tx = context.Document.Database.BeginTransaction();
                foreach (var handle in handles)
                {
                    if (tx.Get(handle) is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    tx.Erase(handle);
                }
                tx.Commit();
                context.Document.Editor.Selection.Clear();
                return CommandResult.Success($"Erased {handles.Length} object(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class ListCommand : ICadCommand
    {
        public string Name => "LIST";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            var entities = tx.Query();
            foreach (var entity in entities)
                context.Document.Editor.WriteMessage($"{entity.Handle} {entity.Kind} {entity.Extents.Min} -> {entity.Extents.Max}");
            return CommandResult.Success($"{entities.Count} object(s).");
        }
    }
}
