#!/usr/bin/env python3
from __future__ import annotations
import pathlib, sys
ROOT = pathlib.Path(__file__).resolve().parents[1]
HOST = ROOT / "src" / "QS3D.Cad.Host"
CLI = ROOT / "src" / "QS3D.Cad.Cli"
DESKTOP = ROOT / "src" / "QS3D.Cad.Desktop"
TESTS = ROOT / "tests" / "QS3D.Cad.SmokeTests"
errors: list[str] = []

def require(path: pathlib.Path, tokens: tuple[str, ...]) -> None:
    if not path.is_file():
        errors.append(f"missing {path.relative_to(ROOT)}")
        return
    text = path.read_text(encoding="utf-8", errors="replace")
    for token in tokens:
        if token not in text: errors.append(f"{path.relative_to(ROOT)} missing required token {token!r}")

def forbid(path: pathlib.Path, tokens: tuple[str, ...]) -> None:
    if not path.is_file():
        errors.append(f"missing {path.relative_to(ROOT)}")
        return
    text = path.read_text(encoding="utf-8", errors="replace")
    for token in tokens:
        if token in text: errors.append(f"{path.relative_to(ROOT)} contains forbidden token {token!r}")

require(HOST / "StandaloneCadApplication.cs", (
    "BuiltInCommands.RegisterAll", "LayerCommands.RegisterAll", "BlockCommands.RegisterAll",
    "SemanticCommands.RegisterAll", "AdvancedReferenceCommands.RegisterAll",
    "XrefReferenceCommands.RegisterAll", "LayoutReferenceCommands.RegisterAll", "PlotReferenceCommands.RegisterAll",
    "StandaloneDocumentManager", "StandaloneCommandCatalog", "OnDocumentOpened", "OnDocumentClosed", "RecordMutation", "ExecuteCommand",
    "_commands.Execute",
))
forbid(HOST / "StandaloneCadApplication.cs", ("public CommandRegistry Commands",))
require(HOST / "StandaloneCommandCatalog.cs", (
    "CommandRegistry", "Names", "Register", "Contains", "ReservedApplicationCommands", "string.IsNullOrWhiteSpace",
))
forbid(HOST / "StandaloneCommandCatalog.cs", ("CommandResult Execute", ".Execute("))
require(HOST / "StandaloneDocumentManager.cs", ("IDocumentManager", "_opened(document)", "_closed(id)", "InMemoryDocumentManager"))
forbid(HOST / "BuiltInCommands.cs", ("new UndoCommand", "new RedoCommand", "class UndoCommand", "class RedoCommand"))
require(HOST / "BuiltInCommands.cs", ("would overflow the finite coordinate range",))
require(HOST / "CommandLineTokenizer.cs", ("tokenStarted", "index + 1", "tokens.Add(current.ToString())"))
require(HOST / "StandaloneSemanticWorkspace.cs", ("Detach(DrawingId", "_states.Remove"))
require(HOST / "StandaloneModelReadinessAnalyzer.cs", ("ORPHAN_HANDLE", "CadTransactionMode.ReadOnly", "ModelReadinessAnalyzer.Analyze"))
require(HOST / "SemanticCommands.cs", ("SemanticAuthoringCommands.RegisterAll", "StandaloneModelReadinessAnalyzer.Analyze"))
require(HOST / "BootstrapDrawingStore.cs", (
    "Bootstrap drawing JSON is invalid.", "Bootstrap drawing content is invalid.",
    "Entity collection contains null.", "Semantic element collection contains null.",
))
require(HOST / "Qs3dBootstrapPackageStore.cs", (
    "manifest.json", "semantic-project.json", "drawing-bootstrap.json", "MaxPackageBytes", "MaxPayloadBytes",
    "SHA256.HashData", "CryptographicOperations.FixedTimeEquals", "duplicate entry", "missing or unexpected entries",
    "JsonException", "QS3D package manifest is invalid.", "Manifest contains unexpected payload", "media type must be",
))
require(HOST / "Qs3dBootstrapBackupWriter.cs", ("BackupPath", "CanLoad", "PublishBackup", "File.Move"))
require(HOST / "Qs3dBootstrapRecoveryReader.cs", ("RecoveredFromBackup", "PrimaryError", "BackupPath"))
require(HOST / "StandaloneCadPackageExtensions.cs", ("SaveProjectPackageWithBackup", "OpenProjectPackageWithRecovery"))
require(HOST / "CadCapabilityValidation.cs", ("Known", "RequireKnown", "unknown flag bits", "At least one CAD capability is required"))
require(HOST / "CadBackendPolicy.cs", ("CadBackendKind.Native", "RequireNative", "Production", "Version", "CadCapabilityValidation.RequireKnown"))
require(HOST / "CadBackendEvidence.cs", ("CadBackendQualificationEvidence", "BackendVersion", "SourceSha", "QualifiedCapabilities", "Passed", "CadCapabilityValidation.RequireKnown"))
require(HOST / "CadQualifiedBackendSelector.cs", ("SelectProduction", "CadBackendKind.Native", "BackendVersion", "SourceSha", "QualifiedCapabilities", "Duplicate CAD backend ID", "Duplicate CAD qualification evidence ID", "allowNone: false"))
require(HOST / "CadBackendQualificationEvidenceJson.cs", ("Serialize", "Deserialize", "BackendVersion", "SourceSha"))
for source in ("XrefReferenceCommands.cs", "LayoutReferenceCommands.cs", "PlotReferenceCommands.cs"):
    if not (HOST / source).is_file(): errors.append(f"missing src/QS3D.Cad.Host/{source}")

