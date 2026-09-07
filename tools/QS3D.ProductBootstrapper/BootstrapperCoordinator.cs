namespace QS3D.ProductBootstrapper;

public sealed record ComponentInstallResult(string ComponentId, string Status, int? ExitCode, string? Detail);
public sealed record BootstrapperReport(IReadOnlyList<HostInstallation> Hosts, InstallPlan Plan, IReadOnlyList<ComponentInstallResult> Results)
{
    public bool HasFailure => Results.Any(r => r.Status == "Failed");
}

public sealed class BootstrapperCoordinator
{
    private readonly IHostDiscovery _discovery;
    private readonly IPackageDownloader _downloader;
    private readonly IPackageInstaller _installer;

    public BootstrapperCoordinator(IHostDiscovery discovery, IPackageDownloader downloader, IPackageInstaller installer)
    {
        _discovery = discovery;
        _downloader = downloader;
        _installer = installer;
    }

    public async Task<BootstrapperReport> RunAsync(ProductFamilyManifest manifest, BootstrapperOptions options, CancellationToken cancellationToken)
    {
        var hosts = _discovery.Discover();
        var plan = InstallPlanner.CreatePlan(manifest, hosts, options);
        var results = new List<ComponentInstallResult>();
        if (options.DryRun)
        {
            foreach (var item in plan.Items) results.Add(new(item.Component.Id, "DryRun", null, null));
            return new(hosts, plan, results);
        }

        var tempRoot = Path.Combine(Path.GetTempPath(), "qs3d-family-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(tempRoot);
        try
        {
            var failed = false;
            foreach (var item in plan.Items)
            {
                if (failed && !options.ContinueOnError)
                {
                    results.Add(new(item.Component.Id, "SkippedAfterFailure", null, null));
                    continue;
                }

                var safeAsset = Path.GetFileName(item.Component.AssetName ?? item.Component.Id);
                var packagePath = Path.Combine(tempRoot, safeAsset);
                try
                {
                    await _downloader.DownloadAsync(item.Component, packagePath, cancellationToken).ConfigureAwait(false);
                    PackageVerifier.Verify(packagePath, item.Component);
                    var exitCode = await _installer.InstallAsync(item.Component, packagePath, cancellationToken).ConfigureAwait(false);
                    if (exitCode == 0) results.Add(new(item.Component.Id, "Installed", 0, null));
                    else
                    {
                        failed = true;
                        results.Add(new(item.Component.Id, "Failed", exitCode, "Child installer returned nonzero exit code."));
                    }
                }
                catch (Exception ex) when (ex is IOException or InvalidDataException or HttpRequestException or InvalidOperationException)
                {
                    failed = true;
                    results.Add(new(item.Component.Id, "Failed", null, Redact(ex.Message)));
                }
            }
        }
        finally
        {
            if (!options.KeepDownloads)
            {
                try { if (Directory.Exists(tempRoot)) Directory.Delete(tempRoot, recursive: true); } catch { }
            }
        }
        return new(hosts, plan, results);
    }

    public static string Render(BootstrapperReport report)
    {
        var lines = new List<string>();
        foreach (var host in report.Hosts)
            lines.Add($"HOST product={host.Product} generation={host.HostGeneration} root={host.InstallRoot}");
        foreach (var item in report.Plan.Items)
            lines.Add($"PLAN component={item.Component.Id} product={item.Component.Product} generation={item.Component.HostGeneration}");
        foreach (var diagnostic in report.Plan.Diagnostics)
            lines.Add($"PLAN-DIAGNOSTIC product={diagnostic.Product} generation={diagnostic.HostGeneration} code={diagnostic.Code}");
        foreach (var result in report.Results)
            lines.Add($"RESULT component={result.ComponentId} status={result.Status} exit={(result.ExitCode?.ToString() ?? "-")}");
        lines.Add($"SUMMARY hosts={report.Hosts.Count} planned={report.Plan.Items.Count} results={report.Results.Count} failed={(report.HasFailure ? 1 : 0)}");
        return string.Join(Environment.NewLine, lines);
    }

    private static string Redact(string value)
    {
        var question = value.IndexOf('?');
        return question >= 0 ? value[..question] : value;
    }
}
