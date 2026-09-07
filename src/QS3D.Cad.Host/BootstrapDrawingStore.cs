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
    public const int CurrentSchema = 4;
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
            Entities = tx.Query().Select(ToEntityDto).ToList(),
            Layers = tx.GetLayers().Select(ToLayerDto).ToList(),
            CurrentLayerName = tx.CurrentLayerName,
            Blocks = tx.GetBlocks().Select(ToBlockDto).ToList(),
            SemanticProject = project is null ? null : ToSemanticProjectDto(project)
        };

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Drawing path has no parent directory.");
        Directory.CreateDirectory(directory);
        var temporary = fullPath + ".tmp-" + Guid.NewGuid().ToString("N");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(dto, JsonOptions));
            if (new FileInfo(temporary).Length > MaxBytes)
                throw new InvalidOperationException("Bootstrap drawing exceeds the configured size limit.");
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

        DrawingDto dto;
        try
        {
            dto = JsonSerializer.Deserialize<DrawingDto>(File.ReadAllText(path), JsonOptions)
                ?? throw new InvalidDataException("Bootstrap drawing is empty or invalid JSON.");
        }
        catch (Exception ex) when (ex is JsonException or NotSupportedException)
        {
            throw new InvalidDataException("Bootstrap drawing JSON is invalid.", ex);
        }

        if (dto.Schema < MinimumReadableSchema || dto.Schema > CurrentSchema)
            throw new InvalidDataException($"Unsupported bootstrap schema {dto.Schema}.");
        if (dto.DrawingId == Guid.Empty) throw new InvalidDataException("Drawing ID is missing.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Drawing name is missing.");
        if (dto.Entities is null) throw new InvalidDataException("Entity collection is missing.");
        if (dto.Schema >= 3 && (dto.Layers is null || string.IsNullOrWhiteSpace(dto.CurrentLayerName)))
            throw new InvalidDataException("Layer state is missing from bootstrap drawing.");
        if (dto.Schema >= 4 && dto.Blocks is null)
            throw new InvalidDataException("Block definition collection is missing from bootstrap drawing.");

        try
        {
            var entities = dto.Entities.Select(item => FromEntityDto(item, dto.Schema)).ToArray();
            var layers = dto.Schema >= 3 ? dto.Layers!.Select(FromLayerDto).ToArray() : null;
            var blocks = dto.Schema >= 4 ? dto.Blocks!.Select(FromBlockDto).ToArray() : null;
            if (dto.Schema >= 3) ValidateLayerReferences(entities, blocks, layers!);
            var currentLayer = dto.Schema >= 3 ? dto.CurrentLayerName : null;
            var database = new InMemoryCadDatabase(entities, layers, currentLayer, blocks);
            var document = new InMemoryCadDocument(new DrawingId(dto.DrawingId), dto.Name, database);
            var project = dto.Schema >= 2 && dto.SemanticProject is not null ? FromSemanticProjectDto(dto.SemanticProject) : null;
            return new BootstrapLoadResult(document, project);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException("Bootstrap drawing content is invalid.", ex);
        }
    }

    private static EntityDto ToEntityDto(CadEntitySnapshot entity) => new()
    {
        Handle = entity.Handle.Value,
        Kind = entity.Kind.ToString(),
        Min = PointToArray(entity.Extents.Min),
        Max = PointToArray(entity.Extents.Max),
        Properties = CloneProperties(entity.Properties),
        LayerName = entity.LayerName
    };

    private static CadEntitySnapshot FromEntityDto(EntityDto? dto, int schema)
    {
        if (dto is null) throw new InvalidDataException("Entity collection contains null.");
        if (string.IsNullOrWhiteSpace(dto.Handle)) throw new InvalidDataException("Entity handle is missing.");
        if (!TryKind(dto.Kind, out var kind)) throw new InvalidDataException($"Unsupported entity kind '{dto.Kind}'.");
        if (schema >= 3 && string.IsNullOrWhiteSpace(dto.LayerName))
            throw new InvalidDataException($"Entity {dto.Handle} layer name is missing from modern bootstrap layer state.");
        try
        {
            return new CadEntitySnapshot(
                new CadHandle(dto.Handle),
                kind,
                Bounds(dto.Min, dto.Max, $"Entity {dto.Handle}"),
                dto.Properties is null ? new Dictionary<string, string>() : CloneProperties(dto.Properties),
                string.IsNullOrWhiteSpace(dto.LayerName) ? "0" : dto.LayerName);
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"Entity {dto.Handle} is invalid.", ex);
        }
    }

    private static LayerDto ToLayerDto(CadLayerSnapshot layer) => new()
    {
        Name = layer.Name,
        IsOn = layer.IsOn,
        IsFrozen = layer.IsFrozen,
        IsLocked = layer.IsLocked
    };

    private static CadLayerSnapshot FromLayerDto(LayerDto? dto)
    {
        if (dto is null) throw new InvalidDataException("Layer collection contains null.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Layer name is missing.");
        return new CadLayerSnapshot(dto.Name, dto.IsOn, dto.IsFrozen, dto.IsLocked);
    }

    private static BlockDto ToBlockDto(CadBlockDefinitionSnapshot block) => new()
    {
        Name = block.Name,
        BasePoint = PointToArray(block.BasePoint),
        Entities = block.Entities.Select(ToDraftDto).ToList()
    };

    private static CadBlockDefinitionSnapshot FromBlockDto(BlockDto? dto)
    {
        if (dto is null) throw new InvalidDataException("Block definition collection contains null.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Block name is missing.");
        if (dto.BasePoint is not { Length: 3 }) throw new InvalidDataException($"Block '{dto.Name}' has an invalid base point.");
        if (dto.Entities is null || dto.Entities.Count == 0) throw new InvalidDataException($"Block '{dto.Name}' has no entities.");
        try
        {
            return new CadBlockDefinitionSnapshot(
                dto.Name,
                new Point3(dto.BasePoint[0], dto.BasePoint[1], dto.BasePoint[2]),
                dto.Entities.Select(FromDraftDto).ToArray());
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or FormatException or OverflowException)
        {
            throw new InvalidDataException($"Block '{dto.Name}' is invalid.", ex);
        }
    }

    private static EntityDraftDto ToDraftDto(CadEntityDraft entity) => new()
    {
        Kind = entity.Kind.ToString(),
        Min = PointToArray(entity.Extents.Min),
        Max = PointToArray(entity.Extents.Max),
        Properties = entity.Properties is null ? null : CloneProperties(entity.Properties),
        LayerName = entity.LayerName
    };

    private static CadEntityDraft FromDraftDto(EntityDraftDto? dto)
    {
        if (dto is null) throw new InvalidDataException("Block entity collection contains null.");
        if (!TryKind(dto.Kind, out var kind)) throw new InvalidDataException($"Unsupported block entity kind '{dto.Kind}'.");
        if (string.IsNullOrWhiteSpace(dto.LayerName))
            throw new InvalidDataException("Block entity layer name is missing from schema-v4 bootstrap layer state.");
        return new CadEntityDraft(
            kind,
            Bounds(dto.Min, dto.Max, "Block entity"),
            dto.Properties is null ? null : CloneProperties(dto.Properties),
            dto.LayerName);
    }

    private static void ValidateLayerReferences(
        IReadOnlyList<CadEntitySnapshot> entities,
        IReadOnlyList<CadBlockDefinitionSnapshot>? blocks,
        IReadOnlyList<CadLayerSnapshot> layers)
    {
        var declared = layers
            .Select(static layer => layer.Name.Trim())
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var entity in entities)
        {
            var layerName = entity.LayerName.Trim();
            if (!declared.Contains(layerName))
                throw new InvalidDataException($"Entity {entity.Handle} references undeclared layer '{entity.LayerName}'.");
        }

        if (blocks is null) return;
        foreach (var block in blocks)
        {
            foreach (var member in block.Entities)
            {
                var layerName = member.LayerName;
                if (string.IsNullOrWhiteSpace(layerName) || !declared.Contains(layerName.Trim()))
                    throw new InvalidDataException($"Block '{block.Name}' entity references undeclared layer '{layerName ?? "<missing>"}'.");
            }
        }
    }

    private static bool TryKind(string? raw, out CadEntityKind kind)
        => Enum.TryParse(raw, false, out kind) && kind != CadEntityKind.Unknown;

    private static BoundingBox3 Bounds(double[]? min, double[]? max, string label)
    {
        if (min is not { Length: 3 } || max is not { Length: 3 }) throw new InvalidDataException($"{label} has invalid bounds.");
        return new BoundingBox3(new Point3(min[0], min[1], min[2]), new Point3(max[0], max[1], max[2]));
    }

    private static double[] PointToArray(Point3 point) => new[] { point.X, point.Y, point.Z };

    private static SemanticProjectDto ToSemanticProjectDto(SemanticProject project) => new()
    {
        ProjectId = project.Id.Value,
        Name = project.Name,
        Floors = project.Floors.Select(static floor => new FloorDto { Id = floor.Id.Value, Name = floor.Name, ElevationM = floor.ElevationM }).ToList(),
        Zones = project.Zones.Select(static zone => new ZoneDto { Id = zone.Id.Value, Name = zone.Name }).ToList(),
        Families = project.Families.Select(static family => new FamilyDto { Id = family.Id.Value, Kind = family.Kind.ToString(), Name = family.Name }).ToList(),
        Elements = project.Elements.Select(ToSemanticElementDto).ToList()
    };

    private static SemanticElementDto ToSemanticElementDto(SemanticElement element) => new()
    {
        Id = element.Id.Value,
        Kind = element.Kind.ToString(),
        Name = element.Name,
        FamilyId = element.FamilyId.Value,
        FloorId = element.FloorId?.Value,
        ZoneId = element.ZoneId?.Value,
        SourceReference = element.SourceReference.HasValue ? ToReferenceDto(element.SourceReference.Value) : null,
        GeneratedReferences = element.GeneratedReferences.Select(ToReferenceDto).ToList(),
        Properties = CloneProperties(element.Properties)
    };

    private static CadReferenceDto ToReferenceDto(CadReference reference) => new()
    {
        DrawingId = reference.DrawingId.Value,
        Handle = reference.Handle.Value
    };

    private static SemanticProject FromSemanticProjectDto(SemanticProjectDto? dto)
    {
        if (dto is null) throw new InvalidDataException("Semantic project is null.");
        if (dto.ProjectId == Guid.Empty) throw new InvalidDataException("Semantic project ID is missing.");
        if (string.IsNullOrWhiteSpace(dto.Name)) throw new InvalidDataException("Semantic project name is missing.");
        if (dto.Floors is null || dto.Zones is null || dto.Families is null || dto.Elements is null)
            throw new InvalidDataException("Semantic project collections are incomplete.");
        try
        {
            var project = new SemanticProject(new ProjectId(dto.ProjectId), dto.Name);
            foreach (var floor in dto.Floors)
            {
                if (floor is null || floor.Id == Guid.Empty || string.IsNullOrWhiteSpace(floor.Name)) throw new InvalidDataException("Semantic floor is invalid.");
                project.AddFloor(new Floor(new FloorId(floor.Id), floor.Name, floor.ElevationM));
            }
            foreach (var zone in dto.Zones)
            {
                if (zone is null || zone.Id == Guid.Empty || string.IsNullOrWhiteSpace(zone.Name)) throw new InvalidDataException("Semantic zone is invalid.");
                project.AddZone(new Zone(new ZoneId(zone.Id), zone.Name));
            }
            foreach (var family in dto.Families)
            {
                if (family is null || family.Id == Guid.Empty || string.IsNullOrWhiteSpace(family.Name) || !Enum.TryParse<SemanticElementKind>(family.Kind, false, out var kind) || kind == SemanticElementKind.Unknown)
                    throw new InvalidDataException("Semantic family is invalid.");
                project.AddFamily(new Family(new FamilyId(family.Id), kind, family.Name));
            }
            foreach (var element in dto.Elements) project.AddElement(FromSemanticElementDto(element));
            return project;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or FormatException or OverflowException)
        {
            throw new InvalidDataException("Semantic project is invalid.", ex);
        }
    }

    private static SemanticElement FromSemanticElementDto(SemanticElementDto? dto)
    {
        if (dto is null) throw new InvalidDataException("Semantic element collection contains null.");
        if (dto.Id == Guid.Empty || dto.FamilyId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Name))
            throw new InvalidDataException("Semantic element identity is invalid.");
        if (!Enum.TryParse<SemanticElementKind>(dto.Kind, false, out var kind) || kind == SemanticElementKind.Unknown)
            throw new InvalidDataException($"Unsupported semantic element kind '{dto.Kind}'.");
        var element = new SemanticElement(new ElementId(dto.Id), kind, dto.Name, new FamilyId(dto.FamilyId));
        element.AssignLocation(dto.FloorId.HasValue ? new FloorId(dto.FloorId.Value) : null, dto.ZoneId.HasValue ? new ZoneId(dto.ZoneId.Value) : null);
        if (dto.SourceReference is not null) element.SetSource(FromReferenceDto(dto.SourceReference));
        if (dto.GeneratedReferences is not null)
        {
            foreach (var reference in dto.GeneratedReferences) element.AddGeneratedReference(FromReferenceDto(reference));
        }
        if (dto.Properties is not null)
        {
            foreach (var property in dto.Properties)
            {
                if (property.Value is null) throw new InvalidDataException($"Semantic property '{property.Key}' has a null value.");
                element.SetProperty(property.Key, property.Value);
            }
        }
        return element;
    }

    private static CadReference FromReferenceDto(CadReferenceDto? dto)
    {
        if (dto is null || dto.DrawingId == Guid.Empty || string.IsNullOrWhiteSpace(dto.Handle)) throw new InvalidDataException("CAD reference is invalid.");
        return new CadReference(new DrawingId(dto.DrawingId), new CadHandle(dto.Handle));
    }

    private static Dictionary<string, string> CloneProperties(IReadOnlyDictionary<string, string> source)
    {
        var result = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var pair in source)
        {
            if (pair.Value is null) throw new InvalidDataException($"Property '{pair.Key}' has a null value.");
            result.Add(pair.Key, pair.Value);
        }
        return result;
    }

    private sealed class DrawingDto
    {
        public int Schema { get; set; }
        public Guid DrawingId { get; set; }
        public string? Name { get; set; }
        public List<EntityDto>? Entities { get; set; }
        public List<LayerDto>? Layers { get; set; }
        public string? CurrentLayerName { get; set; }
        public List<BlockDto>? Blocks { get; set; }
        public SemanticProjectDto? SemanticProject { get; set; }
    }

    private sealed class EntityDto
    {
        public string? Handle { get; set; }
        public string? Kind { get; set; }
        public double[]? Min { get; set; }
        public double[]? Max { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public string? LayerName { get; set; }
    }

    private sealed class LayerDto
    {
        public string? Name { get; set; }
        public bool IsOn { get; set; } = true;
        public bool IsFrozen { get; set; }
        public bool IsLocked { get; set; }
    }

    private sealed class BlockDto
    {
        public string? Name { get; set; }
        public double[]? BasePoint { get; set; }
        public List<EntityDraftDto>? Entities { get; set; }
    }

    private sealed class EntityDraftDto
    {
        public string? Kind { get; set; }
        public double[]? Min { get; set; }
        public double[]? Max { get; set; }
        public Dictionary<string, string>? Properties { get; set; }
        public string? LayerName { get; set; }
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
