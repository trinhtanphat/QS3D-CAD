using System.Text.Json;

namespace QS3D.ProductBootstrapper;

public static class ProductFamilyManifestLoader
{
    private static readonly string[] RootProperties = { "schemaVersion", "familyVersion", "generatedFromSource", "components" };
    private static readonly string[] ComponentProperties =
    {
        "id", "product", "hostGeneration", "enabled", "qualification", "repository", "releaseTag", "sourceSha",
        "assetName", "downloadUrl", "sha256", "bytes", "packageKind", "installStrategy", "arguments", "destination"
    };

    public static ProductFamilyManifest Load(string path)
    {
        try
        {
            using var document = JsonDocument.Parse(File.ReadAllText(path));
            if (document.RootElement.ValueKind != JsonValueKind.Object)
                throw new InvalidDataException("Product-family manifest root must be an object.");
            ValidateObjectShape(document.RootElement, RootProperties, "root");
            if (!TryGetPropertyIgnoreCase(document.RootElement, "components", out var components) || components.ValueKind != JsonValueKind.Array)
                throw new InvalidDataException("components must be an array.");
            foreach (var component in components.EnumerateArray())
            {
                if (component.ValueKind != JsonValueKind.Object) throw new InvalidDataException("Each component must be an object.");
                ValidateObjectShape(component, ComponentProperties, "component");
            }

            var manifest = JsonSerializer.Deserialize<ProductFamilyManifest>(document.RootElement.GetRawText(), new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidDataException("Product-family manifest is empty.");
            ProductFamilyManifestValidator.Validate(manifest);
            return manifest;
        }
        catch (InvalidDataException)
        {
            throw;
        }
        catch (JsonException ex)
        {
            throw new InvalidDataException("Product-family manifest JSON is invalid.", ex);
        }
    }

    private static void ValidateObjectShape(JsonElement element, IReadOnlyCollection<string> allowed, string scope)
    {
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var property in element.EnumerateObject())
        {
            if (!allowed.Contains(property.Name, StringComparer.OrdinalIgnoreCase))
                throw new InvalidDataException($"Unknown {scope} property '{property.Name}'.");
            if (!seen.Add(property.Name))
                throw new InvalidDataException($"Duplicate {scope} property '{property.Name}'.");
        }
        foreach (var required in allowed.Take(scope == "root" ? 4 : 5))
        {
            if (!seen.Contains(required)) throw new InvalidDataException($"Missing required {scope} property '{required}'.");
        }
    }

    private static bool TryGetPropertyIgnoreCase(JsonElement element, string name, out JsonElement value)
    {
        foreach (var property in element.EnumerateObject())
        {
            if (string.Equals(property.Name, name, StringComparison.OrdinalIgnoreCase))
            {
                value = property.Value;
                return true;
            }
        }
        value = default;
        return false;
    }
}
