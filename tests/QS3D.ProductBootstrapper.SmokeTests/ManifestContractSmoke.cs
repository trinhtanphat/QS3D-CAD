using QS3D.ProductBootstrapper;

namespace QS3D.ProductBootstrapper.SmokeTests;

internal static class ManifestContractSmoke
{
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

            var productionManifest = Path.GetFullPath(Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "..", "..", "installer", "product-family.manifest.json"));
            Smoke.True(File.Exists(productionManifest), "Packaged source family manifest must be present during repository smoke.");
            var initial = ProductFamilyManifestLoader.Load(productionManifest);
            Smoke.True(initial.Components.Where(c => c.Product is "autocad" or "bricscad").All(c => !c.Enabled), "Initial vendor-host manifest must remain fail-closed pending durable releases/contracts.");
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
