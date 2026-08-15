using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

public static class SelectionCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new SelectAllCommand());
        registry.Register(new SelectNoneCommand());
        registry.Register(new SelectInvertCommand());
        registry.Register(new SelectKindCommand());
        registry.Register(new SelectLayerCommand());
        registry.Register(new SelectPropertyCommand());
        registry.Register(new SelectBoxCommand());
        SelectionSetManagementCommands.RegisterAll(registry);
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(CommandContext context)
    {
        using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().OrderBy(static entity => entity.Handle.Value, StringComparer.OrdinalIgnoreCase).ToArray();
    }

    private static void Apply(CommandContext context, IEnumerable<CadHandle> handles)
        => context.Document.Editor.Selection.Set(handles.Distinct().ToArray());

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private sealed class SelectAllCommand : ICadCommand
    {
        public string Name => "SELALL";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELALL");
            var entities = Query(context);
            Apply(context, entities.Select(static entity => entity.Handle));
            return CommandResult.Success($"Selected all {entities.Count} object(s).");
        }
    }

    private sealed class SelectNoneCommand : ICadCommand
    {
        public string Name => "SELNONE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELNONE");
            context.Document.Editor.Selection.Clear();
            return CommandResult.Success("Selection cleared.");
        }
    }

    private sealed class SelectInvertCommand : ICadCommand
    {
        public string Name => "SELINVERT";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELINVERT");
            var current = context.Document.Editor.Selection.Current.ToHashSet();
            var inverted = Query(context).Where(entity => !current.Contains(entity.Handle)).Select(static entity => entity.Handle).ToArray();
            Apply(context, inverted);
            return CommandResult.Success($"Selected {inverted.Length} inverted object(s).");
        }
    }

    private sealed class SelectKindCommand : ICadCommand
    {
        public string Name => "SELKIND";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: SELKIND entityKind");
            if (!Enum.TryParse<CadEntityKind>(context.Arguments[0], true, out var kind)
                || kind == CadEntityKind.Unknown
                || !Enum.IsDefined(typeof(CadEntityKind), kind))
                return CommandResult.Failure($"Unknown CAD entity kind '{context.Arguments[0]}'.");

            var handles = Query(context).Where(entity => entity.Kind == kind).Select(static entity => entity.Handle).ToArray();
            Apply(context, handles);
            return CommandResult.Success($"Selected {handles.Length} {kind} object(s).");
        }
    }

    private sealed class SelectLayerCommand : ICadCommand
    {
        public string Name => "SELLAYER";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1 || string.IsNullOrWhiteSpace(context.Arguments[0]))
                return CommandResult.Failure("Usage: SELLAYER layerName");

            var requested = context.Arguments[0];
            using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            if (tx.GetLayer(requested) is null)
                return CommandResult.Failure($"Layer '{requested}' does not exist.");
            var handles = tx.Query()
                .Where(entity => StringComparer.OrdinalIgnoreCase.Equals(entity.LayerName, requested))
                .OrderBy(static entity => entity.Handle.Value, StringComparer.OrdinalIgnoreCase)
                .Select(static entity => entity.Handle)
                .ToArray();
            Apply(context, handles);
            return CommandResult.Success($"Selected {handles.Length} object(s) on layer '{requested}'.");
        }
    }

    private sealed class SelectPropertyCommand : ICadCommand
    {
        public string Name => "SELPROP";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2 || string.IsNullOrWhiteSpace(context.Arguments[0]))
                return CommandResult.Failure("Usage: SELPROP propertyKey propertyValue");

            var key = context.Arguments[0];
            var value = context.Arguments[1];
            var handles = Query(context)
                .Where(entity => entity.Properties.Any(pair => StringComparer.OrdinalIgnoreCase.Equals(pair.Key, key) && StringComparer.Ordinal.Equals(pair.Value, value)))
                .Select(static entity => entity.Handle)
                .ToArray();
            Apply(context, handles);
            return CommandResult.Success($"Selected {handles.Length} object(s) where {key} matches exactly.");
        }
    }

    private sealed class SelectBoxCommand : ICadCommand
    {
        public string Name => "SELBOX";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 5)
                return CommandResult.Failure("Usage: SELBOX Window|Crossing x1 y1 x2 y2");
            if (!Enum.TryParse<BoxMode>(context.Arguments[0], true, out var mode))
                return CommandResult.Failure("Selection box mode must be Window or Crossing.");

            try
            {
                var x1 = Number(context.Arguments[1], "x1");
                var y1 = Number(context.Arguments[2], "y1");
                var x2 = Number(context.Arguments[3], "x2");
                var y2 = Number(context.Arguments[4], "y2");
                var minX = Math.Min(x1, x2);
                var minY = Math.Min(y1, y2);
                var maxX = Math.Max(x1, x2);
                var maxY = Math.Max(y1, y2);

                var handles = Query(context)
                    .Where(entity => mode == BoxMode.Window
                        ? entity.Extents.Min.X >= minX && entity.Extents.Min.Y >= minY && entity.Extents.Max.X <= maxX && entity.Extents.Max.Y <= maxY
                        : entity.Extents.Max.X >= minX && entity.Extents.Max.Y >= minY && entity.Extents.Min.X <= maxX && entity.Extents.Min.Y <= maxY)
                    .Select(static entity => entity.Handle)
                    .ToArray();
                Apply(context, handles);
                return CommandResult.Success($"Selected {handles.Length} object(s) using {mode} box.");
            }
            catch (FormatException ex)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private enum BoxMode
    {
        Window,
        Crossing
    }
}
