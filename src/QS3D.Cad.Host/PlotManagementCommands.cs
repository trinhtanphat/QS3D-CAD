using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

internal static class PlotManagementCommands
{
    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RequestCommand());
        registry.Register(new ListCommand());
        registry.Register(new HealthCommand());
    }

    private static InMemoryAdvancedServices Services(CommandContext context)
    {
        if (context.Document is not InMemoryCadDocument document)
            throw new InvalidOperationException("Plot management requires the standalone reference adapter.");
        return InMemoryAdvancedServicesRegistry.For(document);
    }

    private static bool TryTargetKind(string token, out CadPlotTargetKind kind)
        => Enum.TryParse(token, true, out kind) && Enum.IsDefined(typeof(CadPlotTargetKind), kind);

    private static string Describe(CadPlotRequest request, int index)
        => $"#{index + 1} layout='{request.LayoutName}' kind={request.TargetKind} target='{request.Target}' pageSetup='{request.PageSetupName ?? string.Empty}'";

    private abstract class CommandBase : ICadCommand
    {
        public abstract string Name { get; }
        public abstract CommandFlags Flags { get; }
        public abstract CommandResult Execute(CommandContext context);

        protected static CommandResult Failure(Exception ex)
            => CommandResult.Failure(ex.Message);
    }

    private sealed class RequestCommand : CommandBase
    {
        public override string Name => "PLOTREQUEST";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count is < 2 or > 3)
                return CommandResult.Failure("Usage: PLOTREQUEST layout target [Pdf|Printer]");
            try
            {
                var kind = CadPlotTargetKind.Pdf;
                if (context.Arguments.Count == 3 && !TryTargetKind(context.Arguments[2], out kind))
                    return CommandResult.Failure($"Unknown plot target kind '{context.Arguments[2]}'.");

                var services = Services(context);
                var service = services.Plot;
                var before = service.Requests.Count;
                var request = new CadPlotRequest(context.Arguments[0], kind, context.Arguments[1]);
                var result = service.Plot(request);
                if (service.Requests.Count == before)
                    return CommandResult.Failure(result.Message ?? "Reference plot request was not recorded.");
                return CommandResult.Success($"Recorded reference plot request #{service.Requests.Count} kind={kind}; no native output was produced.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class ListCommand : CommandBase
    {
        public override string Name => "PLOTLIST";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: PLOTLIST");
            try
            {
                var requests = Services(context).Plot.Requests;
                for (var i = 0; i < requests.Count; i++)
                    context.Document.Editor.WriteMessage($"PLOT {Describe(requests[i], i)}");
                return CommandResult.Success($"{requests.Count} recorded reference plot request(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }

    private sealed class HealthCommand : CommandBase
    {
        public override string Name => "PLOTHEALTH";
        public override CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public override CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: PLOTHEALTH");
            try
            {
                var services = Services(context);
                var requests = services.Plot.Requests;
                var layoutNames = services.Layouts.GetLayouts().Select(static item => item.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
                var pdf = requests.Count(static item => item.TargetKind == CadPlotTargetKind.Pdf);
                var printer = requests.Count(static item => item.TargetKind == CadPlotTargetKind.Printer);
                var orphaned = requests.Count(item => !layoutNames.Contains(item.LayoutName));
                var message = $"PLOT health total={requests.Count} pdf={pdf} printer={printer} orphanedLayoutRequests={orphaned}.";
                context.Document.Editor.WriteMessage(message);
                return CommandResult.Success(orphaned == 0 ? message : $"{message} {orphaned} request(s) reference layouts that no longer exist.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return Failure(ex);
            }
        }
    }
}
