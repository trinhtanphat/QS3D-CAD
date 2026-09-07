using System.Diagnostics;
using System.IO.Compression;

namespace QS3D.ProductBootstrapper;

public interface IProcessRunner
{
    Task<int> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken);
}

public sealed class ProcessRunner : IProcessRunner
{
    public async Task<int> RunAsync(string executable, IReadOnlyList<string> arguments, CancellationToken cancellationToken)
    {
        var start = new ProcessStartInfo(executable) { UseShellExecute = false };
        foreach (var argument in arguments) start.ArgumentList.Add(argument);
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start package installer.");
        await process.WaitForExitAsync(cancellationToken).ConfigureAwait(false);
        return process.ExitCode;
    }
}

public interface IPackageInstaller
{
    Task<int> InstallAsync(ProductComponent component, string packagePath, CancellationToken cancellationToken);
}

public sealed class PackageInstaller : IPackageInstaller
{
    private readonly IProcessRunner _runner;
    public PackageInstaller(IProcessRunner? runner = null) => _runner = runner ?? new ProcessRunner();

    public async Task<int> InstallAsync(ProductComponent component, string packagePath, CancellationToken cancellationToken)
    {
        var strategy = component.InstallStrategy?.ToLowerInvariant();
        if (strategy == "execute")
        {
            IReadOnlyList<string> arguments = component.Arguments is null ? Array.Empty<string>() : component.Arguments;
            return await _runner.RunAsync(packagePath, arguments, cancellationToken).ConfigureAwait(false);
        }
        if (strategy == "extract")
        {
            if (string.IsNullOrWhiteSpace(component.Destination)) throw new InvalidDataException("ZIP install requires destination.");
            SafeZipInstaller.ExtractAndPublish(packagePath, component.Destination);
            return 0;
        }
        throw new InvalidDataException("Unknown install strategy.");
    }
}

public static class SafeZipInstaller
{
    public const int MaxEntries = 10_000;
    public const long MaxExpandedBytes = 512L * 1024 * 1024;

    public static void ExtractAndPublish(string archivePath, string destination)
    {
        var fullDestination = Path.GetFullPath(Environment.ExpandEnvironmentVariables(destination));
        if (Directory.Exists(fullDestination) || File.Exists(fullDestination))
            throw new IOException("ZIP destination already exists; bootstrapper refuses destructive overwrite.");
        var parent = Path.GetDirectoryName(fullDestination) ?? throw new InvalidDataException("ZIP destination has no parent.");
        Directory.CreateDirectory(parent);
        var staging = Path.Combine(parent, ".qs3d-staging-" + Guid.NewGuid().ToString("N"));
        try
        {
            using var archive = ZipFile.OpenRead(archivePath);
            if (archive.Entries.Count > MaxEntries) throw new InvalidDataException("ZIP contains too many entries.");
            var paths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            long total = 0;
            foreach (var entry in archive.Entries)
            {
                var normalized = entry.FullName.Replace('\\', '/');
                if (string.IsNullOrWhiteSpace(normalized)) throw new InvalidDataException("ZIP contains a blank path.");
                if (normalized.StartsWith("/", StringComparison.Ordinal) || normalized.StartsWith("../", StringComparison.Ordinal) ||
                    normalized.Contains("/../", StringComparison.Ordinal) || normalized.Contains(':', StringComparison.Ordinal))
                    throw new InvalidDataException("ZIP contains an unsafe path.");
                normalized = normalized.TrimEnd('/');
                if (normalized.Length == 0) continue;
                if (!paths.Add(normalized)) throw new InvalidDataException("ZIP contains duplicate normalized paths.");
                total = checked(total + entry.Length);
                if (total > MaxExpandedBytes) throw new InvalidDataException("ZIP expanded content exceeds bounded policy.");
            }

            Directory.CreateDirectory(staging);
            var stagingRoot = Path.GetFullPath(staging) + Path.DirectorySeparatorChar;
            foreach (var entry in archive.Entries)
            {
                var relative = entry.FullName.Replace('/', Path.DirectorySeparatorChar).Replace('\\', Path.DirectorySeparatorChar);
                var target = Path.GetFullPath(Path.Combine(staging, relative));
                if (!target.StartsWith(stagingRoot, StringComparison.OrdinalIgnoreCase)) throw new InvalidDataException("ZIP path escapes staging root.");
                if (entry.FullName.EndsWith("/", StringComparison.Ordinal) || entry.FullName.EndsWith("\\", StringComparison.Ordinal))
                {
                    Directory.CreateDirectory(target);
                    continue;
                }
                Directory.CreateDirectory(Path.GetDirectoryName(target)!);
                entry.ExtractToFile(target, overwrite: false);
            }
            Directory.Move(staging, fullDestination);
        }
        catch
        {
            try { if (Directory.Exists(staging)) Directory.Delete(staging, recursive: true); } catch { }
            throw;
        }
    }
}
