using System.Text.Json;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class BootstrapLoadResult
{
    public BootstrapLoadResult(InMemoryCadDocument document, SemanticProject? project)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Project = project;
    }

    public InMemoryCadDocument Document { get; }
    public SemanticProject? Project { get; }
}

public sealed class BootstrapDrawingStore
{
    public const int CurrentSchema = 2;
    public const int MinimumReadableSchema = 1;
    public const long MaxBytes = 32L * 1024L * 1024L;
    private static readonly JsonSerializerOptions JsonOptions = new() { WriteIndented = true, MaxDepth = 64 };

    public void Save(InMemoryCadDocument document, string path) => Save(document, null, path);

    public void Save(InMemoryCadDocument document, SemanticProject? project, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var dto = new DrawingDto
        {
            Schema = CurrentSchema,
            DrawingId = document.Id.Value,
            Name = document.Name,
            Entities = tx.Query().Select(ToDto).ToList(),
            SemanticProject = project is null ? null : ToDto(project)
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

    public InMemoryCadDocument Load(string path) => LoadWithProject(path).Document;

    public BootstrapLoadResult LoadWithProject(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var file = new FileInfo(path);
        if (!file.Exists) throw new FileNotFoundException("Bootstrap drawing was not found.", path);
        if (file.Length > MaxBytes) throw new InvalidDataException("Bootstrap drawing exceeds the configured size limit.");
        var dto = JsonSerializer.Deserialize<DrawingDto>(File.ReadAllText(path), JsonOptions)
            ?? throw new InvalidDataException("Bootstrap drawing is empty or invalid JSON.");
        if (dto.Schema < MinimumReadableSchema || dto.Schema > CurrentSchema)
            throw new InvalidDataException($"Unsupported bootstrap schema {dto.Schema}.");
        if (dto.DrawingId == Guid.Empty) throw new InvalidDataException("Drawing ID is missing.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Drawing name is missing.");
        if (dto.Entities is null) throw new InvalidDataException("Entity collection is missing.");
        var entities = dto.Entities.Select(FromDto).ToArray();
        var document = new InMemoryCadDocument(new DrawingId(dto.DrawingId), dto.Name, new InMemoryCadDatabase(entities));
        var project = dto.Schema >= 2 && dto.SemanticProject is not null ? FromDto(dto.SemanticProject) : null;
        return new BootstrapLoadResult(document, project);
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

    private static SemanticProjectDto ToDto(SemanticProject project) => new()
    {
        ProjectId = project.Id.Value,
        Name = project.Name,
        Floors = project.Floors.Select(static floor => new FloorDto { Id = floor.Id.Value, Name = floor.Name, ElevationM = floor.ElevationM }).ToList(),
        Zones = project.Zones.Select(static zone => new ZoneDto { Id = zone.Id.Value, Name = zone.Name }).ToList(),
        Families = project.Families.Select(static family => new FamilyDto { Id = family.Id.Value, Kind = family.Kind.ToString(), Name = family.Name }).ToList(),
        Elements = project.Elements.Select(ToDto).ToList()
    };

    private static SemanticElementDto ToDto(SemanticElement element) => new()
    {
        Id = element.Id.Value,
        Kind = element.Kind.ToString(),
        Name = element.Name,
        FamilyId = element.FamilyId.Value,
        FloorId = element.FloorId?.Value,
        ZoneId = element.ZoneId?.Value,
        SourceReference = element.SourceReference.HasValue ? ToDto(element.SourceReference.Value) : null,
        GeneratedReferences = element.GeneratedReferences.Select(ToDto).ToList(),
        Properties = CloneProperties(element.Properties)
    };

    private static CadReferenceDto ToDto(CadReference reference) => new() { DrawingId = reference.DrawingId.Value, Handle = reference.Handle.Value };

    private static SemanticProject FromDto(SemanticProjectDto dto)
    {
        try
        {
            if (dto.ProjectId == Guid.Empty) throw new InvalidDataException("Semantic project ID is missing.");
            if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Semantic project name is missing.");
            if (dto.Floors is null || dto.Zones is null || dto.Families is null || dto.Elements is null)
                throw new InvalidDataException("Semantic project collections are incomplete.");

            var project = new SemanticProject(new ProjectId(dto.ProjectId), dto.Name);
            foreach (var floor in dto.Floors)
            {
                if (floor.Id == Guid.Empty || string.IsNullOrWhiteSpace(floor.Name)) throw new InvalidDataException("Semantic floor is invalid.");
                project.AddFloor(new Floor(new FloorId(floor.Id), floor.Name, floor.ElevationM));
            }
            foreach (var zone in dto.Zones)
            {
                if (zone.Id == Guid.Empty || string.IsNullOrWhiteSpace(zone.Name)) throw new InvalidDataException("Semantic zone is invalid.");
                project.AddZone(new Zone(new ZoneId(zone.Id), zone.Name));
            }
            foreach (var family in dto.Families)
            {
                if (family.Id == Guid.Empty || string.IsNullOrWhiteSpace(family.Name) || !Enum.TryParse<SemanticElementKind>(family.Kind, false, out var kind) || kind == SemanticElementKind.Unknown)
                    throw new InvalidDataException("Semantic family is invalid.");
                project.AddFamily(new Family(new FamilyId(family.Id), kind, family.Name));
            }
            foreach (var elementDto in dto.Elements)
                project.AddElement(FromDto(elementDto));
            return project;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            if (ex is InvalidDataException) throw;
            throw new InvalidDataException("Semantic project is invalid.", ex);
        }
    }

    private static SemanticElement FromDto(SemanticElementDto dto)
    {
        if (dto.Id == Guid.Empty || dto.FamilyId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidDataException("Semantic element identity is invalid.");
        if (!Enum.TryParse<SemanticElementKind>(dto.Kind, false, out var kind) || kind == SemanticElementKind.Unknown)
            throw new InvalidDataException($"Unsupported semantic element kind '{dto.Kind}'.");
        var element = new SemanticElement(new ElementId(dto.Id), kind, dto.Name, new FamilyId(dto.FamilyId));
        element.AssignLocation(dto.FloorId.HasValue ? new FloorId(dto.FloorId.Value) : null, dto.ZoneId.HasValue ? new ZoneId(dto.ZoneId.Value) : null);
        if (dto.SourceReference is not null) element.SetSource(FromDto(dto.SourceReference));
        if (dto.GeneratedReferences is not null)
        {
            foreach (var generated in dto.GeneratedReferences) element.AddGeneratedReference(FromDto(generated));
        }
        if (dto.Properties is not null)
        {
            foreach (var property in dto.Properties) element.SetProperty(property.Key, property.Value);
        }
        return element;
    }

    private static CadReference FromDto(CadReferenceDto dto)
    {
        if (dto.DrawingId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Handle))
            throw new InvalidDataException("CAD reference is invalid.");
        return new CadReference(new DrawingId(dto.DrawingId), new CadHandle(dto.Handle));
    }

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source) result.Add(pair.Key, pair.Value);
        return result;
    }

    private sealed class DrawingDto
    {
        public int Schema { get; set; }
        public Guid DrawingId { get; set; }
        public string? Name { get; set; }
        public List<EntityDto>? Entities { get; set; }
        public SemanticProjectDto? SemanticProject { get; set; }
    }

    private sealed class EntityDto
    {
        public string? Handle { get; set; }
        public string? Kind { get; set; }
        public double[]? Min { get; set; }
        public double[]? Max { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }

    private sealed class SemanticProjectDto
    {
        public Guid ProjectId { get; set; }
        public string? Name { get; set; }
        public List<FloorDto>? Floors { get; set; }
        public List<ZoneDto>? Zones { get; set; }
        public List<FamilyDto>? Families { get; set; }
        public List<SemanticElementDto>? Elements { get; set; }
    }

    private sealed class FloorDto { public Guid Id { get; set; } public string? Name { get; set; } public double ElevationM { get; set; } }
    private sealed class ZoneDto { public Guid Id { get; set; } public string? Name { get; set; } }
    private sealed class FamilyDto { public Guid Id { get; set; } public string? Kind { get; set; } public string? Name { get; set; } }
    private sealed class CadReferenceDto { public Guid DrawingId { get; set; } public string? Handle { get; set; } }

    private sealed class SemanticElementDto
    {
        public Guid Id { get; set; }
        public string? Kind { get; set; }
        public string? Name { get; set; }
        public Guid FamilyId { get; set; }
        public Guid? FloorId { get; set; }
        public Guid? ZoneId { get; set; }
        public CadReferenceDto? SourceReference { get; set; }
        public List<CadReferenceDto>? GeneratedReferences { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
    }
}
