using System.IO.Compression;
using System.Security.Cryptography;
using QS3D.ProductBootstrapper;

namespace QS3D.ProductBootstrapper.SmokeTests;

internal static class BootstrapperBehaviorSmoke
{
    public static void Run()
    {
        TestHostMapping();
        TestExactPlanningAndDryRun();
        TestPackageVerification();
        TestSafeZip();
    }

    private static void TestHostMapping()
    {
        Smoke.True(HostGenerationMapper.TryMap("autocad", "R24.0", out var a21) && a21 == "2021", "AutoCAD R24.0 mapping failed.");
        Smoke.True(HostGenerationMapper.TryMap("autocad", "R24.3", out var a24) && a24 == "2024", "AutoCAD R24.3 mapping failed.");
        Smoke.True(HostGenerationMapper.TryMap("autocad", "R25.1", out var a26) && a26 == "2026", "AutoCAD R25.1 mapping failed.");
        Smoke.True(HostGenerationMapper.TryMap("autocad", "R26.0", out var a27) && a27 == "2027", "AutoCAD R26.0 mapping failed.");
        Smoke.True(HostGenerationMapper.TryMap("bricscad", "V25x64", out var b25) && b25 == "V25", "BricsCAD V25 mapping failed.");
        Smoke.True(!HostGenerationMapper.TryMap("autocad", "R99.0", out _), "Unknown AutoCAD release must not map.");
    }

    private static void TestExactPlanningAndDryRun()
    {
        var manifest = new ProductFamilyManifest
        {
            SchemaVersion = 1,
            FamilyVersion = "test",
            GeneratedFromSource = new string('a', 40),
            Components =
            {
                Enabled("a25", "autocad", "2025"),
                Enabled("a26", "autocad", "2026"),
                new ProductComponent { Id = "b26", Product = "bricscad", HostGeneration = "V26", Enabled = false, Qualification = "pending" }
            }
        };
        var hosts = new[] { new HostInstallation("autocad", "2026", "C:/AutoCAD2026", "fixture"), new HostInstallation("bricscad", "V26", "C:/BricsCADV26", "fixture") };
        var plan = InstallPlanner.CreatePlan(manifest, hosts, new BootstrapperOptions());
        Smoke.Equal(1, plan.Items.Count, "Planner must select only exact enabled generation.");
        Smoke.Equal("a26", plan.Items[0].Component.Id, "Planner selected neighboring generation.");
        Smoke.True(plan.Diagnostics.Any(d => d.Code == "PENDING" && d.HostGeneration == "V26"), "Pending exact host must be diagnostic.");

        var downloader = new CountingDownloader();
        var installer = new CountingInstaller();
        var coordinator = new BootstrapperCoordinator(new FixedDiscovery(hosts), downloader, installer);
        var report = coordinator.RunAsync(manifest, new BootstrapperOptions { DryRun = true }, CancellationToken.None).GetAwaiter().GetResult();
        Smoke.Equal(0, downloader.Calls, "Dry-run must not download.");
        Smoke.Equal(0, installer.Calls, "Dry-run must not install.");
        Smoke.True(BootstrapperCoordinator.Render(report).Contains("SUMMARY", StringComparison.Ordinal), "Dry-run report missing summary.");
    }

    private static void TestPackageVerification()
    {
        var root = Path.Combine(Path.GetTempPath(), "qs3d-package-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var path = Path.Combine(root, "payload.bin");
            var bytes = "qs3d-exact-bytes"u8.ToArray();
            File.WriteAllBytes(path, bytes);
            var component = Enabled("verify", "autocad", "2026");
            component.Bytes = bytes.Length;
            component.Sha256 = Convert.ToHexString(SHA256.HashData(bytes)).ToLowerInvariant();
            PackageVerifier.Verify(path, component);
            component.Sha256 = new string('0', 64);
            Smoke.Throws<InvalidDataException>(() => PackageVerifier.Verify(path, component), "Digest mismatch must fail closed.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static void TestSafeZip()
    {
        var root = Path.Combine(Path.GetTempPath(), "qs3d-zip-smoke-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var safeZip = Path.Combine(root, "safe.zip");
            using (var archive = ZipFile.Open(safeZip, ZipArchiveMode.Create))
            {
                var entry = archive.CreateEntry("bundle/file.txt");
                using var writer = new StreamWriter(entry.Open());
                writer.Write("ok");
            }
            var destination = Path.Combine(root, "published");
            SafeZipInstaller.ExtractAndPublish(safeZip, destination);
            Smoke.True(File.Exists(Path.Combine(destination, "bundle", "file.txt")), "Safe ZIP did not publish expected file.");

            var badZip = Path.Combine(root, "bad.zip");
            using (var archive = ZipFile.Open(badZip, ZipArchiveMode.Create)) archive.CreateEntry("../escape.txt");
            Smoke.Throws<InvalidDataException>(() => SafeZipInstaller.ExtractAndPublish(badZip, Path.Combine(root, "bad-published")), "Traversal ZIP must fail closed.");
        }
        finally { Directory.Delete(root, true); }
    }

    private static ProductComponent Enabled(string id, string product, string generation) => new()
    {
        Id = id, Product = product, HostGeneration = generation, Enabled = true, Qualification = "source-ready",
        Repository = product == "autocad" ? "trinhtanphat/QS3D-AutoCAD" : "trinhtanphat/QS3D-BricsCAD",
        ReleaseTag = "v1", SourceSha = new string('a', 40), AssetName = id + ".exe",
        DownloadUrl = $"https://github.com/{(product == "autocad" ? "trinhtanphat/QS3D-AutoCAD" : "trinhtanphat/QS3D-BricsCAD")}/releases/download/v1/{id}.exe",
        Sha256 = new string('b', 64), Bytes = 1, PackageKind = "exe", InstallStrategy = "execute"
    };

    private sealed class FixedDiscovery(IReadOnlyList<HostInstallation> hosts) : IHostDiscovery
    {
        public IReadOnlyList<HostInstallation> Discover() => hosts;
    }

    private sealed class CountingDownloader : IPackageDownloader
    {
        public int Calls { get; private set; }
        public Task DownloadAsync(ProductComponent component, string destination, CancellationToken cancellationToken) { Calls++; return Task.CompletedTask; }
    }

    private sealed class CountingInstaller : IPackageInstaller
    {
        public int Calls { get; private set; }
        public Task<int> InstallAsync(ProductComponent component, string packagePath, CancellationToken cancellationToken) { Calls++; return Task.FromResult(0); }
    }
}
