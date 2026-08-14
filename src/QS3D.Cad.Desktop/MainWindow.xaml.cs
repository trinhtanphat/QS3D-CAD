using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.Win32;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.InMemory;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfLine = System.Windows.Shapes.Line;
using WpfRectangle = System.Windows.Shapes.Rectangle;

namespace QS3D.Cad.Desktop;

public partial class MainWindow : Window
{
    private readonly StandaloneCadApplication _app = new();
    private bool _refreshingUi;
    private ToolMode _tool = ToolMode.Select;
    private WorldPoint? _pendingPoint;
    private double _worldMinX = -1000d;
    private double _worldMinY = -750d;
    private double _worldMaxX = 1000d;
    private double _worldMaxY = 750d;
    private double _viewScale = 1d;
    private double _viewOffsetX;
    private double _viewOffsetY;

    public MainWindow()
    {
        InitializeComponent();
        _app.NewDocument("Untitled");
        SetTool(ToolMode.Select, "Ready. Select an entity or choose a drawing tool.");
        RefreshUi();
    }

    public sealed record EntityRow(CadEntitySnapshot Entity)
    {
        public string DisplayText => $"{Entity.Handle}   {Entity.Kind}   [{Entity.LayerName}]";
    }

    public sealed record LayerRow(CadLayerSnapshot Layer, bool IsCurrent)
    {
        public string DisplayText => $"{(IsCurrent ? "●" : "○")} {Layer.Name}   {(Layer.IsOn ? "ON" : "OFF")}{(Layer.IsFrozen ? "  FROZEN" : string.Empty)}{(Layer.IsLocked ? "  LOCKED" : string.Empty)}";
    }

    public sealed record PropertyRow(string Name, string Value);

    private enum ToolMode { Select, Line, Rectangle, Circle }
    private readonly record struct WorldPoint(double X, double Y);

    private void RunCommand(string command)
    {
        if (string.IsNullOrWhiteSpace(command))
        {
            StatusText.Text = "Command line is empty.";
            return;
        }

        try
        {
            var result = _app.Execute(command);
            StatusText.Text = result.Message ?? (result.Succeeded ? "Command complete." : "Command failed.");
        }
        catch (Exception ex)
        {
            StatusText.Text = ex.Message;
        }
        RefreshUi();
    }

    private void RunBatch(params string[] commands)
    {
        var completed = 0;
        foreach (var command in commands)
        {
            var result = _app.Execute(command);
            if (!result.Succeeded)
            {
                StatusText.Text = result.Message ?? $"Command failed: {command}";
                RefreshUi();
                return;
            }
            completed++;
        }
        StatusText.Text = $"Sample drawing created ({completed} commands).";
        RefreshUi();
    }

    private void RefreshUi()
    {
        _refreshingUi = true;
        try
        {
            var documents = _app.Documents.Documents.ToArray();
            DocumentList.ItemsSource = documents;
            var document = _app.Documents.ActiveDocument;
            DocumentList.SelectedItem = document;
            if (document is null)
            {
                EntityList.ItemsSource = Array.Empty<EntityRow>();
                LayerList.ItemsSource = Array.Empty<LayerRow>();
                PropertyList.ItemsSource = Array.Empty<PropertyRow>();
                MessageList.ItemsSource = Array.Empty<string>();
                ViewportCanvas.Children.Clear();
                CurrentLayerText.Text = "Layer: —";
                EntityCountText.Text = "0 entities";
                return;
            }

            using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            var entities = tx.Query().ToArray();
            var layers = tx.GetLayers().ToArray();
            var selected = document.Editor.Selection.Current.ToHashSet();

            var entityRows = entities.Select(static entity => new EntityRow(entity)).ToArray();
            EntityList.ItemsSource = entityRows;
            EntityList.SelectedItem = entityRows.FirstOrDefault(row => selected.Contains(row.Entity.Handle));

            var layerRows = layers.Select(layer => new LayerRow(layer, StringComparer.OrdinalIgnoreCase.Equals(layer.Name, tx.CurrentLayerName))).ToArray();
            LayerList.ItemsSource = layerRows;
            LayerList.SelectedItem = layerRows.FirstOrDefault(static row => row.IsCurrent);

            CurrentLayerText.Text = $"Layer: {tx.CurrentLayerName}";
            EntityCountText.Text = $"{entities.Length} {(entities.Length == 1 ? "entity" : "entities")}";
            PropertyList.ItemsSource = BuildProperties(entities, selected);
            MessageList.ItemsSource = document.Editor is InMemoryEditor editor
                ? editor.Messages.Reverse().Take(200).ToArray()
                : Array.Empty<string>();
            RenderViewport(entities, layers, selected);
        }
        finally
        {
            _refreshingUi = false;
        }
    }

