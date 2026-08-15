using System.Runtime.CompilerServices;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.SmokeTests;

internal static class DocumentReliabilityModuleSmoke
{
    [ModuleInitializer]
    internal static void Run()
    {
        SaveCheckpointTracksUndoRedo();
        ExternalMutationStaysDirtyAndStale();
        AutosaveDoesNotMarkCleanAndManualSaveClearsIt();
        AutosaveRecoveryOpensDirty();
        BackupRecoveryRetainsRequestedPrimaryPath();
    }

    private static void SaveCheckpointTracksUndoRedo()
    {
        var root = TempDirectory();
        var path = Path.Combine(root, "checkpoint.qs3d");
        try
        {
            var app = new StandaloneCadApplication();
            var document = app.NewDocument("checkpoint");
            Clean(app, document, "new document");
            Succeeds(app.Execute("LINE 0 0 10 0"));
            Dirty(app, document, "line mutation");

            app.SaveProjectPackageWithBackup(path);
            var saved = app.GetDocumentReliability(document);
            if (saved.IsDirty) throw new InvalidOperationException("Successful project save must establish a clean checkpoint.");
            Equal(Path.GetFullPath(path), saved.PrimaryPath ?? string.Empty, "primary save path");

            Succeeds(app.Execute("CIRCLE 5 5 2"));
            Dirty(app, document, "post-save mutation");
            Succeeds(app.Execute("UNDO"));
            Clean(app, document, "undo back to save checkpoint");
            Succeeds(app.Execute("REDO"));
            Dirty(app, document, "redo away from save checkpoint");
            Succeeds(app.Execute("UNDO"));
            Clean(app, document, "second undo back to save checkpoint");
        }
        finally { DeleteDirectory(root); }
    }

    private static void ExternalMutationStaysDirtyAndStale()
    {
        var root = TempDirectory();
        var path = Path.Combine(root, "external.qs3d");
        try
        {
            var app = new StandaloneCadApplication();
            var document = app.NewDocument("external");
            Succeeds(app.Execute("LINE 0 0 1 0"));
            app.SaveProjectPackageWithBackup(path);
            using (var tx = document.Database.BeginTransaction())
            {
                var point = new Point3(2, 2);
                tx.Append(new CadEntityDraft(CadEntityKind.Point, new BoundingBox3(point, point)));
                tx.Commit();
            }

            var snapshot = app.GetDocumentReliability(document);
            if (!snapshot.IsDirty || !snapshot.HasExternalMutation)
                throw new InvalidOperationException("Out-of-journal database mutation must be reported dirty and external.");
            var undo = app.Execute("UNDO");
            if (undo.Succeeded || undo.Message is null || !undo.Message.Contains("stale", StringComparison.OrdinalIgnoreCase))
                throw new InvalidOperationException("External mutation must keep prior application undo history fail-closed.");
            Dirty(app, document, "external mutation after stale undo rejection");

            app.SaveProjectPackageWithBackup(path);
            Clean(app, document, "manual save after external mutation");
        }
        finally { DeleteDirectory(root); }
    }

    private static void AutosaveDoesNotMarkCleanAndManualSaveClearsIt()
    {
        var root = TempDirectory();
        var autosave = Path.Combine(root, "autosave");
        var primary = Path.Combine(root, "primary.qs3d");
        try
        {
            var app = new StandaloneCadApplication();
            var document = app.NewDocument("autosave");
            Succeeds(app.Execute("RECTANG 0 0 20 10"));
            var written = app.AutosaveDirtyDocuments(autosave);
            Equal(1, written.Count, "autosave write count");
            if (!File.Exists(written[0].Path)) throw new InvalidOperationException("Autosave package was not published.");
            Dirty(app, document, "autosave must not mark document clean");

            var discovered = app.DiscoverAutosaves(autosave);
            Equal(1, discovered.Count, "validated autosave discovery count");
            Equal(document.Id, discovered[0].DrawingId, "autosave drawing identity");

            File.WriteAllText(Path.Combine(autosave, "corrupt.autosave.qs3d"), "not a package");
            discovered = app.DiscoverAutosaves(autosave);
            Equal(1, discovered.Count, "corrupt autosave must be skipped");

            var autosavePath = written[0].Path;
            app.SaveProjectPackageWithBackup(primary);
            Clean(app, document, "manual save after autosave");
            if (File.Exists(autosavePath)) throw new InvalidOperationException("Successful manual save must clear the stale autosave for that drawing.");
            if (app.GetDocumentReliability(document).AutosavePath is not null)
                throw new InvalidOperationException("Reliability state must clear autosave path after successful manual save.");
        }
        finally { DeleteDirectory(root); }
    }

