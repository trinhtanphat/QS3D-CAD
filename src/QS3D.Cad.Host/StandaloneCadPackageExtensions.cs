using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public static class StandaloneCadPackageExtensions
{
    public static void SaveProjectPackageWithBackup(this StandaloneCadApplication application, string path)
    {
        ArgumentNullException.ThrowIfNull(application);
        var document = application.Documents.ActiveDocument as InMemoryCadDocument
            ?? throw new InvalidOperationException("No standalone document is active.");
        new Qs3dBootstrapBackupWriter().Save(document, application.Projects.Get(document), path);
    }

    public static Qs3dBootstrapRecoveryLoadResult OpenProjectPackageWithRecovery(this StandaloneCadApplication application, string path)
    {
        ArgumentNullException.ThrowIfNull(application);
        var result = new Qs3dBootstrapRecoveryReader().Load(path);
        application.Documents.Open(result.Document);
        try
        {
            application.Projects.Attach(result.Document, result.Project);
            return result;
        }
        catch
        {
            application.Documents.Close(result.Document.Id);
            throw;
        }
    }
}
