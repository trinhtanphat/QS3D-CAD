using System.Runtime.CompilerServices;
using QS3D.Cad.Host;

namespace QS3D.Cad.SmokeTests;

internal static class BootstrapDrawingCorruptionModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qs3d-bootstrap-corruption-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new BootstrapDrawingStore();
            var malformed = Path.Combine(directory, "malformed.json");
            File.WriteAllText(malformed, "{not-json");
            Throws<InvalidDataException>(() => store.LoadWithProject(malformed));

            var drawingId = Guid.NewGuid();
            var nullEntity = Path.Combine(directory, "null-entity.json");
            File.WriteAllText(nullEntity, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Null entity",
              "Entities": [null],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": []
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(nullEntity));

            var duplicateHandle = Path.Combine(directory, "duplicate-handle.json");
            File.WriteAllText(duplicateHandle, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Duplicate handle",
              "Entities": [
                { "Handle": "1", "Kind": "Point", "Min": [0,0,0], "Max": [0,0,0], "Properties": {}, "LayerName": "0" },
                { "Handle": "1", "Kind": "Point", "Min": [1,1,0], "Max": [1,1,0], "Properties": {}, "LayerName": "0" }
              ],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": []
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(duplicateHandle));

            var nullSemanticElement = Path.Combine(directory, "null-semantic-element.json");
            File.WriteAllText(nullSemanticElement, $$"""
            {
              "Schema": 4,
              "DrawingId": "{{drawingId:D}}",
              "Name": "Null semantic element",
              "Entities": [],
              "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
              "CurrentLayerName": "0",
              "Blocks": [],
              "SemanticProject": {
                "ProjectId": "{{Guid.NewGuid():D}}",
                "Name": "Corrupt semantic project",
                "Floors": [],
                "Zones": [],
                "Families": [],
                "Elements": [null]
              }
            }
            """);
            Throws<InvalidDataException>(() => store.LoadWithProject(nullSemanticElement));

            Console.WriteLine("PASS bootstrap drawing corruption is normalized to InvalidDataException");
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
