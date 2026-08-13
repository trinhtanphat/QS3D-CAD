using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public static class LayerCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new LayerCommand());
        registry.Register(new ListLayersCommand());
    }

    private sealed class ListLayersCommand : ICadCommand
    {
        public string Name => "LAYERS";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            foreach (var layer in tx.GetLayers())
            {
                var current = StringComparer.OrdinalIgnoreCase.Equals(layer.Name, tx.CurrentLayerName) ? "*" : " ";
                context.Document.Editor.WriteMessage($"{current} {layer.Name} on={layer.IsOn} frozen={layer.IsFrozen} locked={layer.IsLocked}");
            }
            return CommandResult.Success($"{tx.GetLayers().Count} layer(s); current={tx.CurrentLayerName}.");
        }
    }

    private sealed class LayerCommand : ICadCommand
    {
        public string Name => "LAYER";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2) return Usage();
            var operation = context.Arguments[0].Trim().ToUpperInvariant();
            var name = context.Arguments[1];
            try
            {
                using var tx = context.Document.Database.BeginTransaction();
                switch (operation)
                {
                    case "NEW": tx.CreateLayer(name); break;
                    case "SET": tx.SetCurrentLayer(name); break;
                    case "ON": Update(tx, name, static layer => layer with { IsOn = true }); break;
                    case "OFF": Update(tx, name, static layer => layer with { IsOn = false }); break;
                    case "FREEZE": Update(tx, name, static layer => layer with { IsFrozen = true }); break;
                    case "THAW": Update(tx, name, static layer => layer with { IsFrozen = false }); break;
                    case "LOCK": Update(tx, name, static layer => layer with { IsLocked = true }); break;
                    case "UNLOCK": Update(tx, name, static layer => layer with { IsLocked = false }); break;
                    case "DELETE": tx.EraseLayer(name); break;
                    default: return Usage();
                }
                tx.Commit();
                return CommandResult.Success($"Layer {operation} '{name}' complete.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }

        private static void Update(ICadTransaction tx, string name, Func<CadLayerSnapshot, CadLayerSnapshot> mutate)
        {
            var layer = tx.GetLayer(name) ?? throw new KeyNotFoundException($"Layer '{name}' does not exist.");
            tx.UpdateLayer(mutate(layer));
        }

        private static CommandResult Usage()
            => CommandResult.Failure("Usage: LAYER NEW|SET|ON|OFF|FREEZE|THAW|LOCK|UNLOCK|DELETE name");
    }
}
