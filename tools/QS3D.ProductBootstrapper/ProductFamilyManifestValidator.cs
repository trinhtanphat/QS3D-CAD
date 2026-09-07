namespace QS3D.ProductBootstrapper;

public static class ProductFamilyManifestValidator
{
    public const long MaxComponentBytes = 512L * 1024 * 1024;
    public const int MaxComponents = 32;

    private static readonly HashSet<string> AllowedRepositories = new(StringComparer.OrdinalIgnoreCase)
    {
        "trinhtanphat/QS3D-AutoCAD",
        "trinhtanphat/QS3D-BricsCAD",
        "trinhtanphat/QS3D-CAD"
    };

    public static void Validate(ProductFamilyManifest manifest)
    {
        if (manifest.SchemaVersion != 1) throw new InvalidDataException("Unsupported product-family manifest schema.");
        RequireNonBlank(manifest.FamilyVersion, "familyVersion");
        RequireSha(manifest.GeneratedFromSource, 40, "generatedFromSource");
        if (manifest.Components is null || manifest.Components.Count > MaxComponents)
            throw new InvalidDataException($"components must contain at most {MaxComponents} items.");

        var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var productGenerations = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in manifest.Components)
        {
            if (component is null) throw new InvalidDataException("components contains null.");
            RequireNonBlank(component.Id, "component.id");
            RequireNonBlank(component.Product, "component.product");
            RequireNonBlank(component.HostGeneration, "component.hostGeneration");
            RequireNonBlank(component.Qualification, "component.qualification");
            if (!ids.Add(component.Id)) throw new InvalidDataException($"Duplicate component id '{component.Id}'.");
            if (!productGenerations.Add(component.Product + "\u001f" + component.HostGeneration))
                throw new InvalidDataException($"Duplicate product/host generation '{component.Product}/{component.HostGeneration}'.");
            if (component.Product is not ("autocad" or "bricscad" or "standalone"))
                throw new InvalidDataException($"Unsupported product '{component.Product}'.");

            var hasPackageMetadata = new string?[]
            {
                component.Repository, component.ReleaseTag, component.SourceSha, component.AssetName,
                component.DownloadUrl, component.Sha256, component.PackageKind, component.InstallStrategy
            }.Any(value => !string.IsNullOrWhiteSpace(value)) || component.Bytes.HasValue;

            if (!component.Enabled && !hasPackageMetadata) continue;

            RequireNonBlank(component.Repository, "component.repository");
            if (!AllowedRepositories.Contains(component.Repository!))
                throw new InvalidDataException($"Repository '{component.Repository}' is not allowed.");
            RequireNonBlank(component.ReleaseTag, "component.releaseTag");
            RequireSha(component.SourceSha, 40, "component.sourceSha");
            RequireNonBlank(component.AssetName, "component.assetName");
            RequireSha(component.Sha256, 64, "component.sha256");
            if (component.Bytes is null or <= 0 || component.Bytes > MaxComponentBytes)
                throw new InvalidDataException($"component.bytes must be between 1 and {MaxComponentBytes}.");
            RequireNonBlank(component.PackageKind, "component.packageKind");
            RequireNonBlank(component.InstallStrategy, "component.installStrategy");

            var packageKind = component.PackageKind!.ToLowerInvariant();
            var strategy = component.InstallStrategy!.ToLowerInvariant();
            if ((packageKind == "exe" && strategy != "execute") || (packageKind == "zip" && strategy != "extract"))
                throw new InvalidDataException("Package kind and install strategy are inconsistent.");
            if (packageKind is not ("exe" or "zip")) throw new InvalidDataException("Unsupported package kind.");
            if (packageKind == "zip" && component.Enabled && string.IsNullOrWhiteSpace(component.Destination))
                throw new InvalidDataException("Enabled ZIP components require an explicit destination contract.");

            ValidateDownloadUrl(component);
        }
    }

    private static void ValidateDownloadUrl(ProductComponent component)
    {
        if (!Uri.TryCreate(component.DownloadUrl, UriKind.Absolute, out var uri) ||
            uri.Scheme != Uri.UriSchemeHttps || !string.Equals(uri.Host, "github.com", StringComparison.OrdinalIgnoreCase) ||
            !string.IsNullOrEmpty(uri.UserInfo) || !string.IsNullOrEmpty(uri.Query) || !string.IsNullOrEmpty(uri.Fragment))
            throw new InvalidDataException("component.downloadUrl must be a clean HTTPS github.com release URL.");

        var expectedPath = $"/{component.Repository}/releases/download/{Uri.EscapeDataString(component.ReleaseTag!)}/{Uri.EscapeDataString(component.AssetName!)}";
        if (!string.Equals(uri.AbsolutePath, expectedPath, StringComparison.Ordinal))
            throw new InvalidDataException("component.downloadUrl does not match repository/tag/asset identity.");
    }

    private static void RequireNonBlank(string? value, string name)
    {
        if (string.IsNullOrWhiteSpace(value)) throw new InvalidDataException($"{name} is required.");
    }

    private static void RequireSha(string? value, int length, string name)
    {
        if (string.IsNullOrWhiteSpace(value) || value.Length != length || value.Any(c => !Uri.IsHexDigit(c)))
            throw new InvalidDataException($"{name} must be exactly {length} hexadecimal characters.");
    }
}
