namespace QS3D.ProductBootstrapper;

public sealed class BootstrapperOptions
{
    public string ManifestPath { get; init; } = Path.Combine(AppContext.BaseDirectory, "product-family.manifest.json");
    public string? Product { get; init; }
    public string? HostGeneration { get; init; }
    public bool IncludeStandalone { get; init; }
    public bool DryRun { get; init; }
    public bool ContinueOnError { get; init; }
    public bool KeepDownloads { get; init; }

    public static BootstrapperOptions Parse(IReadOnlyList<string> args)
    {
        string? manifest = null, product = null, generation = null;
        var standalone = false; var dry = false; var cont = false; var keep = false;
        for (var i = 0; i < args.Count; i++)
        {
            switch (args[i])
            {
                case "--manifest": manifest = RequireValue(args, ref i); break;
                case "--product": product = RequireValue(args, ref i).ToLowerInvariant(); break;
                case "--host-generation": generation = RequireValue(args, ref i); break;
                case "--standalone": standalone = true; break;
                case "--dry-run": dry = true; break;
                case "--continue-on-error": cont = true; break;
                case "--keep-downloads": keep = true; break;
                default: throw new ArgumentException($"Unknown option '{args[i]}'.");
            }
        }
        if (product is not null && product is not ("autocad" or "bricscad" or "standalone"))
            throw new ArgumentException("--product must be autocad, bricscad, or standalone.");
        return new BootstrapperOptions
        {
            ManifestPath = manifest ?? Path.Combine(AppContext.BaseDirectory, "product-family.manifest.json"),
            Product = product,
            HostGeneration = generation,
            IncludeStandalone = standalone || product == "standalone",
            DryRun = dry,
            ContinueOnError = cont,
            KeepDownloads = keep
        };
    }

    private static string RequireValue(IReadOnlyList<string> args, ref int index)
    {
        if (++index >= args.Count || args[index].StartsWith("--", StringComparison.Ordinal))
            throw new ArgumentException("Option requires a value.");
        return args[index];
    }
}

public sealed record PlanDiagnostic(string Product, string HostGeneration, string Code, string Message);
public sealed record InstallPlanItem(ProductComponent Component, HostInstallation? Host);
public sealed record InstallPlan(IReadOnlyList<InstallPlanItem> Items, IReadOnlyList<PlanDiagnostic> Diagnostics);

public static class InstallPlanner
{
    public static InstallPlan CreatePlan(ProductFamilyManifest manifest, IReadOnlyList<HostInstallation> hosts, BootstrapperOptions options)
    {
        var items = new List<InstallPlanItem>();
        var diagnostics = new List<PlanDiagnostic>();
        var candidates = manifest.Components
            .Where(c => options.Product is null || string.Equals(c.Product, options.Product, StringComparison.OrdinalIgnoreCase))
            .Where(c => options.HostGeneration is null || string.Equals(c.HostGeneration, options.HostGeneration, StringComparison.OrdinalIgnoreCase))
            .OrderBy(c => c.Product, StringComparer.Ordinal)
            .ThenBy(c => c.HostGeneration, StringComparer.Ordinal)
            .ThenBy(c => c.Id, StringComparer.Ordinal);

        foreach (var component in candidates)
        {
            if (component.Product == "standalone")
            {
                if (!options.IncludeStandalone) continue;
                if (component.Enabled) items.Add(new(component, null));
                else diagnostics.Add(new(component.Product, component.HostGeneration, "PENDING", component.Qualification));
                continue;
            }

            var matches = hosts.Where(h =>
                string.Equals(h.Product, component.Product, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(h.HostGeneration, component.HostGeneration, StringComparison.OrdinalIgnoreCase)).ToArray();
            if (matches.Length == 0) continue;
            foreach (var host in matches)
            {
                if (component.Enabled) items.Add(new(component, host));
                else diagnostics.Add(new(component.Product, component.HostGeneration, "PENDING", component.Qualification));
            }
        }

        if ((options.Product is not null || options.HostGeneration is not null) && items.Count == 0)
            diagnostics.Add(new(options.Product ?? "*", options.HostGeneration ?? "*", "NO_INSTALLABLE_COMPONENT", "Explicit selection has no enabled exact-generation package."));
        return new InstallPlan(items, diagnostics);
    }
}
