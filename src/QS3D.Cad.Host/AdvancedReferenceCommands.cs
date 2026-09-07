using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public static class AdvancedReferenceCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ViewCommand());
        registry.Register(new ZoomExtentsCommand());
        registry.Register(new ZoomWindowCommand());
        ViewportManagementCommands.RegisterAll(registry);
        registry.Register(new HitTestCommand());
        registry.Register(new SnapCommand());
        registry.Register(new SelectPolygonCommand());
    }

    private sealed class ViewCommand : ICadCommand
    {
        public string Name => "VIEW";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            var view = Services(context).Viewport.CurrentView;
            return CommandResult.Success($"View target={view.Target} width={view.Width:R} height={view.Height:R} projection={view.Projection}.");
        }
    }

    private sealed class ZoomExtentsCommand : ICadCommand
    {
        public string Name => "ZOOMEXTENTS";
        public CommandFlags Flags => CommandFlags.RequiresDocument;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: ZOOMEXTENTS");
            try
            {
                Services(context).Viewport.ZoomExtents();
                return CommandResult.Success("Zoom extents complete.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class ZoomWindowCommand : ICadCommand
    {
        public string Name => "ZOOMWINDOW";
        public CommandFlags Flags => CommandFlags.RequiresDocument;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 4) return CommandResult.Failure("Usage: ZOOMWINDOW x1 y1 x2 y2");
            try
            {
                var x1 = Number(context.Arguments[0], "x1");
                var y1 = Number(context.Arguments[1], "y1");
                var x2 = Number(context.Arguments[2], "x2");
                var y2 = Number(context.Arguments[3], "y2");
                Services(context).Viewport.ZoomWindow(new BoundingBox3(
                    new Point3(Math.Min(x1, x2), Math.Min(y1, y2)),
                    new Point3(Math.Max(x1, x2), Math.Max(y1, y2))));
                return CommandResult.Success("Zoom window complete.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class HitTestCommand : ICadCommand
    {
        public string Name => "HITTEST";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 3) return CommandResult.Failure("Usage: HITTEST x y aperturePixels");
            try
            {
                var point = new Point3(Number(context.Arguments[0], "x"), Number(context.Arguments[1], "y"));
                var aperture = NonNegative(context.Arguments[2], "aperturePixels");
                var hits = Services(context).Viewport.HitTest(point, aperture);
                foreach (var hit in hits)
                    context.Document.Editor.WriteMessage($"HIT {hit.Handle} point={hit.WorldPoint} distance={hit.DistancePixels:R}px");
                return CommandResult.Success($"Hit-test found {hits.Count} object(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SnapCommand : ICadCommand
    {
        public string Name => "SNAP";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 4) return CommandResult.Failure("Usage: SNAP x y aperturePixels kinds");
            try
            {
                var point = new Point3(Number(context.Arguments[0], "x"), Number(context.Arguments[1], "y"));
                var aperture = NonNegative(context.Arguments[2], "aperturePixels");
                if (!Enum.TryParse<CadSnapKind>(context.Arguments[3], true, out var kinds) || kinds == CadSnapKind.None)
                    return CommandResult.Failure("kinds must be one or more CadSnapKind names, comma-separated when combined.");
                var candidates = Services(context).Snaps.Query(point, aperture, kinds);
                foreach (var candidate in candidates)
                    context.Document.Editor.WriteMessage($"SNAP {candidate.Kind} {candidate.Handle} point={candidate.Point} distance={candidate.DistancePixels:R}px");
                return CommandResult.Success($"Snap query found {candidates.Count} candidate(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SelectPolygonCommand : ICadCommand
    {
        public string Name => "SELPOLY";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 7 || ((context.Arguments.Count - 1) % 2) != 0)
                return CommandResult.Failure("Usage: SELPOLY Window|Crossing|Fence|Lasso x1 y1 x2 y2 x3 y3 [...]");
            try
            {
                if (!Enum.TryParse<CadSelectionMode>(context.Arguments[0], true, out var mode))
                    return CommandResult.Failure($"Unknown selection mode '{context.Arguments[0]}'.");
                var points = new List<Point3>();
                for (var index = 1; index < context.Arguments.Count; index += 2)
                    points.Add(new Point3(Number(context.Arguments[index], $"x{(index + 1) / 2}"), Number(context.Arguments[index + 1], $"y{(index + 1) / 2}")));
                var handles = Services(context).SpatialSelection.SelectPolygon(points, mode);
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success($"Selected {handles.Count} object(s) using {mode} polygon.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private static InMemoryAdvancedServices Services(CommandContext context)
    {
        return context.Document is InMemoryCadDocument document
            ? InMemoryAdvancedServicesRegistry.For(document)
            : throw new InvalidOperationException("Advanced reference commands require the standalone in-memory document adapter.");
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static double NonNegative(string token, string label)
    {
        var value = Number(token, label);
        if (value < 0d) throw new FormatException($"{label} must be non-negative.");
        return value;
    }
}
