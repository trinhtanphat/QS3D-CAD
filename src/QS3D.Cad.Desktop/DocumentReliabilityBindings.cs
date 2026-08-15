using System.ComponentModel;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using Microsoft.Win32;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Domain;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private DispatcherTimer? _documentReliabilityTimer;
    private DateTimeOffset _nextAutosaveUtc;
    private bool _reliabilityCloseApproved;
    private bool _recoveryOffered;

    private static string AutosaveDirectory => Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "QS3D",
        "CAD",
        "Autosave");

    private void InitializeDocumentReliabilityBindings()
    {
        if (_documentReliabilityTimer is not null) return;
        _nextAutosaveUtc = DateTimeOffset.UtcNow.AddMinutes(2);
        _documentReliabilityTimer = new DispatcherTimer(DispatcherPriority.Background, Dispatcher)
        {
            Interval = TimeSpan.FromSeconds(2)
        };
        _documentReliabilityTimer.Tick += DocumentReliabilityTimer_Tick;
        _documentReliabilityTimer.Start();
        Dispatcher.BeginInvoke(DispatcherPriority.ApplicationIdle, new Action(OfferAutosaveRecovery));
        UpdateReliabilityTitle();
    }

    protected override void OnPreviewKeyDown(KeyEventArgs e)
    {
        if ((Keyboard.Modifiers & ModifierKeys.Control) != 0 && e.Key == Key.S)
        {
            var forceSaveAs = (Keyboard.Modifiers & ModifierKeys.Alt) != 0;
            if ((Keyboard.Modifiers & ModifierKeys.Shift) == 0 || forceSaveAs)
            {
                SaveReliabilityAware(forceSaveAs);
                e.Handled = true;
                return;
            }
        }
        base.OnPreviewKeyDown(e);
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        if (_reliabilityCloseApproved)
        {
            StopReliabilityTimer();
            base.OnClosing(e);
            return;
        }

        var dirty = _app.Documents.Documents
            .Where(document => _app.GetDocumentReliability(document).IsDirty)
            .ToArray();
        if (dirty.Length == 0)
        {
            StopReliabilityTimer();
            base.OnClosing(e);
            return;
        }

        var choice = MessageBox.Show(
            this,
            $"{dirty.Length} drawing(s) contain unsaved changes. Save before exiting?",
            "QS3D CAD — Unsaved changes",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning,
            MessageBoxResult.Yes);
        if (choice == MessageBoxResult.Cancel)
        {
            e.Cancel = true;
            return;
        }

        if (choice == MessageBoxResult.No)
        {
            foreach (var document in dirty)
                _app.DiscardAutosave(document.Id);
            _reliabilityCloseApproved = true;
            StopReliabilityTimer();
            base.OnClosing(e);
            return;
        }

        var activeId = _app.Documents.ActiveDocument?.Id;
        foreach (var document in dirty)
        {
            try { _app.Documents.Activate(document.Id); }
            catch (Exception ex)
            {
                StatusText.Text = ex.Message;
                e.Cancel = true;
                RestoreActiveDocument(activeId);
                return;
            }

            if (SaveReliabilityAware(forceSaveAs: false)) continue;
            e.Cancel = true;
            RestoreActiveDocument(activeId);
            return;
        }

        _reliabilityCloseApproved = true;
        StopReliabilityTimer();
        base.OnClosing(e);
    }

    private void DocumentReliabilityTimer_Tick(object? sender, EventArgs e)
    {
        UpdateReliabilityTitle();
        if (DateTimeOffset.UtcNow < _nextAutosaveUtc) return;
        _nextAutosaveUtc = DateTimeOffset.UtcNow.AddMinutes(2);
        try
        {
            var written = _app.AutosaveDirtyDocuments(AutosaveDirectory);
            if (written.Count > 0)
                StatusText.Text = $"Autosaved {written.Count} dirty drawing(s).";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException or InvalidOperationException)
        {
            StatusText.Text = $"Autosave failed: {ex.Message}";
        }
    }

    private bool SaveReliabilityAware(bool forceSaveAs)
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null)
        {
            StatusText.Text = "No active drawing to save.";
            return false;
        }

        var snapshot = _app.GetDocumentReliability(document);
        var path = forceSaveAs ? null : snapshot.PrimaryPath;
        if (string.IsNullOrWhiteSpace(path))
        {
            var dialog = new SaveFileDialog
            {
                Filter = "QS3D project (*.qs3d)|*.qs3d",
                AddExtension = true,
                DefaultExt = ".qs3d",
                FileName = SafeSuggestedFileName(document.Name)
            };
            if (dialog.ShowDialog(this) != true) return false;
            path = dialog.FileName;
        }

        try
        {
            _app.SaveProjectPackageWithBackup(path);
            StatusText.Text = $"Saved QS3D project: {path}";
            UpdateReliabilityTitle();
            return true;
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
            return false;
        }
    }

    private void OfferAutosaveRecovery()
    {
        if (_recoveryOffered) return;
        _recoveryOffered = true;
        IReadOnlyList<StandaloneAutosaveSnapshotInfo> snapshots;
        try { snapshots = _app.DiscoverAutosaves(AutosaveDirectory); }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or InvalidDataException)
        {
            StatusText.Text = $"Autosave discovery failed: {ex.Message}";
            return;
        }
        if (snapshots.Count == 0) return;

        var newest = snapshots[0];
        var choice = MessageBox.Show(
            this,
            $"Found {snapshots.Count} validated autosave snapshot(s). Open the most recent recovery for '{newest.DrawingName}'?",
            "QS3D CAD — Recovery available",
            MessageBoxButton.YesNo,
            MessageBoxImage.Information,
            MessageBoxResult.Yes);
        if (choice != MessageBoxResult.Yes)
        {
            StatusText.Text = $"Autosave recovery preserved at {newest.Path}.";
            return;
        }

        try
        {
            _app.OpenAutosaveSnapshot(newest.Path);
            SetTool(ToolMode.Select, $"Recovered autosave for '{newest.DrawingName}'. Save As to publish a primary project file.");
            RefreshUi();
            UpdateReliabilityTitle();
        }
        catch (Exception ex)
        {
            StatusText.Text = $"Autosave recovery failed: {ex.Message}";
        }
    }

    private void UpdateReliabilityTitle()
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null)
        {
            Title = "QS3D CAD — Standalone";
            return;
        }
        var snapshot = _app.GetDocumentReliability(document);
        var dirty = snapshot.IsDirty ? " *" : string.Empty;
        var recovered = snapshot.AutosavePath is not null && snapshot.PrimaryPath is null ? " [Recovered]" : string.Empty;
        Title = $"QS3D CAD — {document.Name}{dirty}{recovered}";
    }

    private void RestoreActiveDocument(DrawingId? drawingId)
    {
        if (drawingId is null) return;
        try { _app.Documents.Activate(drawingId.Value); }
        catch { }
        RefreshUi();
        UpdateReliabilityTitle();
    }

    private void StopReliabilityTimer()
    {
        if (_documentReliabilityTimer is null) return;
        _documentReliabilityTimer.Stop();
        _documentReliabilityTimer.Tick -= DocumentReliabilityTimer_Tick;
        _documentReliabilityTimer = null;
    }

    private static string SafeSuggestedFileName(string name)
    {
        var invalid = Path.GetInvalidFileNameChars().ToHashSet();
        var cleaned = new string(name.Select(character => invalid.Contains(character) ? '_' : character).ToArray()).Trim();
        return string.IsNullOrWhiteSpace(cleaned) ? "Drawing.qs3d" : Path.ChangeExtension(cleaned, ".qs3d");
    }
}
