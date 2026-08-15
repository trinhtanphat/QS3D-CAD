using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.SmokeTests;

internal static class MeasurementCommandsModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var app = new StandaloneCadApplication();
        var document = app.NewDocument("measurement-smoke");

        Succeeds(app.Execute("LINE 0 0 3 4"));
        Succeeds(app.Execute("CIRCLE 10 10 2"));
        Succeeds(app.Execute("RECTANG 20 20 25 24"));
        var entities = Query(document);
        var line = entities.Single(static entity => entity.Kind == CadEntityKind.Line);
        var circle = entities.Single(static entity => entity.Kind == CadEntityKind.Circle);
        var rectangle = entities.Single(static entity => entity.Kind == CadEntityKind.Polyline);
        var revision = document.Database.Revision;

        var distance = app.Execute("DIST 0 0 3 4");
        Succeeds(distance);
        Contains(distance.Message, "Distance=5");
        Contains(distance.Message, "dx=3");
        Contains(distance.Message, "dy=4");
        if (document.Database.Revision != revision)
            throw new InvalidOperationException("DIST must not create a drawing revision.");

        var measured = app.Execute($"MEASURE {line.Handle} {circle.Handle} {rectangle.Handle}");
        Succeeds(measured);
        if (document.Database.Revision != revision)
            throw new InvalidOperationException("MEASURE must not create a drawing revision.");

        var editor = document.Editor as InMemoryEditor
            ?? throw new InvalidOperationException("Expected in-memory editor for measurement smoke.");
        var metricLines = editor.Messages.Where(static message => message.StartsWith("MEASURE ", StringComparison.Ordinal)).ToArray();
        if (!metricLines.Any(message => message.Contains($"{line.Handle} Line length=5", StringComparison.Ordinal)))
            throw new InvalidOperationException("Line measurement output is missing the expected 3-4-5 length.");
        if (!metricLines.Any(message => message.Contains($"{circle.Handle} Circle radius=2", StringComparison.Ordinal) && message.Contains("area=12.566", StringComparison.Ordinal)))
            throw new InvalidOperationException("Circle measurement output is missing radius/area evidence.");
        if (!metricLines.Any(message => message.Contains($"{rectangle.Handle} Polyline(reference-rectangle)", StringComparison.Ordinal) && message.Contains("area=20", StringComparison.Ordinal)))
            throw new InvalidOperationException("Reference rectangle measurement output is missing area evidence.");

        var metricCountBeforeFailure = metricLines.Length;
        Fails(app.Execute($"MEASURE {line.Handle} FFFF"));
        var metricCountAfterFailure = editor.Messages.Count(static message => message.StartsWith("MEASURE ", StringComparison.Ordinal));
        if (metricCountAfterFailure != metricCountBeforeFailure)
            throw new InvalidOperationException("Failed MEASURE must not emit partial per-entity metric output.");
        if (document.Database.Revision != revision)
            throw new InvalidOperationException("Failed MEASURE must remain read-only.");

        Fails(app.Execute("DIST -1E308 0 1E308 0"));
        if (document.Database.Revision != revision)
            throw new InvalidOperationException("Overflowing DIST must remain read-only.");

        if (!app.Commands.Contains("DIST") || !app.Commands.Contains("MEASURE"))
            throw new InvalidOperationException("Measurement commands are not registered.");
    }

    private static IReadOnlyList<CadEntitySnapshot> Query(ICadDocument document)
    {
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Query().ToArray();
    }

    private static void Contains(string? actual, string expected)
    {
        if (actual is null || !actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"Expected '{expected}' in '{actual ?? "<null>"}'.");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded)
            throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }

    private static void Fails(QS3D.Platform.Application.CommandResult result)
    {
        if (result.Succeeded)
            throw new InvalidOperationException("Expected command failure.");
    }
}
