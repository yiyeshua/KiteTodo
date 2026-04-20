using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KiteTodo.Controls;

/// <summary>
/// 轻量当前行高亮层，固定占满编辑区域，通过自绘方式渲染高亮矩形，避免频繁调整布局属性。
/// </summary>
public class CurrentLineHighlightLayer : Control
{
    public static readonly DependencyProperty HighlightTopProperty = DependencyProperty.Register(
        nameof(HighlightTop),
        typeof(double),
        typeof(CurrentLineHighlightLayer),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty HighlightHeightProperty = DependencyProperty.Register(
        nameof(HighlightHeight),
        typeof(double),
        typeof(CurrentLineHighlightLayer),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty IsHighlightVisibleProperty = DependencyProperty.Register(
        nameof(IsHighlightVisible),
        typeof(bool),
        typeof(CurrentLineHighlightLayer),
        new FrameworkPropertyMetadata(false, FrameworkPropertyMetadataOptions.AffectsRender));

    static CurrentLineHighlightLayer()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(CurrentLineHighlightLayer), new FrameworkPropertyMetadata(typeof(CurrentLineHighlightLayer)));
    }

    public double HighlightTop
    {
        get => (double)GetValue(HighlightTopProperty);
        set => SetValue(HighlightTopProperty, value);
    }

    public double HighlightHeight
    {
        get => (double)GetValue(HighlightHeightProperty);
        set => SetValue(HighlightHeightProperty, value);
    }

    public bool IsHighlightVisible
    {
        get => (bool)GetValue(IsHighlightVisibleProperty);
        set => SetValue(IsHighlightVisibleProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        if (!IsHighlightVisible || HighlightHeight <= 0 || RenderSize.Width <= 0 || RenderSize.Height <= 0)
            return;

        var top = Math.Max(0, HighlightTop);
        var height = Math.Min(HighlightHeight, Math.Max(0, RenderSize.Height - top));
        if (height <= 0)
            return;

        var rect = new Rect(0, top, RenderSize.Width, height);
        var pen = BorderBrush is Brush borderBrush
            ? new Pen(borderBrush, BorderThickness.Top > 0 ? BorderThickness.Top : 1)
            : null;

        drawingContext.DrawRectangle(Background, pen, rect);
    }
}