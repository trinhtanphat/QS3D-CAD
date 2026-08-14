using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class Qs3dBootstrapPackageModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = (InMemoryCadDocument)app.NewDocument("Package Project");
        Success(app.Execute("LINE 0 0 10 0"));
        Success(app.Execute("QSTAG 1 Wall \"Packaged Wall\""));
        Success(app.Execute("QSPROP 1 LengthMm 2500"));
        var project = app.Projects.Get(document);

        var package = new Qs3dBootstrapPackageStore(app.Store);
        var path = Path.Combine(Path.GetTempPath(), $"qs3d-package-{Guid.NewGuid():N}.qs3d");
        var corrupt = Path.Combine(Path.GetTempPath(), $"qs3d-package-corrupt-{Guid.NewGuid():N}.qs3d");
        var malformedManifest = Path.Combine(Path.GetTempPath(), $"qs3d-package-manifest-{Guid.NewGuid():N}.qs3d");
        var extraManifestPayload = Path.Combine(Path.GetTempPath(), $"qs3d-package-extra-manifest-{Guid.NewGuid():N}.qs3d");
        var wrongMediaType = Path.Combine(Path.GetTempPath(), $"qs3d-package-media-type-{Guid.NewGuid():N}.qs3d");
        try
        {
            package.Save(document, project, path);
            Require(File.Exists(path), "package save must publish the target file");
            var loaded = package.Load(path);
            Equal(project.Id, loaded.Project.Id);
            Equal(document.Id, loaded.Document.Id);
            var element = loaded.Project.Elements.Single();
            Equal("Packaged Wall", element.Name);
            Equal("2500", element.Properties["LengthMm"]);
            Equal("1", element.SourceReference!.Value.Handle.Value);

            File.Copy(path, corrupt, overwrite: true);
            using (var file = new FileStream(corrupt, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: false))
            {
                var semantic = archive.GetEntry("semantic-project.json") ?? throw new InvalidOperationException("semantic payload missing");
                semantic.Delete();
                var replacement = archive.CreateEntry("semantic-project.json");
                using var writer = new StreamWriter(replacement.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write("{}");
            }
            Throws<InvalidDataException>(() => package.Load(corrupt));

            File.Copy(path, malformedManifest, overwrite: true);
            using (var file = new FileStream(malformedManifest, FileMode.Open, FileAccess.ReadWrite, FileShare.None))
            using (var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: false))
            {
                var manifest = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("manifest payload missing");
                manifest.Delete();
                var replacement = archive.CreateEntry("manifest.json");
                using var writer = new StreamWriter(replacement.Open(), Encoding.UTF8, leaveOpen: false);
                writer.Write("{not-json");
            }
            Throws<InvalidDataException>(() => package.Load(malformedManifest));

            File.Copy(path, extraManifestPayload, overwrite: true);
            RewriteManifest(extraManifestPayload, manifest =>
            {
                var payloads = manifest["Payloads"]?.AsArray() ?? throw new InvalidOperationException("manifest payload array missing");
                payloads.Add(new JsonObject
                {
                    ["Name"] = "undeclared-extra.json",
                    ["MediaType"] = "application/octet-stream",
                    ["LengthBytes"] = 0,
                    ["Sha256Hex"] = new string('0', 64)
                });
            });
            Throws<InvalidDataException>(() => package.Load(extraManifestPayload));

            File.Copy(path, wrongMediaType, overwrite: true);
            RewriteManifest(wrongMediaType, manifest =>
            {
                var payloads = manifest["Payloads"]?.AsArray() ?? throw new InvalidOperationException("manifest payload array missing");
                var semantic = payloads
                    .OfType<JsonObject>()
                    .Single(candidate => StringComparer.Ordinal.Equals(candidate["Name"]?.GetValue<string>(), "semantic-project.json"));
                semantic["MediaType"] = "application/octet-stream";
            });
            Throws<InvalidDataException>(() => package.Load(wrongMediaType));
        }
        finally
        {
            foreach (var candidate in new[] { path, corrupt, malformedManifest, extraManifestPayload, wrongMediaType })
            {
                if (File.Exists(candidate)) File.Delete(candidate);
            }
        }

        Console.WriteLine("PASS qs3d bootstrap package integrity and round trip");
    }

    private static void RewriteManifest(string path, Action<JsonObject> mutate)
    {
        using var file = new FileStream(path, FileMode.Open, FileAccess.ReadWrite, FileShare.None);
        using var archive = new ZipArchive(file, ZipArchiveMode.Update, leaveOpen: false);
        var entry = archive.GetEntry("manifest.json") ?? throw new InvalidOperationException("manifest payload missing");
        string json;
        using (var reader = new StreamReader(entry.Open(), Encoding.UTF8, detectEncodingFromByteOrderMarks: true, leaveOpen: false))
            json = reader.ReadToEnd();
        var manifest = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("manifest JSON object missing");
        mutate(manifest);
        entry.Delete();
        var replacement = archive.CreateEntry("manifest.json");
        using var writer = new StreamWriter(replacement.Open(), Encoding.UTF8, leaveOpen: false);
        writer.Write(manifest.ToJsonString(new JsonSerializerOptions { WriteIndented = true }));
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
