using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public enum CadBackendQualificationDiagnosticCode
{
    Qualified = 0,
    Unavailable = 1,
    NonNative = 2,
    MissingVersion = 3,
    BackendCapabilityMismatch = 4,
    MissingEvidence = 5,
    FailedEvidence = 6,
    VersionMismatch = 7,
    SourceMismatch = 8,
    EvidenceCapabilityMismatch = 9
}

public sealed record CadBackendQualificationCandidateDiagnostic(
    CadBackendDescriptor Backend,
    CadBackendQualificationDiagnosticCode Code,
    string Detail,
    string? EvidenceId = null);

public sealed record CadBackendQualificationReport(
    string SourceSha,
    CadCapabilities RequiredCapabilities,
    IReadOnlyList<CadBackendQualificationCandidateDiagnostic> Candidates,
    CadQualifiedBackendSelection? Selection)
{
    public bool IsQualified => Selection is not null;
}

public static class CadQualifiedBackendSelector
{
    public static CadQualifiedBackendSelection SelectProduction(
        IEnumerable<CadBackendDescriptor> candidates,
        IEnumerable<CadBackendQualificationEvidence> evidence,
        CadCapabilities requiredCapabilities,
        string sourceSha)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (evidence is null) throw new ArgumentNullException(nameof(evidence));
        CadCapabilityValidation.RequireKnown(requiredCapabilities, nameof(requiredCapabilities), allowNone: false);
        if (sourceSha is null || sourceSha.Length != 40 || sourceSha.Any(static c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Source SHA must be an exact commit SHA.", nameof(sourceSha));

        var normalizedSha = sourceSha.ToLowerInvariant();
        var backends = candidates.ToArray();
        var evidenceItems = evidence.ToArray();
        if (backends.Any(static item => item is null)) throw new ArgumentException("Backend candidates must not contain null entries.", nameof(candidates));
        if (evidenceItems.Any(static item => item is null)) throw new ArgumentException("Qualification evidence must not contain null entries.", nameof(evidence));

        var duplicateBackend = backends.GroupBy(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateBackend is not null)
            throw new InvalidOperationException($"Duplicate CAD backend ID '{duplicateBackend.Key}' makes production qualification ambiguous.");

        var duplicateEvidence = evidenceItems.GroupBy(static item => item.EvidenceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateEvidence is not null)
            throw new InvalidOperationException($"Duplicate CAD qualification evidence ID '{duplicateEvidence.Key}' makes production qualification ambiguous.");

        var eligible = backends
            .Where(static backend => backend.IsAvailable && backend.Kind == CadBackendKind.Native)
            .Where(backend => (backend.Capabilities & requiredCapabilities) == requiredCapabilities)
            .Where(static backend => !string.IsNullOrWhiteSpace(backend.Version))
            .OrderByDescending(static backend => backend.Priority)
            .ThenBy(static backend => backend.Id, StringComparer.Ordinal);

        foreach (var backend in eligible)
        {
            var match = evidenceItems
                .Where(static item => item.Passed)
                .Where(item => StringComparer.Ordinal.Equals(item.BackendId, backend.Id))
                .Where(item => StringComparer.Ordinal.Equals(item.BackendVersion, backend.Version))
                .Where(item => StringComparer.Ordinal.Equals(item.SourceSha, normalizedSha))
                .Where(item => (item.QualifiedCapabilities & requiredCapabilities) == requiredCapabilities)
                .OrderByDescending(static item => item.QualifiedAt)
                .ThenBy(static item => item.EvidenceId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match is not null) return new CadQualifiedBackendSelection(backend, match);
        }

        throw new InvalidOperationException($"No native CAD backend has passing exact-source and exact-version evidence for {normalizedSha} and required capabilities {requiredCapabilities}.");
    }

    public static CadBackendQualificationReport EvaluateProduction(
        IEnumerable<CadBackendDescriptor> candidates,
        IEnumerable<CadBackendQualificationEvidence> evidence,
        CadCapabilities requiredCapabilities,
        string sourceSha)
    {
        if (candidates is null) throw new ArgumentNullException(nameof(candidates));
        if (evidence is null) throw new ArgumentNullException(nameof(evidence));
        CadCapabilityValidation.RequireKnown(requiredCapabilities, nameof(requiredCapabilities), allowNone: false);
        if (sourceSha is null || sourceSha.Length != 40 || sourceSha.Any(static c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Source SHA must be an exact commit SHA.", nameof(sourceSha));

        var normalizedSha = sourceSha.ToLowerInvariant();
        var backends = candidates.ToArray();
        var evidenceItems = evidence.ToArray();
        if (backends.Any(static item => item is null)) throw new ArgumentException("Backend candidates must not contain null entries.", nameof(candidates));
        if (evidenceItems.Any(static item => item is null)) throw new ArgumentException("Qualification evidence must not contain null entries.", nameof(evidence));

        var duplicateBackend = backends.GroupBy(static item => item.Id, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateBackend is not null)
            throw new InvalidOperationException($"Duplicate CAD backend ID '{duplicateBackend.Key}' makes production qualification ambiguous.");

        var duplicateEvidence = evidenceItems.GroupBy(static item => item.EvidenceId, StringComparer.Ordinal)
            .FirstOrDefault(static group => group.Count() > 1);
        if (duplicateEvidence is not null)
            throw new InvalidOperationException($"Duplicate CAD qualification evidence ID '{duplicateEvidence.Key}' makes production qualification ambiguous.");

        var orderedBackends = backends
            .OrderByDescending(static backend => backend.Kind)
            .ThenByDescending(static backend => backend.Priority)
            .ThenBy(static backend => backend.Id, StringComparer.Ordinal)
            .ToArray();

        var diagnostics = new List<CadBackendQualificationCandidateDiagnostic>(orderedBackends.Length);
        CadQualifiedBackendSelection? selection = null;

        foreach (var backend in orderedBackends)
        {
            var diagnostic = EvaluateCandidate(backend, evidenceItems, requiredCapabilities, normalizedSha);
            diagnostics.Add(diagnostic);
            if (selection is null
                && diagnostic.Code == CadBackendQualificationDiagnosticCode.Qualified
                && diagnostic.EvidenceId is not null)
            {
                var selectedEvidence = evidenceItems.Single(item => StringComparer.Ordinal.Equals(item.EvidenceId, diagnostic.EvidenceId));
                selection = new CadQualifiedBackendSelection(backend, selectedEvidence);
            }
        }

        return new CadBackendQualificationReport(normalizedSha, requiredCapabilities, diagnostics.ToArray(), selection);
    }

    private static CadBackendQualificationCandidateDiagnostic EvaluateCandidate(
        CadBackendDescriptor backend,
        IReadOnlyList<CadBackendQualificationEvidence> evidence,
        CadCapabilities requiredCapabilities,
        string normalizedSha)
    {
        if (!backend.IsAvailable)
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.Unavailable, backend.UnavailableReason ?? "Backend is unavailable.");

        if (backend.Kind != CadBackendKind.Native)
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.NonNative, "Production qualification requires a native backend.");

        if (string.IsNullOrWhiteSpace(backend.Version))
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.MissingVersion, "Native backend does not declare an exact version.");

        var missingBackendCapabilities = requiredCapabilities & ~backend.Capabilities;
        if (missingBackendCapabilities != CadCapabilities.None)
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.BackendCapabilityMismatch, $"Backend lacks required capabilities: {missingBackendCapabilities}.");