require(CLI / "Program.cs", ("ExecuteCommand(args[0], args.Skip(1))", "messageCursor", "app.Commands.Names"))
forbid(CLI / "Program.cs", ("string.Join(' ', args)", 'string.Join(" ", args)'))

require(DESKTOP / "MainWindow.xaml.cs", (
    "SaveProjectPackageWithBackup", "OpenProjectPackageWithRecovery", "RecoveredFromBackup", "*.qs3d",
    "DocumentList_SelectionChanged", "Activate(document.Id)", "_refreshingUi",
))
forbid(DESKTOP / "MainWindow.xaml.cs", ("SaveBootstrap(", "OpenBootstrap("))
require(DESKTOP / "MainWindow.xaml", (
    "_Open project...", "_Save project...", "QS3D CAD — Standalone",
    'DisplayMemberPath="Name"', 'SelectionChanged="DocumentList_SelectionChanged"',
))

require(TESTS / "QS3D.Cad.SmokeTests.csproj", ("QS3D.Platform.Parity", "QS3D.Platform.Families"))
require(TESTS / "Qs3dBackupRecoveryModuleSmoke.cs", ("CorruptDrawingPayloadWithMatchingManifest", "drawing-bootstrap.json"))
require(TESTS / "CommandRegistrationModuleSmoke.cs", (
    "journalCommand", "UNDO", "REDO", "StandaloneCommandCatalog", "TestCommand", "Commands.Contains", "TryResolve",
))
forbid(TESTS / "CommandRegistrationModuleSmoke.cs", ("Commands.TryResolve", "ReservedCommand"))
require(TESTS / "CommandLineTokenizerModuleSmoke.cs", ("windowsPath", "ExecuteCommand"))
require(TESTS / "DocumentLifecycleCleanupModuleSmoke.cs", ("app.Documents.Close", "Lifecycle reopened", "reused stale application journal"))
require(TESTS / "CadBackendQualificationEvidenceJsonModuleSmoke.cs", ("RewriteCapabilities", "1L << 20"))
for regression in (
    "AdvancedReferenceCommandsModuleSmoke.cs", "DocumentReferenceSurfaceModuleSmoke.cs",
    "Qs3dBackupRecoveryModuleSmoke.cs", "CadBackendPolicyModuleSmoke.cs",
    "CadBackendQualificationModuleSmoke.cs", "CadBackendQualificationEvidenceJsonModuleSmoke.cs",
    "Qs3dBootstrapPackageModuleSmoke.cs", "SemanticAuthoringQuantityModuleSmoke.cs",
    "StandaloneParityModuleSmoke.cs", "StandaloneFamilySchemaModuleSmoke.cs",
    "CommandRegistrationModuleSmoke.cs", "DocumentLifecycleCleanupModuleSmoke.cs",
    "StandaloneOrphanHandleHealthModuleSmoke.cs", "BootstrapDrawingCorruptionModuleSmoke.cs",
    "CommandJournalFailureModuleSmoke.cs", "DerivedCoordinateOverflowModuleSmoke.cs",
    "CommandLineTokenizerModuleSmoke.cs",
):
    if not (TESTS / regression).is_file(): errors.append(f"missing tests/QS3D.Cad.SmokeTests/{regression}")
if errors:
    print("QS3D standalone source boundary FAILED", file=sys.stderr)
    for error in errors: print(f"- {error}", file=sys.stderr)
    raise SystemExit(1)
print("QS3D standalone source boundary PASS")
