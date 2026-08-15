using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

internal static class SelectionSetManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new StatusCommand());
        registry.Register(new HandlesCommand());
        registry.Register(new AddCommand());
        registry.Register(new RemoveCommand());
        registry.Register(new ToggleCommand());
        registry.Register(new HealthCommand());
        registry.Register(new PruneCommand());
    }

    private static IReadOnlyDictionary<CadHandle, CadEntitySnapshot> Live(CommandContext context)
    {
        using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToDictionary(static entity => entity.Handle);
    }

    private static CadHandle[] ParseHandles(CommandContext context, string usage)
    {
        if (context.Arguments.Count == 0) throw new FormatException(usage);
        var handles = new List<CadHandle>(context.Arguments.Count);
        foreach (var token in context.Arguments)
        {
            try { handles.Add(new CadHandle(token)); }
            catch (Exception ex) when (ex is ArgumentException or FormatException)
            {
                throw new FormatException(ex.Message, ex);
            }
        }
        return handles.Distinct().OrderBy(static handle => handle.Value, StringComparer.Ordinal).ToArray();
    }

    private static CommandResult Missing(IEnumerable<CadHandle> handles)
    {
        var values = handles.Select(static handle => handle.Value).OrderBy(static value => value, StringComparer.Ordinal).ToArray();
        return CommandResult.Failure($"Selection handle(s) are not live in the drawing: {string.Join(", ", values)}.");
    }

    private static void Set(CommandContext context, IEnumerable<CadHandle> handles)
        => context.Document.Editor.Selection.Set(handles.OrderBy(static handle => handle.Value, StringComparer.Ordinal));

    private static (CadHandle[] Current, CadHandle[] Live, CadHandle[] Stale) Snapshot(CommandContext context)
    {
        var current = context.Document.Editor.Selection.Current
            .Distinct()
            .OrderBy(static handle => handle.Value, StringComparer.Ordinal)
            .ToArray();
        var live = Live(context);
        return (
            current,
            current.Where(live.ContainsKey).ToArray(),
            current.Where(handle => !live.ContainsKey(handle)).ToArray());
    }

    private abstract class CommandBase : ICadCommand
    {
        public abstract string Name { get; }
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public abstract CommandResult Execute(CommandContext context);
    }

    private sealed class StatusCommand : CommandBase
    {
        public override string Name => "SELSTATUS";

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELSTATUS");
            var snapshot = Snapshot(context);
            var liveByHandle = Live(context);
            foreach (var handle in snapshot.Current)
            {
                if (liveByHandle.TryGetValue(handle, out var entity))
                    context.Document.Editor.WriteMessage($"SELECTED {handle.Value} kind={entity.Kind} layer={entity.LayerName}");
                else
                    context.Document.Editor.WriteMessage($"STALE {handle.Value}");
            }
            return CommandResult.Success($"Selection total={snapshot.Current.Length} live={snapshot.Live.Length} stale={snapshot.Stale.Length}.");
        }
    }

    private sealed class HandlesCommand : CommandBase
    {
        public override string Name => "SELHANDLES";

        public override CommandResult Execute(CommandContext context)
        {
            try
            {
                var requested = ParseHandles(context, "Usage: SELHANDLES handle [handle ...]");
                var live = Live(context);
                var missing = requested.Where(handle => !live.ContainsKey(handle)).ToArray();
                if (missing.Length != 0) return Missing(missing);
                Set(context, requested);
                return CommandResult.Success($"Selected {requested.Length} explicit handle(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class AddCommand : CommandBase
    {
        public override string Name => "SELADD";

        public override CommandResult Execute(CommandContext context)
        {
            try
            {
                var requested = ParseHandles(context, "Usage: SELADD handle [handle ...]");
                var live = Live(context);
                var missing = requested.Where(handle => !live.ContainsKey(handle)).ToArray();
                if (missing.Length != 0) return Missing(missing);
                var next = context.Document.Editor.Selection.Current.Concat(requested).Distinct().ToArray();
                Set(context, next);
                return CommandResult.Success($"Selection now contains {next.Length} handle(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class RemoveCommand : CommandBase
    {
        public override string Name => "SELREMOVE";

        public override CommandResult Execute(CommandContext context)
        {
            try
            {
                var requested = ParseHandles(context, "Usage: SELREMOVE handle [handle ...]").ToHashSet();
                var next = context.Document.Editor.Selection.Current.Where(handle => !requested.Contains(handle)).Distinct().ToArray();
                Set(context, next);
                return CommandResult.Success($"Selection now contains {next.Length} handle(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class ToggleCommand : CommandBase
    {
        public override string Name => "SELTOGGLE";

        public override CommandResult Execute(CommandContext context)
        {
            try
            {
                var requested = ParseHandles(context, "Usage: SELTOGGLE handle [handle ...]");
                var current = context.Document.Editor.Selection.Current.ToHashSet();
                var live = Live(context);
                var missingToAdd = requested.Where(handle => !current.Contains(handle) && !live.ContainsKey(handle)).ToArray();
                if (missingToAdd.Length != 0) return Missing(missingToAdd);
                foreach (var handle in requested)
                {
                    if (!current.Remove(handle)) current.Add(handle);
                }
                Set(context, current);
                return CommandResult.Success($"Selection now contains {current.Count} handle(s).");
            }
            catch (FormatException ex) { return CommandResult.Failure(ex.Message); }
        }
    }

    private sealed class HealthCommand : CommandBase
    {
        public override string Name => "SELHEALTH";

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELHEALTH");
            var snapshot = Snapshot(context);
            if (snapshot.Stale.Length != 0)
                context.Document.Editor.WriteMessage($"STALE HANDLES {string.Join(",", snapshot.Stale.Select(static handle => handle.Value))}");
            return CommandResult.Success($"Selection health={(snapshot.Stale.Length == 0 ? "healthy" : "unhealthy")} total={snapshot.Current.Length} live={snapshot.Live.Length} stale={snapshot.Stale.Length}.");
        }
    }

    private sealed class PruneCommand : CommandBase
    {
        public override string Name => "SELPRUNE";

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: SELPRUNE");
            var snapshot = Snapshot(context);
            Set(context, snapshot.Live);
            return CommandResult.Success($"Pruned {snapshot.Stale.Length} stale handle(s); selection now contains {snapshot.Live.Length} live handle(s).");
        }
    }
}
