using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class ReferencePrimitivePackageModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var path = Path.Combine(Path.GetTempPath(), $"qs3d-reference-primitives-{Guid.NewGuid():N}.qs3d");
        var backup = path + ".bak";
        try
        {
            var source = new StandaloneCadApplication();
            source.NewDocument("reference-primitive-package");
            Succeeds(source.Execute("ARC 10 20 8 15 120"));
            Succeeds(source.Execute("POINT -3 7"));
            Succeeds(source.Execute("POLYGON 6 40 50 12 30"));
            source.SaveProjectPackageWithBackup(path);

            var reopened = new StandaloneCadApplication();
            var result = reopened.OpenProjectPackageWithRecovery(path);
            if (result.RecoveredFromBackup)
                throw new InvalidOperationException("Fresh reference primitive package unexpectedly required backup recovery.");
            using var tx = result.Document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            var entities = tx.Query();
            if (!entities.Any(entity => ReferencePrimitiveGeometry.TryGetArc(entity, out var arc) && Math.Abs(arc.Radius - 8d) < 1e-9)
                || !entities.Any(entity => ReferencePrimitiveGeometry.TryGetPoint(entity, out var point) && Math.Abs(point.X + 3d) < 1e-9 && Math.Abs(point.Y - 7d) < 1e-9)
                || !entities.Any(entity => ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out var polygon) && polygon.Sides == 6 && Math.Abs(polygon.Radius - 12d) < 1e-9))
                throw new InvalidOperationException("QS3D package round-trip lost reference primitive geometry.");
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
            if (File.Exists(backup)) File.Delete(backup);
        }
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Expected command success.");
    }
}
