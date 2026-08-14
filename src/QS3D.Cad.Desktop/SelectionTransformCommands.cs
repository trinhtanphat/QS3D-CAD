using System.Windows;
using System.Windows.Controls;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private void MoveSelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("MOVE", "Move");

    private void CopySelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("COPY", "Copy");

    private void PrepareSelectionTransform(string command, string operation)
    {
        var selected = _app.Documents.ActiveDocument?.Editor.Selection.Current.ToArray()
            ?? Array.Empty<QS3D.Platform.Domain.CadHandle>();
        if (selected.Length == 0)
        {
            StatusText.Text = $"Select one or more entities before {operation}.";
            return;
        }

        var handles = string.Join(" ", selected.Select(static handle => QuoteToken(handle.ToString())));
        CommandBox.Text = $"{command} {handles} 100 0";
        CommandBox.SelectAll();
        CommandBox.Focus();
        StatusText.Text = $"{operation} prepared for {selected.Length} object(s). Edit dx/dy in the command line, then press Enter.";
    }

    private void EntityListMulti_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingUi) return;
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;

        var selectedRows = EntityList.SelectedItems.Cast<EntityRow>().ToArray();
        if (selectedRows.Length == 0)
        {
            document.Editor.Selection.Clear();
            StatusText.Text = "Selection cleared.";
        }
        else
        {
            var handles = selectedRows.Select(static row => row.Entity.Handle).ToArray();
            document.Editor.Selection.Set(handles);
            StatusText.Text = $"Selected {handles.Length} object(s). Ctrl/Shift-click to adjust the selection.";
        }

        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var entities = tx.Query().ToArray();
        var selected = document.Editor.Selection.Current.ToHashSet();
        PropertyList.ItemsSource = BuildProperties(entities, selected);
        RenderViewport(entities, tx.GetLayers(), selected);
    }
}
