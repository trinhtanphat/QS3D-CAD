using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Parity;
using QS3D.Platform.Persistence;
using QS3D.Platform.Quantity;

namespace QS3D.Cad.SmokeTests;

internal static class StandaloneParityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("StandaloneParity");
        Success(app.Execute("LINE 0 0 1 0"));
        Success(app.Execute("QSTAG 1 Wall WallA"));
        Success(app.Execute("QSPROP 1 LengthMm 2500"));
        Success(app.Execute("QSPROP 1 HeightMm 3000"));

        var project = app.Projects.Get(document);
        var element = project.Elements.Single();
        var fixture = new GoldenParityFixture(
            "standalone-wall-area",
            SemanticSnapshotService.Capture(project),
            new[]
            {
                new QuantityRuleDefinition(element.Kind, "WALL.AREA", QuantityDimension.Area, new[]
                {
                    new QuantityFactor("LengthMm", QuantityUnit.Millimeter),
                    new QuantityFactor("HeightMm", QuantityUnit.Millimeter)
                })
            },
            expectedQuantities: new[]
            {
                new GoldenQuantityExpectation(element.Id.Value, "WALL.AREA", QuantityDimension.Area, 7.5d)
            });
        var result = GoldenParityRunner.Run(fixture);
        if (!result.Passed) throw new InvalidOperationException(string.Join("; ", result.Failures));
        Console.WriteLine("PASS standalone state through shared golden parity runner");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }
}
