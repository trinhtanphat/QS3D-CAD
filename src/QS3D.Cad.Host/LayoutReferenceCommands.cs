using QS3D.Platform.Application;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public static class LayoutReferenceCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new Command());
    }

    private sealed class Command : ICadCommand
    {
        public string Name => "LAYOUTREF";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0) return CommandResult.Failure("Usage: LAYOUTREF LIST|CREATE|SET|DELETE ...");
            if (context.Document is not InMemoryCadDocument document) return CommandResult.Failure("LAYOUTREF requires the standalone reference adapter.");
            var service = InMemoryAdvancedServicesRegistry.For(document).Layouts;
            try
            {
                switch (context.Arguments[0].ToUpperInvariant())
                {
                    case "LIST":
                        if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: LAYOUTREF LIST");
                        foreach (var layout in service.GetLayouts()) context.Document.Editor.WriteMessage($"LAYOUTREF {layout.Name} model={layout.IsModel} paper={layout.PaperWidthMm:R}x{layout.PaperHeightMm:R}mm");
                        return CommandResult.Success($"Current reference layout: {service.CurrentLayoutName}.");
                    case "CREATE":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: LAYOUTREF CREATE name");
                        var created = service.Create(context.Arguments[1]);
                        return CommandResult.Success($"Created reference layout '{created.Name}'.");
                    case "SET":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: LAYOUTREF SET name");
                        service.SetCurrent(context.Arguments[1]);
                        return CommandResult.Success($"Current reference layout: {service.CurrentLayoutName}.");
                    case "DELETE":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: LAYOUTREF DELETE name");
                        service.Delete(context.Arguments[1]);
                        return CommandResult.Success($"Deleted reference layout '{context.Arguments[1]}'.");
                    default:
                        return CommandResult.Failure($"Unknown LAYOUTREF action '{context.Arguments[0]}'.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
