using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class StandaloneDrawingAffinityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = (InMemoryCadDocument)app.NewDocument("Drawing affinity");
        var originalProjectId = app.Projects.Get(document).Id;

        var foreignProject = new SemanticProject(ProjectId.New(), "Foreign semantic project");
        var family = new Family(FamilyId.New(), SemanticElementKind.Wall, "Wall");
        foreignProject.AddFamily(family);
        var element = new SemanticElement(ElementId.New(), SemanticElementKind.Wall, "Foreign wall", family.Id);
        element.SetSource(new CadReference(DrawingId.New(), new CadHandle("1")));
        foreignProject.AddElement(element);

        Throws<InvalidOperationException>(() => app.Projects.Attach(document, foreignProject));
        Require(app.Projects.Get(document).Id == originalProjectId,
            "rejected foreign project attachment must preserve the existing document semantic state");

        var report = StandaloneModelReadinessAnalyzer.Analyze(document, foreignProject);
        Require(!report.IsReady, "foreign-drawing CAD references must block standalone readiness");
        Require(report.Findings.Any(static finding => finding.Code == "CAD_REFERENCE_DRAWING_MISMATCH"),
            "readiness must identify foreign-drawing CAD references explicitly");

        Console.WriteLine("PASS standalone drawing-affinity rejection and readiness diagnostics");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
