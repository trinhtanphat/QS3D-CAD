using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CadBackendQualificationEvidenceJsonModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        var evidence = new CadBackendQualificationEvidence(
            "native.test",
            "1.2.3",
            "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa",
            CadCapabilities.TwoDimensional | CadCapabilities.Layouts,
            new DateTimeOffset(2026, 8, 13, 12, 0, 0, TimeSpan.Zero),
            "evidence-001",
            true);

        var json = CadBackendQualificationEvidenceJson.Serialize(new[] { evidence });
        var restored = CadBackendQualificationEvidenceJson.Deserialize(json).Single();
        Equal(evidence.BackendId, restored.BackendId);
        Equal(evidence.BackendVersion, restored.BackendVersion);
        Equal(evidence.SourceSha, restored.SourceSha);
        Equal(evidence.QualifiedCapabilities, restored.QualifiedCapabilities);
        Equal(evidence.EvidenceId, restored.EvidenceId);
        Equal(evidence.Passed, restored.Passed);

        Throws<InvalidDataException>(() => CadBackendQualificationEvidenceJson.Deserialize("{}"));
        Throws<InvalidOperationException>(() => CadBackendQualificationEvidenceJson.Serialize(new[] { evidence, evidence }));
        Console.WriteLine("PASS qualification evidence JSON codec");
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
