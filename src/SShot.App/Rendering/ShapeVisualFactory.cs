using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using SShot.Core.Annotation;
using SShot.Core.Annotation.Rendering;
using SShot.Core.Annotation.Shapes;

namespace SShot.App.Rendering;

/// <summary>
/// Builds the WPF visual for a shape model. Mosaic/Blur bake their pixel effect once here
/// (via Skia, cropped from the base image) and are shown as a plain Image from then on - so
/// the final RenderTargetBitmap flatten (see EditorViewModel.Flatten) needs no special-casing
/// per shape type, it just renders whatever's on the canvas.
/// </summary>
internal static class ShapeVisualFactory
{
    public static UIElement Create(AnnotationShapeBase shape, BitmapSource baseImage, ShapePatchCache patchCache)
    {
        return shape switch
        {
            RectangleShape r => CreateRectangle(r),
            EllipseShape e => CreateEllipse(e),
            ArrowShape a => CreateArrow(a),
            FreehandShape f => CreateFreehand(f),
            HighlighterShape h => CreateHighlighter(h),
            TextShape t => CreateText(t),
            StepStampShape s => CreateStepStamp(s),
            MosaicShape m => CreateMosaic(m, baseImage, patchCache),
            BlurShape b => CreateBlur(b, baseImage, patchCache),
            _ => throw new NotSupportedException($"Unknown shape type: {shape.GetType().Name}"),
        };
    }

