namespace QS3D.Cad.Host;

public sealed class Qs3dBootstrapRecoveryReader
{
    private readonly Qs3dBootstrapPackageStore _store;

    public Qs3dBootstrapRecoveryReader(Qs3dBootstrapPackageStore? store = null)
        => _store = store ?? new Qs3dBootstrapPackageStore();

    public Qs3dBootstrapRecoveryLoadResult Load(string path)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(path);
        var primary = Path.GetFullPath(path);
        try
        {
            var loaded = _store.Load(primary);
            return new Qs3dBootstrapRecoveryLoadResult(loaded.Document, loaded.Project, false, null, primary);
        }
        catch (Exception primaryError) when (primaryError is FileNotFoundException or InvalidDataException)
        {
            var backup = Qs3dBootstrapBackupWriter.BackupPath(primary);
            if (!File.Exists(backup))
                throw new InvalidDataException($"Primary QS3D package failed and no backup is available: {primaryError.Message}", primaryError);
            try
            {
                var loaded = _store.Load(backup);
                return new Qs3dBootstrapRecoveryLoadResult(loaded.Document, loaded.Project, true, primaryError.Message, backup);
            }
            catch (Exception backupError) when (backupError is FileNotFoundException or InvalidDataException)
            {
                throw new InvalidDataException(
                    $"Primary and backup QS3D packages are unusable. Primary: {primaryError.Message} Backup: {backupError.Message}",
                    new AggregateException(primaryError, backupError));
            }
        }
    }
}
