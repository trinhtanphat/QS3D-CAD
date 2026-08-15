using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using QS3D.Platform.Cad.Abstractions;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private static readonly RoutedUICommand SelectAllDesktopCommand = new("Select all", nameof(SelectAllDesktopCommand), typeof(MainWindow));
    private static readonly RoutedUICommand ClearSelectionDesktopCommand = new("Clear selection", nameof(ClearSelectionDesktopCommand), typeof(MainWindow));
    private static readonly RoutedUICommand InvertSelectionDesktopCommand = new("Invert selection", nameof(InvertSelectionDesktopCommand), typeof(MainWindow));
    private readonly List<string> _typedCommandHistory = new();
    private int _typedCommandHistoryIndex;
    private string _typedCommandDraft = string.Empty;

    private void RegisterProductivityBindings()
    {
        RegisterProductivityBinding(SelectAllDesktopCommand, Key.A, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+A", "Select all", "SELALL");
        RegisterProductivityBinding(ClearSelectionDesktopCommand, Key.D, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+D", "Clear selection", "SELNONE");
        RegisterProductivityBinding(InvertSelectionDesktopCommand, Key.I, ModifierKeys.Control | ModifierKeys.Shift, "Ctrl+Shift+I", "Invert selection", "SELINVERT");
        CommandBox.PreviewKeyDown += CommandBoxHistory_PreviewKeyDown;

        var contextMenu = EntityList.ContextMenu ?? new ContextMenu();
        if (EntityList.ContextMenu is null)
            EntityList.ContextMenu = contextMenu;
        if (contextMenu.Items.Count > 0)
            contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(CreateProductivityMenuItem("Select all", "Ctrl+Shift+A", SelectAllDesktopCommand));
        contextMenu.Items.Add(CreateProductivityMenuItem("Clear selection", "Ctrl+Shift+D", ClearSelectionDesktopCommand));
        contextMenu.Items.Add(CreateProductivityMenuItem("Invert selection", "Ctrl+Shift+I", InvertSelectionDesktopCommand));
        contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(CreateActionMenuItem("Select same type", SelectSameType_Click));
        contextMenu.Items.Add(CreateActionMenuItem("Select same layer", SelectSameLayer_Click));
    }

    private void RegisterProductivityBinding(RoutedUICommand command, Key key, ModifierKeys modifiers, string gestureText, string label, string commandLine)
    {
        CommandBindings.Add(new CommandBinding(command, (_, _) => RunCommand(commandLine)));
        InputBindings.Add(new KeyBinding(command, new KeyGesture(key, modifiers)));
    }

    private static MenuItem CreateProductivityMenuItem(string header, string gesture, RoutedUICommand command)
        => new()
        {
            Header = header,
            InputGestureText = gesture,
            Command = command
        };

    private MenuItem CreateActionMenuItem(string header, RoutedEventHandler handler)
    {
        var item = new MenuItem { Header = header };
        item.Click += handler;
        return item;
    }

    private void SelectSameType_Click(object sender, RoutedEventArgs e)
    {
        var seed = SelectedSeedEntity();
        if (seed is null)
        {
            StatusText.Text = "Select an entity before Select same type.";
            return;
        }
        RunCommand($"SELKIND {seed.Kind}");
    }

    private void SelectSameLayer_Click(object sender, RoutedEventArgs e)
    {
        var seed = SelectedSeedEntity();
        if (seed is null)
        {
            StatusText.Text = "Select an entity before Select same layer.";
            return;
        }
        RunCommand($"SELLAYER {QuoteToken(seed.LayerName)}");
    }

    private CadEntitySnapshot? SelectedSeedEntity()
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null) return null;
        var handle = document.Editor.Selection.Current.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(handle.Value)) return null;
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        return tx.Get(handle);
    }

    private void CommandBoxHistory_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Up)
        {
            NavigateCommandHistory(-1);
            e.Handled = true;
            return;
        }
        if (e.Key == Key.Down)
        {
            NavigateCommandHistory(1);
            e.Handled = true;
            return;
        }
        if (e.Key != Key.Enter) return;

        var command = CommandBox.Text.Trim();
        if (command.Length == 0)
        {
            if (_typedCommandHistory.Count == 0) return;
            CommandBox.Text = _typedCommandHistory[^1];
            CommandBox.SelectAll();
            _typedCommandHistoryIndex = _typedCommandHistory.Count;
            return;
        }

        if (_typedCommandHistory.Count == 0 || !StringComparer.Ordinal.Equals(_typedCommandHistory[^1], command))
            _typedCommandHistory.Add(command);
        _typedCommandHistoryIndex = _typedCommandHistory.Count;
        _typedCommandDraft = string.Empty;
    }

    private void NavigateCommandHistory(int direction)
    {
        if (_typedCommandHistory.Count == 0) return;
        if (_typedCommandHistoryIndex < 0 || _typedCommandHistoryIndex > _typedCommandHistory.Count)
            _typedCommandHistoryIndex = _typedCommandHistory.Count;
        if (_typedCommandHistoryIndex == _typedCommandHistory.Count)
            _typedCommandDraft = CommandBox.Text;

        _typedCommandHistoryIndex = Math.Clamp(_typedCommandHistoryIndex + direction, 0, _typedCommandHistory.Count);
        CommandBox.Text = _typedCommandHistoryIndex == _typedCommandHistory.Count
            ? _typedCommandDraft
            : _typedCommandHistory[_typedCommandHistoryIndex];
        CommandBox.CaretIndex = CommandBox.Text.Length;
        CommandBox.SelectAll();
    }
}
