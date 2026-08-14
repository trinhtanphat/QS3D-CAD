#!/usr/bin/env python3
from __future__ import annotations
import pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
HOST = ROOT / "src" / "QS3D.Cad.Host"
TESTS = ROOT / "tests" / "QS3D.Cad.SmokeTests"
errors: list[str] = []

def require(path: pathlib.Path, tokens: tuple[str, ...]) -> None:
    if not path.is_file():
        errors.append(f"missing {path.relative_to(ROOT)}")
        return
    text = path.read_text(encoding="utf-8", errors="replace")
    for token in tokens:
        if token not in text: errors.append(f"{path.relative_to(ROOT)} missing required token {token!r}")

require(HOST / "StandaloneCadApplication.cs", (
    "BuiltInCommands.RegisterAll", "LayerCommands.RegisterAll", "BlockCommands.RegisterAll",
    "SemanticCommands.RegisterAll", "AdvancedReferenceCommands.RegisterAll",
    "XrefReferenceCommands.RegisterAll", "LayoutReferenceCommands.RegisterAll", "PlotReferenceCommands.RegisterAll",
    "CloseDocument", "Projects.Detach", "_undo.Remove", "_redo.Remove",
))
require(HOST / "StandaloneSemanticWorkspace.cs", ("Detach(DrawingId", "_states.Remove"))
require(HOST / "StandaloneModelReadinessAnalyzer.cs", ("ORPHAN_HANDLE", "CadTransactionMode.ReadOnly", "ModelReadinessAnalyzer.Analyze"))
require(HOST / "SemanticCommands.cs", ("SemanticAuthoringCommands.RegisterAll", "StandaloneModelReadinessAnalyzer.Analyze"))
require(HOST / "Qs3dBootstrapPackageStore.cs", (
    "manifest.json", "semantic-project.json", "drawing-bootstrap.json", "MaxPackageBytes", "MaxPayloadBytes",
    "SHA256.HashData", "CryptographicOperations.FixedTimeEquals", "duplicate entry", "missing or unexpected entries",
    "JsonException", "QS3D package manifest is invalid.", "Manifest contains unexpected payload", "media type must be",
))
require(HOST / "Qs3dBootstrapBackupWriter.cs", ("BackupPath", "CanLoad", "PublishBackup", "File.Move"))
require(HOST / "Qs3dBootstrapRecoveryReader.cs", ("RecoveredFromBackup", "PrimaryError", "BackupPath"))
require(HOST / "StandaloneCadPackageExtensions.cs", ("SaveProjectPackageWithBackup", "OpenProjectPackageWithRecovery"))
require(HOST / "CadBackendPolicy.cs", ("CadBackendKind.Native", "RequireNative", "Production", "Version"))
require(HOST / "CadBackendEvidence.cs", ("CadBackendQualificationEvidence", "BackendVersion", "SourceSha", "QualifiedCapabilities", "Passed"))
require(HOST / "CadQualifiedBackendSelector.cs", ("SelectProduction", "CadBackendKind.Native", "BackendVersion", "SourceSha", "QualifiedCapabilities", "Duplicate CAD backend ID", "Duplicate CAD qualification evidence ID"))
require(HOST / "CadBackendQualificationEvidenceJson.cs", ("Serialize", "Deserialize", "BackendVersion", "SourceSha"))
for source in ("XrefReferenceCommands.cs", "LayoutReferenceCommands.cs", "PlotReferenceCommands.cs"):
    if not (HOST / source).is_file(): errors.append(f"missing src/QS3D.Cad.Host/{source}")
require(TESTS / "QS3D.Cad.SmokeTests.csproj", ("QS3D.Platform.Parity", "QS3D.Platform.Families"))
for regression in (
    "AdvancedReferenceCommandsModuleSmoke.cs", "DocumentReferenceSurfaceModuleSmoke.cs",
    "Qs3dBackupRecoveryModuleSmoke.cs", "CadBackendPolicyModuleSmoke.cs",
    "CadBackendQualificationModuleSmoke.cs", "CadBackendQualificationEvidenceJsonModuleSmoke.cs",
    "Qs3dBootstrapPackageModuleSmoke.cs", "SemanticAuthoringQuantityModuleSmoke.cs",
    "StandaloneParityModuleSmoke.cs", "StandaloneFamilySchemaModuleSmoke.cs",
    "CommandRegistrationModuleSmoke.cs", "DocumentLifecycleCleanupModuleSmoke.cs",
    "StandaloneOrphanHandleHealthModuleSmoke.cs",
):
    if not (TESTS / regression).is_file(): errors.append(f"missing tests/QS3D.Cad.SmokeTests/{regression}")
if errors:
    print("QS3D standalone source boundary FAILED", file=sys.stderr)
    for error in errors: print(f"- {error}", file=sys.stderr)
    raise SystemExit(1)
print("QS3D standalone source boundary PASS")