    private static void AutosaveRecoveryOpensDirty()
    {
        var root = TempDirectory();
        var autosave = Path.Combine(root, "autosave");
        var primary = Path.Combine(root, "recovered.qs3d");
        try
        {
            var sourceApp = new StandaloneCadApplication();
            var source = sourceApp.NewDocument("recovery");
            Succeeds(sourceApp.Execute("LINE 1 2 3 4"));
            var snapshot = sourceApp.AutosaveDirtyDocuments(autosave).Single();

            var recoveredApp = new StandaloneCadApplication();
            var recovered = recoveredApp.OpenAutosaveSnapshot(snapshot.Path);
            var state = recoveredApp.GetDocumentReliability(recovered);
            if (!state.IsDirty) throw new InvalidOperationException("Recovered autosave must remain dirty until explicitly published.");
            if (state.PrimaryPath is not null) throw new InvalidOperationException("Recovered autosave must not impersonate a primary project path.");
            Equal(Path.GetFullPath(snapshot.Path), state.AutosavePath ?? string.Empty, "recovered autosave path");
            using (var tx = recovered.Database.BeginTransaction(CadTransactionMode.ReadOnly))
                Equal(1, tx.Query().Count, "recovered entity count");

            recoveredApp.SaveProjectPackageWithBackup(primary);
            Clean(recoveredApp, recovered, "published recovered project");
            if (File.Exists(snapshot.Path)) throw new InvalidOperationException("Publishing recovered project must clear its recovery snapshot.");
        }
        finally { DeleteDirectory(root); }
    }

    private static void BackupRecoveryRetainsRequestedPrimaryPath()
    {
        var root = TempDirectory();
        var primary = Path.Combine(root, "backup-recovery.qs3d");
        try
        {
            var sourceApp = new StandaloneCadApplication();
            sourceApp.NewDocument("backup-recovery");
            Succeeds(sourceApp.Execute("LINE 0 0 5 0"));
            sourceApp.SaveProjectPackageWithBackup(primary);
            Succeeds(sourceApp.Execute("CIRCLE 2 2 1"));
            sourceApp.SaveProjectPackageWithBackup(primary);
            var backup = Qs3dBootstrapBackupWriter.BackupPath(primary);
            if (!File.Exists(backup)) throw new InvalidOperationException("Second save must publish a validated previous-generation backup.");
            File.WriteAllText(primary, "corrupt primary");

            var recoveredApp = new StandaloneCadApplication();
            var result = recoveredApp.OpenProjectPackageWithRecovery(primary);
            if (!result.RecoveredFromBackup) throw new InvalidOperationException("Corrupt primary must recover from validated backup.");
            var state = recoveredApp.GetDocumentReliability(result.Document);
            Equal(Path.GetFullPath(primary), state.PrimaryPath ?? string.Empty, "requested primary path after backup recovery");
            Clean(recoveredApp, result.Document, "backup recovery opens at a clean checkpoint");
        }
        finally { DeleteDirectory(root); }
    }

    private static void Dirty(StandaloneCadApplication app, ICadDocument document, string label)
    {
        if (!app.GetDocumentReliability(document).IsDirty)
            throw new InvalidOperationException($"Expected dirty document after {label}.");
    }

    private static void Clean(StandaloneCadApplication app, ICadDocument document, string label)
    {
        if (app.GetDocumentReliability(document).IsDirty)
            throw new InvalidOperationException($"Expected clean document after {label}.");
    }

    private static void Succeeds(QS3D.Platform.Application.CommandResult result)
    {
        if (!result.Succeeded) throw new InvalidOperationException(result.Message ?? "Command failed.");
    }

    private static void Equal<T>(T expected, T actual, string label) where T : notnull
    {
        if (!EqualityComparer<T>.Default.Equals(expected, actual))
            throw new InvalidOperationException($"{label}: expected {expected} but got {actual}.");
    }

    private static string TempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), "qs3d-reliability-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(path);
        return path;
    }

    private static void DeleteDirectory(string path)
    {
        try { if (Directory.Exists(path)) Directory.Delete(path, recursive: true); }
        catch { }
    }
}
