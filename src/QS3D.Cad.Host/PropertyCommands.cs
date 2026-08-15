using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Host;

public static class PropertyCommands
{
    private static readonly HashSet<string> ReservedPropertyKeys = new(StringComparer.OrdinalIgnoreCase)
    {
        "x1", "y1", "x2", "y2", "cx", "cy", "radius",
        CadBlockReferencePropertyNames.BlockName,
        CadBlockReferencePropertyNames.InsertionX,
        CadBlockReferencePropertyNames.InsertionY,
        CadBlockReferencePropertyNames.InsertionZ,
        CadBlockReferencePropertyNames.UniformScale,
        CadBlockReferencePropertyNames.RotationRadians
    };

    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ChangeLayerCommand());
        registry.Register(new SetPropertyCommand());
        registry.Register(new DeletePropertyCommand());
    }

    private static CadHandle[] Handles(IReadOnlyList<string> arguments, int trailingArgumentCount, string usage)
    {
        if (arguments.Count <= trailingArgumentCount)
            throw new FormatException($"Usage: {usage}");
        return arguments
            .Take(arguments.Count - trailingArgumentCount)
            .Select(static token => new CadHandle(token))
            .Distinct()
            .ToArray();
    }

    private static List<CadEntitySnapshot> RequireEntities(ICadTransaction tx, IReadOnlyList<CadHandle> handles)
    {
        var result = new List<CadEntitySnapshot>(handles.Count);
        foreach (var handle in handles)
        {
            var entity = tx.Get(handle);
            if (entity is null)
                throw new KeyNotFoundException($"Entity {handle} does not exist.");
            result.Add(entity);
        }
        return result;
    }

    private static CadLayerSnapshot RequireEditableSourceLayer(ICadTransaction tx, CadEntitySnapshot entity)
    {
        var layer = tx.GetLayer(entity.LayerName)
            ?? throw new KeyNotFoundException($"Layer '{entity.LayerName}' does not exist.");
        if (layer.IsLocked)
            throw new InvalidOperationException($"Layer '{layer.Name}' is locked.");
        if (layer.IsFrozen)
            throw new InvalidOperationException($"Layer '{layer.Name}' is frozen.");
        return layer;
    }

    private static string RequireMetadataKey(string key)
    {
        if (string.IsNullOrWhiteSpace(key))
            throw new ArgumentException("Metadata property key must not be blank.");
        var normalized = key.Trim();
        if (normalized.Length > 128)
            throw new ArgumentException("Metadata property key must not exceed 128 characters.");
        if (ReservedPropertyKeys.Contains(normalized) || normalized.StartsWith("QS3D.", StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Property '{normalized}' is structural/reserved and cannot be edited through SETPROP/DELPROP.");
        return normalized;
    }

    private static string ResolveStableKey(IReadOnlyDictionary<string, string> properties, string requested)
    {
        var matches = properties.Keys
            .Where(key => StringComparer.OrdinalIgnoreCase.Equals(key, requested))
            .ToArray();
        if (matches.Length > 1)
            throw new InvalidOperationException($"Property key '{requested}' is ambiguous because the entity contains multiple case variants.");
        return matches.Length == 1 ? matches[0] : requested;
    }

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
            result.Add(pair.Key, pair.Value);
        return result;
    }

    private sealed class ChangeLayerCommand : ICadCommand
    {
        public string Name => "CHLAYER";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            try
            {
                var handles = Handles(context.Arguments, 1, "CHLAYER handle... layerName");
                var requestedLayer = context.Arguments[^1];
                using var tx = context.Document.Database.BeginTransaction();
                var targetLayer = tx.GetLayer(requestedLayer)
                    ?? throw new KeyNotFoundException($"Layer '{requestedLayer}' does not exist.");
                if (targetLayer.IsLocked)
                    throw new InvalidOperationException($"Layer '{targetLayer.Name}' is locked.");
                if (targetLayer.IsFrozen)
                    throw new InvalidOperationException($"Layer '{targetLayer.Name}' is frozen.");

                var entities = RequireEntities(tx, handles);
                var changed = new List<CadEntitySnapshot>(entities.Count);
                foreach (var entity in entities)
                {
                    if (StringComparer.OrdinalIgnoreCase.Equals(entity.LayerName, targetLayer.Name))
                        continue;
                    RequireEditableSourceLayer(tx, entity);
                    changed.Add(entity with { LayerName = targetLayer.Name });
                }

                foreach (var entity in changed)
                    tx.Update(entity);
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success(changed.Count == 0
                    ? $"All {handles.Length} object(s) are already on layer '{targetLayer.Name}'."
                    : $"Moved {changed.Count} object(s) to layer '{targetLayer.Name}'.");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SetPropertyCommand : ICadCommand
    {
        public string Name => "SETPROP";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            try
            {
                var handles = Handles(context.Arguments, 2, "SETPROP handle... key value");
                var requestedKey = RequireMetadataKey(context.Arguments[^2]);
                var value = context.Arguments[^1];
                if (value.Length > 4096)
                    throw new ArgumentException("Metadata property value must not exceed 4096 characters.");

                using var tx = context.Document.Database.BeginTransaction();
                var entities = RequireEntities(tx, handles);
                var changed = new List<CadEntitySnapshot>(entities.Count);
                foreach (var entity in entities)
                {
                    var stableKey = ResolveStableKey(entity.Properties, requestedKey);
                    if (entity.Properties.TryGetValue(stableKey, out var current) && StringComparer.Ordinal.Equals(current, value))
                        continue;
                    RequireEditableSourceLayer(tx, entity);
                    var properties = CloneProperties(entity.Properties);
                    properties[stableKey] = value;
                    changed.Add(entity with { Properties = properties });
                }

                foreach (var entity in changed)
                    tx.Update(entity);
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success(changed.Count == 0
                    ? $"Property '{requestedKey}' already has the requested value on all {handles.Length} object(s)."
                    : $"Set metadata property '{requestedKey}' on {changed.Count} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class DeletePropertyCommand : ICadCommand
    {
        public string Name => "DELPROP";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            try
            {
                var handles = Handles(context.Arguments, 1, "DELPROP handle... key");
                var requestedKey = RequireMetadataKey(context.Arguments[^1]);
                using var tx = context.Document.Database.BeginTransaction();
                var entities = RequireEntities(tx, handles);
                var changed = new List<CadEntitySnapshot>(entities.Count);
                foreach (var entity in entities)
                {
                    var stableKey = ResolveStableKey(entity.Properties, requestedKey);
                    if (!entity.Properties.ContainsKey(stableKey))
                        continue;
                    RequireEditableSourceLayer(tx, entity);
                    var properties = CloneProperties(entity.Properties);
                    properties.Remove(stableKey);
                    changed.Add(entity with { Properties = properties });
                }

                foreach (var entity in changed)
                    tx.Update(entity);
                tx.Commit();
                context.Document.Editor.Selection.Set(handles);
                return CommandResult.Success(changed.Count == 0
                    ? $"Property '{requestedKey}' is absent from all {handles.Length} object(s)."
                    : $"Deleted metadata property '{requestedKey}' from {changed.Count} object(s).");
            }
            catch (Exception ex) when (ex is FormatException or ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
