using System.Windows;

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
}
