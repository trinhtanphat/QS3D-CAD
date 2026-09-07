using System.Text.Json;

namespace QS3D.ProductBootstrapper;

public static class ProductFamilyManifestLoader
{
    public static ProductFamilyManifest Load(string path)
    {
        var json = File.ReadAllText(path);
        var manifest = JsonSerializer.Deserialize<ProductFamilyManifest>(json, new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true
        });
        return manifest ?? throw new InvalidDataException("Product-family manifest is empty.");
    }
}
