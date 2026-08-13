using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class DocumentReferenceSurfaceModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("ReferenceSurface");
        var db = document.Database.Revision;
        var semantic = app.Projects.Revision(document);
        Require(app.Execute("XREFREF ATTACH Missing missing.dwg Overlay").Succeeded);
        Require(app.Execute("XREFREF DETACH Missing").Succeeded);
        Require(app.Execute("LAYOUTREF CREATE Sheet01").Succeeded);
        Require(app.Execute("LAYOUTREF SET Sheet01").Succeeded);
        Require(!app.Execute("LAYOUTREF DELETE Sheet01").Succeeded);
        Require(app.Execute("LAYOUTREF SET Model").Succeeded);
        Require(app.Execute("LAYOUTREF DELETE Sheet01").Succeeded);
        Require(!app.Execute("PLOTREF Missing missing.pdf").Succeeded);
        var plot = app.Execute("PLOTREF Model model.pdf");
        Require(plot.Succeeded);
        Require(plot.Message is not null && plot.Message.Contains("no native file was produced", StringComparison.Ordinal));
        Require(InMemoryAdvancedServicesRegistry.For((InMemoryCadDocument)document).Plot.Requests.Count == 1);
        Require(document.Database.Revision == db);
        Require(app.Projects.Revision(document) == semantic);
        Console.WriteLine("PASS document reference surface");
    }

    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Document reference surface regression failed.");
    }
}