        var backendEvidence = evidence
            .Where(item => StringComparer.Ordinal.Equals(item.BackendId, backend.Id))
            .OrderBy(static item => item.BackendVersion, StringComparer.Ordinal)
            .ThenBy(static item => item.SourceSha, StringComparer.Ordinal)
            .ThenByDescending(static item => item.QualifiedAt)
            .ThenBy(static item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();
        if (backendEvidence.Length == 0)
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.MissingEvidence, "No qualification evidence exists for this backend ID.");

        var passingEvidence = backendEvidence.Where(static item => item.Passed).ToArray();
        if (passingEvidence.Length == 0)
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.FailedEvidence, $"All {backendEvidence.Length} qualification evidence item(s) for this backend are failed.");

        var versionEvidence = passingEvidence
            .Where(item => StringComparer.Ordinal.Equals(item.BackendVersion, backend.Version))
            .ToArray();
        if (versionEvidence.Length == 0)
        {
            var versions = passingEvidence.Select(static item => item.BackendVersion).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal);
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.VersionMismatch, $"Passing evidence exists only for version(s): {string.Join(", ", versions)}.");
        }

        var sourceEvidence = versionEvidence
            .Where(item => StringComparer.Ordinal.Equals(item.SourceSha, normalizedSha))
            .ToArray();
        if (sourceEvidence.Length == 0)
        {
            var sources = versionEvidence.Select(static item => item.SourceSha).Distinct(StringComparer.Ordinal).OrderBy(static value => value, StringComparer.Ordinal);
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.SourceMismatch, $"Passing exact-version evidence exists only for source SHA(s): {string.Join(", ", sources)}.");
        }

        var capabilityEvidence = sourceEvidence
            .Where(item => (item.QualifiedCapabilities & requiredCapabilities) == requiredCapabilities)
            .OrderByDescending(static item => item.QualifiedAt)
            .ThenBy(static item => item.EvidenceId, StringComparer.Ordinal)
            .ToArray();
        if (capabilityEvidence.Length == 0)
        {
            var covered = sourceEvidence
                .Select(static item => item.QualifiedCapabilities.ToString())
                .Distinct(StringComparer.Ordinal)
                .OrderBy(static value => value, StringComparer.Ordinal);
            return Diagnostic(backend, CadBackendQualificationDiagnosticCode.EvidenceCapabilityMismatch, $"Exact-version/source evidence does not cover required capabilities. Qualified sets: {string.Join("; ", covered)}.");
        }

        var match = capabilityEvidence[0];
        return Diagnostic(backend, CadBackendQualificationDiagnosticCode.Qualified, $"Qualified by evidence '{match.EvidenceId}'.", match.EvidenceId);
    }

    private static CadBackendQualificationCandidateDiagnostic Diagnostic(
        CadBackendDescriptor backend,
        CadBackendQualificationDiagnosticCode code,
        string detail,
        string? evidenceId = null)
        => new(backend, code, detail, evidenceId);
}
