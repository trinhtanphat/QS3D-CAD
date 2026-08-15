using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public static class XrefReferenceCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new Command());
        XrefManagementCommands.RegisterAll(registry);
    }

    private sealed class Command : ICadCommand
    {
        public string Name => "XREFREF";

        // Legacy compatibility wrapper mixes LIST with lifecycle mutations, so it must not
        // advertise the stronger ReadOnly or ModifiesDrawing contract. New explicit commands
        // expose precise flags per operation.
        public CommandFlags Flags => CommandFlags.RequiresDocument;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count == 0) return CommandResult.Failure("Usage: XREFREF LIST|ATTACH|RELOAD|UNLOAD|DETACH ...");
            if (context.Document is not InMemoryCadDocument document) return CommandResult.Failure("XREFREF requires the standalone reference adapter.");
            var service = InMemoryAdvancedServicesRegistry.For(document).Xrefs;
            try
            {
                switch (context.Arguments[0].ToUpperInvariant())
                {
                    case "LIST":
                        if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: XREFREF LIST");
                        foreach (var item in service.GetXrefs()) context.Document.Editor.WriteMessage($"XREFREF {item.Name} {item.Kind} {item.Status} '{item.Path}'");
                        return CommandResult.Success($"{service.GetXrefs().Count} reference xref(s).");
                    case "ATTACH":
                        if (context.Arguments.Count is < 3 or > 4) return CommandResult.Failure("Usage: XREFREF ATTACH name path [Attach|Overlay]");
                        var kind = CadXrefKind.Attach;
                        if (context.Arguments.Count == 4 && !Enum.TryParse(context.Arguments[3], true, out kind)) return CommandResult.Failure($"Unknown xref kind '{context.Arguments[3]}'.");
                        var attached = service.Attach(context.Arguments[2], context.Arguments[1], kind);
                        return CommandResult.Success($"Reference xref '{attached.Name}' state={attached.Status}.");
                    case "RELOAD":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: XREFREF RELOAD name");
                        service.Reload(context.Arguments[1]);
                        return CommandResult.Success($"Reference xref '{context.Arguments[1]}' reloaded.");
                    case "UNLOAD":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: XREFREF UNLOAD name");
                        service.Unload(context.Arguments[1]);
                        return CommandResult.Success($"Reference xref '{context.Arguments[1]}' unloaded.");
                    case "DETACH":
                        if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: XREFREF DETACH name");
                        service.Detach(context.Arguments[1]);
                        return CommandResult.Success($"Reference xref '{context.Arguments[1]}' detached.");
                    default:
                        return CommandResult.Failure($"Unknown XREFREF action '{context.Arguments[0]}'.");
                }
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
