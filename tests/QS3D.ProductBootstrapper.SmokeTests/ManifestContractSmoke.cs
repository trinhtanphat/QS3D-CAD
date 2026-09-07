using QS3D.ProductBootstrapper;

namespace QS3D.ProductBootstrapper.SmokeTests;

internal static class ManifestContractSmoke
{
    private const string EngineeringTag = "test-v0.1.0-ci.266";
    private const string EngineeringSourceSha = "8dd65a7e5061430f76e027467261beb8f18c69a8";
    private const string EngineeringAsset = "QS3D-AutoCAD-0.0.0-ci-Setup.exe";
    private const string EngineeringSha256 = "9452fd0bac1ece086393499f11716d265f0a49d8266ddd2cc47d55bc9d3a07de";
    private const long EngineeringBytes = 67998359;

    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "qs3d-family-manifest-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var valid = Write(root, "valid.json", """
            {
              "schemaVersion": 1,
              "familyVersion": "0.1.0-preview.1",
              "generatedFromSource": "0123456789abcdef0123456789abcdef01234567",
              "components": [
                {
                  "id": "standalone-preview",
                  "product": "standalone",
                  "hostGeneration": "standalone",
                  "enabled": false,
                  "qualification": "pending"
                }
              ]
            }
            """);
            var manifest = ProductFamilyManifestLoader.Load(valid);
            Smoke.Equal(1, manifest.SchemaVersion, "Schema version must round-trip.");
            Smoke.Equal("standalone-preview", manifest.Components.Single().Id, "Component ID must round-trip.");

            var unknown = Write(root, "unknown.json", """
            { "schemaVersion":1, "familyVersion":"v", "generatedFromSource":"0123456789abcdef0123456789abcdef01234567", "components":[], "unexpected":true }
            """);
            Smoke.Throws<InvalidDataException>(() => ProductFamilyManifestLoader.Load(unknown), "Unknown root property must fail closed.");

            var duplicate = Write(root, "duplicate.json", """
            { "schemaVersion":1, "SchemaVersion":1, "familyVersion":"v", "generatedFromSource":"0123456789abcdef0123456789abcdef01234567", "components":[] }
            """);
            Smoke.Throws<InvalidDataException>(() => ProductFamilyManifestLoader.Load(duplicate), "Case-insensitive duplicate property must fail closed.");

            var duplicateId = Write(root, "duplicate-id.json", """
            {
              "schemaVersion":1, "familyVersion":"v", "generatedFromSource":"0123456789abcdef0123456789abcdef01234567",
              "components":[
                {"id":"same","product":"autocad","hostGeneration":"2025","enabled":false,"qualification":"pending"},
                {"id":"SAME","product":"autocad","hostGeneration":"2026","enabled":false,"qualification":"pending"}
              ]
            }
            """);
            Smoke.Throws<InvalidDataException>(() => ProductFamilyManifestLoader.Load(duplicateId), "Duplicate component IDs must fail closed.");

            var invalidEnabled = Write(root, "invalid-enabled.json", """
            {
              "schemaVersion":1, "familyVersion":"v", "generatedFromSource":"0123456789abcdef0123456789abcdef01234567",
              "components":[{"id":"a26","product":"autocad","hostGeneration":"2026","enabled":true,"qualification":"source-ready"}]
            }
            """);
            Smoke.Throws<InvalidDataException>(() => ProductFamilyManifestLoader.Load(invalidEnabled), "Enabled package without exact identity/integrity metadata must fail closed.");

            var repositoryRoot = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", ".."));
            var productionManifest = Path.Combine(repositoryRoot, "installer", "product-family.manifest.json");
            Smoke.True(File.Exists(productionManifest), "Packaged source family manifest must be present during repository smoke.");
            var initial = ProductFamilyManifestLoader.Load(productionManifest);
            Smoke.True(initial.Components.Where(c => c.Product is "autocad" or "bricscad").All(c => !c.Enabled), "Initial vendor-host manifest must remain fail-closed pending durable releases/contracts.");
            Smoke.True(initial.Components.All(c => string.IsNullOrWhiteSpace(c.ReleaseTag) || !c.ReleaseTag.StartsWith("test-v", StringComparison.Ordinal)), "Production family manifest must not embed engineering test release tags.");

            var engineeringManifest = Path.Combine(repositoryRoot, "installer", "product-family.engineering.manifest.json");
            Smoke.True(File.Exists(engineeringManifest), "Engineering family manifest must be present during repository smoke.");
            var engineering = ProductFamilyManifestLoader.Load(engineeringManifest);
            var autocad = engineering.Components.Where(c => c.Product == "autocad").OrderBy(c => c.HostGeneration, StringComparer.Ordinal).ToArray();
            Smoke.Equal(7, autocad.Length, "Engineering manifest must contain exactly seven AutoCAD generations.");
            Smoke.True(autocad.All(c => c.Enabled), "Engineering AutoCAD components must be enabled only for native-test planning.");
            Smoke.True(autocad.All(c => c.ReleaseTag == EngineeringTag), "Engineering components must pin exact CI #266 tag.");
            Smoke.True(autocad.All(c => c.SourceSha == EngineeringSourceSha), "Engineering components must pin exact integrated AutoCAD source SHA.");
            Smoke.True(autocad.All(c => c.AssetName == EngineeringAsset), "Engineering components must pin exact Setup asset.");
            Smoke.True(autocad.All(c => c.Sha256 == EngineeringSha256), "Engineering components must pin exact Setup SHA-256.");
            Smoke.True(autocad.All(c => c.Bytes == EngineeringBytes), "Engineering components must pin exact Setup byte count.");
            Smoke.True(autocad.All(c => c.Qualification.Contains("engineering", StringComparison.OrdinalIgnoreCase) && c.Qualification.Contains("not-native-pass", StringComparison.OrdinalIgnoreCase)), "Engineering components must explicitly remain non-production/native-pending evidence.");
        }
        finally { Directory.Delete(root, recursive: true); }
    }

    private static string Write(string root, string name, string content)
    {
        var path = Path.Combine(root, name);
        File.WriteAllText(path, content);
        return path;
    }
}
