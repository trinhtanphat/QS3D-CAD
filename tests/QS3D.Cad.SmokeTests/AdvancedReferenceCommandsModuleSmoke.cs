using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class AdvancedReferenceCommandsModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("AdvancedReference");
        Success(app.Execute("LINE 0 0 10 0"));
        var databaseRevision = document.Database.Revision;
        var semanticRevision = app.Projects.Revision(document);

        Success(app.Execute("VIEW"));
        Success(app.Execute("ZOOMEXTENTS"));
        Success(app.Execute("ZOOMWINDOW -1 -1 11 1"));
        Success(app.Execute("HITTEST 5 0 100"));

        var endpoint = app.Execute("SNAP 0 0 100 Endpoint");
        Success(endpoint);
        Require(endpoint.Message is not null && !endpoint.Message.Contains("0 candidate", StringComparison.Ordinal),
            "endpoint snap must return at least one reference candidate");

        var unsupported = app.Execute("SNAP 5 0 100 Intersection,Tangent");
        Success(unsupported);
        Require(unsupported.Message is not null && unsupported.Message.Contains("0 candidate", StringComparison.Ordinal),
            "reference snap must not manufacture intersection or tangent candidates");

        Success(app.Execute("SELPOLY Window -1 -1 11 -1 11 1 -1 1"));
        Require(new HashSet<CadHandle>(document.Editor.Selection.Current).SetEquals(new[] { new CadHandle("1") }),
            "polygon selection must select the contained line");

        Equal(databaseRevision, document.Database.Revision);
        Equal(semanticRevision, app.Projects.Revision(document));
        Console.WriteLine("PASS advanced reference command registration and read-only behavior");
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
