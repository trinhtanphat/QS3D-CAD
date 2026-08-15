using System.Runtime.CompilerServices;
using System.Text.Json.Nodes;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CadBackendQualificationEvidenceJsonIntegrityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var evidence = new CadBackendQualificationEvidence(
            "native.integrity",
            "25.1.0",
            "0123456789abcdef0123456789abcdef01234567",
            CadCapabilities.TwoDimensional | CadCapabilities.Layouts,
            new DateTimeOffset(2026, 8, 15, 8, 0, 0, TimeSpan.Zero),
            "integrity-001",
            true);
        var canonical = CadBackendQualificationEvidenceJson.Serialize(new[] { evidence });

        var caseInsensitive = canonical
            .Replace("\"Schema\"", "\"schema\"", StringComparison.Ordinal)
            .Replace("\"Items\"", "\"items\"", StringComparison.Ordinal)
            .Replace("\"Passed\"", "\"passed\"", StringComparison.Ordinal);
        var restored = CadBackendQualificationEvidenceJson.Deserialize(caseInsensitive).Single();
        Equal(evidence.EvidenceId, restored.EvidenceId, "case-insensitive evidence ID");
        Equal(true, restored.Passed, "case-insensitive Passed");

        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(AddRootProperty(canonical, "Unexpected", "1")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(AddRootProperty(canonical, "schema", "1")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(AddItemProperty(canonical, "Unexpected", "1")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(AddItemProperty(canonical, "passed", "false")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(RemoveItemProperty(canonical, "Passed")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(RemoveItemProperty(canonical, "QualifiedAt")));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize(ReplaceItemsWithNull(canonical)));
        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize("[]"));

        var two = CadBackendQualificationEvidenceJson.Serialize(new[]
        {
            evidence,
            new CadBackendQualificationEvidence(
                "native.integrity.second",
                "25.1.0",
                "fedcba9876543210fedcba9876543210fedcba98",
                CadCapabilities.TwoDimensional,
                evidence.QualifiedAt.AddMinutes(1),
                "integrity-002",
                false)
        });
        Equal(2, CadBackendQualificationEvidenceJson.Deserialize(two).Count, "canonical multi-item round trip");

        Console.WriteLine("PASS qualification evidence JSON rejects ambiguous schema-1 shapes without weakening evidence validation");
    }

    private static string AddRootProperty(string json, string name, string value)
    {
        var index = json.IndexOf('{');
        if (index < 0) throw new InvalidOperationException("Canonical evidence root missing.");
        return json.Insert(index + 1, $"\n  \"{name}\": {value},");
    }

    private static string AddItemProperty(string json, string name, string value)
    {
        var marker = "\"Items\": [";
        var items = json.IndexOf(marker, StringComparison.Ordinal);
        if (items < 0) throw new InvalidOperationException("Canonical evidence items missing.");
        var item = json.IndexOf('{', items + marker.Length);
        if (item < 0) throw new InvalidOperationException("Canonical evidence item missing.");
        return json.Insert(item + 1, $"\n      \"{name}\": {value},");
    }

    private static string RemoveItemProperty(string json, string name)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Canonical evidence root missing.");
        var item = root["Items"]?.AsArray().Single()?.AsObject() ?? throw new InvalidOperationException("Canonical evidence item missing.");
        if (!item.Remove(name)) throw new InvalidOperationException($"Canonical evidence property '{name}' missing.");
        return root.ToJsonString();
    }

    private static string ReplaceItemsWithNull(string json)
    {
        var root = JsonNode.Parse(json)?.AsObject() ?? throw new InvalidOperationException("Canonical evidence root missing.");
        root["Items"] = null;
        return root.ToJsonString();
    }

    private static void Equal<T>(T expected, T actual, string operation) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{operation}: expected {expected}, got {actual}.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
