using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Host;

public sealed class CadBackendQualificationEvidence
{
    public CadBackendQualificationEvidence(string backendId, string backendVersion, string sourceSha, CadCapabilities qualifiedCapabilities, DateTimeOffset qualifiedAt, string evidenceId, bool passed)
    {
        if (string.IsNullOrWhiteSpace(backendId)) throw new ArgumentException("Backend ID must not be blank.", nameof(backendId));
        if (string.IsNullOrWhiteSpace(backendVersion)) throw new ArgumentException("Backend version must not be blank.", nameof(backendVersion));
        if (sourceSha is null || sourceSha.Length != 40 || sourceSha.Any(static c => !Uri.IsHexDigit(c))) throw new ArgumentException("Source SHA must be an exact commit SHA.", nameof(sourceSha));
        CadCapabilityValidation.RequireKnown(qualifiedCapabilities, nameof(qualifiedCapabilities), allowNone: !passed);
        if (string.IsNullOrWhiteSpace(evidenceId)) throw new ArgumentException("Evidence ID must not be blank.", nameof(evidenceId));
        BackendId = backendId.Trim().ToLowerInvariant();
        BackendVersion = backendVersion.Trim();
        SourceSha = sourceSha.ToLowerInvariant();
        QualifiedCapabilities = qualifiedCapabilities;
        QualifiedAt = qualifiedAt;
        EvidenceId = evidenceId.Trim();
        Passed = passed;
    }

    public string BackendId { get; }
    public string BackendVersion { get; }
    public string SourceSha { get; }
    public CadCapabilities QualifiedCapabilities { get; }
    public DateTimeOffset QualifiedAt { get; }
    public string EvidenceId { get; }
    public bool Passed { get; }
}

public sealed record CadQualifiedBackendSelection(CadBackendDescriptor Backend, CadBackendQualificationEvidence Evidence);
