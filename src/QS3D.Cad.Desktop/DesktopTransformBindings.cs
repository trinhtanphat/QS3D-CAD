using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private static readonly RoutedUICommand ScaleSelectedDesktopCommand = new("Scale selected", nameof(ScaleSelectedDesktopCommand), typeof(MainWindow));
    private static readonly RoutedUICommand RotateSelectedDesktopCommand = new("Rotate selected", nameof(RotateSelectedDesktopCommand), typeof(MainWindow));
    private static readonly RoutedUICommand MirrorSelectedDesktopCommand = new("Mirror selected", nameof(MirrorSelectedDesktopCommand), typeof(MainWindow));

    protected override void OnSourceInitialized(EventArgs e)
    {
        base.OnSourceInitialized(e);

        RegisterTransformBinding(ScaleSelectedDesktopCommand, Key.S, ScaleSelection_Click);
        RegisterTransformBinding(RotateSelectedDesktopCommand, Key.R, RotateSelection_Click);
        RegisterTransformBinding(MirrorSelectedDesktopCommand, Key.M, MirrorSelection_Click);

        var contextMenu = EntityList.ContextMenu ?? new ContextMenu();
        if (EntityList.ContextMenu is null)
            EntityList.ContextMenu = contextMenu;

        if (contextMenu.Items.Count > 0)
            contextMenu.Items.Add(new Separator());
        contextMenu.Items.Add(CreateTransformMenuItem("Scale selected...", "Ctrl+Shift+S", ScaleSelectedDesktopCommand));
        contextMenu.Items.Add(CreateTransformMenuItem("Rotate selected...", "Ctrl+Shift+R", RotateSelectedDesktopCommand));
        contextMenu.Items.Add(CreateTransformMenuItem("Mirror selected...", "Ctrl+Shift+M", MirrorSelectedDesktopCommand));

        RegisterProductivityBindings();
        InitializeDocumentReliabilityBindings();
    }

    private void RegisterTransformBinding(RoutedUICommand command, Key key, RoutedEventHandler handler)
    {
        CommandBindings.Add(new CommandBinding(command, (sender, args) => handler(sender, args)));
        InputBindings.Add(new KeyBinding(command, new KeyGesture(key, ModifierKeys.Control | ModifierKeys.Shift)));
    }

    private MenuItem CreateTransformMenuItem(string header, string gesture, RoutedUICommand command)
        => new()
        {
            Header = header,
            InputGestureText = gesture,
            Command = command,
            CommandTarget = this
        };
}
