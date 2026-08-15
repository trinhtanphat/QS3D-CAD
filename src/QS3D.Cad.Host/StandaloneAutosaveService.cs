using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed record StandaloneAutosaveSnapshotInfo(
    string Path,
    DrawingId DrawingId,
    string DrawingName,
    DateTimeOffset LastWriteUtc);

public sealed class StandaloneAutosaveService
{
    public const int MaxDiscoveredSnapshots = 256;
    private readonly Qs3dBootstrapPackageStore _store;

    public StandaloneAutosaveService(Qs3dBootstrapPackageStore? store = null)
        => _store = store ?? new Qs3dBootstrapPackageStore();

    public string Save(InMemoryCadDocument document, SemanticProject project, string directory)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        Directory.CreateDirectory(root);
        var path = Path.Combine(root, $"{document.Id.Value:N}.autosave.qs3d");
        _store.Save(document, project, path);
        return path;
    }

    public Qs3dBootstrapLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return _store.Load(Path.GetFullPath(path));
    }

    public IReadOnlyList<StandaloneAutosaveSnapshotInfo> Discover(string directory)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(directory);
        var root = Path.GetFullPath(directory);
        if (!Directory.Exists(root)) return Array.Empty<StandaloneAutosaveSnapshotInfo>();

        var files = Directory.EnumerateFiles(root, "*.autosave.qs3d", SearchOption.TopDirectoryOnly)
            .OrderByDescending(File.GetLastWriteTimeUtc)
            .Take(MaxDiscoveredSnapshots)
            .ToArray();
        var result = new List<StandaloneAutosaveSnapshotInfo>(files.Length);
        foreach (var path in files)
        {
            try
            {
                var loaded = _store.Load(path);
                result.Add(new StandaloneAutosaveSnapshotInfo(
                    Path.GetFullPath(path),
                    loaded.Document.Id,
                    loaded.Document.Name,
                    new DateTimeOffset(File.GetLastWriteTimeUtc(path), TimeSpan.Zero)));
            }
            catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException or IOException or UnauthorizedAccessException)
            {
            }
        }
        return result;
    }
}
