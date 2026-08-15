using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using QS3D.Platform.Geometry;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private bool _precisionHandlersAttached;
    private bool _objectSnapEnabled = true;
    private bool _orthoEnabled;
    private bool _gridSnapEnabled;
    private Shape? _precisionMarker;

    protected override void OnContentRendered(EventArgs e)
    {
        base.OnContentRendered(e);
        if (_precisionHandlersAttached) return;
        _precisionHandlersAttached = true;

        ViewportCanvas.PreviewMouseLeftButtonDown += PrecisionViewport_PreviewMouseLeftButtonDown;
        ViewportCanvas.PreviewMouseMove += PrecisionViewport_PreviewMouseMove;
        PreviewKeyDown += PrecisionWindow_PreviewKeyDown;
        SelectToolButton.Click += PrecisionToolButton_Click;
        LineToolButton.Click += PrecisionToolButton_Click;
        RectangleToolButton.Click += PrecisionToolButton_Click;
        CircleToolButton.Click += PrecisionToolButton_Click;
        UpdatePrecisionToolStatus();
    }

    private void PrecisionToolButton_Click(object sender, RoutedEventArgs e)
        => UpdatePrecisionToolStatus();

    private void PrecisionWindow_PreviewKeyDown(object sender, KeyEventArgs e)
    {
        switch (e.Key)
        {
            case Key.F3:
                _objectSnapEnabled = !_objectSnapEnabled;
                StatusText.Text = $"Object snap {OnOff(_objectSnapEnabled)}. Supported reference snaps: endpoint, midpoint, center, quadrant, intersection, nearest.";
                e.Handled = true;
                break;
            case Key.F8:
                _orthoEnabled = !_orthoEnabled;
                StatusText.Text = $"ORTHO {OnOff(_orthoEnabled)}.";
                e.Handled = true;
                break;
            case Key.F9:
                _gridSnapEnabled = !_gridSnapEnabled;
                StatusText.Text = $"Grid snap {OnOff(_gridSnapEnabled)} at current visible grid spacing {CurrentGridSpacing():0.###}.";
                e.Handled = true;
                break;
            default:
                return;
        }
        ClearPrecisionMarker();
        UpdatePrecisionToolStatus();
    }

    private void PrecisionViewport_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (_tool == ToolMode.Select) return;
        var document = _app.Documents.ActiveDocument;
        if (document is null) return;

        try
        {
            var resolved = ResolvePrecisionPoint(e.GetPosition(ViewportCanvas));
            var point = new WorldPoint(resolved.Point.X, resolved.Point.Y);
            if (_pendingPoint is null)
            {
                _pendingPoint = point;
                StatusText.Text = _tool == ToolMode.Circle
                    ? $"Circle center: {FormatPoint(point.X, point.Y)}{DescribeResolution(resolved)}. Pick radius point."
                    : $"First point: {FormatPoint(point.X, point.Y)}{DescribeResolution(resolved)}. Pick second point.";
                DrawPrecisionMarker(resolved);
                e.Handled = true;
                return;
            }

            var first = _pendingPoint.Value;
            _pendingPoint = null;
            ClearPrecisionMarker();
            if (_tool == ToolMode.Line)
            {
                RunCommand($"LINE {N(first.X)} {N(first.Y)} {N(point.X)} {N(point.Y)}");
            }
            else if (_tool == ToolMode.Rectangle)
            {
                RunCommand($"RECTANG {N(first.X)} {N(first.Y)} {N(point.X)} {N(point.Y)}");
            }
            else
            {
                var radius = ReferencePrecisionInput.Distance2D(new Point3(first.X, first.Y), resolved.Point);
                if (!double.IsFinite(radius) || radius <= .000001d)
                {
                    StatusText.Text = "Circle radius must be finite and greater than zero.";
                    e.Handled = true;
                    return;
                }
                RunCommand($"CIRCLE {N(first.X)} {N(first.Y)} {N(radius)}");
            }
            e.Handled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            StatusText.Text = $"Precision input rejected: {ex.Message}";
            e.Handled = true;
        }
    }

    private void PrecisionViewport_PreviewMouseMove(object sender, MouseEventArgs e)
    {
        if (_tool == ToolMode.Select) return;
        try
        {
            var resolved = ResolvePrecisionPoint(e.GetPosition(ViewportCanvas));
            CursorStatusText.Text = $"X: {resolved.Point.X:0.###}   Y: {resolved.Point.Y:0.###}{DescribeResolution(resolved)}";
            DrawPrecisionMarker(resolved);
            e.Handled = true;
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or OverflowException)
        {
            ClearPrecisionMarker();
            CursorStatusText.Text = $"Precision input unavailable: {ex.Message}";
            e.Handled = true;
        }
    }

    private ReferencePrecisionResult ResolvePrecisionPoint(System.Windows.Point screenPoint)
    {
        var document = _app.Documents.ActiveDocument
            ?? throw new InvalidOperationException("No active drawing.");
        var raw = WorldFromScreen(screenPoint);
        Point3? anchor = _pendingPoint is null
            ? null
            : new Point3(_pendingPoint.Value.X, _pendingPoint.Value.Y);
        var apertureWorld = 12d / Math.Max(_viewScale, .000001d);
        var settings = new ReferencePrecisionSettings(
            ObjectSnapEnabled: _objectSnapEnabled,
            OrthoEnabled: _orthoEnabled,
            GridSnapEnabled: _gridSnapEnabled,
            GridSpacing: CurrentGridSpacing(),
            SnapKinds: ReferencePrecisionInput.DefaultSnapKinds);
        return ReferencePrecisionInput.Resolve(
            document,
            new Point3(raw.X, raw.Y),
            anchor,
            apertureWorld,
            settings);
    }

    private double CurrentGridSpacing()
    {
        var spanX = _worldMaxX - _worldMinX;
        var spanY = _worldMaxY - _worldMinY;
        var span = Math.Max(spanX, spanY);
        return NiceGridStep(span / 12d);
    }

    private void DrawPrecisionMarker(ReferencePrecisionResult result)
    {
        ClearPrecisionMarker();
        if (result.Snap is null) return;
        var size = 10d;
        var marker = new Rectangle
        {
            Width = size,
            Height = size,
            Stroke = new SolidColorBrush(Color.FromRgb(255, 190, 73)),
            StrokeThickness = 1.5d,
            Fill = Brushes.Transparent,
            IsHitTestVisible = false
        };
        Canvas.SetLeft(marker, ScreenX(result.Point.X) - size * .5d);
        Canvas.SetTop(marker, ScreenY(result.Point.Y) - size * .5d);
        ViewportCanvas.Children.Add(marker);
        _precisionMarker = marker;
    }

    private void ClearPrecisionMarker()
    {
        if (_precisionMarker is not null && ViewportCanvas.Children.Contains(_precisionMarker))
            ViewportCanvas.Children.Remove(_precisionMarker);
        _precisionMarker = null;
    }

    private void UpdatePrecisionToolStatus()
    {
        ToolStatusText.Text = $"Tool: {_tool} | OSNAP {OnOff(_objectSnapEnabled)} | ORTHO {OnOff(_orthoEnabled)} | GRID {OnOff(_gridSnapEnabled)}";
    }

    private static string DescribeResolution(ReferencePrecisionResult result)
    {
        if (result.Snap is not null)
            return $" [{result.Snap.Kind} {result.Snap.Handle}]";
        if (result.OrthoApplied && result.GridApplied) return " [GRID+ORTHO]";
        if (result.OrthoApplied) return " [ORTHO]";
        if (result.GridApplied) return " [GRID]";
        return string.Empty;
    }

    private static string OnOff(bool value) => value ? "ON" : "OFF";
}
