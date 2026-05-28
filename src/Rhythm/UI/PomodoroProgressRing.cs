using System;
using System.Windows;
using System.Windows.Media;

namespace Rhythm.UI;

public sealed class PomodoroProgressRing : FrameworkElement
{
    public static readonly DependencyProperty ProgressProperty =
        DependencyProperty.Register(
            nameof(Progress),
            typeof(double),
            typeof(PomodoroProgressRing),
            new FrameworkPropertyMetadata(1.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty ProgressBrushProperty =
        DependencyProperty.Register(
            nameof(ProgressBrush),
            typeof(Brush),
            typeof(PomodoroProgressRing),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty TrackBrushProperty =
        DependencyProperty.Register(
            nameof(TrackBrush),
            typeof(Brush),
            typeof(PomodoroProgressRing),
            new FrameworkPropertyMetadata(Brushes.Transparent, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty StrokeThicknessProperty =
        DependencyProperty.Register(
            nameof(StrokeThickness),
            typeof(double),
            typeof(PomodoroProgressRing),
            new FrameworkPropertyMetadata(12.0, FrameworkPropertyMetadataOptions.AffectsRender));

    public double Progress
    {
        get => (double)GetValue(ProgressProperty);
        set => SetValue(ProgressProperty, value);
    }

    public Brush ProgressBrush
    {
        get => (Brush)GetValue(ProgressBrushProperty);
        set => SetValue(ProgressBrushProperty, value);
    }

    public Brush TrackBrush
    {
        get => (Brush)GetValue(TrackBrushProperty);
        set => SetValue(TrackBrushProperty, value);
    }

    public double StrokeThickness
    {
        get => (double)GetValue(StrokeThicknessProperty);
        set => SetValue(StrokeThicknessProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        var strokeThickness = Math.Max(1, StrokeThickness);
        var radius = Math.Max(0, (Math.Min(ActualWidth, ActualHeight) - strokeThickness) / 2);
        if (radius <= 0)
            return;

        var center = new Point(ActualWidth / 2, ActualHeight / 2);
        var trackPen = new Pen(TrackBrush, strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };
        drawingContext.DrawEllipse(null, trackPen, center, radius, radius);

        var progress = double.IsFinite(Progress) ? Math.Clamp(Progress, 0, 1) : 0;
        if (progress <= 0)
            return;

        var progressPen = new Pen(ProgressBrush, strokeThickness)
        {
            StartLineCap = PenLineCap.Round,
            EndLineCap = PenLineCap.Round,
        };

        if (progress >= 0.999)
        {
            drawingContext.DrawEllipse(null, progressPen, center, radius, radius);
            return;
        }

        var startAngle = -90.0;
        var endAngle = startAngle + (360.0 * progress);
        var startPoint = PointOnCircle(center, radius, startAngle);
        var endPoint = PointOnCircle(center, radius, endAngle);
        var isLargeArc = progress > 0.5;

        var geometry = new StreamGeometry();
        using (var context = geometry.Open())
        {
            context.BeginFigure(startPoint, isFilled: false, isClosed: false);
            context.ArcTo(
                endPoint,
                new Size(radius, radius),
                rotationAngle: 0,
                isLargeArc,
                SweepDirection.Clockwise,
                isStroked: true,
                isSmoothJoin: false);
        }

        geometry.Freeze();
        drawingContext.DrawGeometry(null, progressPen, geometry);
    }

    private static Point PointOnCircle(Point center, double radius, double angleDegrees)
    {
        var radians = angleDegrees * Math.PI / 180.0;
        return new Point(
            center.X + (radius * Math.Cos(radians)),
            center.Y + (radius * Math.Sin(radians)));
    }
}
