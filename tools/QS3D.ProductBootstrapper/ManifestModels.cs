using System.Text.Json.Serialization;

namespace QS3D.ProductBootstrapper;

public sealed class ProductFamilyManifest
{
    public int SchemaVersion { get; set; }
    public string FamilyVersion { get; set; } = string.Empty;
    public string GeneratedFromSource { get; set; } = string.Empty;
    public List<ProductComponent> Components { get; set; } = new();
}

public sealed class ProductComponent
{
    public string Id { get; set; } = string.Empty;
    public string Product { get; set; } = string.Empty;
    public string HostGeneration { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string Qualification { get; set; } = string.Empty;
    public string? Repository { get; set; }
    public string? ReleaseTag { get; set; }
    public string? SourceSha { get; set; }
    public string? AssetName { get; set; }
    public string? DownloadUrl { get; set; }
    public string? Sha256 { get; set; }
    public long? Bytes { get; set; }
    public string? PackageKind { get; set; }
    public string? InstallStrategy { get; set; }
    public List<string>? Arguments { get; set; }
    public string? Destination { get; set; }
}
