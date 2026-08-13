using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Diagnostics;
using QS3D.Platform.Domain;
using QS3D.Platform.Quantity;

namespace QS3D.Cad.Host;

public static class SemanticCommands
{
    public static void RegisterAll(CommandRegistry registry, StandaloneSemanticWorkspace workspace)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(workspace);
        registry.Register(new TagCommand(workspace));
        registry.Register(new SemanticListCommand(workspace));
        registry.Register(new HealthCommand(workspace));
        registry.Register(new CountCommand(workspace));
    }

    private sealed class TagCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public TagCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSTAG";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 2) return CommandResult.Failure("Usage: QSTAG handle kind [name]");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                if (!Enum.TryParse<SemanticElementKind>(context.Arguments[1], true, out var kind) || kind == SemanticElementKind.Unknown)
                    return CommandResult.Failure($"Unknown semantic kind '{context.Arguments[1]}'.");

                using (var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
                {
                    if (tx.Get(handle) is null) return CommandResult.Failure($"Entity {handle} does not exist.");
                }

                var name = context.Arguments.Count > 2 ? string.Join(" ", context.Arguments.Skip(2)) : null;
                var element = _workspace.TagSource(context.Document, handle, kind, name);
                return CommandResult.Success($"Tagged {handle} as {kind} element {element.Id.Value:D}.");
            }
            catch (Exception ex) when (ex is FormatException or InvalidOperationException or ArgumentOutOfRangeException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class SemanticListCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public SemanticListCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSLIST";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            var project = _workspace.Ensure(context.Document);
            foreach (var element in project.Elements.OrderBy(static x => x.Kind).ThenBy(static x => x.Name, StringComparer.Ordinal))
            {
                var source = element.SourceReference.HasValue ? element.SourceReference.Value.Handle.ToString() : "-";
                context.Document.Editor.WriteMessage($"{element.Id.Value:D} {element.Kind} '{element.Name}' source={source}");
            }
            return CommandResult.Success($"{project.Elements.Count} semantic element(s).");
        }
    }

    private sealed class HealthCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public HealthCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSHEALTH";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            var report = SemanticHealthAnalyzer.Analyze(_workspace.Ensure(context.Document));
            foreach (var finding in report.Findings)
                context.Document.Editor.WriteMessage($"{finding.Severity} {finding.Code}: {finding.Message}");
            return report.IsReady
                ? CommandResult.Success($"QS health ready: {report.WarningCount} warning(s), 0 error(s).")
                : CommandResult.Failure($"QS health blocked: {report.WarningCount} warning(s), {report.ErrorCount} error(s).");
        }
    }

    private sealed class CountCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public CountCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSCOUNT";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            SemanticElementKind? filter = null;
            if (context.Arguments.Count > 1) return CommandResult.Failure("Usage: QSCOUNT [kind]");
            if (context.Arguments.Count == 1)
            {
                if (!Enum.TryParse<SemanticElementKind>(context.Arguments[0], true, out var parsed) || parsed == SemanticElementKind.Unknown)
                    return CommandResult.Failure($"Unknown semantic kind '{context.Arguments[0]}'.");
                filter = parsed;
            }

            var project = _workspace.Ensure(context.Document);
            var elements = project.Elements.Where(element => !filter.HasValue || element.Kind == filter.Value).ToArray();
            var facts = elements.Select(element => new QuantityFact(element.Id, "ELEMENT.COUNT", new QuantityValue(QuantityDimension.Count, 1d), element.SourceReference)).ToArray();
            var summaries = QuantityAccumulator.Summarize(facts);
            var count = summaries.Count == 0 ? 0d : summaries.Single().Quantity.Value;
            var label = filter.HasValue ? filter.Value.ToString() : "all semantic elements";
            return CommandResult.Success($"QS count {label}: {count:R} ea.");
        }
    }
}
