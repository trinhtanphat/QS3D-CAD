using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CadBackendPolicyModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var reference = new CadBackendDescriptor(
            "reference.inmemory",
            "In-memory reference backend",
            CadBackendKind.Reference,
            CadCapabilities.TwoDimensional | CadCapabilities.Blocks | CadCapabilities.Layouts,
            isAvailable: true,
            priority: 100);
        var unavailableNative = new CadBackendDescriptor(
            "native.placeholder",
            "Native backend placeholder",
            CadBackendKind.Native,
            CadCapabilities.TwoDimensional | CadCapabilities.ThreeDimensional | CadCapabilities.Blocks | CadCapabilities.Layouts | CadCapabilities.NativeSolids,
            isAvailable: false,
            unavailableReason: "Licensed native SDK is not configured.",
            priority: 1000);

        var development = CadBackendSelector.Select(
            new[] { reference, unavailableNative },
            CadBackendSelectionPolicy.Development(CadCapabilities.TwoDimensional | CadCapabilities.Blocks));
        Equal(reference.Id, development.Id);

        Throws<InvalidOperationException>(() => CadBackendSelector.Select(
            new[] { reference, unavailableNative },
            CadBackendSelectionPolicy.Production(CadCapabilities.TwoDimensional)));

        var native = new CadBackendDescriptor(
            "native.test",
            "Qualified native test backend",
            CadBackendKind.Native,
            CadCapabilities.TwoDimensional | CadCapabilities.Blocks | CadCapabilities.Layouts,
            isAvailable: true,
            priority: 1);
        var production = CadBackendSelector.Select(
            new[] { reference, native },
            CadBackendSelectionPolicy.Production(CadCapabilities.TwoDimensional | CadCapabilities.Layouts));
        Equal(native.Id, production.Id);

        Throws<InvalidOperationException>(() => CadBackendSelector.Select(
            new[]
            {
                reference,
                new CadBackendDescriptor("REFERENCE.INMEMORY", "Duplicate reference", CadBackendKind.Reference, CadCapabilities.TwoDimensional, true)
            },
            CadBackendSelectionPolicy.Development()));

        Console.WriteLine("PASS fail-closed CAD backend selection policy");
    }

    private static void Equal<T>(T expected, T actual) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"Expected {expected} but got {actual}.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
