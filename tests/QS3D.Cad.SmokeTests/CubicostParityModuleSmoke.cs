using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.SmokeTests;

internal static class CubicostParityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("CubicostParity");

        Success(app.Execute("LAYER NEW MEP_DUCT"));
        Success(app.Execute("LAYER SET MEP_DUCT"));
        Success(app.Execute("LINE 0 0 3 4"));
        Success(app.Execute("LAYER NEW STRUCT_BEAM"));
        Success(app.Execute("LAYER SET STRUCT_BEAM"));
        Success(app.Execute("LINE 2 -1 2 1"));
        Success(app.Execute("SELECT 1 2"));

        var drawingRevision = document.Database.Revision;
        var semanticRevision = app.Projects.Revision(document);

        var recognition = app.Execute("QSMEPRECOGNIZE");
        Success(recognition);
        Contains(recognition.Message, "matched=2");

        var takeoff = app.Execute("QSMEPTAKEOFF 1");
        Success(takeoff);
        Contains(takeoff.Message, "recognized=1");
        Contains(takeoff.Message, "groups=1");

        var unitScaled = app.Execute("QSMEPTAKEOFF 0.001");
        Success(unitScaled);
        var badUnit = app.Execute("QSMEPTAKEOFF 0");
        Require(!badUnit.Succeeded, "zero metersPerUnit must fail closed");

        var clash = app.Execute("QSMEPCLASH 0 1");
        Success(clash);
        Contains(clash.Message, "clashes=1");

        Success(app.Execute("SELNONE"));
        Success(app.Execute("SELECT 1 2"));
        var locate = app.Execute("QSMEPCLASHLOCATE 1 0 1");
        Success(locate);
        Require(new HashSet<CadHandle>(document.Editor.Selection.Current).SetEquals(new[] { new CadHandle("1"), new CadHandle("2") }),
            "clash locate must replace selection with exactly the live pair");

        var issues = app.Execute("QSMEPISSUES 0 1");
        Success(issues);
        Contains(issues.Message, "1 in-memory coordination issue");
        Contains(issues.Message, "no project/cloud persistence");

        Equal(drawingRevision, document.Database.Revision);
        Equal(semanticRevision, app.Projects.Revision(document));
        Console.WriteLine("PASS Cubicost standalone MEP recognition/takeoff/clash/locate/issues parity");
    }

    private static void Success(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Contains(string? value, string expected)
    {
        if (value is null || !value.Contains(expected, StringComparison.OrdinalIgnoreCase))
            throw new InvalidOperationException($"Expected message to contain '{expected}', actual '{value ?? "<null>"}'.");
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
