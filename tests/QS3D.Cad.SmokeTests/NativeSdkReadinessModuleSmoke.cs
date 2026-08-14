using System.Runtime.CompilerServices;
using QS3D.Cad.Native.Oda.Bootstrap;

namespace QS3D.Cad.SmokeTests;

internal static class NativeSdkReadinessModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var notConfigured = NativeSdkReadinessProbe.Inspect(null);
        Equal(NativeSdkReadinessState.NotConfigured, notConfigured.State);
        Require(!notConfigured.IsConfigured, "Missing SDK path was reported as configured.");
        Require(!notConfigured.IsProductionQualified, "Missing SDK path was reported as production-qualified.");

        var missingPath = Path.Combine(Path.GetTempPath(), "qs3d-native-sdk-missing-" + Guid.NewGuid().ToString("N"));
        var missing = NativeSdkReadinessProbe.Inspect(missingPath);
        Equal(NativeSdkReadinessState.DirectoryMissing, missing.State);
        Require(!missing.IsConfigured, "Missing SDK directory was reported as configured.");
        Require(!missing.IsProductionQualified, "Missing SDK directory was reported as production-qualified.");

        var configuredPath = Path.Combine(Path.GetTempPath(), "qs3d-native-sdk-probe-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(configuredPath);
        try
        {
            var configured = NativeSdkReadinessProbe.Inspect(configuredPath);
            Equal(NativeSdkReadinessState.ConfiguredUnqualified, configured.State);
            Require(configured.IsConfigured, "Existing SDK directory was not reported as configured.");
            Require(!configured.IsProductionQualified, "Existing SDK directory was incorrectly promoted to production qualification.");
            Equal(Path.GetFullPath(configuredPath), configured.SdkRoot!);
        }
        finally
        {
            Directory.Delete(configuredPath, recursive: true);
        }

        Console.WriteLine("PASS native SDK discovery remains unqualified without runtime evidence");
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
