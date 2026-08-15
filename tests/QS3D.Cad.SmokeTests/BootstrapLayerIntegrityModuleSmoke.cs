using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class BootstrapLayerIntegrityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var directory = Path.Combine(Path.GetTempPath(), "qs3d-bootstrap-layer-integrity-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(directory);
        try
        {
            var store = new BootstrapDrawingStore();
            RejectBlankModernEntityLayer(store, directory);
            RejectUndeclaredModernEntityLayer(store, directory);
            RejectBlankModernBlockMemberLayer(store, directory);
            RejectUndeclaredModernBlockMemberLayer(store, directory);
            PreserveLegacySchema2LayerFallback(store, directory);
            LoadValidModernDeclaredLayerGraph(store, directory);
            Console.WriteLine("PASS modern bootstrap layer references fail closed while legacy schema-2 fallback remains compatible");
        }
        finally
        {
            try { if (Directory.Exists(directory)) Directory.Delete(directory, true); } catch { }
        }
    }

    private static void RejectBlankModernEntityLayer(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "blank-modern-entity-layer.json");
        File.WriteAllText(path, ModernDrawing(
            "Blank entity layer",
            "{ \"Handle\": \"1\", \"Kind\": \"Point\", \"Min\": [0,0,0], \"Max\": [0,0,0], \"Properties\": {}, \"LayerName\": \"   \" }",
            "[]"));
        Throws<InvalidDataException>(() => store.LoadWithProject(path));
    }

    private static void RejectUndeclaredModernEntityLayer(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "undeclared-modern-entity-layer.json");
        File.WriteAllText(path, ModernDrawing(
            "Undeclared entity layer",
            "{ \"Handle\": \"1\", \"Kind\": \"Point\", \"Min\": [0,0,0], \"Max\": [0,0,0], \"Properties\": {}, \"LayerName\": \"A-MISSING\" }",
            "[]"));
        Throws<InvalidDataException>(() => store.LoadWithProject(path));
    }

    private static void RejectBlankModernBlockMemberLayer(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "blank-modern-block-layer.json");
        var blocks = """
        [{
          "Name": "BadBlock",
          "BasePoint": [0,0,0],
          "Entities": [
            { "Kind": "Point", "Min": [0,0,0], "Max": [0,0,0], "Properties": {}, "LayerName": "" }
          ]
        }]
        """;
        File.WriteAllText(path, ModernDrawing("Blank block layer", string.Empty, blocks));
        Throws<InvalidDataException>(() => store.LoadWithProject(path));
    }

    private static void RejectUndeclaredModernBlockMemberLayer(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "undeclared-modern-block-layer.json");
        var blocks = """
        [{
          "Name": "BadBlock",
          "BasePoint": [0,0,0],
          "Entities": [
            { "Kind": "Point", "Min": [0,0,0], "Max": [0,0,0], "Properties": {}, "LayerName": "A-MISSING" }
          ]
        }]
        """;
        File.WriteAllText(path, ModernDrawing("Undeclared block layer", string.Empty, blocks));
        Throws<InvalidDataException>(() => store.LoadWithProject(path));
    }

    private static void PreserveLegacySchema2LayerFallback(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "legacy-schema2-missing-layer.json");
        var drawingId = Guid.NewGuid();
        File.WriteAllText(path, $$"""
        {
          "Schema": 2,
          "DrawingId": "{{drawingId:D}}",
          "Name": "Legacy schema 2",
          "Entities": [
            { "Handle": "1", "Kind": "Point", "Min": [0,0,0], "Max": [0,0,0], "Properties": {} }
          ]
        }
        """);

        var loaded = store.LoadWithProject(path).Document;
        using var tx = loaded.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var entity = tx.Get(new CadHandle("1")) ?? throw new InvalidOperationException("Legacy schema-2 entity did not load.");
        if (!StringComparer.Ordinal.Equals(entity.LayerName, "0"))
            throw new InvalidOperationException($"Legacy schema-2 missing layer must fall back to 0, got '{entity.LayerName}'.");
    }

    private static void LoadValidModernDeclaredLayerGraph(BootstrapDrawingStore store, string directory)
    {
        var path = Path.Combine(directory, "valid-modern-layer-graph.json");
        var drawingId = Guid.NewGuid();
        File.WriteAllText(path, $$"""
        {
          "Schema": 4,
          "DrawingId": "{{drawingId:D}}",
          "Name": "Valid modern layers",
          "Entities": [
            { "Handle": "1", "Kind": "Point", "Min": [1,2,0], "Max": [1,2,0], "Properties": {}, "LayerName": "A-WALL" }
          ],
          "Layers": [
            { "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false },
            { "Name": "A-WALL", "IsOn": true, "IsFrozen": false, "IsLocked": false }
          ],
          "CurrentLayerName": "0",
          "Blocks": [{
            "Name": "WallPoint",
            "BasePoint": [0,0,0],
            "Entities": [
              { "Kind": "Point", "Min": [0,0,0], "Max": [0,0,0], "Properties": {}, "LayerName": "A-WALL" }
            ]
          }]
        }
        """);

        var loaded = store.LoadWithProject(path).Document;
        using var tx = loaded.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        if (!tx.GetLayers().Any(static layer => StringComparer.Ordinal.Equals(layer.Name, "A-WALL")))
            throw new InvalidOperationException("Valid declared layer did not load.");
        if (!StringComparer.Ordinal.Equals(tx.Get(new CadHandle("1"))?.LayerName, "A-WALL"))
            throw new InvalidOperationException("Valid entity layer ownership did not load.");
        var block = tx.GetBlock("WallPoint") ?? throw new InvalidOperationException("Valid block did not load.");
        if (!StringComparer.Ordinal.Equals(block.Entities.Single().LayerName, "A-WALL"))
            throw new InvalidOperationException("Valid block-member layer ownership did not load.");
    }

    private static string ModernDrawing(string name, string entity, string blocks)
    {
        var drawingId = Guid.NewGuid();
        var entities = string.IsNullOrWhiteSpace(entity) ? "[]" : "[" + entity + "]";
        return $$"""
        {
          "Schema": 4,
          "DrawingId": "{{drawingId:D}}",
          "Name": "{{name}}",
          "Entities": {{entities}},
          "Layers": [{ "Name": "0", "IsOn": true, "IsFrozen": false, "IsLocked": false }],
          "CurrentLayerName": "0",
          "Blocks": {{blocks}}
        }
        """;
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
