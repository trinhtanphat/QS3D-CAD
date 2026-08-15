using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using QS3D.Cad.Host;
using QS3D.Platform.Cad.Abstractions;
using WpfEllipse = System.Windows.Shapes.Ellipse;
using WpfPath = System.Windows.Shapes.Path;
using WpfPolygon = System.Windows.Shapes.Polygon;

namespace QS3D.Cad.Desktop;

public partial class MainWindow
{
    private const string ReferencePrimitiveRenderTag = "QS3D.ReferencePrimitiveRenderer";
    private bool _referencePrimitiveRenderPass;

    protected override void OnInitialized(EventArgs e)
    {
        base.OnInitialized(e);
        ViewportCanvas.LayoutUpdated += ReferencePrimitiveViewport_LayoutUpdated;
    }

    private void ReferencePrimitiveViewport_LayoutUpdated(object? sender, EventArgs e)
    {
        if (_referencePrimitiveRenderPass || _app.Documents.ActiveDocument is not ICadDocument document) return;
        _referencePrimitiveRenderPass = true;
        try
        {
            using var tx = document.Database.BeginTransaction(CadTransactionMode.ReadOnly);
            var selected = document.Editor.Selection.Current.ToHashSet();
            foreach (var entity in tx.Query())
            {
                if (!IsReferencePrimitive(entity)) continue;
                if (HasReferencePrimitiveShape(entity)) continue;
                RemoveFallbackShape(entity);
                var shape = CreateReferencePrimitiveShape(entity, selected.Contains(entity.Handle));
                if (shape is null) continue;
                shape.Tag = $"{ReferencePrimitiveRenderTag}:{entity.Handle}";
                shape.Cursor = System.Windows.Input.Cursors.Hand;
                shape.ToolTip = $"{entity.Kind} {entity.Handle} • Layer {entity.LayerName}";
                shape.MouseLeftButtonDown += (_, args) => { SelectEntity(entity); args.Handled = true; };
                ViewportCanvas.Children.Add(shape);
            }
        }
        finally
        {
            _referencePrimitiveRenderPass = false;
        }
    }

    private static bool IsReferencePrimitive(CadEntitySnapshot entity)
        => ReferencePrimitiveGeometry.TryGetArc(entity, out _)
            || ReferencePrimitiveGeometry.TryGetPoint(entity, out _)
            || ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out _);

    private bool HasReferencePrimitiveShape(CadEntitySnapshot entity)
        => ViewportCanvas.Children
            .OfType<Shape>()
            .Any(shape => StringComparer.Ordinal.Equals(shape.Tag as string, $"{ReferencePrimitiveRenderTag}:{entity.Handle}"));

    private void RemoveFallbackShape(CadEntitySnapshot entity)
    {
        var tooltip = $"{entity.Kind} {entity.Handle} • Layer {entity.LayerName}";
        var fallback = ViewportCanvas.Children
            .OfType<Shape>()
            .FirstOrDefault(shape => shape.Tag is null && StringComparer.Ordinal.Equals(shape.ToolTip as string, tooltip));
        if (fallback is not null)
            ViewportCanvas.Children.Remove(fallback);
    }

    private Shape? CreateReferencePrimitiveShape(CadEntitySnapshot entity, bool isSelected)
    {
        var stroke = isSelected
            ? new SolidColorBrush(Color.FromRgb(255, 190, 73))
            : new SolidColorBrush(Color.FromRgb(50, 187, 255));
        var thickness = isSelected ? 3d : 2d;

        if (ReferencePrimitiveGeometry.TryGetArc(entity, out var arc))
        {
            var start = arc.StartPoint;
            var end = arc.EndPoint;
            var figure = new PathFigure
            {
                StartPoint = new System.Windows.Point(ScreenX(start.X), ScreenY(start.Y)),
                IsClosed = false,
                IsFilled = false
            };
            figure.Segments.Add(new ArcSegment
            {
                Point = new System.Windows.Point(ScreenX(end.X), ScreenY(end.Y)),
                Size = new Size(Math.Max(1d, arc.Radius * _viewScale), Math.Max(1d, arc.Radius * _viewScale)),
                RotationAngle = 0d,
                IsLargeArc = Math.Abs(arc.SweepAngleDegrees) > 180d,
                SweepDirection = arc.SweepAngleDegrees > 0d ? SweepDirection.Counterclockwise : SweepDirection.Clockwise,
                IsStroked = true
            });
            return new WpfPath
            {
                Data = new PathGeometry(new[] { figure }),
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = Brushes.Transparent
            };
        }

        if (ReferencePrimitiveGeometry.TryGetPoint(entity, out var point))
        {
            const double size = 9d;
            var ellipse = new WpfEllipse
            {
                Width = size,
                Height = size,
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = isSelected ? new SolidColorBrush(Color.FromArgb(60, 255, 190, 73)) : Brushes.Transparent
            };
            Canvas.SetLeft(ellipse, ScreenX(point.X) - size * .5d);
            Canvas.SetTop(ellipse, ScreenY(point.Y) - size * .5d);
            return ellipse;
        }

        if (ReferencePrimitiveGeometry.TryGetRegularPolygon(entity, out var polygon))
        {
            var shape = new WpfPolygon
            {
                Stroke = stroke,
                StrokeThickness = thickness,
                Fill = new SolidColorBrush(Color.FromArgb(18, 25, 167, 255))
            };
            foreach (var vertex in polygon.Vertices)
                shape.Points.Add(new System.Windows.Point(ScreenX(vertex.X), ScreenY(vertex.Y)));
            return shape;
        }

        return null;
    }
}
