using System.Windows;
using System.Windows.Documents;
using System.Windows.Media;

namespace Rhythm.UI;

public sealed class DropLineAdorner : Adorner
{
    private readonly Pen _pen;
    private double _y;
    private bool _visible;

    public DropLineAdorner(UIElement adornedElement, Brush brush) : base(adornedElement)
    {
        _pen = new Pen(brush, 2.0);
        _pen.Freeze();
        IsHitTestVisible = false;
    }

    public void Show(double y)
    {
        _y = y;
        _visible = true;
        InvalidateVisual();
    }

    public void Hide()
    {
        if (!_visible) return;
        _visible = false;
        InvalidateVisual();
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        if (!_visible) return;
        var width = AdornedElement.RenderSize.Width;
        drawingContext.DrawLine(_pen, new Point(0, _y), new Point(width, _y));
    }
}
