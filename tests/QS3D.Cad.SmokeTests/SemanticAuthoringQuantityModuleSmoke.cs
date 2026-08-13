using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class SemanticAuthoringQuantityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = (InMemoryCadDocument)app.NewDocument("Semantic Authoring");
        Success(app.Execute("LINE 0 0 10 0"));
        Success(app.Execute("QSTAG 1 Wall \"Wall A\""));
        Success(app.Execute("QSFLOOR \"Level 1\" 0"));
        Success(app.Execute("QSZONE \"North Zone\""));

        var project = app.Projects.Get(document);
        var floor = project.Floors.Single();
        var zone = project.Zones.Single();
        Success(app.Execute($"QSLOC 1 {floor.Id.Value:D} {zone.Id.Value:D}"));
        Success(app.Execute("QSPROP 1 LengthMm 2500"));
        Success(app.Execute("QSPROP 1 HeightMm 3000"));

        var element = app.Projects.Get(document).Elements.Single();
        Equal("3000", element.Properties["HeightMm"]);
        Success(app.Execute("UNDO"));
        element = app.Projects.Get(document).Elements.Single();
        Require(!element.Properties.ContainsKey("HeightMm"), "UNDO must remove the last semantic property mutation");
        Success(app.Execute("REDO"));
        element = app.Projects.Get(document).Elements.Single();
        Equal("3000", element.Properties["HeightMm"]);

        var quantity = app.Execute("QSQTY 1 WALL.AREA Area LengthMm:Millimeter HeightMm:Millimeter");
        Success(quantity);
        Require(quantity.Message?.Contains("7.5 m2", StringComparison.Ordinal) == true, "QSQTY must evaluate 2500mm x 3000mm as 7.5 m2");

        var schedule = app.Execute("QSSCHEDULE Wall WALL.AREA Area LengthMm:Millimeter HeightMm:Millimeter");
        Success(schedule);
        Require(schedule.Message?.Contains("1 row(s)", StringComparison.Ordinal) == true, "QSSCHEDULE must contain one wall row");
        Require(((InMemoryEditor)document.Editor).Messages.Any(static message => message.Contains("WALL.AREA=7.5 m2", StringComparison.Ordinal)),
            "QSSCHEDULE output must include the evaluated wall area");

        var path = Path.Combine(Path.GetTempPath(), $"qs3d-semantic-authoring-{Guid.NewGuid():N}.json");
        try
        {
            app.SaveBootstrap(path);
            var reopened = new StandaloneCadApplication();
            var reopenedDocument = reopened.OpenBootstrap(path);
            var reopenedProject = reopened.Projects.Get(reopenedDocument);
            Equal(1, reopenedProject.Floors.Count);
            Equal(1, reopenedProject.Zones.Count);
            var reopenedElement = reopenedProject.Elements.Single();
            Equal(floor.Id, reopenedElement.FloorId!.Value);
            Equal(zone.Id, reopenedElement.ZoneId!.Value);
            Equal("2500", reopenedElement.Properties["LengthMm"]);
            Equal("3000", reopenedElement.Properties["HeightMm"]);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }

        Console.WriteLine("PASS standalone semantic authoring and quantity rules");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected} but got {actual}.");
    }
}
