using QS3D.ProductBootstrapper;

namespace QS3D.ProductBootstrapper.SmokeTests;

internal static class ManifestContractSmoke
{
    public static void Run()
    {
        var root = Path.Combine(Path.GetTempPath(), "qs3d-family-red-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(root);
        try
        {
            var valid = Path.Combine(root, "valid.json");
            File.WriteAllText(valid, """
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

            var unknown = Path.Combine(root, "unknown.json");
            File.WriteAllText(unknown, """
            {
              "schemaVersion": 1,
              "familyVersion": "0.1.0-preview.1",
              "generatedFromSource": "0123456789abcdef0123456789abcdef01234567",
              "components": [],
              "unexpected": true
            }
            """);
            Smoke.Throws<InvalidDataException>(
                () => ProductFamilyManifestLoader.Load(unknown),
                "Schema-1 manifest must fail closed on unknown root properties.");
        }
        finally
        {
            Directory.Delete(root, recursive: true);
        }
    }
}
