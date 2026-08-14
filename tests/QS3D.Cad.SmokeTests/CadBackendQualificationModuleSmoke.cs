using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CadBackendQualificationModuleSmoke
{
    private const string SourceA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";

    [ModuleInitializer]
    internal static void Run()
    {
        var native = new CadBackendDescriptor(
            "native.test",
            "Native test backend",
            CadBackendKind.Native,
            CadCapabilities.TwoDimensional | CadCapabilities.ThreeDimensional | CadCapabilities.Layouts | CadCapabilities.NativeSolids,
            isAvailable: true,
            priority: 10,
            version: "1.0.0");
        var evidence = new CadBackendQualificationEvidence(
            native.Id,
            "1.0.0",
            SourceA,
            CadCapabilities.TwoDimensional | CadCapabilities.Layouts,
            DateTimeOffset.UtcNow,
            "local-qualification-001",
            passed: true);

        var selected = CadQualifiedBackendSelector.SelectProduction(
            new[] { native },
            new[] { evidence },
            CadCapabilities.TwoDimensional | CadCapabilities.Layouts,
            SourceA);
        Equal(native.Id, selected.Backend.Id);
        Equal(native.Version!, selected.Evidence.BackendVersion);
        Equal(evidence.EvidenceId, selected.Evidence.EvidenceId);

        Throws<ArgumentOutOfRangeException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native }, new[] { evidence }, CadCapabilities.None, SourceA));
        Throws<ArgumentOutOfRangeException>(() => new CadBackendQualificationEvidence(
            native.Id, native.Version!, SourceA, CadCapabilities.None, DateTimeOffset.UtcNow, "empty-pass", true));
        Throws<ArgumentOutOfRangeException>(() => new CadBackendQualificationEvidence(
            native.Id, native.Version!, SourceA, (CadCapabilities)(1 << 20), DateTimeOffset.UtcNow, "unknown-pass", true));

        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native }, new[] { evidence }, CadCapabilities.TwoDimensional, SourceB));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native },
            new[] { new CadBackendQualificationEvidence(native.Id, "2.0.0", SourceA, CadCapabilities.TwoDimensional, DateTimeOffset.UtcNow, "wrong-version", true) },
            CadCapabilities.TwoDimensional,
            SourceA));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[]
            {
                new CadBackendDescriptor(native.Id, native.DisplayName, native.Kind, native.Capabilities, true, priority: 10)
            },
            new[] { evidence },
            CadCapabilities.TwoDimensional,
            SourceA));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native },
            new[] { new CadBackendQualificationEvidence(native.Id, "1.0.0", SourceA, CadCapabilities.TwoDimensional, DateTimeOffset.UtcNow, "failed", false) },
            CadCapabilities.TwoDimensional,
            SourceA));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native }, new[] { evidence }, CadCapabilities.NativeSolids, SourceA));

        var duplicateBackend = new CadBackendDescriptor(
            native.Id,
            "Duplicate native test backend",
            CadBackendKind.Native,
            native.Capabilities,
            isAvailable: true,
            priority: 20,
            version: native.Version);
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native, duplicateBackend },
            new[] { evidence },
            CadCapabilities.TwoDimensional,
            SourceA));

        var duplicateEvidence = new CadBackendQualificationEvidence(
            native.Id,
            native.Version!,
            SourceA,
            CadCapabilities.TwoDimensional,
            evidence.QualifiedAt.AddMinutes(1),
            evidence.EvidenceId,
            passed: true);
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.SelectProduction(
            new[] { native },
            new[] { evidence, duplicateEvidence },
            CadCapabilities.TwoDimensional,
            SourceA));

        Console.WriteLine("PASS exact-SHA, exact-version, explicit-capability and unambiguous native backend qualification policy");
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
