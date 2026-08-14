using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;
using QS3D.Platform.Persistence;

namespace QS3D.Cad.Host;

public sealed class Qs3dBootstrapPackageStore
{
    public const int CurrentFormatVersion = 1;
    public const long MaxPackageBytes = 128L * 1024L * 1024L;
    public const long MaxManifestBytes = 1024L * 1024L;
    public const long MaxPayloadBytes = 64L * 1024L * 1024L;

    private const string ManifestEntry = "manifest.json";
    private const string SemanticEntry = "semantic-project.json";
    private const string DrawingEntry = "drawing-bootstrap.json";
    private const string SemanticMediaType = "application/vnd.qs3d.semantic+json";
    private const string DrawingMediaType = "application/vnd.qs3d.bootstrap+json";
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true,
        MaxDepth = 64
    };

    private readonly BootstrapDrawingStore _drawingStore;

    public Qs3dBootstrapPackageStore(BootstrapDrawingStore? drawingStore = null)
        => _drawingStore = drawingStore ?? new BootstrapDrawingStore();

    public void Save(InMemoryCadDocument document, SemanticProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);

        var validatedProject = SemanticSnapshotService.Restore(SemanticSnapshotService.Capture(project));
        var semanticBytes = JsonSerializer.SerializeToUtf8Bytes(SemanticSnapshotService.Capture(validatedProject), JsonOptions);
        RequirePayloadSize(semanticBytes.LongLength, SemanticEntry);

        var fullPath = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(fullPath) ?? throw new InvalidOperationException("Package path has no parent directory.");
        Directory.CreateDirectory(directory);
        var drawingTemp = Path.Combine(directory, ".qs3d-drawing-" + Guid.NewGuid().ToString("N") + ".json");
        var packageTemp = Path.Combine(directory, ".qs3d-package-" + Guid.NewGuid().ToString("N") + ".tmp");

        try
        {
            _drawingStore.Save(document, validatedProject, drawingTemp);
            var drawingBytes = File.ReadAllBytes(drawingTemp);
            RequirePayloadSize(drawingBytes.LongLength, DrawingEntry);

            var manifest = new ManifestDto
            {
                FormatVersion = CurrentFormatVersion,
                ProjectId = validatedProject.Id.Value,
                Payloads = new List<PayloadDto>
                {
                    PayloadDto.Create(SemanticEntry, SemanticMediaType, semanticBytes),
                    PayloadDto.Create(DrawingEntry, DrawingMediaType, drawingBytes)
                }
            };
            var manifestBytes = JsonSerializer.SerializeToUtf8Bytes(manifest, JsonOptions);
            if (manifestBytes.LongLength > MaxManifestBytes) throw new InvalidOperationException("QS3D package manifest exceeds the configured size limit.");

            using (var file = new FileStream(packageTemp, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Create, leaveOpen: false))
            {
                WriteEntry(archive, ManifestEntry, manifestBytes);
                WriteEntry(archive, SemanticEntry, semanticBytes);
                WriteEntry(archive, DrawingEntry, drawingBytes);
            }

            var packageInfo = new FileInfo(packageTemp);
            if (packageInfo.Length > MaxPackageBytes) throw new InvalidOperationException("QS3D package exceeds the configured size limit.");
            File.Move(packageTemp, fullPath, true);
        }
        finally
        {
            DeleteIfExists(drawingTemp);
            DeleteIfExists(packageTemp);
        }
    }

    public Qs3dBootstrapLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var info = new FileInfo(path);
        if (!info.Exists) throw new FileNotFoundException("QS3D package was not found.", path);
        if (info.Length > MaxPackageBytes) throw new InvalidDataException("QS3D package exceeds the configured size limit.");

        byte[] manifestBytes;
        byte[] semanticBytes;
        byte[] drawingBytes;
        using (var file = new FileStream(info.FullName, FileMode.Open, FileAccess.Read, FileShare.Read))
        using (var archive = new ZipArchive(file, ZipArchiveMode.Read, leaveOpen: false))
        {
            var duplicate = archive.Entries.GroupBy(static entry => entry.FullName, StringComparer.Ordinal).FirstOrDefault(static group => group.Count() > 1);
            if (duplicate is not null) throw new InvalidDataException($"QS3D package contains duplicate entry '{duplicate.Key}'.");
            var names = archive.Entries.Select(static entry => entry.FullName).OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            var expected = new[] { DrawingEntry, ManifestEntry, SemanticEntry }.OrderBy(static name => name, StringComparer.Ordinal).ToArray();
            if (!names.SequenceEqual(expected, StringComparer.Ordinal)) throw new InvalidDataException("QS3D package contains missing or unexpected entries.");

            manifestBytes = ReadEntry(archive, ManifestEntry, MaxManifestBytes);
            semanticBytes = ReadEntry(archive, SemanticEntry, MaxPayloadBytes);
            drawingBytes = ReadEntry(archive, DrawingEntry, MaxPayloadBytes);
        }

        ManifestDto manifest;
        try
        {
            manifest = JsonSerializer.Deserialize<ManifestDto>(manifestBytes, JsonOptions)
                ?? throw new InvalidDataException("QS3D package manifest is empty or invalid.");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("QS3D package manifest is invalid.", ex);
        }
        manifest.Validate();
        if (manifest.FormatVersion != CurrentFormatVersion) throw new InvalidDataException($"Unsupported QS3D package format {manifest.FormatVersion}.");
        ValidatePayload(manifest, SemanticEntry, semanticBytes);
        ValidatePayload(manifest, DrawingEntry, drawingBytes);

        var semanticSnapshot = DeserializeSemanticSnapshot(semanticBytes);
        var project = SemanticSnapshotService.Restore(semanticSnapshot);
        if (project.Id.Value != manifest.ProjectId) throw new InvalidDataException("Manifest project identity does not match the semantic payload.");

        var directory = Path.GetDirectoryName(info.FullName) ?? Path.GetTempPath();
        var drawingTemp = Path.Combine(directory, ".qs3d-load-" + Guid.NewGuid().ToString("N") + ".json");
        try
        {
            File.WriteAllBytes(drawingTemp, drawingBytes);
            var loaded = _drawingStore.LoadWithProject(drawingTemp);
            if (loaded.Project is not null)
            {
                if (loaded.Project.Id != project.Id) throw new InvalidDataException("Drawing payload project identity does not match the semantic payload.");
                var embeddedBytes = JsonSerializer.SerializeToUtf8Bytes(SemanticSnapshotService.Capture(loaded.Project), JsonOptions);
                if (!CryptographicOperations.FixedTimeEquals(SHA256.HashData(embeddedBytes), SHA256.HashData(semanticBytes)))
                    throw new InvalidDataException("Drawing payload semantic state does not match the canonical semantic payload.");
            }
            return new Qs3dBootstrapLoadResult(loaded.Document, project);
        }
        finally
        {
            DeleteIfExists(drawingTemp);
        }
    }

    private static SemanticProjectSnapshot DeserializeSemanticSnapshot(byte[] bytes)
    {
        try
        {
            using var json = JsonDocument.Parse(bytes, new JsonDocumentOptions { MaxDepth = JsonOptions.MaxDepth });
            var root = RequireObject(json.RootElement, "semantic project");
            var schemaVersion = RequireInt32(root, "SchemaVersion");
            var projectId = RequireGuid(root, "ProjectId");
            var name = RequireString(root, "Name");
            var floors = RequireArray(root, "Floors").EnumerateArray().Select(ParseFloor).ToArray();
            var zones = RequireArray(root, "Zones").EnumerateArray().Select(ParseZone).ToArray();
            var families = RequireArray(root, "Families").EnumerateArray().Select(ParseFamily).ToArray();
            var elements = RequireArray(root, "Elements").EnumerateArray().Select(ParseElement).ToArray();
            return new SemanticProjectSnapshot(schemaVersion, projectId, name, floors, zones, families, elements);
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException or OverflowException or InvalidOperationException)
        {
            throw new InvalidDataException("Semantic project payload is invalid.", ex);
        }
    }

    private static FloorSnapshot ParseFloor(JsonElement value)
    {
        var item = RequireObject(value, "semantic floor");
        return new FloorSnapshot(RequireGuid(item, "Id"), RequireString(item, "Name"), RequireDouble(item, "ElevationM"));
    }

    private static ZoneSnapshot ParseZone(JsonElement value)
    {
        var item = RequireObject(value, "semantic zone");
        return new ZoneSnapshot(RequireGuid(item, "Id"), RequireString(item, "Name"));
    }

    private static FamilySnapshot ParseFamily(JsonElement value)
    {
        var item = RequireObject(value, "semantic family");
        return new FamilySnapshot(RequireGuid(item, "Id"), RequireKind(item, "Kind"), RequireString(item, "Name"));
    }

    private static ElementSnapshot ParseElement(JsonElement value)
    {
        var item = RequireObject(value, "semantic element");
        var source = RequireProperty(item, "SourceReference");
        var sourceReference = source.ValueKind == JsonValueKind.Null ? null : ParseReference(source);
        var generated = RequireArray(item, "GeneratedReferences").EnumerateArray().Select(ParseReference).ToArray();
        var propertiesElement = RequireObject(RequireProperty(item, "Properties"), "semantic element properties");
        var properties = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var property in propertiesElement.EnumerateObject())
        {
            if (property.Value.ValueKind != JsonValueKind.String)
                throw new InvalidDataException($"Semantic property '{property.Name}' must be a string.");
            properties.Add(property.Name, property.Value.GetString() ?? string.Empty);
        }

        return new ElementSnapshot(
            RequireGuid(item, "Id"),
            RequireKind(item, "Kind"),
            RequireString(item, "Name"),
            RequireGuid(item, "FamilyId"),
            ReadNullableGuid(item, "FloorId"),
            ReadNullableGuid(item, "ZoneId"),
            sourceReference,
            generated,
            properties);
    }

    private static CadReferenceSnapshot ParseReference(JsonElement value)
    {
        var item = RequireObject(value, "CAD reference");
        return new CadReferenceSnapshot(RequireGuid(item, "DrawingId"), RequireString(item, "Handle"));
    }

    private static JsonElement RequireProperty(JsonElement parent, string name)
    {
        if (!parent.TryGetProperty(name, out var value))
            throw new InvalidDataException($"Semantic project payload is missing '{name}'.");
        return value;
    }

    private static JsonElement RequireObject(JsonElement value, string label)
    {
        if (value.ValueKind != JsonValueKind.Object) throw new InvalidDataException($"{label} must be a JSON object.");
        return value;
    }

    private static JsonElement RequireArray(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.Array) throw new InvalidDataException($"Semantic project '{name}' must be an array.");
        return value;
    }

    private static string RequireString(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String) throw new InvalidDataException($"Semantic project '{name}' must be a string.");
        return value.GetString() ?? string.Empty;
    }

    private static int RequireInt32(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var result))
            throw new InvalidDataException($"Semantic project '{name}' must be a 32-bit integer.");
        return result;
    }

    private static double RequireDouble(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetDouble(out var result))
            throw new InvalidDataException($"Semantic project '{name}' must be numeric.");
        return result;
    }

    private static Guid RequireGuid(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var result))
            throw new InvalidDataException($"Semantic project '{name}' must be a GUID.");
        return result;
    }

    private static Guid? ReadNullableGuid(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind == JsonValueKind.Null) return null;
        if (value.ValueKind != JsonValueKind.String || !value.TryGetGuid(out var result))
            throw new InvalidDataException($"Semantic project '{name}' must be null or a GUID.");
        return result;
    }

    private static SemanticElementKind RequireKind(JsonElement parent, string name)
    {
        var value = RequireProperty(parent, name);
        if (value.ValueKind != JsonValueKind.Number || !value.TryGetInt32(out var raw))
            throw new InvalidDataException($"Semantic project '{name}' must be a semantic element kind value.");
        return (SemanticElementKind)raw;
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] bytes)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Optimal);
        using var stream = entry.Open();
        stream.Write(bytes, 0, bytes.Length);
    }

    private static byte[] ReadEntry(ZipArchive archive, string name, long limit)
    {
        var entry = archive.GetEntry(name) ?? throw new InvalidDataException($"QS3D package entry '{name}' is missing.");
        if (entry.Length < 0 || entry.Length > limit) throw new InvalidDataException($"QS3D package entry '{name}' exceeds the configured size limit.");
        using var source = entry.Open();
        using var output = new MemoryStream();
        var buffer = new byte[81920];
        long total = 0;
        while (true)
        {
            var read = source.Read(buffer, 0, buffer.Length);
            if (read == 0) break;
            total += read;
            if (total > limit) throw new InvalidDataException($"QS3D package entry '{name}' exceeded the configured size limit while reading.");
            output.Write(buffer, 0, read);
        }
        return output.ToArray();
    }

    private static void ValidatePayload(ManifestDto manifest, string name, byte[] bytes)
    {
        var payload = manifest.Payloads.SingleOrDefault(candidate => StringComparer.Ordinal.Equals(candidate.Name, name))
            ?? throw new InvalidDataException($"Manifest payload '{name}' is missing.");
        if (payload.LengthBytes != bytes.LongLength) throw new InvalidDataException($"Payload '{name}' length does not match its manifest declaration.");
        var actual = Convert.ToHexString(SHA256.HashData(bytes));
        if (!StringComparer.Ordinal.Equals(actual, payload.Sha256Hex)) throw new InvalidDataException($"Payload '{name}' SHA-256 does not match its manifest declaration.");
    }

    private static void RequirePayloadSize(long length, string name)
    {
        if (length < 0 || length > MaxPayloadBytes) throw new InvalidOperationException($"Payload '{name}' exceeds the configured size limit.");
    }

    private static void DeleteIfExists(string path)
    {
        try { if (File.Exists(path)) File.Delete(path); }
        catch { }
    }

    private sealed class ManifestDto
    {
        public int FormatVersion { get; set; }
        public Guid ProjectId { get; set; }
        public List<PayloadDto> Payloads { get; set; } = new();

        public void Validate()
        {
            if (FormatVersion < 1) throw new InvalidDataException("Manifest format version is invalid.");
            if (ProjectId == Guid.Empty) throw new InvalidDataException("Manifest project identity is missing.");
            if (Payloads is null) throw new InvalidDataException("Manifest payload collection is missing.");

            var expected = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [SemanticEntry] = SemanticMediaType,
                [DrawingEntry] = DrawingMediaType
            };
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var payload in Payloads)
            {
                if (payload is null) throw new InvalidDataException("Manifest payload collection contains null.");
                payload.Validate();
                if (!names.Add(payload.Name)) throw new InvalidDataException($"Manifest contains duplicate payload '{payload.Name}'.");
                if (!expected.TryGetValue(payload.Name, out var mediaType))
                    throw new InvalidDataException($"Manifest contains unexpected payload '{payload.Name}'.");
                if (!StringComparer.Ordinal.Equals(payload.MediaType, mediaType))
                    throw new InvalidDataException($"Manifest payload '{payload.Name}' media type must be '{mediaType}'.");
            }
            foreach (var name in expected.Keys)
            {
                if (!names.Contains(name)) throw new InvalidDataException($"Manifest payload '{name}' is missing.");
            }
        }
    }

    private sealed class PayloadDto
    {
        public string Name { get; set; } = string.Empty;
        public string MediaType { get; set; } = string.Empty;
        public long LengthBytes { get; set; }
        public string Sha256Hex { get; set; } = string.Empty;

        public static PayloadDto Create(string name, string mediaType, byte[] bytes) => new()
        {
            Name = name,
            MediaType = mediaType,
            LengthBytes = bytes.LongLength,
            Sha256Hex = Convert.ToHexString(SHA256.HashData(bytes))
        };

        public void Validate()
        {
            if (string.IsNullOrWhiteSpace(Name)) throw new InvalidDataException("Manifest payload name is blank.");
            if (Name.Contains('/') || Name.Contains('\\')) throw new InvalidDataException("Manifest payload name must not contain a path separator.");
            if (string.IsNullOrWhiteSpace(MediaType)) throw new InvalidDataException($"Manifest payload '{Name}' media type is blank.");
            if (LengthBytes < 0 || LengthBytes > MaxPayloadBytes) throw new InvalidDataException($"Manifest payload '{Name}' length is invalid.");
            if (Sha256Hex.Length != 64 || Sha256Hex.Any(static character => !Uri.IsHexDigit(character))) throw new InvalidDataException($"Manifest payload '{Name}' SHA-256 is invalid.");
            Sha256Hex = Sha256Hex.ToUpperInvariant();
        }
    }
}

public sealed record Qs3dBootstrapLoadResult(InMemoryCadDocument Document, SemanticProject Project);
