using System.Text.Json;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class BootstrapDrawingStore
{
    public const int CurrentSchema = 1;
    public const long MaxBytes = 32L * 1024L * 1024L;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 32 };

    public void Save(InMemoryCadDocument document, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var dto = new DrawingDto
        {
            Schema = CurrentSchema,
            DrawingId = document.Id.Value,
            Name = document.Name,
            Entities = tx.Query().Select(ToDto).ToList()
        };
        var json = JsonSerializer.Serialize(dto, JsonOptions);
        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Drawing path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, json);
            if (new FileInfo(temporary).Length > MaxBytes) throw new InvalidOperationException("Bootstrap drawing exceeds the configured size limit.");
            File.Move(temporary, fullPath, true);
        }
        finally
        {
            if (File.Exists(temporary)) File.Delete(temporary);
        }
    }

    public InMemoryCadDocument Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("Bootstrap drawing was not found.", path);
        if (file.Length > MaxBytes) throw new InvalidDataException("Bootstrap drawing exceeds the configured size limit.");
        var dto = JsonSerializer.Deserialize<DrawingDto>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Bootstrap drawing is empty or invalid JSON.");
        if (dto.Schema != CurrentSchema) throw new InvalidDataException($"Unsupported bootstrap schema {dto.Schema}.");
        if (dto.DrawingId == Guid.Empty) throw new InvalidDataException("Drawing ID is missing.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Drawing name is missing.");
        if (dto.Entities is null) throw new InvalidDataException("Entity collection is missing.");
        var entities = dto.Entities.Select(FromDto).ToArray();
        return new InMemoryCadDocument(new DrawingId(dto.DrawingId), dto.Name, new InMemoryCadDatabase(entities));
    }

    private static EntityDto ToDto(CadEntitySnapshot entity) => new()
    {
        Handle = entity.Handle.Value,
        Kind = entity.Kind.ToString(),
        Min = new[] { entity.Extents.Min.X, entity.Extents.Min.Y, entity.Extents.Min.Z },
        Max = new[] { entity.Extents.Max.X, entity.Extents.Max.Y, entity.Extents.Max.Z },
        Properties = CloneProperties(entity.Properties)
    };

    private static CadEntitySnapshot FromDto(EntityDto dto)
    {
        if (string.IsNullOrWhiteSpace(dto.Handle)) throw new InvalidDataException("Entity handle is missing.");
        if (!Enum.TryParse<CadEntityKind>(dto.Kind, false, out var kind) || kind == CadEntityKind.Unknown)
            throw new InvalidDataException($"Unsupported entity kind '{dto.Kind}'.");
        if (dto.Min is not { Length: 3 } || dto.Max is not { Length: 3 })
            throw new InvalidDataException($"Entity {dto.Handle} has invalid bounds.");
        try
        {
            var extents = new BoundingBox3(new Point3(dto.Min[0], dto.Min[1], dto.Min[2]), new Point3(dto.Max[0], dto.Max[1], dto.Max[2]));
            return new CadEntitySnapshot(new CadHandle(dto.Handle), kind, extents, dto.Properties is null ? new Dictionary<string, string>() : CloneProperties(dto.Properties));
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"Entity {dto.Handle} is invalid.", ex);
        }
    }

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
            result.Add(pair.Key, pair.Value);
        return result;
    }

    private sealed class DrawingDto
    {
        public int Schema { get; set; }
        public Guid DrawingId { get; set; }
        public string? Name { get; set; }
        public List<EntityDto>? Entities { get; set; }
    }

    private sealed class EntityDto
    {
        public string? Handle { get; set; }
        public string? Kind { get; set; }
        public double[]? Min { get; set; }
        public double[]? Max { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }
}
