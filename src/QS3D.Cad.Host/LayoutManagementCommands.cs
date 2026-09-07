using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

internal static class LayoutManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new ListCommand());
        registry.Register(new CurrentCommand());
        registry.Register(new CreateCommand());
        registry.Register(new SetCommand());
        registry.Register(new DeleteCommand());
        registry.Register(new HealthCommand());
    }

    private static InMemoryLayoutService Service(CommandContext context)
    {
        if (context.Document is not InMemoryCadDocument document)
            throw new InvalidOperationException("Layout management requires the standalone reference adapter.");
        return InMemoryAdvancedServicesRegistry.For(document).Layouts;
    }

    private static CadLayoutSnapshot Require(InMemoryLayoutService service, string name)
    {
        var normalized = name?.Trim();
        if (string.IsNullOrWhiteSpace(normalized)) throw new ArgumentException("Layout name must not be blank.", nameof(name));
        return service.GetLayouts().FirstOrDefault(item => StringComparer.OrdinalIgnoreCase.Equals(item.Name, normalized))
            ?? throw new KeyNotFoundException($"Layout '{normalized}' does not exist.");
    }

    private static string Describe(CadLayoutSnapshot layout)
        => $"{layout.Name} model={layout.IsModel} paper={layout.PaperWidthMm:R}x{layout.PaperHeightMm:R}mm pageSetup='{layout.PageSetupName ?? string.Empty}'";

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
        public override string Name => "LAYOUTLIST";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: LAYOUTLIST");
            try
            {
                var service = Service(context);
                var items = service.GetLayouts();
                foreach (var item in items)
                {
                    var current = StringComparer.OrdinalIgnoreCase.Equals(item.Name, service.CurrentLayoutName);
                    context.Document.Editor.WriteMessage($"LAYOUT {Describe(item)} current={current}");
                }
                return CommandResult.Success($"{items.Count} layout(s); current='{service.CurrentLayoutName}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class CurrentCommand : CommandBase
    {
        public override string Name => "LAYOUTCURRENT";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: LAYOUTCURRENT");
            try
            {
                var service = Service(context);
                var current = Require(service, service.CurrentLayoutName);
                context.Document.Editor.WriteMessage($"LAYOUT current {Describe(current)}");
                return CommandResult.Success($"Current layout: '{current.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class CreateCommand : CommandBase
    {
        public override string Name => "LAYOUTCREATE";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: LAYOUTCREATE name");
            try
            {
                var created = Service(context).Create(context.Arguments[0]);
                return CommandResult.Success($"Created layout '{created.Name}' paper={created.PaperWidthMm:R}x{created.PaperHeightMm:R}mm.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class SetCommand : CommandBase
    {
        public override string Name => "LAYOUTSET";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: LAYOUTSET name");
            try
            {
                var service = Service(context);
                service.SetCurrent(context.Arguments[0]);
                return CommandResult.Success($"Current layout: '{service.CurrentLayoutName}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class DeleteCommand : CommandBase
    {
        public override string Name => "LAYOUTDELETE";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: LAYOUTDELETE name");
            try
            {
                var service = Service(context);
                var item = Require(service, context.Arguments[0]);
                service.Delete(item.Name);
                return CommandResult.Success($"Deleted layout '{item.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class HealthCommand : CommandBase
    {
        public override string Name => "LAYOUTHEALTH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: LAYOUTHEALTH");
            try
            {
                var service = Service(context);
                var items = service.GetLayouts();
                var model = items.Count(static item => item.IsModel);
                var paper = items.Count - model;
                var currentExists = items.Any(item => StringComparer.OrdinalIgnoreCase.Equals(item.Name, service.CurrentLayoutName));
                var healthy = model == 1 && currentExists;
                var message = $"LAYOUT health total={items.Count} model={model} paper={paper} current='{service.CurrentLayoutName}' currentExists={currentExists} healthy={healthy}.";
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
