using System.Windows;
using System.Windows.Controls;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private void MoveSelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("MOVE", "Move", "100 0", "dx dy");

    private void CopySelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("COPY", "Copy", "100 0", "dx dy");

    private void ScaleSelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("SCALE", "Scale", "0 0 2", "baseX baseY factor");

    private void RotateSelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("ROTATE", "Rotate", "0 0 90", "baseX baseY angleDegrees");

    private void MirrorSelection_Click(object sender, RoutedEventArgs e)
        => PrepareSelectionTransform("MIRROR", "Mirror", "0 0 100 0", "axisX1 axisY1 axisX2 axisY2");

    private void PrepareSelectionTransform(string command, string operation, string defaultArguments, string parameterHint)
    {
        var selected = _app.Documents.ActiveDocument?.Editor.Selection.Current.ToArray()
            ?? Array.Empty<QS3D.Platform.Domain.CadHandle>();
        if (selected.Length == 0)
        {
            StatusText.Text = $"Select one or more entities before {operation}.";
            return;
        }

        var handles = string.Join(" ", selected.Select(static handle => QuoteToken(handle.ToString())));
        CommandBox.Text = $"{command} {handles} {defaultArguments}";
        CommandBox.SelectAll();
        CommandBox.Focus();
        StatusText.Text = $"{operation} prepared for {selected.Length} object(s). Edit {parameterHint} in the command line, then press Enter.";
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
