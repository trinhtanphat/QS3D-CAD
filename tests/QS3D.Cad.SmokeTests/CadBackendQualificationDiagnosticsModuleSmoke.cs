using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.SmokeTests;

internal static class CadBackendQualificationDiagnosticsModuleSmoke
{
    private const string SourceA = "aaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaaa";
    private const string SourceB = "bbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbbb";
    private static readonly CadCapabilities Required = CadCapabilities.TwoDimensional | CadCapabilities.Layouts;
    private static readonly DateTimeOffset QualifiedAt = new(2026, 8, 15, 0, 0, 0, TimeSpan.Zero);

    [ModuleInitializer]
    internal static void Run()
    {
        var qualifiedHigh = Native("native.qualified-high", priority: 100);
        var qualifiedLow = Native("native.qualified-low", priority: 50);
        var unavailable = new CadBackendDescriptor(
            "native.unavailable", "Unavailable", CadBackendKind.Native, Required, false, "SDK not installed", 95, "1.0.0");
        var missingVersion = new CadBackendDescriptor(
            "native.missing-version", "Missing version", CadBackendKind.Native, Required, true, priority: 90);
        var backendCapabilityMismatch = new CadBackendDescriptor(
            "native.backend-capability", "Backend capability mismatch", CadBackendKind.Native, CadCapabilities.TwoDimensional, true, priority: 85, version: "1.0.0");
        var missingEvidence = Native("native.missing-evidence", priority: 80);
        var failedEvidence = Native("native.failed-evidence", priority: 75);
        var versionMismatch = Native("native.version-mismatch", priority: 70);
        var sourceMismatch = Native("native.source-mismatch", priority: 65);
        var evidenceCapabilityMismatch = Native("native.evidence-capability", priority: 60);
        var reference = new CadBackendDescriptor(
            "reference.only", "Reference only", CadBackendKind.Reference, Required, true, priority: 1000, version: "1.0.0");

        var candidates = new[]
        {
            reference,
            evidenceCapabilityMismatch,
            sourceMismatch,
            versionMismatch,
            failedEvidence,
            missingEvidence,
            backendCapabilityMismatch,
            missingVersion,
            unavailable,
            qualifiedLow,
            qualifiedHigh
        };

        var highOlder = Evidence(qualifiedHigh, "qualified-high-older", Required, SourceA, "1.0.0", true, QualifiedAt);
        var highNewest = Evidence(qualifiedHigh, "qualified-high-newest", Required, SourceA, "1.0.0", true, QualifiedAt.AddMinutes(5));
        var low = Evidence(qualifiedLow, "qualified-low", Required, SourceA, "1.0.0", true, QualifiedAt.AddMinutes(10));
        var failed = Evidence(failedEvidence, "failed", CadCapabilities.None, SourceA, "1.0.0", false, QualifiedAt);
        var wrongVersion = Evidence(versionMismatch, "wrong-version", Required, SourceA, "2.0.0", true, QualifiedAt);
        var wrongSource = Evidence(sourceMismatch, "wrong-source", Required, SourceB, "1.0.0", true, QualifiedAt);
        var insufficientEvidence = Evidence(evidenceCapabilityMismatch, "insufficient-caps", CadCapabilities.TwoDimensional, SourceA, "1.0.0", true, QualifiedAt);

        var evidence = new[]
        {
            insufficientEvidence,
            wrongSource,
            wrongVersion,
            failed,
            low,
            highNewest,
            highOlder
        };

        var report = CadQualifiedBackendSelector.EvaluateProduction(candidates, evidence, Required, SourceA.ToUpperInvariant());
        True(report.IsQualified, "report qualification state");
        Equal(SourceA, report.SourceSha, "normalized source SHA");
        Equal(Required, report.RequiredCapabilities, "required capabilities");
        Equal(qualifiedHigh.Id, report.Selection!.Backend.Id, "selected backend");
        Equal(highNewest.EvidenceId, report.Selection.Evidence.EvidenceId, "selected newest evidence");

        var production = CadQualifiedBackendSelector.SelectProduction(candidates, evidence, Required, SourceA);
        Equal(production.Backend.Id, report.Selection.Backend.Id, "selection parity backend");
        Equal(production.Evidence.EvidenceId, report.Selection.Evidence.EvidenceId, "selection parity evidence");

        Code(report, qualifiedHigh.Id, CadBackendQualificationDiagnosticCode.Qualified);
        Code(report, qualifiedLow.Id, CadBackendQualificationDiagnosticCode.Qualified);
        Code(report, unavailable.Id, CadBackendQualificationDiagnosticCode.Unavailable);
        Code(report, missingVersion.Id, CadBackendQualificationDiagnosticCode.MissingVersion);
        Code(report, backendCapabilityMismatch.Id, CadBackendQualificationDiagnosticCode.BackendCapabilityMismatch);
        Code(report, missingEvidence.Id, CadBackendQualificationDiagnosticCode.MissingEvidence);
        Code(report, failedEvidence.Id, CadBackendQualificationDiagnosticCode.FailedEvidence);
        Code(report, versionMismatch.Id, CadBackendQualificationDiagnosticCode.VersionMismatch);
        Code(report, sourceMismatch.Id, CadBackendQualificationDiagnosticCode.SourceMismatch);
        Code(report, evidenceCapabilityMismatch.Id, CadBackendQualificationDiagnosticCode.EvidenceCapabilityMismatch);
        Code(report, reference.Id, CadBackendQualificationDiagnosticCode.NonNative);

        var highDiagnostic = report.Candidates.Single(item => item.Backend.Id == qualifiedHigh.Id);
        Equal(highNewest.EvidenceId, highDiagnostic.EvidenceId!, "qualified diagnostic evidence");
        Contains(highDiagnostic.Detail, highNewest.EvidenceId, "qualified diagnostic detail");
        Contains(report.Candidates.Single(item => item.Backend.Id == unavailable.Id).Detail, "SDK not installed", "unavailable detail");
        Contains(report.Candidates.Single(item => item.Backend.Id == versionMismatch.Id).Detail, "2.0.0", "version mismatch detail");
        Contains(report.Candidates.Single(item => item.Backend.Id == sourceMismatch.Id).Detail, SourceB, "source mismatch detail");

        var reversed = CadQualifiedBackendSelector.EvaluateProduction(
            candidates.Reverse(),
            evidence.Reverse(),
            Required,
            SourceA);
        SequenceEqual(
            report.Candidates.Select(static item => item.Backend.Id),
            reversed.Candidates.Select(static item => item.Backend.Id),
            "candidate diagnostic ordering");
        SequenceEqual(
            report.Candidates.Select(static item => item.Code),
            reversed.Candidates.Select(static item => item.Code),
            "candidate diagnostic codes");
        Equal(report.Selection.Backend.Id, reversed.Selection!.Backend.Id, "reversed selected backend");
        Equal(report.Selection.Evidence.EvidenceId, reversed.Selection.Evidence.EvidenceId, "reversed selected evidence");

        var noQualified = CadQualifiedBackendSelector.EvaluateProduction(
            new[] { missingEvidence, unavailable, reference },
            Array.Empty<CadBackendQualificationEvidence>(),
            Required,
            SourceA);
        False(noQualified.IsQualified, "unqualified report state");
        if (noQualified.Selection is not null)
            throw new InvalidOperationException("Unqualified diagnostic report must not manufacture a selected backend.");

        Throws<ArgumentOutOfRangeException>(() => CadQualifiedBackendSelector.EvaluateProduction(candidates, evidence, CadCapabilities.None, SourceA));
        Throws<ArgumentException>(() => CadQualifiedBackendSelector.EvaluateProduction(candidates, evidence, Required, "not-a-sha"));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.EvaluateProduction(
            new[] { qualifiedHigh, Native(qualifiedHigh.Id, priority: 1) }, evidence, Required, SourceA));
        Throws<InvalidOperationException>(() => CadQualifiedBackendSelector.EvaluateProduction(
            candidates,
            evidence.Concat(new[] { Evidence(qualifiedLow, highNewest.EvidenceId, Required, SourceA, "1.0.0", true, QualifiedAt) }),
            Required,
            SourceA));