    private static IReadOnlyList<PropertyRow> BuildProperties(IReadOnlyList<CadEntitySnapshot> entities, HashSet<QS3D.Platform.Domain.CadHandle> selected)
    {
        var entity = entities.FirstOrDefault(candidate => selected.Contains(candidate.Handle));
        if (entity is null)
            return new[] { new PropertyRow("Selection", selected.Count == 0 ? "Nothing selected" : $"{selected.Count} objects") };

        var rows = new List<PropertyRow>
        {
            new("Handle", entity.Handle.ToString()),
            new("Type", entity.Kind.ToString()),
            new("Layer", entity.LayerName),
            new("Min", FormatPoint(entity.Extents.Min.X, entity.Extents.Min.Y)),
            new("Max", FormatPoint(entity.Extents.Max.X, entity.Extents.Max.Y))
        };
        rows.AddRange(entity.Properties.OrderBy(static pair => pair.Key, StringComparer.OrdinalIgnoreCase)
            .Select(static pair => new PropertyRow(pair.Key, pair.Value)));
        return rows;
    }

    private void RenderViewport(IReadOnlyList<CadEntitySnapshot> entities, IReadOnlyList<CadLayerSnapshot> layers, HashSet<QS3D.Platform.Domain.CadHandle> selected)
    {
        ViewportCanvas.Children.Clear();
        var width = Math.Max(1d, ViewportCanvas.ActualWidth);
        var height = Math.Max(1d, ViewportCanvas.ActualHeight);
        FitWorld(entities, width, height);
        DrawGrid(width, height);

        var layerState = layers.ToDictionary(static layer => layer.Name, StringComparer.OrdinalIgnoreCase);
        foreach (var entity in entities)
        {
            if (layerState.TryGetValue(entity.LayerName, out var layer) && (!layer.IsOn || layer.IsFrozen)) continue;
            DrawEntity(entity, selected.Contains(entity.Handle));
        }
    }

    private void FitWorld(IReadOnlyList<CadEntitySnapshot> entities, double width, double height)
    {
        if (entities.Count == 0)
        {
            _worldMinX = -1000d; _worldMinY = -750d; _worldMaxX = 1000d; _worldMaxY = 750d;
        }
        else
        {
            _worldMinX = entities.Min(static e => e.Extents.Min.X);
            _worldMinY = entities.Min(static e => e.Extents.Min.Y);
            _worldMaxX = entities.Max(static e => e.Extents.Max.X);
            _worldMaxY = entities.Max(static e => e.Extents.Max.Y);
            var rawWidth = Math.Max(1d, _worldMaxX - _worldMinX);
            var rawHeight = Math.Max(1d, _worldMaxY - _worldMinY);
            var marginX = Math.Max(100d, rawWidth * .12d);
            var marginY = Math.Max(100d, rawHeight * .12d);
            _worldMinX -= marginX; _worldMaxX += marginX; _worldMinY -= marginY; _worldMaxY += marginY;
        }

        var worldWidth = Math.Max(1d, _worldMaxX - _worldMinX);
        var worldHeight = Math.Max(1d, _worldMaxY - _worldMinY);
        _viewScale = Math.Max(.000001d, Math.Min(width / worldWidth, height / worldHeight));
        _viewOffsetX = (width - worldWidth * _viewScale) * .5d;
        _viewOffsetY = (height - worldHeight * _viewScale) * .5d;
    }

