using QS3D.Platform.Domain;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Host;

public sealed record Qs3dBootstrapRecoveryLoadResult(
    InMemoryCadDocument Document,
    SemanticProject Project,
    bool RecoveredFromBackup,
    string? PrimaryError,
    string SourcePath);