        Console.WriteLine("PASS structured deterministic production backend qualification diagnostics without weakening selection policy");
    }

    private static CadBackendDescriptor Native(string id, int priority)
        => new(id, id, CadBackendKind.Native, Required | CadCapabilities.ThreeDimensional, true, priority: priority, version: "1.0.0");

    private static CadBackendQualificationEvidence Evidence(
        CadBackendDescriptor backend,
        string evidenceId,
        CadCapabilities capabilities,
        string sourceSha,
        string version,
        bool passed,
        DateTimeOffset qualifiedAt)
        => new(backend.Id, version, sourceSha, capabilities, qualifiedAt, evidenceId, passed);

    private static void Code(CadBackendQualificationReport report, string backendId, CadBackendQualificationDiagnosticCode expected)
    {
        var diagnostic = report.Candidates.Single(item => item.Backend.Id == backendId);
        Equal(expected, diagnostic.Code, $"diagnostic code for {backendId}");
    }

    private static void Equal<T>(T expected, T actual, string operation) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{operation}: expected {expected} but got {actual}.");
    }

    private static void True(bool value, string operation)
    {
        if (!value) throw new InvalidOperationException($"{operation}: expected true.");
    }

    private static void False(bool value, string operation)
    {
        if (value) throw new InvalidOperationException($"{operation}: expected false.");
    }

    private static void Contains(string actual, string expected, string operation)
    {
        if (!actual.Contains(expected, StringComparison.Ordinal))
            throw new InvalidOperationException($"{operation}: missing '{expected}' in '{actual}'.");
    }

    private static void SequenceEqual<T>(IEnumerable<T> expected, IEnumerable<T> actual, string operation)
    {
        if (!expected.SequenceEqual(actual))
            throw new InvalidOperationException($"{operation}: deterministic sequence mismatch.");
    }

    private static void Throws<T>(Action action) where T : Exception
    {
        try { action(); }
        catch (T) { return; }
        throw new InvalidOperationException($"Expected {typeof(T).Name}.");
    }
}
