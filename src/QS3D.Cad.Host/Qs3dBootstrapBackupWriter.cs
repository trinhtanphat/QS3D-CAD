using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed class Qs3dBootstrapBackupWriter
{
    private readonly Qs3dBootstrapPackageStore _store;

    public Qs3dBootstrapBackupWriter(Qs3dBootstrapPackageStore? store = null)
        => _store = store ?? new Qs3dBootstrapPackageStore();

    public static string BackupPath(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        return Path.GetFullPath(path) + ".bak";
    }

    public void Save(InMemoryCadDocument document, SemanticProject project, string path)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(project);
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var primary = Path.GetFullPath(path);
        var directory = Path.GetDirectoryName(primary) ?? throw new InvalidOperationException("Package path has no parent directory.");
        Directory.CreateDirectory(directory);
        if (File.Exists(primary) && CanLoad(primary)) PublishBackup(primary, BackupPath(primary));
        _store.Save(document, project, primary);
    }

    private bool CanLoad(string path)
    {
        try { _store.Load(path); return true; }
        catch (Exception ex) when (ex is FileNotFoundException or InvalidDataException) { return false; }
    }

    private void PublishBackup(string primary, string backup)
    {
        var directory = Path.GetDirectoryName(primary) ?? throw new InvalidOperationException("Package path has no parent directory.");
        var temporary = Path.Combine(directory, ".qs3d-backup-" + Guid.NewGuid().ToString("N") + ".tmp");
        try
        {
            File.Copy(primary, temporary, false);
            _store.Load(temporary);
            File.Move(temporary, backup, true);
        }
        finally
        {
            try { if (File.Exists(temporary)) File.Delete(temporary); } catch { }
        }
    }
}
