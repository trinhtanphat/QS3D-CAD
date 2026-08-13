using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;

namespace QS3D.Cad.Desktop;

public partial class MainWindow : Window
{
    private readonly StandaloneCadApplication _app = new();

    public MainWindow()
    {
        InitializeComponent();
        _app.NewDocument("Untitled");
        RefreshUi();
    }

    private void RunCommand(string command)
    {
        var result = _app.Execute(command);
        StatusText.Text = result.Succeeded ? result.Message ?? "OK" : result.Message ?? "Command failed";
        RefreshUi();
    }

    private void RefreshUi()
    {
        DocumentList.ItemsSource = _app.Documents.Documents.Select(x => x.Name).ToArray();
        var document = _app.Documents.ActiveDocument;
        if (document is null)
        {
            EntityList.ItemsSource = Array.Empty<string>();
            MessageList.ItemsSource = Array.Empty<string>();
            return;
        }
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        EntityList.ItemsSource = tx.Query().Select(x => $"{x.Handle}  {x.Kind}  {x.Extents.Min} -> {x.Extents.Max}").ToArray();
        MessageList.ItemsSource = document.Editor is InMemoryEditor editor ? editor.Messages.Reverse().Take(200).ToArray() : Array.Empty<string>();
    }

    private void New_Click(object sender, RoutedEventArgs e) { _app.NewDocument($"Drawing{_app.Documents.Documents.Count + 1}"); RefreshUi(); }
    private void Undo_Click(object sender, RoutedEventArgs e) => RunCommand("UNDO");
    private void Redo_Click(object sender, RoutedEventArgs e) => RunCommand("REDO");
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshUi();
    private void Line_Click(object sender, RoutedEventArgs e) { CommandBox.Text = "LINE 0 0 1000 0"; CommandBox.Focus(); }
    private void Circle_Click(object sender, RoutedEventArgs e) { CommandBox.Text = "CIRCLE 0 0 500"; CommandBox.Focus(); }
    private void Run_Click(object sender, RoutedEventArgs e) { RunCommand(CommandBox.Text); CommandBox.SelectAll(); CommandBox.Focus(); }

    private void CommandBox_KeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key != Key.Enter) return;
        RunCommand(CommandBox.Text);
        CommandBox.SelectAll();
        e.Handled = true;
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "QS3D bootstrap (*.qs3d-bootstrap.json)|*.qs3d-bootstrap.json", AddExtension = true };
        if (dialog.ShowDialog(this) != true) return;
        try { _app.SaveBootstrap(dialog.FileName); StatusText.Text = "Saved bootstrap fixture."; }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "QS3D bootstrap (*.qs3d-bootstrap.json)|*.qs3d-bootstrap.json" };
        if (dialog.ShowDialog(this) != true) return;
        try { _app.OpenBootstrap(dialog.FileName); StatusText.Text = "Opened bootstrap fixture."; RefreshUi(); }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }
}