    private void DrawGrid(double width, double height)
    {
        var span = Math.Max(_worldMaxX - _worldMinX, _worldMaxY - _worldMinY);
        var step = NiceGridStep(span / 12d);
        var grid = new SolidColorBrush(Color.FromRgb(31, 44, 58));
        var axis = new SolidColorBrush(Color.FromRgb(65, 92, 118));
        var startX = Math.Ceiling(_worldMinX / step) * step;
        for (var x = startX; x <= _worldMaxX && x < startX + step * 80d; x += step)
        {
            var sx = ScreenX(x);
            if (sx < 0d || sx > width) continue;
            ViewportCanvas.Children.Add(new WpfLine { X1 = sx, Y1 = 0d, X2 = sx, Y2 = height, Stroke = Math.Abs(x) < step * .001d ? axis : grid, StrokeThickness = Math.Abs(x) < step * .001d ? 1.2d : .65d, IsHitTestVisible = false });
        }
        var startY = Math.Ceiling(_worldMinY / step) * step;
        for (var y = startY; y <= _worldMaxY && y < startY + step * 80d; y += step)
        {
            var sy = ScreenY(y);
            if (sy < 0d || sy > height) continue;
            ViewportCanvas.Children.Add(new WpfLine { X1 = 0d, Y1 = sy, X2 = width, Y2 = sy, Stroke = Math.Abs(y) < step * .001d ? axis : grid, StrokeThickness = Math.Abs(y) < step * .001d ? 1.2d : .65d, IsHitTestVisible = false });
        }
    }

    private void DrawEntity(CadEntitySnapshot entity, bool isSelected)
    {
        var stroke = isSelected ? new SolidColorBrush(Color.FromRgb(255, 190, 73)) : new SolidColorBrush(Color.FromRgb(50, 187, 255));
        var thickness = isSelected ? 3d : 2d;
        Shape shape;
        switch (entity.Kind)
        {
            case CadEntityKind.Line:
                shape = new WpfLine
                {
                    X1 = ScreenX(Number(entity, "x1", entity.Extents.Min.X)),
                    Y1 = ScreenY(Number(entity, "y1", entity.Extents.Min.Y)),
                    X2 = ScreenX(Number(entity, "x2", entity.Extents.Max.X)),
                    Y2 = ScreenY(Number(entity, "y2", entity.Extents.Max.Y)),
                    Stroke = stroke, StrokeThickness = thickness
                };
                break;
            case CadEntityKind.Circle:
                var cx = Number(entity, "cx", (entity.Extents.Min.X + entity.Extents.Max.X) * .5d);
                var cy = Number(entity, "cy", (entity.Extents.Min.Y + entity.Extents.Max.Y) * .5d);
                var radius = Number(entity, "radius", Math.Abs(entity.Extents.Max.X - entity.Extents.Min.X) * .5d);
                var diameter = Math.Max(1d, radius * 2d * _viewScale);
                var ellipse = new WpfEllipse { Width = diameter, Height = diameter, Stroke = stroke, StrokeThickness = thickness, Fill = Brushes.Transparent };
                Canvas.SetLeft(ellipse, ScreenX(cx - radius)); Canvas.SetTop(ellipse, ScreenY(cy + radius)); shape = ellipse;
                break;
            default:
                var left = ScreenX(entity.Extents.Min.X); var right = ScreenX(entity.Extents.Max.X);
                var top = ScreenY(entity.Extents.Max.Y); var bottom = ScreenY(entity.Extents.Min.Y);
                var rectangle = new WpfRectangle { Width = Math.Max(1d, right - left), Height = Math.Max(1d, bottom - top), Stroke = stroke, StrokeThickness = thickness, Fill = entity.Kind == CadEntityKind.Polyline ? new SolidColorBrush(Color.FromArgb(18, 25, 167, 255)) : Brushes.Transparent };
                if (entity.Kind != CadEntityKind.Polyline) rectangle.StrokeDashArray = new DoubleCollection { 4d, 3d };
                Canvas.SetLeft(rectangle, left); Canvas.SetTop(rectangle, top); shape = rectangle;
                break;
        }
        shape.Cursor = Cursors.Hand;
        shape.ToolTip = $"{entity.Kind} {entity.Handle} • Layer {entity.LayerName}";
        shape.MouseLeftButtonDown += (_, args) => { SelectEntity(entity); args.Handled = true; };
        ViewportCanvas.Children.Add(shape);
    }

