using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Host;

public static class BlockCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new CreateBlockCommand());
        registry.Register(new InsertBlockCommand());
        registry.Register(new ListBlocksCommand());
        registry.Register(new DeleteBlockCommand());
    }

    private static double Number(string token, string label)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) || !Numeric.IsFinite(value))
            throw new FormatException($"{label} must be a finite invariant-culture number.");
        return value;
    }

    private sealed class CreateBlockCommand : ICadCommand
    {
        public string Name => "BLOCK";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 2) return CommandResult.Failure("Usage: BLOCK name handle...");
            try
            {
                var handles = context.Arguments.Skip(1).Select(static token => new CadHandle(token)).Distinct().ToArray();
                using var tx = context.Document.Database.BeginTransaction();
                var snapshots = new List<CadEntitySnapshot>(handles.Length);
                foreach (var handle in handles)
                {
                    var entity = tx.Get(handle);
                    if (entity is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                    snapshots.Add(entity);
                }
                if (snapshots.Count == 0) return CommandResult.Failure("A block requires at least one source entity.");

                var basePoint = new Point3(
                    snapshots.Min(static x => x.Extents.Min.X),
                    snapshots.Min(static x => x.Extents.Min.Y),
                    snapshots.Min(static x => x.Extents.Min.Z));
                var members = snapshots.Select(static entity =>
                    new CadEntityDraft(entity.Kind, entity.Extents, entity.Properties, entity.LayerName)).ToArray();
                tx.CreateBlock(context.Arguments[0], basePoint, members);
                tx.Commit();
                return CommandResult.Success($"Created block '{context.Arguments[0]}' with {members.Length} member(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class InsertBlockCommand : ICadCommand
    {
        public string Name => "INSERT";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 3 || context.Arguments.Count > 5)
                return CommandResult.Failure("Usage: INSERT name x y [scale] [rotationDegrees]");
            try
            {
                var x = Number(context.Arguments[1], "x");
                var y = Number(context.Arguments[2], "y");
                var scale = context.Arguments.Count >= 4 ? Number(context.Arguments[3], "scale") : 1d;
                var degrees = context.Arguments.Count == 5 ? Number(context.Arguments[4], "rotationDegrees") : 0d;
                using var tx = context.Document.Database.BeginTransaction();
                var handle = tx.InsertBlock(context.Arguments[0], new Point3(x, y), scale, degrees * Math.PI / 180d);
                tx.Commit();
                return CommandResult.Success($"Inserted block '{context.Arguments[0]}' as {handle}.");
            }
            catch (Exception ex) when (ex is ArgumentException or FormatException or InvalidOperationException or KeyNotFoundException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class ListBlocksCommand : ICadCommand
    {
        public string Name => "BLOCKS";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            foreach (var block in tx.GetBlocks())
                context.Document.Editor.WriteMessage($"{block.Name} members={block.Entities.Count} base={block.BasePoint}");
            return CommandResult.Success($"{tx.GetBlocks().Count} block definition(s).");
        }
    }

    private sealed class DeleteBlockCommand : ICadCommand
    {
        public string Name => "BLOCKDELETE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: BLOCKDELETE name");
            try
            {
                using var tx = context.Document.Database.BeginTransaction();
                tx.EraseBlock(context.Arguments[0]);
                tx.Commit();
                return CommandResult.Success($"Deleted block '{context.Arguments[0]}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
