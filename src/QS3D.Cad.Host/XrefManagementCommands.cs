using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

internal static class XrefManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ListCommand());
        registry.Register(new StatusCommand());
        registry.Register(new AttachCommand());
        registry.Register(new ReloadCommand());
        registry.Register(new UnloadCommand());
        registry.Register(new DetachCommand());
        registry.Register(new HealthCommand());
        registry.Register(new ReloadAllCommand());
    }

    private static InMemoryXrefService Service(CommandContext context)
    {
        if (context.Document is not InMemoryCadDocument document)
            throw new InvalidOperationException("Xref management requires the standalone reference adapter.");
        return InMemoryAdvancedServicesRegistry.For(document).Xrefs;
    }

    private static CadXrefSnapshot Require(InMemoryXrefService service, string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Xref name must not be blank.", nameof(name));
        return service.GetXrefs().FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Name, normalized))
            ?? throw new KeyNotFoundException($"Xref '{normalized}' does not exist.");
    }

    private static string Describe(CadXrefSnapshot item)
        => $"{item.Name} kind={item.Kind} status={item.Status} path='{item.Path}'";

    private static bool TryKind(string token, out CadXrefKind kind)
        => Enum.TryParse(token, true, out kind) && Enum.IsDefined(typeof(CadXrefKind), kind);

    private abstract class CommandBase : ICadCommand
    {
        public abstract string Name { get; }
        public abstract CommandFlags Flags { get; }
        public abstract CommandResult Execute(CommandContext context);

        protected static CommandResult Failure(Exception ex)
            => CommandResult.Failure(ex.Message);
    }

    private sealed class ListCommand : CommandBase
    {
        public override string Name => "XREFLIST";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: XREFLIST");
            try
            {
                var items = Service(context).GetXrefs();
                foreach (var item in items) context.Document.Editor.WriteMessage($"XREF {Describe(item)}");
                return CommandResult.Success($"{items.Count} external reference(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class StatusCommand : CommandBase
    {
        public override string Name => "XREFSTATUS";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: XREFSTATUS name");
            try
            {
                var item = Require(Service(context), context.Arguments[0]);
                context.Document.Editor.WriteMessage($"XREF {Describe(item)}");
                return CommandResult.Success($"Xref '{item.Name}' status={item.Status}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class AttachCommand : CommandBase
    {
        public override string Name => "XREFATTACH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count is < 2 or > 3)
                return CommandResult.Failure("Usage: XREFATTACH name path [Attach|Overlay]");
            try
            {
                var kind = CadXrefKind.Attach;
                if (context.Arguments.Count == 3 && !TryKind(context.Arguments[2], out kind))
                    return CommandResult.Failure($"Unknown xref kind '{context.Arguments[2]}'.");
                var item = Service(context).Attach(context.Arguments[1], context.Arguments[0], kind);
                return CommandResult.Success($"Attached xref '{item.Name}' kind={item.Kind} status={item.Status}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class ReloadCommand : CommandBase
    {
        public override string Name => "XREFRELOAD";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: XREFRELOAD name");
            try
            {
                var service = Service(context);
                service.Reload(context.Arguments[0]);
                var item = Require(service, context.Arguments[0]);
                return CommandResult.Success($"Reloaded xref '{item.Name}' status={item.Status}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class UnloadCommand : CommandBase
    {
        public override string Name => "XREFUNLOAD";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: XREFUNLOAD name");
            try
            {
                var service = Service(context);
                service.Unload(context.Arguments[0]);
                var item = Require(service, context.Arguments[0]);
                return CommandResult.Success($"Unloaded xref '{item.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class DetachCommand : CommandBase
    {
        public override string Name => "XREFDETACH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: XREFDETACH name");
            try
            {
                var service = Service(context);
                var item = Require(service, context.Arguments[0]);
                service.Detach(item.Name);
                return CommandResult.Success($"Detached xref '{item.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class HealthCommand : CommandBase
    {
        public override string Name => "XREFHEALTH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: XREFHEALTH");
            try
            {
                var items = Service(context).GetXrefs();
                var loaded = items.Count(static item => item.Status == CadXrefStatus.Loaded);
                var unloaded = items.Count(static item => item.Status == CadXrefStatus.Unloaded);
                var missing = items.Count(static item => item.Status == CadXrefStatus.Missing);
                var unresolved = items.Count(static item => item.Status == CadXrefStatus.Unresolved);
                var circular = items.Count(static item => item.Status == CadXrefStatus.CircularDependency);
                var problematic = missing + unresolved + circular;
                var message = $"XREF health total={items.Count} loaded={loaded} unloaded={unloaded} missing={missing} unresolved={unresolved} circular={circular}.";
                context.Document.Editor.WriteMessage(message);
                return CommandResult.Success(problematic == 0 ? message : $"{message} {problematic} problematic reference(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class ReloadAllCommand : CommandBase
    {
        public override string Name => "XREFRELOADALL";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: XREFRELOADALL");
            try
            {
                var service = Service(context);
                var names = service.GetXrefs().Select(static item => item.Name).ToArray();
                foreach (var name in names)
                {
                    context.CancellationToken.ThrowIfCancellationRequested();
                    service.Reload(name);
                }
                var items = service.GetXrefs();
                var loaded = items.Count(static item => item.Status == CadXrefStatus.Loaded);
                var missing = items.Count(static item => item.Status == CadXrefStatus.Missing);
                var unresolved = items.Count(static item => item.Status == CadXrefStatus.Unresolved);
                var circular = items.Count(static item => item.Status == CadXrefStatus.CircularDependency);
                return CommandResult.Success($"Reloaded {items.Count} xref(s): loaded={loaded}, missing={missing}, unresolved={unresolved}, circular={circular}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }
}
