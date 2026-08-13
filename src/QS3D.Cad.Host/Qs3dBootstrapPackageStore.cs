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
                    PayloadDto.Create(SemanticEntry, "application/vnd.qs3d.semantic+json", semanticBytes),
                    PayloadDto.Create(DrawingEntry, "application/vnd.qs3d.bootstrap+json", drawingBytes)
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

        SemanticProjectSnapshot semanticSnapshot;
        try
        {
            semanticSnapshot = JsonSerializer.Deserialize<SemanticProjectSnapshot>(semanticBytes, JsonOptions)
                ?? throw new InvalidDataException("Semantic project payload is empty.");
        }
        catch (Exception ex) when (ex is JsonException or ArgumentException or FormatException)
        {
            throw new InvalidDataException("Semantic project payload is invalid.", ex);
        }
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
            var names = new HashSet<string>(StringComparer.Ordinal);
            foreach (var payload in Payloads)
            {
                if (payload is null) throw new InvalidDataException("Manifest payload collection contains null.");
                payload.Validate();
                if (!names.Add(payload.Name)) throw new InvalidDataException($"Manifest contains duplicate payload '{payload.Name}'.");
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