    private static UIElement CreateRectangle(RectangleShape shape)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(0, shape.Bounds.Width),
            Height = Math.Max(0, shape.Bounds.Height),
            Stroke = new SolidColorBrush(shape.StrokeColor),
            StrokeThickness = shape.StrokeThickness,
            Fill = Brushes.Transparent,
        };
        Canvas.SetLeft(rect, shape.Bounds.X);
        Canvas.SetTop(rect, shape.Bounds.Y);
        return rect;
    }

    private static UIElement CreateEllipse(EllipseShape shape)
    {
        var ellipse = new Ellipse
        {
            Width = Math.Max(0, shape.Bounds.Width),
            Height = Math.Max(0, shape.Bounds.Height),
            Stroke = new SolidColorBrush(shape.StrokeColor),
            StrokeThickness = shape.StrokeThickness,
            Fill = Brushes.Transparent,
        };
        Canvas.SetLeft(ellipse, shape.Bounds.X);
        Canvas.SetTop(ellipse, shape.Bounds.Y);
        return ellipse;
    }

    private static UIElement CreateArrow(ArrowShape shape)
    {
        return new Path
        {
            Data = ArrowGeometryBuilder.Build(shape.Start, shape.End, shape.StrokeThickness),
            Stroke = new SolidColorBrush(shape.StrokeColor),
            Fill = new SolidColorBrush(shape.StrokeColor),
            StrokeThickness = shape.StrokeThickness,
        };
    }

    private static UIElement CreateFreehand(FreehandShape shape)
    {
        if (shape.Points.Count == 0)
        {
            return new Path();
        }

        var figure = new PathFigure { StartPoint = shape.Points[0], IsClosed = false };
        for (int i = 1; i < shape.Points.Count; i++)
        {
            figure.Segments.Add(new LineSegment(shape.Points[i], true));
        }

        return new Path
        {
            Data = new PathGeometry(new[] { figure }),
            Stroke = new SolidColorBrush(shape.StrokeColor),
            StrokeThickness = shape.StrokeThickness,
            StrokeStartLineCap = PenLineCap.Round,
            StrokeEndLineCap = PenLineCap.Round,
            StrokeLineJoin = PenLineJoin.Round,
        };
    }

    private static UIElement CreateHighlighter(HighlighterShape shape)
    {
        var rect = new Rectangle
        {
            Width = Math.Max(0, shape.Bounds.Width),
            Height = Math.Max(0, shape.Bounds.Height),
            Fill = new SolidColorBrush(shape.FillColor),
        };
        Canvas.SetLeft(rect, shape.Bounds.X);
        Canvas.SetTop(rect, shape.Bounds.Y);
        return rect;
    }

    private static UIElement CreateText(TextShape shape)
    {
        var typeface = new Typeface("Segoe UI");
        var formatted = new FormattedText(
            string.IsNullOrEmpty(shape.Text) ? " " : shape.Text,
            CultureInfo.CurrentUICulture,
            FlowDirection.LeftToRight,
            typeface,
            shape.FontSize,
            new SolidColorBrush(shape.StrokeColor),
            96);
        shape.MeasuredSize = new Size(Math.Max(1, formatted.Width), Math.Max(1, formatted.Height));

        var textBlock = new TextBlock
        {
            Text = shape.Text,
            FontSize = shape.FontSize,
            FontFamily = typeface.FontFamily,
            Foreground = new SolidColorBrush(shape.StrokeColor),
        };
        Canvas.SetLeft(textBlock, shape.Position.X);
        Canvas.SetTop(textBlock, shape.Position.Y);
        return textBlock;
    }

    private static UIElement CreateStepStamp(StepStampShape shape)
    {
        double diameter = shape.Radius * 2;
        var ellipse = new Ellipse
        {
            Width = diameter,
            Height = diameter,
            Fill = new SolidColorBrush(shape.FillColor),
        };
        var text = new TextBlock
        {
            Text = shape.Number.ToString(CultureInfo.InvariantCulture),
            Foreground = Brushes.White,
            FontSize = Math.Max(10, shape.Radius),
            FontWeight = FontWeights.Bold,
            HorizontalAlignment = HorizontalAlignment.Center,
            VerticalAlignment = VerticalAlignment.Center,
        };

        var container = new Grid { Width = diameter, Height = diameter };
        container.Children.Add(ellipse);
        container.Children.Add(text);

        Canvas.SetLeft(container, shape.Center.X - shape.Radius);
        Canvas.SetTop(container, shape.Center.Y - shape.Radius);
        return container;
    }

    private static UIElement CreateMosaic(MosaicShape shape, BitmapSource baseImage, ShapePatchCache patchCache)
    {
        var clamped = ClampToImage(shape.Bounds, baseImage);
        if (clamped.Region.Width <= 0 || clamped.Region.Height <= 0)
        {
            return new Canvas();
        }

        var patch = patchCache.GetOrCreate(
            shape.Id, clamped.Region, shape.BlockSize,
            () => SkiaMosaicRenderer.Apply(baseImage, clamped.Region, shape.BlockSize));
        var image = new Image { Source = patch, Width = clamped.Display.Width, Height = clamped.Display.Height };
        Canvas.SetLeft(image, clamped.Display.X);
        Canvas.SetTop(image, clamped.Display.Y);
        return image;
    }

    private static UIElement CreateBlur(BlurShape shape, BitmapSource baseImage, ShapePatchCache patchCache)
    {
        var clamped = ClampToImage(shape.Bounds, baseImage);
        if (clamped.Region.Width <= 0 || clamped.Region.Height <= 0)
        {
            return new Canvas();
        }

        var patch = patchCache.GetOrCreate(
            shape.Id, clamped.Region, shape.Radius,
            () => SkiaBlurRenderer.Apply(baseImage, clamped.Region, shape.Radius));
        var image = new Image { Source = patch, Width = clamped.Display.Width, Height = clamped.Display.Height };
        Canvas.SetLeft(image, clamped.Display.X);
        Canvas.SetTop(image, clamped.Display.Y);
        return image;
    }

    /// <summary>
    /// Clamps shape bounds to the image's pixel rect and rounds once, so the Skia crop region
    /// and the on-canvas placement always agree. Previously each was rounded/clamped
    /// independently: a shape dragged past the canvas's top/left edge (reachable via
    /// CaptureMouse, which keeps delivering mouse-move events outside the Canvas) clamped the
    /// crop's X/Y to 0 but left the displayed Width/Height/position at the unclamped bounds,
    /// shifting the baked patch away from where it was dragged - or, if the shape was entirely
    /// off-edge, collapsing the crop to nothing while the shape still looked selectable.
    /// </summary>
    private static (Int32Rect Region, Rect Display) ClampToImage(Rect bounds, BitmapSource baseImage)
    {
        double left = Math.Max(0, bounds.Left);
        double top = Math.Max(0, bounds.Top);
        double right = Math.Min(baseImage.PixelWidth, bounds.Right);
        double bottom = Math.Min(baseImage.PixelHeight, bounds.Bottom);

        int x = (int)Math.Round(left);
        int y = (int)Math.Round(top);
        int width = Math.Max(0, (int)Math.Round(right) - x);
        int height = Math.Max(0, (int)Math.Round(bottom) - y);

        return (new Int32Rect(x, y, width, height), new Rect(x, y, width, height));
    }
}
