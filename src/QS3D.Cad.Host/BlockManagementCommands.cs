using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

internal static class BlockManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new CreateBlockAtCommand());
        registry.Register(new BlockInfoCommand());
        registry.Register(new BlockReferencesCommand());
        registry.Register(new CloneBlockCommand());
        registry.Register(new PurgeBlocksCommand());
        registry.Register(new SetBlockReferenceCommand());
    }

    internal static IReadOnlyList<string> DefinitionOwnersReferencing(
        IReadOnlyList<CadBlockDefinitionSnapshot> blocks,
        string targetName)
    {
        ArgumentNullException.ThrowIfNull(blocks);
        if (string.IsNullOrWhiteSpace(targetName)) throw new ArgumentException("Block name must not be blank.", nameof(targetName));
        return blocks
            .Where(block => block.Entities.Any(member => ReferencesBlock(member, targetName)))
            .Select(static block => block.Name)
            .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
            .ThenBy(static name => name, StringComparer.Ordinal)
            .ToArray();
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !double.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);

    private static bool ReferencesBlock(CadEntityDraft entity, string blockName)
        => entity.Kind == CadEntityKind.BlockReference
            && entity.Properties is not null
            && entity.Properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var referenced)
            && StringComparer.OrdinalIgnoreCase.Equals(referenced, blockName);

    private static bool ReferencesBlock(CadEntitySnapshot entity, string blockName)
        => entity.Kind == CadEntityKind.BlockReference
            && entity.Properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var referenced)
            && StringComparer.OrdinalIgnoreCase.Equals(referenced, blockName);

    private static bool TryReferencedBlock(CadEntityDraft entity, out string name)
    {
        name = string.Empty;
        if (entity.Kind != CadEntityKind.BlockReference || entity.Properties is null) return false;
        if (!entity.Properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var raw) || string.IsNullOrWhiteSpace(raw)) return false;
        name = raw.Trim();
        return true;
    }

    private static ReferenceState ReadReference(CadEntitySnapshot entity)
    {
        if (entity.Kind != CadEntityKind.BlockReference)
            throw new InvalidOperationException($"Entity {entity.Handle} is not a block reference.");
        var properties = entity.Properties;
        if (!properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var blockName) || string.IsNullOrWhiteSpace(blockName))
            throw new InvalidOperationException($"Block reference {entity.Handle} has no valid block definition name.");
        return new ReferenceState(
            blockName.Trim(),
            PropertyNumber(properties, CadBlockReferencePropertyNames.InsertionX, entity.Handle),
            PropertyNumber(properties, CadBlockReferencePropertyNames.InsertionY, entity.Handle),
            PropertyNumber(properties, CadBlockReferencePropertyNames.InsertionZ, entity.Handle),
            PositivePropertyNumber(properties, CadBlockReferencePropertyNames.UniformScale, entity.Handle),
            PropertyNumber(properties, CadBlockReferencePropertyNames.RotationRadians, entity.Handle));
    }

    private static double PropertyNumber(IReadOnlyDictionary<string, string> properties, string key, CadHandle handle)
    {
        if (!properties.TryGetValue(key, out var raw)
            || !double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value)
            || !double.IsFinite(value))
            throw new InvalidOperationException($"Block reference {handle} property '{key}' is missing or invalid.");
        return value;
    }

    private static double PositivePropertyNumber(IReadOnlyDictionary<string, string> properties, string key, CadHandle handle)
    {
        var value = PropertyNumber(properties, key, handle);
        if (value <= 0d) throw new InvalidOperationException($"Block reference {handle} property '{key}' must be greater than zero.");
        return value;
    }

    private static BoundingBox3 TransformExtents(
        CadBlockDefinitionSnapshot block,
        Point3 insertion,
        double scale,
        double rotationRadians)
    {
        if (!double.IsFinite(scale) || scale <= 0d) throw new ArgumentOutOfRangeException(nameof(scale), scale, "Block scale must be a finite value greater than zero.");
        if (!double.IsFinite(rotationRadians)) throw new ArgumentOutOfRangeException(nameof(rotationRadians), rotationRadians, "Block rotation must be finite.");
        Point3? minimum = null;
        Point3? maximum = null;
        foreach (var member in block.Entities)
        {
            var box = member.Extents;
            Accumulate(Transform(new Point3(box.Min.X, box.Min.Y, box.Min.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Min.X, box.Min.Y, box.Max.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Min.X, box.Max.Y, box.Min.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Min.X, box.Max.Y, box.Max.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Max.X, box.Min.Y, box.Min.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Max.X, box.Min.Y, box.Max.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Max.X, box.Max.Y, box.Min.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
            Accumulate(Transform(new Point3(box.Max.X, box.Max.Y, box.Max.Z), block.BasePoint, insertion, scale, rotationRadians), ref minimum, ref maximum);
        }
        return new BoundingBox3(
            minimum ?? throw new InvalidOperationException($"Block '{block.Name}' has no extents."),
            maximum ?? throw new InvalidOperationException($"Block '{block.Name}' has no extents."));
    }

    private static Point3 Transform(Point3 point, Point3 basePoint, Point3 insertion, double scale, double rotationRadians)
    {
        var localX = (point.X - basePoint.X) * scale;
        var localY = (point.Y - basePoint.Y) * scale;
        var localZ = (point.Z - basePoint.Z) * scale;
        var cosine = Math.Cos(rotationRadians);
        var sine = Math.Sin(rotationRadians);
        var x = insertion.X + (localX * cosine) - (localY * sine);
        var y = insertion.Y + (localX * sine) + (localY * cosine);
        var z = insertion.Z + localZ;
        if (!double.IsFinite(x) || !double.IsFinite(y) || !double.IsFinite(z))
            throw new OverflowException("Block reference transform produced a non-finite coordinate.");
        return new Point3(x, y, z);
    }

    private static void Accumulate(Point3 point, ref Point3? minimum, ref Point3? maximum)
    {
        if (minimum is null)
        {
            minimum = point;
            maximum = point;
            return;
        }
        var min = minimum.Value;
        var max = maximum!.Value;
        minimum = new Point3(Math.Min(min.X, point.X), Math.Min(min.Y, point.Y), Math.Min(min.Z, point.Z));
        maximum = new Point3(Math.Max(max.X, point.X), Math.Max(max.Y, point.Y), Math.Max(max.Z, point.Z));
    }

    private static bool SameBox(BoundingBox3 left, BoundingBox3 right)
        => left.Min.Equals(right.Min) && left.Max.Equals(right.Max);

    private sealed class CreateBlockAtCommand : ICadCommand
    {
        public string Name => "BLOCKBASE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 4) return CommandResult.Failure("Usage: BLOCKBASE name baseX baseY handle...");
            try
            {
                var baseX = Number(context.Arguments[1], "baseX");
                var baseY = Number(context.Arguments[2], "baseY");
                var handles = context.Arguments.Skip(3).Select(static token => new CadHandle(token)).Distinct().ToArray();
                using var tx = context.Document.Database.BeginTransaction();
                var snapshots = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    snapshots.Add(entity);
                }
                if (snapshots.Count == 0) return CommandResult.Failure("A block requires at least one source entity.");
                var baseZ = snapshots.Min(static entity => entity.Extents.Min.Z);
                var members = snapshots.Select(static entity => new CadEntityDraft(entity.Kind, entity.Extents, entity.Properties, entity.LayerName)).ToArray();
                tx.CreateBlock(context.Arguments[0], new Point3(baseX, baseY, baseZ), members);
                tx.Commit();
                return CommandResult.Success($"Created block '{context.Arguments[0]}' with explicit base point ({N(baseX)}, {N(baseY)}) and {members.Length} member(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class BlockInfoCommand : ICadCommand
    {
        public string Name => "BLOCKINFO";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: BLOCKINFO name");
            try
            {
                using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
                var block = tx.GetBlock(context.Arguments[0]);
                if (block is null) return CommandResult.Failure($"Block '{context.Arguments[0]}' does not exist.");
                var references = tx.Query().Count(entity => ReferencesBlock(entity, block.Name));
                var dependencies = block.Entities
                    .Select(member => TryReferencedBlock(member, out var name) ? name : null)
                    .Where(static name => name is not null)
                    .Select(static name => name!)
                    .Distinct(StringComparer.OrdinalIgnoreCase)
                    .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                var kinds = block.Entities.GroupBy(static member => member.Kind)
                    .OrderBy(static group => group.Key)
                    .Select(static group => $"{group.Key}:{group.Count()}")
                    .ToArray();
                context.Document.Editor.WriteMessage($"BLOCK {block.Name} base=({N(block.BasePoint.X)},{N(block.BasePoint.Y)},{N(block.BasePoint.Z)}) members={block.Entities.Count} references={references}");
                context.Document.Editor.WriteMessage($"Kinds: {(kinds.Length == 0 ? "none" : string.Join(", ", kinds))}");
                context.Document.Editor.WriteMessage($"Dependencies: {(dependencies.Length == 0 ? "none" : string.Join(", ", dependencies))}");
                return CommandResult.Success($"Inspected block '{block.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class BlockReferencesCommand : ICadCommand
    {
        public string Name => "BLOCKREFS";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: BLOCKREFS name");
            try
            {
                using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
                var block = tx.GetBlock(context.Arguments[0]);
                if (block is null) return CommandResult.Failure($"Block '{context.Arguments[0]}' does not exist.");
                var references = tx.Query()
                    .Where(entity => ReferencesBlock(entity, block.Name))
                    .Select(static entity => entity.Handle)
                    .OrderBy(static handle => handle)
                    .ToArray();
                context.Document.Editor.Selection.Set(references);
                return CommandResult.Success($"Selected {references.Length} reference(s) to block '{block.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class CloneBlockCommand : ICadCommand
    {
        public string Name => "BLOCKCLONE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: BLOCKCLONE sourceName targetName");
            try
            {
                using var tx = context.Document.Database.BeginTransaction();
                var source = tx.GetBlock(context.Arguments[0]);
                if (source is null) return CommandResult.Failure($"Block '{context.Arguments[0]}' does not exist.");
                tx.CreateBlock(context.Arguments[1], source.BasePoint, source.Entities);
                tx.Commit();
                return CommandResult.Success($"Cloned block '{source.Name}' as '{context.Arguments[1]}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class PurgeBlocksCommand : ICadCommand
    {
        public string Name => "BLOCKPURGE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: BLOCKPURGE");
            try
            {
                using var tx = context.Document.Database.BeginTransaction();
                var blocks = tx.GetBlocks();
                var byName = blocks.ToDictionary(static block => block.Name, StringComparer.OrdinalIgnoreCase);
                var reachable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                var pending = new Stack<string>();
                foreach (var entity in tx.Query())
                {
                    if (entity.Kind != CadEntityKind.BlockReference) continue;
                    var state = ReadReference(entity);
                    if (!byName.ContainsKey(state.BlockName))
                        return CommandResult.Failure($"Block reference {entity.Handle} targets missing block '{state.BlockName}'.");
                    pending.Push(state.BlockName);
                }

                while (pending.Count > 0)
                {
                    var name = pending.Pop();
                    if (!reachable.Add(name)) continue;
                    var block = byName[name];
                    foreach (var member in block.Entities)
                    {
                        if (member.Kind != CadEntityKind.BlockReference) continue;
                        if (!TryReferencedBlock(member, out var dependency))
                            return CommandResult.Failure($"Block '{block.Name}' contains a malformed block reference member.");
                        if (!byName.ContainsKey(dependency))
                            return CommandResult.Failure($"Block '{block.Name}' references missing block '{dependency}'.");
                        pending.Push(dependency);
                    }
                }

                var purge = blocks
                    .Where(block => !reachable.Contains(block.Name))
                    .Select(static block => block.Name)
                    .OrderBy(static name => name, StringComparer.OrdinalIgnoreCase)
                    .ThenBy(static name => name, StringComparer.Ordinal)
                    .ToArray();
                foreach (var name in purge) tx.EraseBlock(name);
                tx.Commit();
                return CommandResult.Success(purge.Length == 0
                    ? "No unreferenced block definitions to purge."
                    : $"Purged {purge.Length} unreferenced block definition(s): {string.Join(", ", purge)}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SetBlockReferenceCommand : ICadCommand
    {
        public string Name => "BLOCKSET";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 6)
                return CommandResult.Failure("Usage: BLOCKSET handle blockName x y scale rotationDegrees");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                var x = Number(context.Arguments[2], "x");
                var y = Number(context.Arguments[3], "y");
                var scale = Number(context.Arguments[4], "scale");
                if (scale <= 0d) return CommandResult.Failure("scale must be greater than zero.");
                var degrees = Number(context.Arguments[5], "rotationDegrees");
                var radians = degrees * Math.PI / 180d;
                if (!double.IsFinite(radians)) return CommandResult.Failure("rotationDegrees is too large.");

                using var tx = context.Document.Database.BeginTransaction();
                var existing = tx.Get(handle);
                if (existing is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                var current = ReadReference(existing);
                var block = tx.GetBlock(context.Arguments[1]);
                if (block is null) return CommandResult.Failure($"Block '{context.Arguments[1]}' does not exist.");
                var insertion = new Point3(x, y, current.InsertionZ);
                var extents = TransformExtents(block, insertion, scale, radians);

                var properties = new Dictionary<string, string>(existing.Properties, StringComparer.Ordinal)
                {
                    [CadBlockReferencePropertyNames.BlockName] = block.Name,
                    [CadBlockReferencePropertyNames.InsertionX] = N(x),
                    [CadBlockReferencePropertyNames.InsertionY] = N(y),
                    [CadBlockReferencePropertyNames.InsertionZ] = N(current.InsertionZ),
                    [CadBlockReferencePropertyNames.UniformScale] = N(scale),
                    [CadBlockReferencePropertyNames.RotationRadians] = N(radians)
                };

                var unchanged = StringComparer.OrdinalIgnoreCase.Equals(current.BlockName, block.Name)
                    && current.InsertionX.Equals(x)
                    && current.InsertionY.Equals(y)
                    && current.UniformScale.Equals(scale)
                    && current.RotationRadians.Equals(radians)
                    && SameBox(existing.Extents, extents);
                if (!unchanged)
                {
                    tx.Update(existing with { Extents = extents, Properties = properties });
                    tx.Commit();
                }
                context.Document.Editor.Selection.Set(new[] { handle });
                return CommandResult.Success(unchanged
                    ? $"Block reference {handle} already matches the requested parameters."
                    : $"Updated block reference {handle} to '{block.Name}' at ({N(x)}, {N(y)}) scale={N(scale)} rotation={N(degrees)}deg.");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed record ReferenceState(
        string BlockName,
        double InsertionX,
        double InsertionY,
        double InsertionZ,
        double UniformScale,
        double RotationRadians);
}
