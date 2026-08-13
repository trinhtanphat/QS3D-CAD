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
        if (sourceSha is null || sourceSha.Length != 40 || sourceSha.Any(static c => !Uri.IsHexDigit(c)))
            throw new ArgumentException("Source SHA must be an exact commit SHA.", nameof(sourceSha));

        var normalizedSha = sourceSha.ToLowerInvariant();
        var backends = candidates.ToArray();
        var evidenceItems = evidence.ToArray();
        if (backends.Any(static item => item is null)) throw new ArgumentException("Backend candidates must not contain null entries.", nameof(candidates));
        if (evidenceItems.Any(static item => item is null)) throw new ArgumentException("Qualification evidence must not contain null entries.", nameof(evidence));

        var eligible = backends
            .Where(static backend => backend.IsAvailable && backend.Kind == CadBackendKind.Native)
            .Where(backend => (backend.Capabilities & requiredCapabilities) == requiredCapabilities)
            .OrderByDescending(static backend => backend.Priority)
            .ThenBy(static backend => backend.Id, StringComparer.Ordinal);

        foreach (var backend in eligible)
        {
            var match = evidenceItems
                .Where(static item => item.Passed)
                .Where(item => StringComparer.Ordinal.Equals(item.BackendId, backend.Id))
                .Where(item => StringComparer.Ordinal.Equals(item.SourceSha, normalizedSha))
                .Where(item => (item.QualifiedCapabilities & requiredCapabilities) == requiredCapabilities)
                .OrderByDescending(static item => item.QualifiedAt)
                .ThenBy(static item => item.EvidenceId, StringComparer.Ordinal)
                .FirstOrDefault();
            if (match is not null) return new CadQualifiedBackendSelection(backend, match);
        }

        throw new InvalidOperationException($"No native CAD backend has passing exact-source evidence for {normalizedSha} and required capabilities {requiredCapabilities}.");
    }
}
