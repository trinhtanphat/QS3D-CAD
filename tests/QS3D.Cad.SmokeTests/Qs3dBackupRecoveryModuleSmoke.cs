using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Nodes;
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

            CorruptDrawingPayloadWithMatchingManifest(path);
            var drawingRecovered = reader.Load(path);
            Require(drawingRecovered.RecoveredFromBackup, "well-hashed malformed drawing payload must recover from backup");
            Require(!string.IsNullOrWhiteSpace(drawingRecovered.PrimaryError), "drawing-payload recovery must retain primary diagnostics");
            Equal(Path.GetFullPath(backup), drawingRecovered.SourcePath);
            Equal(0, drawingRecovered.Project.Elements.Count);

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

    private static void CorruptDrawingPayloadWithMatchingManifest(string path)
    {
        var malformed = Encoding.UTF8.GetBytes("{not-json");
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: false);

        var manifestEntry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("manifest payload missing");
        string manifestJson;
        using (var reader = new StreamReader(manifestEntry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false))
            manifestJson = reader.ReadToEnd();
        var manifest = JsonNode.Parse(manifestJson)?.AsObject() ?? throw new InvalidOperationException("manifest JSON object missing");
        var payloads = manifest["Payloads"]?.AsArray() ?? throw new InvalidOperationException("manifest payload array missing");
        var drawingDeclaration = payloads
            .OfType<JsonObject>()
            .Single(candidate => StringComparer.Ordinal.Equals(candidate["Name"]?.GetValue<string>(), "drawing-bootstrap.json"));
        drawingDeclaration["LengthBytes"] = malformed.LongLength;
        drawingDeclaration["Sha256Hex"] = Convert.ToHexString(SHA256.HashData(malformed));

        var drawingEntry = archive.GetEntry("drawing-bootstrap.json") ?? throw new InvalidOperationException("drawing payload missing");
        drawingEntry.Delete();
        var drawingReplacement = archive.CreateEntry("drawing-bootstrap.json");
        using (var stream = drawingReplacement.Open()) stream.Write(malformed, 0, malformed.Length);

        manifestEntry.Delete();
        var manifestReplacement = archive.CreateEntry("manifest.json");
        var manifestBytes = Encoding.UTF8.GetBytes(manifest.ToJsonString());
        using (var stream = manifestReplacement.Open()) stream.Write(manifestBytes, 0, manifestBytes.Length);
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
