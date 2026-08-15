using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

internal static class ViewportManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new StatusCommand());
        registry.Register(new SetCommand());
        registry.Register(new CenterCommand());
        registry.Register(new PanCommand());
        registry.Register(new ZoomCommand());
        registry.Register(new ResetCommand());
        registry.Register(new HealthCommand());
    }

    private static InMemoryViewportService Service(CommandContext context)
    {
        if (context.Document is not InMemoryCadDocument document)
            throw new InvalidOperationException("Viewport management requires the standalone reference adapter.");
        return InMemoryAdvancedServicesRegistry.For(document).Viewport;
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static double Positive(string token, string label)
    {
        var value = Number(token, label);
        if (value <= 0d) throw new FormatException($"{label} must be positive.");
        return value;
    }

    private static bool TryProjection(string token, out CadViewProjection projection)
        => Enum.TryParse(token, true, out projection) && Enum.IsDefined(typeof(CadViewProjection), projection);

    private static string Describe(CadViewState view)
        => $"target=({view.Target.X:R},{view.Target.Y:R},{view.Target.Z:R}) direction=({view.Direction.X:R},{view.Direction.Y:R},{view.Direction.Z:R}) up=({view.Up.X:R},{view.Up.Y:R},{view.Up.Z:R}) width={view.Width:R} height={view.Height:R} projection={view.Projection}";

    private abstract class CommandBase : ICadCommand
    {
        public abstract string Name { get; }
        public abstract CommandFlags Flags { get; }
        public abstract CommandResult Execute(CommandContext context);
        protected static CommandResult Failure(Exception ex) => CommandResult.Failure(ex.Message);
    }

    private sealed class StatusCommand : CommandBase
    {
        public override string Name => "VIEWSTATUS";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: VIEWSTATUS");
            try
            {
                var message = $"VIEW {Describe(Service(context).CurrentView)}.";
                context.Document.Editor.WriteMessage(message);
                return CommandResult.Success(message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class SetCommand : CommandBase
    {
        public override string Name => "VIEWSET";
        public override CommandFlags Flags => CommandFlags.RequiresDocument;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count is < 11 or > 12)
                return CommandResult.Failure("Usage: VIEWSET targetX targetY targetZ dirX dirY dirZ upX upY upZ width height [Orthographic|Perspective]");
            try
            {
                var projection = CadViewProjection.Orthographic;
                if (context.Arguments.Count == 12 && !TryProjection(context.Arguments[11], out projection))
                    return CommandResult.Failure($"Unknown view projection '{context.Arguments[11]}'.");
                var view = new CadViewState(
                    new Point3(Number(context.Arguments[0], "targetX"), Number(context.Arguments[1], "targetY"), Number(context.Arguments[2], "targetZ")),
                    new Vector3(Number(context.Arguments[3], "dirX"), Number(context.Arguments[4], "dirY"), Number(context.Arguments[5], "dirZ")),
                    new Vector3(Number(context.Arguments[6], "upX"), Number(context.Arguments[7], "upY"), Number(context.Arguments[8], "upZ")),
                    Positive(context.Arguments[9], "width"),
                    Positive(context.Arguments[10], "height"),
                    projection);
                Service(context).SetView(view);
                return CommandResult.Success($"View updated: {Describe(view)}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class CenterCommand : CommandBase
    {
        public override string Name => "VIEWCENTER";
        public override CommandFlags Flags => CommandFlags.RequiresDocument;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count is < 2 or > 3) return CommandResult.Failure("Usage: VIEWCENTER x y [z]");
            try
            {
                var service = Service(context);
                var current = service.CurrentView;
                var target = new Point3(
                    Number(context.Arguments[0], "x"),
                    Number(context.Arguments[1], "y"),
                    context.Arguments.Count == 3 ? Number(context.Arguments[2], "z") : current.Target.Z);
                service.SetView(current with { Target = target });
                return CommandResult.Success($"View centered at ({target.X:R},{target.Y:R},{target.Z:R}).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class PanCommand : CommandBase
    {
        public override string Name => "VIEWPAN";
        public override CommandFlags Flags => CommandFlags.RequiresDocument;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count is < 2 or > 3) return CommandResult.Failure("Usage: VIEWPAN dx dy [dz]");
            try
            {
                var service = Service(context);
                var current = service.CurrentView;
                var target = new Point3(
                    current.Target.X + Number(context.Arguments[0], "dx"),
                    current.Target.Y + Number(context.Arguments[1], "dy"),
                    current.Target.Z + (context.Arguments.Count == 3 ? Number(context.Arguments[2], "dz") : 0d));
                service.SetView(current with { Target = target });
                return CommandResult.Success($"View panned to ({target.X:R},{target.Y:R},{target.Z:R}).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class ZoomCommand : CommandBase
    {
        public override string Name => "VIEWZOOM";
        public override CommandFlags Flags => CommandFlags.RequiresDocument;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: VIEWZOOM factor");
            try
            {
                var factor = Positive(context.Arguments[0], "factor");
                var service = Service(context);
                var current = service.CurrentView;
                var width = current.Width / factor;
                var height = current.Height / factor;
                if (!double.IsFinite(width) || !double.IsFinite(height) || width <= 0d || height <= 0d)
                    return CommandResult.Failure("factor produces a non-representable view size.");
                var updated = current with { Width = width, Height = height };
                service.SetView(updated);
                return CommandResult.Success($"View zoom factor={factor:R}; width={width:R} height={height:R}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class ResetCommand : CommandBase
    {
        public override string Name => "VIEWRESET";
        public override CommandFlags Flags => CommandFlags.RequiresDocument;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: VIEWRESET");
            try
            {
                var view = new CadViewState(new Point3(0d, 0d, 0d), new Vector3(0d, 0d, -1d), new Vector3(0d, 1d, 0d), 100d, 100d, CadViewProjection.Orthographic);
                Service(context).SetView(view);
                return CommandResult.Success("View reset to standalone reference default.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class HealthCommand : CommandBase
    {
        public override string Name => "VIEWHEALTH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: VIEWHEALTH");
            try
            {
                var view = Service(context).CurrentView;
                var directionLength = view.Direction.Length;
                var upLength = view.Up.Length;
                var cx = (view.Direction.Y * view.Up.Z) - (view.Direction.Z * view.Up.Y);
                var cy = (view.Direction.Z * view.Up.X) - (view.Direction.X * view.Up.Z);
                var cz = (view.Direction.X * view.Up.Y) - (view.Direction.Y * view.Up.X);
                var crossLength = Math.Sqrt((cx * cx) + (cy * cy) + (cz * cz));
                var aspect = view.Width / view.Height;
                var healthy = double.IsFinite(directionLength) && directionLength > 0d
                    && double.IsFinite(upLength) && upLength > 0d
                    && double.IsFinite(crossLength) && crossLength > 0d
                    && double.IsFinite(aspect) && aspect > 0d;
                var message = $"VIEW health={(healthy ? "healthy" : "unhealthy")} projection={view.Projection} aspect={aspect:R} directionLength={directionLength:R} upLength={upLength:R} basisCrossLength={crossLength:R}.";
                context.Document.Editor.WriteMessage(message);
                return CommandResult.Success(message);
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }
}
