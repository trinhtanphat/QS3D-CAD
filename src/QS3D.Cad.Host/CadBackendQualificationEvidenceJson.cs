using System.Text.Json;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public static class CadBackendQualificationEvidenceJson
{
    private const int Schema = 1;
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = true, PropertyNameCaseInsensitive = true, MaxDepth = 16 };
    private static readonly string[] RootProperties = { "Schema", "Items" };
    private static readonly string[] ItemProperties =
    {
        "BackendId",
        "BackendVersion",
        "SourceSha",
        "QualifiedCapabilities",
        "QualifiedAt",
        "EvidenceId",
        "Passed"
    };

    public static string Serialize(IEnumerable<CadBackendQualificationEvidence> evidence)
    {
        if (evidence is null) throw new ArgumentNullException(nameof(evidence));
        var items = evidence.ToArray();
        if (items.Any(static item => item is null)) throw new ArgumentException("Evidence must not contain null entries.", nameof(evidence));
        if (items.GroupBy(static item => item.EvidenceId, StringComparer.Ordinal).Any(static group => group.Count() > 1))
            throw new InvalidOperationException("Evidence IDs must be unique.");
        return JsonSerializer.Serialize(new FileDto
        {
            Schema = Schema,
            Items = items.OrderBy(static item => item.BackendId, StringComparer.Ordinal)
                .ThenBy(static item => item.BackendVersion, StringComparer.Ordinal)
                .ThenBy(static item => item.SourceSha, StringComparer.Ordinal)
                .ThenBy(static item => item.EvidenceId, StringComparer.Ordinal)
                .Select(static item => new ItemDto(item.BackendId, item.BackendVersion, item.SourceSha, (long)item.QualifiedCapabilities, item.QualifiedAt, item.EvidenceId, item.Passed))
                .ToList()
        }, Options);
    }

    public static IReadOnlyList<CadBackendQualificationEvidence> Deserialize(string json)
    {
        if (string.IsNullOrWhiteSpace(json)) throw new InvalidDataException("Evidence JSON must not be blank.");
        FileDto dto;
        try
        {
            ValidateJsonShape(json);
            dto = JsonSerializer.Deserialize<FileDto>(json, Options) ?? throw new InvalidDataException("Evidence JSON is empty.");
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Evidence JSON is invalid.", ex);
        }
        if (dto.Schema != Schema) throw new InvalidDataException($"Unsupported evidence schema {dto.Schema}.");
        if (dto.Items is null) throw new InvalidDataException("Evidence items are missing.");
        var ids = new HashSet<string>(StringComparer.Ordinal);
        var result = new List<CadBackendQualificationEvidence>();
        foreach (var item in dto.Items)
        {
            if (item is null) throw new InvalidDataException("Evidence contains null item.");
            CadBackendQualificationEvidence restored;
            try { restored = new CadBackendQualificationEvidence(item.BackendId ?? "", item.BackendVersion ?? "", item.SourceSha ?? "", (CadCapabilities)item.QualifiedCapabilities, item.QualifiedAt, item.EvidenceId ?? "", item.Passed); }
            catch (ArgumentException ex) { throw new InvalidDataException("Evidence item is invalid.", ex); }
            if (!ids.Add(restored.EvidenceId)) throw new InvalidDataException($"Duplicate evidence ID '{restored.EvidenceId}'.");
            result.Add(restored);
        }
        return result;
    }

    private static void ValidateJsonShape(string json)
    {
        using var document = JsonDocument.Parse(json, new JsonDocumentOptions { MaxDepth = Options.MaxDepth });
        var root = document.RootElement;
        if (root.ValueKind != JsonValueKind.Object)
            throw new InvalidDataException("Evidence JSON root must be an object.");
        ValidateProperties(root, "evidence root", RootProperties);

        var items = GetProperty(root, "Items");
        if (items.ValueKind != JsonValueKind.Array)
            throw new InvalidDataException("Evidence 'Items' must be an array.");
        var index = 0;
        foreach (var item in items.EnumerateArray())
        {
            if (item.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException($"Evidence item {index} must be an object.");
            ValidateProperties(item, $"evidence item {index}", ItemProperties);
            index++;
        }
    }

    private static void ValidateProperties(JsonElement value, string label, IReadOnlyCollection<string> required)
    {
        var allowed = required.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in value.EnumerateObject())
        {
            if (!allowed.Contains(property.Name))
                throw new InvalidDataException($"{label} contains unknown property '{property.Name}'.");
            if (!seen.Add(property.Name))
                throw new InvalidDataException($"{label} contains duplicate property '{property.Name}'.");
        }

        foreach (var property in required)
        {
            if (!seen.Contains(property))
                throw new InvalidDataException($"{label} is missing required property '{property}'.");
        }
    }

    private static JsonElement GetProperty(JsonElement value, string name)
    {
        foreach (var property in value.EnumerateObject())
        {
            if (StringComparer.OrdinalIgnoreCase.Equals(property.Name, name))
                return property.Value;
        }
        throw new InvalidDataException($"Evidence JSON is missing required property '{name}'.");
    }

    private sealed class FileDto { public int Schema { get; set; } public List<ItemDto>? Items { get; set; } }
    private sealed record ItemDto(string? BackendId, string? BackendVersion, string? SourceSha, long QualifiedCapabilities, DateTimeOffset QualifiedAt, string? EvidenceId, bool Passed);
}
