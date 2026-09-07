using System.Globalization;
using QS3D.Platform.Application;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Parity;

namespace QS3D.Cad.Host;

public static class CubicostParityCommands
{
    private const string DefaultRegion = "DRAWING";
    private const int MaxLocatePairs = 200;
    private static readonly MepRecognitionProfile RecognitionProfile = MepRecognitionProfiles.CreateDefault();

    public static void RegisterAll(CommandRegistry registry)
    {
        ArgumentNullException.ThrowIfNull(registry);
        registry.Register(new RecognizeCommand());
        registry.Register(new TakeoffCommand());
        registry.Register(new ClashCommand());
        registry.Register(new ClashLocateCommand());
        registry.Register(new IssuesCommand());
    }

    private sealed class RecognizeCommand : ICadCommand
    {
        public string Name => "QSMEPRECOGNIZE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 0) return CommandResult.Failure("Usage: QSMEPRECOGNIZE");
            var selected = Selected(context);
            if (selected.Count == 0) return CommandResult.Failure("QSMEPRECOGNIZE requires a non-empty selection.");

            var matched = 0;
            var ambiguous = 0;
            var unmatched = 0;
            foreach (var entity in selected)
            {
                var recognition = Recognize(entity);
                switch (recognition.Status)
                {
                    case MepRecognitionStatus.Matched:
                        matched++;
                        context.Document.Editor.WriteMessage($"{entity.Handle} Matched {recognition.Discipline}/{recognition.Category}/{recognition.MepKind?.ToString() ?? "-"}");
                        break;
                    case MepRecognitionStatus.Ambiguous:
                        ambiguous++;
                        context.Document.Editor.WriteMessage($"{entity.Handle} Ambiguous rules={string.Join(",", recognition.MatchedRuleIds)}");
                        break;
                    default:
                        unmatched++;
                        context.Document.Editor.WriteMessage($"{entity.Handle} Unmatched");
                        break;
                }
            }
            return CommandResult.Success($"MEP recognition: matched={matched}, ambiguous={ambiguous}, unmatched={unmatched}.");
        }
    }

    private sealed class TakeoffCommand : ICadCommand
    {
        public string Name => "QSMEPTAKEOFF";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 1) return CommandResult.Failure("Usage: QSMEPTAKEOFF metersPerUnit");
            if (!TryPositive(context.Arguments[0], "metersPerUnit", out var metersPerUnit, out var error)) return CommandResult.Failure(error);

            var selected = Selected(context);
            if (selected.Count == 0) return CommandResult.Failure("QSMEPTAKEOFF requires a non-empty selection.");

            var elements = new List<MepElement>();
            var skipped = 0;
            foreach (var entity in selected)
            {
                var recognition = Recognize(entity);
                if (recognition.Status != MepRecognitionStatus.Matched || recognition.Discipline != MepDiscipline.Mep || !recognition.MepKind.HasValue)
                {
                    skipped++;
                    continue;
                }

                if (!TryCount(entity, out var count, out error)) return CommandResult.Failure(error);
                if (!TryMetric(entity, Metric.Length, metersPerUnit, out var lengthM, out error)) return CommandResult.Failure(error);
                if (!TryMetric(entity, Metric.Area, metersPerUnit, out var areaM2, out error)) return CommandResult.Failure(error);
                if (!TryMetric(entity, Metric.Volume, metersPerUnit, out var volumeM3, out error)) return CommandResult.Failure(error);

                var category = recognition.Category ?? recognition.MepKind.Value.ToString();
                elements.Add(new MepElement(
                    entity.Handle.ToString(),
                    recognition.MepKind.Value,
                    TextProperty(entity, "QS3D.Mep.System", entity.LayerName),
                    TextProperty(entity, "QS3D.Mep.Specification", BlockOrKind(entity, category)),
                    TextProperty(entity, "QS3D.Mep.Region", DefaultRegion),
                    count,
                    lengthM,
                    areaM2,
                    volumeM3));
            }

            if (elements.Count == 0) return CommandResult.Failure($"No unambiguous MEP entities were recognized; skipped={skipped}.");
            var rows = new MepQuantityService().Aggregate(elements);
            foreach (var row in rows)
            {
                context.Document.Editor.WriteMessage(
                    $"{row.Region} | {row.System} | {row.Specification} | {row.Kind} | entities={row.ElementCount} count={row.QuantityCount} " +
                    $"L={row.LengthM.ToString("0.###", CultureInfo.InvariantCulture)}m " +
                    $"A={row.AreaM2.ToString("0.###", CultureInfo.InvariantCulture)}m2 " +
                    $"V={row.VolumeM3.ToString("0.###", CultureInfo.InvariantCulture)}m3");
            }
            return CommandResult.Success($"MEP takeoff: recognized={elements.Count}, groups={rows.Count}, skipped={skipped}.");
        }
    }

    private sealed class ClashCommand : ICadCommand
    {
        public string Name => "QSMEPCLASH";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (!TryClashArguments(context, "QSMEPCLASH", out var clearanceM, out var metersPerUnit, out var error)) return CommandResult.Failure(error);
            if (!TryBuildClashSet(context, metersPerUnit, out var candidates, out var disciplines, out var skipped, out error)) return CommandResult.Failure(error);
            var clashes = DetectRelevant(candidates, disciplines, clearanceM);
            for (var i = 0; i < clashes.Count; i++) WriteClash(context, clashes[i], null);
            return CommandResult.Success($"MEP clash: candidates={candidates.Count}, clashes={clashes.Count}, skipped={skipped}.");
        }
    }

    private sealed class ClashLocateCommand : ICadCommand
    {
        public string Name => "QSMEPCLASHLOCATE";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (context.Arguments.Count != 3) return CommandResult.Failure("Usage: QSMEPCLASHLOCATE index clearanceMeters metersPerUnit");
            if (!int.TryParse(context.Arguments[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var index) || index <= 0)
                return CommandResult.Failure("index must be a positive integer.");
            if (!TryNonNegative(context.Arguments[1], "clearanceMeters", out var clearanceM, out var error)) return CommandResult.Failure(error);
            if (!TryPositive(context.Arguments[2], "metersPerUnit", out var metersPerUnit, out error)) return CommandResult.Failure(error);
            if (!TryBuildClashSet(context, metersPerUnit, out var candidates, out var disciplines, out _, out error)) return CommandResult.Failure(error);

            var clashes = DetectRelevant(candidates, disciplines, clearanceM);
            var reviewCount = Math.Min(clashes.Count, MaxLocatePairs);
            if (index > reviewCount) return CommandResult.Failure($"index must be in 1..{reviewCount}; total clashes={clashes.Count}.");
            var clash = clashes[index - 1];

            var left = new CadHandle(clash.LeftElementId);
            var right = new CadHandle(clash.RightElementId);
            using (var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
            {
                if (tx.Get(left) is null || tx.Get(right) is null)
                    return CommandResult.Failure("Clash pair is stale; existing selection was preserved.");
            }
            context.Document.Editor.Selection.Set(new[] { left, right });
            return CommandResult.Success($"Located clash {index}: selected exactly two live handles.");
        }
    }

    private sealed class IssuesCommand : ICadCommand
    {
        public string Name => "QSMEPISSUES";
        public CommandFlags Flags => CommandFlags.RequiresDocument | CommandFlags.ReadOnly;

        public CommandResult Execute(CommandContext context)
        {
            if (!TryClashArguments(context, "QSMEPISSUES", out var clearanceM, out var metersPerUnit, out var error)) return CommandResult.Failure(error);
            if (!TryBuildClashSet(context, metersPerUnit, out var candidates, out var disciplines, out var skipped, out error)) return CommandResult.Failure(error);
            var clashes = DetectRelevant(candidates, disciplines, clearanceM);
            var createdAt = DateTime.UtcNow;
            var catalog = new CoordinationIssueCatalog();

            for (var i = 0; i < clashes.Count; i++)
            {
                var clash = clashes[i];
                var left = candidates.Single(x => StringComparer.OrdinalIgnoreCase.Equals(x.ElementId, clash.LeftElementId));
                var right = candidates.Single(x => StringComparer.OrdinalIgnoreCase.Equals(x.ElementId, clash.RightElementId));
                var issue = new CoordinationIssue(
                    "MEP-" + (i + 1).ToString("D4", CultureInfo.InvariantCulture),
                    clash.Kind == ClashKind.Hard ? CoordinationIssueKind.HardClash : CoordinationIssueKind.ClearanceClash,
                    clash.Kind == ClashKind.Hard ? CoordinationIssueSeverity.High : CoordinationIssueSeverity.Medium,
                    clash.Kind + " MEP coordination clash",
                    left.ElementId,
                    right.ElementId,
                    new CadReference(context.Document.Id, new CadHandle(left.ElementId)),
                    new CadReference(context.Document.Id, new CadHandle(right.ElementId)),
                    left.Discipline.ToString(),
                    left.Category + " <-> " + right.Category,
                    left.System + " <-> " + right.System,
                    left.Region,
                    clash.SeparationM,
                    createdAt);
                catalog.Add(issue);
                context.Document.Editor.WriteMessage($"{issue.IssueId} {issue.Severity} {issue.Kind} {issue.LeftSemanticId}<->{issue.RightSemanticId} gap={issue.SeparationM.ToString("0.###", CultureInfo.InvariantCulture)}m");
            }
            return CommandResult.Success($"Built {catalog.Issues.Count} in-memory coordination issue(s); skipped={skipped}; no project/cloud persistence was performed.");
        }
    }

    private static IReadOnlyList<CadEntitySnapshot> Selected(CommandContext context)
    {
        var handles = context.Document.Editor.Selection.Current.Distinct().OrderBy(static x => x.Value, StringComparer.Ordinal).ToArray();
        if (handles.Length == 0) return Array.Empty<CadEntitySnapshot>();
        var result = new List<CadEntitySnapshot>(handles.Length);
        using var tx = context.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        foreach (var handle in handles)
        {
            var entity = tx.Get(handle);
            if (entity is not null) result.Add(entity);
        }
        return result;
    }

    private static MepRecognitionResult Recognize(CadEntitySnapshot entity)
    {
        entity.Properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var blockName);
        return RecognitionProfile.Recognize(entity.LayerName, blockName);
    }

    private static bool TryClashArguments(CommandContext context, string command, out double clearanceM, out double metersPerUnit, out string error)
    {
        clearanceM = 0d;
        metersPerUnit = 0d;
        if (context.Arguments.Count != 2)
        {
            error = $"Usage: {command} clearanceMeters metersPerUnit";
            return false;
        }
        if (!TryNonNegative(context.Arguments[0], "clearanceMeters", out clearanceM, out error)) return false;
        return TryPositive(context.Arguments[1], "metersPerUnit", out metersPerUnit, out error);
    }

    private static bool TryBuildClashSet(
        CommandContext context,
        double metersPerUnit,
        out IReadOnlyList<CoordinationElement> candidates,
        out IReadOnlyDictionary<string, MepDiscipline> disciplines,
        out int skipped,
        out string error)
    {
        var selected = Selected(context);
        var result = new List<CoordinationElement>();
        var map = new Dictionary<string, MepDiscipline>(StringComparer.OrdinalIgnoreCase);
        skipped = 0;
        error = string.Empty;

        foreach (var entity in selected)
        {
            var recognition = Recognize(entity);
            if (recognition.Status != MepRecognitionStatus.Matched || !recognition.Discipline.HasValue || string.IsNullOrWhiteSpace(recognition.Category))
            {
                skipped++;
                continue;
            }
            if (!TryScale(entity.Extents.Min.X, metersPerUnit, out var minX) ||
                !TryScale(entity.Extents.Min.Y, metersPerUnit, out var minY) ||
                !TryScale(entity.Extents.Min.Z, metersPerUnit, out var minZ) ||
                !TryScale(entity.Extents.Max.X, metersPerUnit, out var maxX) ||
                !TryScale(entity.Extents.Max.Y, metersPerUnit, out var maxY) ||
                !TryScale(entity.Extents.Max.Z, metersPerUnit, out var maxZ))
            {
                candidates = Array.Empty<CoordinationElement>();
                disciplines = new Dictionary<string, MepDiscipline>();
                error = $"Entity {entity.Handle} extents overflow after unit conversion.";
                return false;
            }
            var category = recognition.Category!;
            var id = entity.Handle.ToString();
            result.Add(new CoordinationElement(
                id,
                recognition.Discipline.Value,
                category,
                TextProperty(entity, "QS3D.Mep.System", entity.LayerName),
                TextProperty(entity, "QS3D.Mep.Region", DefaultRegion),
                new AxisAlignedBox(minX, minY, minZ, maxX, maxY, maxZ)));
            map.Add(id, recognition.Discipline.Value);
        }
        result.Sort(static (left, right) => StringComparer.OrdinalIgnoreCase.Compare(left.ElementId, right.ElementId));
        candidates = result;
        disciplines = map;
        if (result.Count < 2)
        {
            error = $"At least two unambiguously recognized selected entities are required; candidates={result.Count}, skipped={skipped}.";
            return false;
        }
        return true;
    }

    private static IReadOnlyList<ClashResult> DetectRelevant(
        IReadOnlyList<CoordinationElement> candidates,
        IReadOnlyDictionary<string, MepDiscipline> disciplines,
        double clearanceM)
    {
        return new ClashDetectionService().Detect(candidates, clearanceM, includeSameDiscipline: true)
            .Where(clash =>
                (disciplines.TryGetValue(clash.LeftElementId, out var left) && left == MepDiscipline.Mep) ||
                (disciplines.TryGetValue(clash.RightElementId, out var right) && right == MepDiscipline.Mep))
            .ToArray();
    }

    private static void WriteClash(CommandContext context, ClashResult clash, int? index)
    {
        context.Document.Editor.WriteMessage(
            (index.HasValue ? index.Value.ToString(CultureInfo.InvariantCulture) + ". " : string.Empty) +
            $"{clash.Kind} {clash.LeftElementId}<->{clash.RightElementId} gap={clash.SeparationM.ToString("0.###", CultureInfo.InvariantCulture)}m");
    }

    private static bool TryCount(CadEntitySnapshot entity, out int count, out string error)
    {
        count = 1;
        error = string.Empty;
        if (!TryProperty(entity, "QS3D.Mep.Count", out var raw)) return true;
        if (!int.TryParse(raw, NumberStyles.Integer, CultureInfo.InvariantCulture, out count) || count < 0)
        {
            error = $"Entity {entity.Handle} property QS3D.Mep.Count must be a non-negative integer.";
            return false;
        }
        return true;
    }

    private static bool TryMetric(CadEntitySnapshot entity, Metric metric, double metersPerUnit, out double value, out string error)
    {
        value = 0d;
        error = string.Empty;
        var propertyName = metric switch
        {
            Metric.Length => "QS3D.Mep.Length",
            Metric.Area => "QS3D.Mep.Area",
            Metric.Volume => "QS3D.Mep.Volume",
            _ => throw new ArgumentOutOfRangeException(nameof(metric))
        };

        if (TryProperty(entity, propertyName, out var raw))
        {
            if (!TryNonNegative(raw, propertyName, out var drawingValue, out error)) return false;
            var factor = metric switch
            {
                Metric.Length => metersPerUnit,
                Metric.Area => metersPerUnit * metersPerUnit,
                Metric.Volume => metersPerUnit * metersPerUnit * metersPerUnit,
                _ => 0d
            };
            if (!double.IsFinite(factor) || !TryScale(drawingValue, factor, out value))
            {
                error = $"Entity {entity.Handle} {propertyName} overflows after unit conversion.";
                return false;
            }
            return true;
        }

        if (!TryKnownReferenceMetric(entity, metric, out var exactDrawingValue)) return true;
        var exactFactor = metric switch
        {
            Metric.Length => metersPerUnit,
            Metric.Area => metersPerUnit * metersPerUnit,
            Metric.Volume => metersPerUnit * metersPerUnit * metersPerUnit,
            _ => 0d
        };
        if (!double.IsFinite(exactFactor) || !TryScale(exactDrawingValue, exactFactor, out value))
        {
            error = $"Entity {entity.Handle} exact {metric} overflows after unit conversion.";
            return false;
        }
        return true;
    }

    private static bool TryKnownReferenceMetric(CadEntitySnapshot entity, Metric metric, out double value)
    {
        value = 0d;
        if (entity.Kind == CadEntityKind.Line && metric == Metric.Length &&
            TryCoordinate(entity, "x1", out var x1) && TryCoordinate(entity, "y1", out var y1) &&
            TryCoordinate(entity, "x2", out var x2) && TryCoordinate(entity, "y2", out var y2))
        {
            value = Math.Sqrt(((x2 - x1) * (x2 - x1)) + ((y2 - y1) * (y2 - y1)));
            return double.IsFinite(value);
        }
        if (entity.Kind == CadEntityKind.Circle && TryCoordinate(entity, "radius", out var radius) && radius >= 0d)
        {
            if (metric == Metric.Length)
            {
                value = 2d * Math.PI * radius;
                return double.IsFinite(value);
            }
            if (metric == Metric.Area)
            {
                value = Math.PI * radius * radius;
                return double.IsFinite(value);
            }
        }
        if (entity.Kind == CadEntityKind.Polyline &&
            TryCoordinate(entity, "x1", out x1) && TryCoordinate(entity, "y1", out y1) &&
            TryCoordinate(entity, "x2", out x2) && TryCoordinate(entity, "y2", out y2))
        {
            var width = Math.Abs(x2 - x1);
            var height = Math.Abs(y2 - y1);
            if (!double.IsFinite(width) || !double.IsFinite(height)) return false;
            if (metric == Metric.Length)
            {
                value = 2d * (width + height);
                return double.IsFinite(value);
            }
            if (metric == Metric.Area)
            {
                value = width * height;
                return double.IsFinite(value);
            }
        }
        return false;
    }

    private static bool TryCoordinate(CadEntitySnapshot entity, string key, out double value)
    {
        value = 0d;
        return TryProperty(entity, key, out var raw) &&
            double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out value) &&
            double.IsFinite(value);
    }

    private static bool TryProperty(CadEntitySnapshot entity, string key, out string value)
    {
        foreach (var pair in entity.Properties)
        {
            if (!StringComparer.OrdinalIgnoreCase.Equals(pair.Key, key)) continue;
            value = pair.Value;
            return true;
        }
        value = string.Empty;
        return false;
    }

    private static string TextProperty(CadEntitySnapshot entity, string key, string fallback)
    {
        if (TryProperty(entity, key, out var value) && !string.IsNullOrWhiteSpace(value)) return value.Trim();
        var normalized = (fallback ?? string.Empty).Trim();
        return normalized.Length == 0 ? "UNSPECIFIED" : normalized;
    }

    private static string BlockOrKind(CadEntitySnapshot entity, string fallback)
    {
        if (entity.Properties.TryGetValue(CadBlockReferencePropertyNames.BlockName, out var blockName) && !string.IsNullOrWhiteSpace(blockName))
            return blockName.Trim();
        return entity.Kind == CadEntityKind.Unknown ? fallback : entity.Kind.ToString();
    }

    private static bool TryPositive(string token, string label, out double value, out string error)
    {
        if (!TryNonNegative(token, label, out value, out error)) return false;
        if (value <= 0d)
        {
            error = label + " must be greater than zero.";
            return false;
        }
        return true;
    }

    private static bool TryNonNegative(string token, string label, out double value, out string error)
    {
        if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out value) || !double.IsFinite(value) || value < 0d)
        {
            error = label + " must be a finite non-negative invariant-culture number.";
            return false;
        }
        error = string.Empty;
        return true;
    }

    private static bool TryScale(double value, double factor, out double result)
    {
        result = value * factor;
        return double.IsFinite(result);
    }

    private enum Metric
    {
        Length,
        Area,
        Volume
    }
}
