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
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
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
            if (context.Arguments.Count != 3) return Usage("MOVE handle dx dy");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                var dx = Number(context.Arguments[1], "dx");
                var dy = Number(context.Arguments[2], "dy");
                using var tx = context.Document.Database.BeginTransaction();
                var entity = tx.Get(handle);
                if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                var delta = new Vector3(dx, dy);
                var properties = TranslateProperties(entity.Properties, dx, dy);
                tx.Update(entity with { Extents = new BoundingBox3(entity.Extents.Min + delta, entity.Extents.Max + delta), Properties = properties });
                tx.Commit();
                return CommandResult.Success($"Moved {handle}.");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }

        private static IReadOnlyDictionary<string, string> TranslateProperties(IReadOnlyDictionary<string, string> source, double dx, double dy)
        {
            var result = CloneProperties(source);
            Shift(result, "x1", dx); Shift(result, "x2", dx); Shift(result, "cx", dx);
            Shift(result, "y1", dy); Shift(result, "y2", dy); Shift(result, "cy", dy);
            return result;
        }

        private static void Shift(Dictionary<string, string> properties, string key, double delta)
        {
            if (!properties.TryGetValue(key, out var raw)) return;
            if (!double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var current) || !double.IsFinite(current))
                throw new FormatException($"Entity property {key} is not a finite number.");
            properties[key] = (current + delta).ToString("R", CultureInfo.InvariantCulture);
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
