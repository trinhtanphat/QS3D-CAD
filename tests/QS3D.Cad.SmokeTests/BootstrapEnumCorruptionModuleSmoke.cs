using System.Runtime.CompilerServices;
using QS3D.Cad.Host;

namespace QS3D.Cad.SmokeTests;

internal static class BootstrapEnumCorruptionModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qs3d-bootstrap-enum-corruption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new BootstrapDrawingStore();
            var drawingId = Guid.NewGuid();

            var invalidEntityKind = Path.Combine(directory, "invalid-entity-kind.json");
            File.WriteAllText(invalidEntityKind, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Invalid entity kind",
              "Entities": [
                { "Handle": "1", "Kind": "999", "Min": [0,0,0], "Max": [1,1,0], "Properties": {}, "LayerName": "0" }
              ],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": []
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(invalidEntityKind));

            var invalidBlockEntityKind = Path.Combine(directory, "invalid-block-entity-kind.json");
            File.WriteAllText(invalidBlockEntityKind, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Invalid block entity kind",
              "Entities": [],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": [
                {
                  "Name": "BAD",
                  "BasePoint": [0,0,0],
                  "Entities": [
                    { "Kind": "999", "Min": [0,0,0], "Max": [1,1,0], "Properties": {}, "LayerName": "0" }
                  ]
                }
              ]
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(invalidBlockEntityKind));

            var familyId = Guid.NewGuid();
            var invalidSemanticKind = Path.Combine(directory, "invalid-semantic-kind.json");
            File.WriteAllText(invalidSemanticKind, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Invalid semantic kind",
              "Entities": [],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": [],
              "SemanticProject": {
                "ProjectId": "{{Guid.NewGuid():D}}",
                "Name": "Invalid semantic kind project",
                "Floors": [],
                "Zones": [],
                "Families": [
                  { "Id": "{{familyId:D}}", "Kind": "999", "Name": "Invalid family" }
                ],
                "Elements": []
              }
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(invalidSemanticKind));

            Console.WriteLine("PASS bootstrap undefined enum values are rejected");
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
