using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public static class PlotReferenceCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new Command());
        PlotManagementCommands.RegisterAll(registry);
    }

    private sealed class Command : ICadCommand
    {
        public string Name => "PLOTREF";

        // The legacy command records a request in the reference plot service, so it is not
        // read-only. It is retained as a compatibility wrapper while the explicit commands
        // expose precise flags and diagnostics.
        public CommandFlags Flags => CommandFlags.RequiresDocument;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: PLOTREF layout targetPdfPath");
            if (context.Document is not InMemoryCadDocument document) return CommandResult.Failure("PLOTREF requires the standalone reference adapter.");
            var service = InMemoryAdvancedServicesRegistry.For(document).Plot;
            var before = service.Requests.Count;
            try
            {
                var result = service.Plot(new CadPlotRequest(context.Arguments[0], CadPlotTargetKind.Pdf, context.Arguments[1]));
                if (service.Requests.Count == before)
                    return CommandResult.Failure(result.Message ?? "Reference plot request was not recorded.");
                return CommandResult.Success("Reference plot request recorded; no native file was produced.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }
}
