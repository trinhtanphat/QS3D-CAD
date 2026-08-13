using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;
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
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(corrupt)) File.Delete(corrupt);
        }

        Console.WriteLine("PASS qs3d bootstrap package integrity and round trip");
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
