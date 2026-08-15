using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public static class ReferencePrimitiveCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ArcCommand());
        registry.Register(new PointCommand());
        registry.Register(new PolygonCommand());
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static int Integer(string token, string label)
    {
        if (!int.TryParse(token, NumberStyles.Integer, CultureInfo.InvariantCulture, out var value))
            throw new FormatException($"{label} must be an invariant-culture integer.");
        return value;
    }

    private sealed class ArcCommand : ICadCommand
    {
        public string Name => "ARC";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 5)
                return CommandResult.Failure("Usage: ARC cx cy radius startAngleDeg sweepAngleDeg");
            try
            {
                var draft = ReferencePrimitiveGeometry.CreateArcDraft(
                    Number(context.Arguments[0], "cx"),
                    Number(context.Arguments[1], "cy"),
                    Number(context.Arguments[2], "radius"),
                    Number(context.Arguments[3], "startAngleDeg"),
                    Number(context.Arguments[4], "sweepAngleDeg"));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(draft);
                tx.Commit();
                context.Document.Editor.Selection.Set(new[] { handle });
                return CommandResult.Success($"Created ARC {handle}.");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class PointCommand : ICadCommand
    {
        public string Name => "POINT";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2)
                return CommandResult.Failure("Usage: POINT x y");
            try
            {
                var draft = ReferencePrimitiveGeometry.CreatePointDraft(
                    Number(context.Arguments[0], "x"),
                    Number(context.Arguments[1], "y"));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(draft);
                tx.Commit();
                context.Document.Editor.Selection.Set(new[] { handle });
                return CommandResult.Success($"Created POINT {handle}.");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class PolygonCommand : ICadCommand
    {
        public string Name => "POLYGON";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 5)
                return CommandResult.Failure("Usage: POLYGON sides cx cy radius rotationDeg");
            try
            {
                var draft = ReferencePrimitiveGeometry.CreateRegularPolygonDraft(
                    Integer(context.Arguments[0], "sides"),
                    Number(context.Arguments[1], "cx"),
                    Number(context.Arguments[2], "cy"),
                    Number(context.Arguments[3], "radius"),
                    Number(context.Arguments[4], "rotationDeg"));
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.Append(draft);
                tx.Commit();
                context.Document.Editor.Selection.Set(new[] { handle });
                return CommandResult.Success($"Created POLYGON {handle}.");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or OverflowException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
