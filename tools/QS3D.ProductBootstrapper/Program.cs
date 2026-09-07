namespace QS3D.ProductBootstrapper;

internal static class Program
{
    public static async Task<int> Main(string[] args)
    {
        try
        {
            var options = BootstrapperOptions.Parse(args);
            var manifest = ProductFamilyManifestLoader.Load(options.ManifestPath);
            using var downloader = new HttpPackageDownloader();
            var coordinator = new BootstrapperCoordinator(new WindowsRegistryHostDiscovery(), downloader, new PackageInstaller());
            var report = await coordinator.RunAsync(manifest, options, CancellationToken.None).ConfigureAwait(false);
            Console.WriteLine(BootstrapperCoordinator.Render(report));
            if (report.HasFailure) return 1;
            if ((options.Product is not null || options.HostGeneration is not null) && report.Plan.Items.Count == 0) return 2;
            return 0;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidDataException or IOException)
        {
            Console.Error.WriteLine("ERROR " + ex.Message);
            return 2;
        }
    }
}
