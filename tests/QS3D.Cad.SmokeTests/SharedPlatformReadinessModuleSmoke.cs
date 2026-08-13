using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class SharedPlatformReadinessModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = (InMemoryCadDocument)app.NewDocument("Readiness");
        var family = new Family(FamilyId.New(), SemanticElementKind.Wall, "Wall");
        var project = new SemanticProject(ProjectId.New(), "Ownership Collision");
        project.AddFamily(family);

        var first = new SemanticElement(ElementId.New(), SemanticElementKind.Wall, "W1", family.Id);
        first.SetSource(new CadReference(document.Id, new CadHandle("A")));
        project.AddElement(first);
        var second = new SemanticElement(ElementId.New(), SemanticElementKind.Wall, "W2", family.Id);
        second.SetSource(new CadReference(document.Id, new CadHandle("000a")));
        project.AddElement(second);

        app.Projects.Attach(document, project);
        var health = app.Execute("QSHEALTH");
        Require(!health.Succeeded, "QSHEALTH must fail when canonical source ownership is ambiguous");
        var messages = ((InMemoryEditor)document.Editor).Messages;
        Require(messages.Any(static message => message.Contains("SEM_CAD_REFERENCE_OWNERSHIP_CONFLICT", StringComparison.Ordinal)),
            "QSHEALTH output must surface the canonical ownership conflict");

        var clone = app.Projects.Get(document);
        Require(clone.Elements.Count == 2, "shared persistence clone must preserve attached semantic elements");
        Require(clone.Elements.Select(static element => element.SourceReference!.Value.Handle.Value).All(static handle => handle == "A"),
            "shared persistence clone must preserve canonical CAD handles");

        Console.WriteLine("PASS standalone shared persistence/readiness integration");
    }

    private static void Require(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
