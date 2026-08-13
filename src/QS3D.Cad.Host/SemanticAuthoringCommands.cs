using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Quantity;

namespace QS3D.Cad.Host;

public static class SemanticAuthoringCommands
{
    public static void RegisterAll(CommandRegistry registry, StandaloneSemanticWorkspace workspace)
    {
        registry.Register(new FloorCommand(workspace));
        registry.Register(new ZoneCommand(workspace));
        registry.Register(new PropertyCommand(workspace));
        registry.Register(new LocationCommand(workspace));
        registry.Register(new QuantityCommand(workspace));
        registry.Register(new ScheduleCommand(workspace));
    }

    private sealed class FloorCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public FloorCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSFLOOR";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 2) return CommandResult.Failure("Usage: QSFLOOR name elevationM");
            if (!double.TryParse(context.Arguments[1], NumberStyles.Float, CultureInfo.InvariantCulture, out var elevation) || !double.IsFinite(elevation))
                return CommandResult.Failure("elevationM must be a finite invariant-culture number.");
            try
            {
                var floor = _workspace.AddFloor(context.Document, context.Arguments[0], elevation);
                return CommandResult.Success($"Created floor {floor.Id.Value:D} '{floor.Name}' at {floor.ElevationM:R} m.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class ZoneCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public ZoneCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSZONE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: QSZONE name");
            try
            {
                var zone = _workspace.AddZone(context.Document, context.Arguments[0]);
                return CommandResult.Success($"Created zone {zone.Id.Value:D} '{zone.Name}'.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class PropertyCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public PropertyCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSPROP";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 3) return CommandResult.Failure("Usage: QSPROP handle key value");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                _workspace.SetProperty(context.Document, handle, context.Arguments[1], context.Arguments[2]);
                return CommandResult.Success($"Set semantic property '{context.Arguments[1]}' on {handle}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class LocationCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public LocationCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSLOC";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ModifiesDrawing;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 3) return CommandResult.Failure("Usage: QSLOC handle floorId|- zoneId|-");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                var floor = ParseFloor(context.Arguments[1]);
                var zone = ParseZone(context.Arguments[2]);
                _workspace.AssignLocation(context.Document, handle, floor, zone);
                return CommandResult.Success($"Assigned semantic location on {handle}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }

        private static FloorId? ParseFloor(string token)
        {
            if (token == "-") return null;
            return Guid.TryParse(token, out var value) && value != Guid.Empty
                ? new FloorId(value)
                : throw new FormatException("floorId must be a non-empty GUID or '-'.");
        }

        private static ZoneId? ParseZone(string token)
        {
            if (token == "-") return null;
            return Guid.TryParse(token, out var value) && value != Guid.Empty
                ? new ZoneId(value)
                : throw new FormatException("zoneId must be a non-empty GUID or '-'.");
        }
    }

    private sealed class QuantityCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public QuantityCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSQTY";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 3) return CommandResult.Failure("Usage: QSQTY handle code dimension [Property:Unit[:Exponent] ...]");
            try
            {
                var handle = new CadHandle(context.Arguments[0]);
                var project = _workspace.Ensure(context.Document);
                var element = _workspace.GetElementBySource(context.Document, handle);
                var rule = ParseRule(element.Kind, context.Arguments[1], context.Arguments[2], context.Arguments.Skip(3));
                var facts = QuantityRuleEngine.Evaluate(project, new QuantityRuleCatalog(new[] { rule }), skipRuleWhenInputMissing: true);
                var fact = facts.FirstOrDefault(candidate => candidate.ElementId == element.Id && StringComparer.Ordinal.Equals(candidate.Code, rule.Code));
                if (fact is null) return CommandResult.Failure($"Required rule inputs are missing for semantic element '{element.Name}'.");
                return CommandResult.Success($"{element.Name} {rule.Code} = {fact.Quantity.Value:R} {CanonicalSymbol(fact.Quantity.Dimension)}.");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or KeyNotFoundException or FormatException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private sealed class ScheduleCommand : ICadCommand
    {
        private readonly StandaloneSemanticWorkspace _workspace;
        public ScheduleCommand(StandaloneSemanticWorkspace workspace) => _workspace = workspace;
        public string Name => "QSSCHEDULE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;
        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count < 3) return CommandResult.Failure("Usage: QSSCHEDULE kind code dimension [Property:Unit[:Exponent] ...]");
            try
            {
                if (!Enum.TryParse<SemanticElementKind>(context.Arguments[0], true, out var kind) || kind == SemanticElementKind.Unknown)
                    return CommandResult.Failure($"Unknown semantic kind '{context.Arguments[0]}'.");
                var project = _workspace.Ensure(context.Document);
                var rule = ParseRule(kind, context.Arguments[1], context.Arguments[2], context.Arguments.Skip(3));
                var facts = QuantityRuleEngine.Evaluate(project, new QuantityRuleCatalog(new[] { rule }), skipRuleWhenInputMissing: true);
                var schedule = QuantityScheduleProjector.Project(project, facts);
                foreach (var row in schedule.Rows)
                {
                    var quantity = row.Quantities.Single(summary => StringComparer.Ordinal.Equals(summary.Code, rule.Code));
                    context.Document.Editor.WriteMessage($"{row.ElementId.Value:D} {row.ElementName} {rule.Code}={quantity.Quantity.Value:R} {CanonicalSymbol(quantity.Quantity.Dimension)}");
                }
                return CommandResult.Success($"QS schedule {rule.Code}: {schedule.Rows.Count} row(s).");
            }
            catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
            {
                return CommandResult.Failure(ex.Message);
            }
        }
    }

    private static QuantityRuleDefinition ParseRule(SemanticElementKind kind, string code, string dimensionToken, IEnumerable<string> factorTokens)
    {
        if (!Enum.TryParse<QuantityDimension>(dimensionToken, true, out var dimension))
            throw new FormatException($"Unknown quantity dimension '{dimensionToken}'.");
        var factors = factorTokens.Select(ParseFactor).ToArray();
        return new QuantityRuleDefinition(kind, code, dimension, factors);
    }

    private static QuantityFactor ParseFactor(string token)
    {
        var parts = token.Split(':');
        if (parts.Length is < 2 or > 3 || string.IsNullOrWhiteSpace(parts[0]))
            throw new FormatException($"Invalid factor '{token}'. Expected Property:Unit[:Exponent].");
        if (!Enum.TryParse<QuantityUnit>(parts[1], true, out var unit))
            throw new FormatException($"Unknown quantity unit '{parts[1]}'.");
        var exponent = 1;
        if (parts.Length == 3 && (!int.TryParse(parts[2], NumberStyles.Integer, CultureInfo.InvariantCulture, out exponent) || exponent < 1 || exponent > 3))
            throw new FormatException($"Invalid exponent in factor '{token}'.");
        return new QuantityFactor(parts[0], unit, exponent);
    }

    private static string CanonicalSymbol(QuantityDimension dimension)
    {
        return dimension switch
        {
            QuantityDimension.Count => "ea",
            QuantityDimension.Length => "m",
            QuantityDimension.Area => "m2",
            QuantityDimension.Volume => "m3",
            QuantityDimension.Mass => "kg",
            _ => throw new ArgumentOutOfRangeException(nameof(dimension))
        };
    }
}
