using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private static readonly RoutedUICommand MoveSelectionToCurrentLayerCommand = new(
        "Move selected to current layer",
        nameof(MoveSelectionToCurrentLayerCommand),
        typeof(MainWindow));

    private static readonly RoutedUICommand PrepareSetMetadataCommand = new(
        "Set metadata property",
        nameof(PrepareSetMetadataCommand),
        typeof(MainWindow));

    private bool _propertyEditingBindingsAttached;

    protected override void OnActivated(EventArgs e)
    {
        base.OnActivated(e);
        if (_propertyEditingBindingsAttached) return;
        _propertyEditingBindingsAttached = true;

        CommandBindings.Add(new CommandBinding(MoveSelectionToCurrentLayerCommand, MoveSelectionToCurrentLayer_Executed));
        CommandBindings.Add(new CommandBinding(PrepareSetMetadataCommand, PrepareSetMetadata_Executed));
        InputBindings.Add(new KeyBinding(MoveSelectionToCurrentLayerCommand, new KeyGesture(Key.L, ModifierKeys.Control | ModifierKeys.Shift)));
        InputBindings.Add(new KeyBinding(PrepareSetMetadataCommand, new KeyGesture(Key.P, ModifierKeys.Control | ModifierKeys.Shift)));

        var contextMenu = EntityList.ContextMenu ?? new ContextMenu();
        if (EntityList.ContextMenu is null)
            EntityList.ContextMenu = contextMenu;
        if (contextMenu.Items.Count > 0)
            contextMenu.Items.Add(new Separator());

        contextMenu.Items.Add(new MenuItem
        {
            Header = "Move selected to current layer",
            InputGestureText = "Ctrl+Shift+L",
            Command = MoveSelectionToCurrentLayerCommand,
            CommandTarget = this
        });
        contextMenu.Items.Add(new MenuItem
        {
            Header = "Set metadata property...",
            InputGestureText = "Ctrl+Shift+P",
            Command = PrepareSetMetadataCommand,
            CommandTarget = this
        });
        var deleteProperty = new MenuItem { Header = "Delete metadata property..." };
        deleteProperty.Click += PrepareDeleteMetadata_Click;
        contextMenu.Items.Add(deleteProperty);
    }

    private void MoveSelectionToCurrentLayer_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        var selected = SelectedPropertyEditHandles();
        if (selected.Length == 0)
        {
            StatusText.Text = "Select one or more entities before changing layer.";
            return;
        }

        var document = _app.Documents.ActiveDocument;
        if (document is null) return;
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        var currentLayer = tx.CurrentLayerName;
        var handles = string.Join(" ", selected.Select(static handle => QuoteToken(handle.ToString())));
        RunCommand($"CHLAYER {handles} {QuoteToken(currentLayer)}");
    }

    private void PrepareSetMetadata_Executed(object sender, ExecutedRoutedEventArgs e)
        => PrepareMetadataCommand("SETPROP", "Set metadata property", $"{QuoteToken("User.Tag")} {QuoteToken("Value")}");

    private void PrepareDeleteMetadata_Click(object sender, RoutedEventArgs e)
        => PrepareMetadataCommand("DELPROP", "Delete metadata property", QuoteToken("User.Tag"));

    private void PrepareMetadataCommand(string command, string operation, string trailingArguments)
    {
        var selected = SelectedPropertyEditHandles();
        if (selected.Length == 0)
        {
            StatusText.Text = $"Select one or more entities before {operation}.";
            return;
        }

        var handles = string.Join(" ", selected.Select(static handle => QuoteToken(handle.ToString())));
        CommandBox.Text = $"{command} {handles} {trailingArguments}";
        CommandBox.SelectAll();
        CommandBox.Focus();
        StatusText.Text = $"{operation} prepared for {selected.Length} object(s). Edit the trailing metadata arguments, then press Enter.";
    }

    private QS3D.Platform.Domain.CadHandle[] SelectedPropertyEditHandles()
        => _app.Documents.ActiveDocument?.Editor.Selection.Current.Distinct().ToArray()
            ?? Array.Empty<QS3D.Platform.Domain.CadHandle>();
}