    private void SelectEntity(CadEntitySnapshot entity)
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;
        document.Editor.Selection.Set(new[] { entity.Handle });
        StatusText.Text = $"Selected {entity.Kind} {entity.Handle}.";
        RefreshUi();
    }

    private void DocumentList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingUi || DocumentList.SelectedItem is not ICadDocument document || _app.Documents.ActiveDocument?.Id == document.Id) return;
        try { _app.Documents.Activate(document.Id); StatusText.Text = $"Activated {document.Name}."; _pendingPoint = null; RefreshUi(); }
        catch (Exception ex) { StatusText.Text = ex.Message; RefreshUi(); }
    }

    private void EntityList_SelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (_refreshingUi) return;
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;
        if (EntityList.SelectedItem is EntityRow row) { document.Editor.Selection.Set(new[] { row.Entity.Handle }); StatusText.Text = $"Selected {row.Entity.Kind} {row.Entity.Handle}."; }
        else document.Editor.Selection.Clear();
        RefreshUi();
    }

    private void ViewportCanvas_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;
        var point = WorldFromScreen(e.GetPosition(ViewportCanvas));
        if (_tool == ToolMode.Select) { document.Editor.Selection.Clear(); StatusText.Text = "Selection cleared."; RefreshUi(); return; }
        if (_pendingPoint is null)
        {
            _pendingPoint = point;
            StatusText.Text = _tool == ToolMode.Circle ? $"Circle center: {FormatPoint(point.X, point.Y)}. Pick radius point." : $"First point: {FormatPoint(point.X, point.Y)}. Pick second point.";
            return;
        }
        var first = _pendingPoint.Value; _pendingPoint = null;
        if (_tool == ToolMode.Line) RunCommand($"LINE {N(first.X)} {N(first.Y)} {N(point.X)} {N(point.Y)}");
        else if (_tool == ToolMode.Rectangle) RunCommand($"RECTANG {N(first.X)} {N(first.Y)} {N(point.X)} {N(point.Y)}");
        else
        {
            var radius = Math.Sqrt(Math.Pow(point.X - first.X, 2d) + Math.Pow(point.Y - first.Y, 2d));
            if (radius <= .000001d) { StatusText.Text = "Circle radius must be greater than zero."; return; }
            RunCommand($"CIRCLE {N(first.X)} {N(first.Y)} {N(radius)}");
        }
    }

    private void ViewportCanvas_MouseMove(object sender, MouseEventArgs e)
    {
        var point = WorldFromScreen(e.GetPosition(ViewportCanvas));
        CursorStatusText.Text = $"X: {point.X:0.###}   Y: {point.Y:0.###}";
    }

    private void ViewportCanvas_SizeChanged(object sender, SizeChangedEventArgs e)
    {
        if (!_refreshingUi && _app.Documents.ActiveDocument is not null) RenderActiveViewport();
    }

    private void RenderActiveViewport()
    {
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;
        using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
        RenderViewport(tx.Query(), tx.GetLayers(), document.Editor.Selection.Current.ToHashSet());
    }

    private void SetTool(ToolMode tool, string status)
    {
        _tool = tool; _pendingPoint = null; ToolStatusText.Text = $"Tool: {tool}"; StatusText.Text = status;
        SelectToolButton.FontWeight = tool == ToolMode.Select ? FontWeights.Bold : FontWeights.Normal;
        LineToolButton.FontWeight = tool == ToolMode.Line ? FontWeights.Bold : FontWeights.Normal;
        RectangleToolButton.FontWeight = tool == ToolMode.Rectangle ? FontWeights.Bold : FontWeights.Normal;
        CircleToolButton.FontWeight = tool == ToolMode.Circle ? FontWeights.Bold : FontWeights.Normal;
    }

    private void SelectTool_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Select, "Select mode. Click an entity in the viewport or entity list.");
    private void Line_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Line, "Line tool. Pick first point in the viewport.");
    private void Rectangle_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Rectangle, "Rectangle tool. Pick first corner in the viewport.");
    private void Circle_Click(object sender, RoutedEventArgs e) => SetTool(ToolMode.Circle, "Circle tool. Pick center in the viewport.");
    private void Undo_Click(object sender, RoutedEventArgs e) => RunCommand("UNDO");
    private void Redo_Click(object sender, RoutedEventArgs e) => RunCommand("REDO");
    private void Refresh_Click(object sender, RoutedEventArgs e) => RefreshUi();
    private void ZoomExtents_Click(object sender, RoutedEventArgs e) { RenderActiveViewport(); StatusText.Text = "Viewport fitted to drawing extents."; }
    private void ListEntities_Click(object sender, RoutedEventArgs e) => RunCommand("LIST");
    private void ListLayers_Click(object sender, RoutedEventArgs e) => RunCommand("LAYERS");
    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void New_Click(object sender, RoutedEventArgs e)
    {
        _app.NewDocument($"Drawing{_app.Documents.Documents.Count + 1}");
        SetTool(ToolMode.Select, "Created new drawing. Select a drawing tool or create a sample.");
        RefreshUi();
    }

    private void Sample_Click(object sender, RoutedEventArgs e) => RunBatch("RECTANG 0 0 1200 800", "LINE 0 400 1200 400", "LINE 600 0 600 800", "CIRCLE 300 200 120", "CIRCLE 900 600 120");

    private void Erase_Click(object sender, RoutedEventArgs e)
    {
        var selected = _app.Documents.ActiveDocument?.Editor.Selection.Current.ToArray() ?? Array.Empty<QS3D.Platform.Domain.CadHandle>();
        if (selected.Length == 0) { StatusText.Text = "Select one or more entities before Erase."; return; }
        RunCommand("ERASE " + string.Join(" ", selected.Select(static handle => QuoteToken(handle.ToString()))));
    }

    private void Move_Click(object sender, RoutedEventArgs e)
    {
        var selected = _app.Documents.ActiveDocument?.Editor.Selection.Current.ToArray() ?? Array.Empty<QS3D.Platform.Domain.CadHandle>();
        if (selected.Length == 0) { StatusText.Text = "Select an entity before Move."; return; }
        CommandBox.Text = $"MOVE {QuoteToken(selected[0].ToString())} 100 0"; CommandBox.SelectAll(); CommandBox.Focus();
        StatusText.Text = "Edit dx/dy in the command line, then press Enter.";
    }

    private void NewLayer_Click(object sender, RoutedEventArgs e) { CommandBox.Text = "LAYER NEW Layer1"; CommandBox.SelectAll(); CommandBox.Focus(); StatusText.Text = "Enter a new layer name and press Enter."; }
    private void SetCurrentLayer_Click(object sender, RoutedEventArgs e) { if (LayerList.SelectedItem is LayerRow row) RunCommand($"LAYER SET {QuoteToken(row.Layer.Name)}"); else StatusText.Text = "Choose a layer first."; }
    private void LayerList_MouseDoubleClick(object sender, MouseButtonEventArgs e) => SetCurrentLayer_Click(sender, e);
    private void ToggleLayerOn_Click(object sender, RoutedEventArgs e) { if (LayerList.SelectedItem is LayerRow row) RunCommand($"LAYER {(row.Layer.IsOn ? "OFF" : "ON")} {QuoteToken(row.Layer.Name)}"); else StatusText.Text = "Choose a layer first."; }
    private void ToggleLayerLock_Click(object sender, RoutedEventArgs e) { if (LayerList.SelectedItem is LayerRow row) RunCommand($"LAYER {(row.Layer.IsLocked ? "UNLOCK" : "LOCK")} {QuoteToken(row.Layer.Name)}"); else StatusText.Text = "Choose a layer first."; }

    private void Run_Click(object sender, RoutedEventArgs e) { RunCommand(CommandBox.Text); CommandBox.SelectAll(); CommandBox.Focus(); }
    private void CommandBox_KeyDown(object sender, KeyEventArgs e) { if (e.Key != Key.Enter) return; RunCommand(CommandBox.Text); CommandBox.SelectAll(); e.Handled = true; }

    private void Window_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Escape) { SetTool(ToolMode.Select, "Command cancelled. Select mode active."); e.Handled = true; return; }
        if (e.Key == Key.Delete) { Erase_Click(sender, e); e.Handled = true; return; }
        if (e.Key == Key.F5) { RefreshUi(); e.Handled = true; return; }
        if ((Keyboard.Modifiers & ModifierKeys.Control) == 0) return;
        switch (e.Key)
        {
            case Key.N: New_Click(sender, e); e.Handled = true; break;
            case Key.O: Open_Click(sender, e); e.Handled = true; break;
            case Key.S: Save_Click(sender, e); e.Handled = true; break;
            case Key.Z: Undo_Click(sender, e); e.Handled = true; break;
            case Key.Y: Redo_Click(sender, e); e.Handled = true; break;
        }
    }

    private void Save_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new SaveFileDialog { Filter = "QS3D project (*.qs3d)|*.qs3d", AddExtension = true, DefaultExt = ".qs3d" };
        if (dialog.ShowDialog(this) != true) return;
        try { _app.SaveProjectPackageWithBackup(dialog.FileName); StatusText.Text = "Saved QS3D project package."; }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private void Open_Click(object sender, RoutedEventArgs e)
    {
        var dialog = new OpenFileDialog { Filter = "QS3D project (*.qs3d)|*.qs3d", DefaultExt = ".qs3d" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            var result = _app.OpenProjectPackageWithRecovery(dialog.FileName);
            var status = result.RecoveredFromBackup ? $"Recovered QS3D project from validated backup: {result.SourcePath}" : "Opened QS3D project package.";
            SetTool(ToolMode.Select, status); RefreshUi();
        }
        catch (Exception ex) { StatusText.Text = ex.Message; }
    }

    private double ScreenX(double worldX) => _viewOffsetX + (worldX - _worldMinX) * _viewScale;
    private double ScreenY(double worldY) => ViewportCanvas.ActualHeight - _viewOffsetY - (worldY - _worldMinY) * _viewScale;
    private WorldPoint WorldFromScreen(System.Windows.Point screen) => new(_worldMinX + (screen.X - _viewOffsetX) / Math.Max(_viewScale, .000001d), _worldMinY + (ViewportCanvas.ActualHeight - screen.Y - _viewOffsetY) / Math.Max(_viewScale, .000001d));

    private static double NiceGridStep(double requested)
    {
        if (!double.IsFinite(requested) || requested <= 0d) return 100d;
        var power = Math.Pow(10d, Math.Floor(Math.Log10(requested)));
        var normalized = requested / power;
        return (normalized <= 1d ? 1d : normalized <= 2d ? 2d : normalized <= 5d ? 5d : 10d) * power;
    }

    private static double Number(CadEntitySnapshot entity, string key, double fallback)
        => entity.Properties.TryGetValue(key, out var raw) && double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) && double.IsFinite(value) ? value : fallback;

    private static string N(double value) => value.ToString("R", CultureInfo.InvariantCulture);
    private static string FormatPoint(double x, double y) => $"{x.ToString("0.###", CultureInfo.InvariantCulture)}, {y.ToString("0.###", CultureInfo.InvariantCulture)}";
    private static string QuoteToken(string value) => value.Any(char.IsWhiteSpace) || value.Contains('"') ? $"\"{value.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("\"", "\\\"", StringComparison.Ordinal)}\"" : value;
}
