using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class LayerBlockPersistenceModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("LayerBlock");

        Success(app.Execute("LAYER NEW A-WALL"));
        Success(app.Execute("LAYER SET A-WALL"));
        Success(app.Execute("LINE 0 0 10 0"));
        Success(app.Execute("BLOCK WallSegment 1"));
        Success(app.Execute("LAYER SET 0"));
        Success(app.Execute("INSERT WallSegment 20 5 2 0"));

        using (var read = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
        {
            Require(read.CurrentLayerName == "0", "current layer must be 0 after explicit switch");
            var source = read.Get(new CadHandle("1")) ?? throw new InvalidOperationException("source entity missing");
            Require(source.LayerName == "A-WALL", "source entity layer ownership mismatch");
            Require(read.GetBlocks().Count == 1, "block definition missing");
            var inserted = read.Get(new CadHandle("2")) ?? throw new InvalidOperationException("inserted block reference missing");
            Require(inserted.Kind == CadEntityKind.BlockReference, "INSERT must create a block reference");
        }

        Success(app.Execute("UNDO"));
        using (var read = document.Database.BeginTransaction(CadTransactionMode.ReadOnly))
        {
            Require(read.Get(new CadHandle("2")) is null, "UNDO INSERT must remove block reference");
            Require(read.GetBlock("wallsegment") is not null, "UNDO INSERT must preserve definition");
        }
        Success(app.Execute("REDO"));

        Success(app.Execute("QSTAG 1 Wall \"Layer Wall\""));
        var projectId = app.Projects.Get(document).Id;
        var path = Path.Combine(Path.GetTempPath(), $"qs3d-layer-block-{Guid.NewGuid():N}.json");
        try
        {
            app.SaveBootstrap(path);
            var second = new StandaloneCadApplication();
            var loaded = second.OpenBootstrap(path);
            using (var read = loaded.Database.BeginTransaction(CadTransactionMode.ReadOnly))
            {
                Require(read.GetLayers().Any(static layer => layer.Name == "A-WALL"), "layer must survive schema-v4 round trip");
                Require(read.Get(new CadHandle("1"))?.LayerName == "A-WALL", "entity layer must survive round trip");
                Require(read.GetBlock("WallSegment") is not null, "block definition must survive round trip");
                Require(read.Get(new CadHandle("2"))?.Kind == CadEntityKind.BlockReference, "block reference must survive round trip");
            }
            Require(second.Projects.Get(loaded).Id == projectId, "semantic project must survive schema-v4 round trip");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        Console.WriteLine("PASS standalone layer/block schema-v4 foundation");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
