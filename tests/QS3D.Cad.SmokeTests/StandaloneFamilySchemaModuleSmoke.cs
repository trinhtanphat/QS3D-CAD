using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;
using QS3D.Platform.Families;
using QS3D.Platform.Quantity;

namespace QS3D.Cad.SmokeTests;

internal static class StandaloneFamilySchemaModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("FamilySchema");
        Require(app.Execute("LINE 0 0 1 0"));
        Require(app.Execute("QSTAG 1 Wall WallA"));
        var project = app.Projects.Get(document);
        var family = project.Families.Single();
        var schema = new FamilySchemaDefinition("qs3d.wall", 1, SemanticElementKind.Wall, "Wall", new[]
        {
            new FamilyParameterDefinition("Thickness", FamilyParameterType.Quantity, true, quantityDimension: QuantityDimension.Length)
        });
        var values = new FamilyParameterSet("qs3d.wall", 1, new[]
        {
            new KeyValuePair<string, FamilyParameterValue>("Thickness", FamilyParameterValue.FromQuantity(new QuantityValue(QuantityDimension.Length, 0.2d)))
        });
        var binding = new ProjectFamilySchemaCatalog().Bind(project, family.Id, schema, values);
        if (Math.Abs(binding.Values.Values["Thickness"].Quantity.Value - 0.2d) > 1e-12d)
            throw new InvalidOperationException("Family Thickness mismatch.");
        Console.WriteLine("PASS standalone shared family schema binding");
    }

    private static void Require(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }
}
