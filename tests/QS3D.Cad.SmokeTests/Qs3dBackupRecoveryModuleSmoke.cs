using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class Qs3dBackupRecoveryModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qs3d-recovery-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        var path = Path.Combine(directory, "project.qs3d");
        try
        {
            var app = new StandaloneCadApplication();
            var document = (InMemoryCadDocument)app.NewDocument("Recovery");
            var writer = new Qs3dBootstrapBackupWriter();
            var reader = new Qs3dBootstrapRecoveryReader();
            var raw = new Qs3dBootstrapPackageStore();

            writer.Save(document, app.Projects.Get(document), path);
            Require(!File.Exists(Qs3dBootstrapBackupWriter.BackupPath(path)), "first save must not invent a backup");

            Success(app.Execute("LINE 0 0 10 0"));
            Success(app.Execute("QSTAG 1 Wall RecoveryWall"));
            writer.Save(document, app.Projects.Get(document), path);

            var backup = Qs3dBootstrapBackupWriter.BackupPath(path);
            Require(File.Exists(backup), "second save must publish a validated previous-generation backup");
            Equal(1, raw.Load(path).Project.Elements.Count);
            Equal(0, raw.Load(backup).Project.Elements.Count);

            File.WriteAllBytes(path, new byte[] { 0x51, 0x53, 0x33, 0x44 });
            var recovered = reader.Load(path);
            Require(recovered.RecoveredFromBackup, "corrupt primary must be reported as backup recovery");
            Require(!string.IsNullOrWhiteSpace(recovered.PrimaryError), "recovery must retain primary failure diagnostics");
            Equal(Path.GetFullPath(backup), recovered.SourcePath);
            Equal(0, recovered.Project.Elements.Count);

            File.Delete(backup);
            Throws<InvalidDataException>(() => reader.Load(path));
            Console.WriteLine("PASS qs3d validated backup recovery lifecycle");
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected} but got {actual}.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
