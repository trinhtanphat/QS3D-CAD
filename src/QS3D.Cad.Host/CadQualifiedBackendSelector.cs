using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

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
}
