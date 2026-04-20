using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace KiteTodo.Controls;

/// <summary>
/// 轻量行号绘制控件，按可见区域直接绘制文本，避免使用额外 TextBox 带来的布局和文本同步开销。
/// </summary>
public class LineNumberGutter : Control
{
    public static readonly DependencyProperty LineCountProperty = DependencyProperty.Register(
        nameof(LineCount),
        typeof(int),
        typeof(LineNumberGutter),
        new FrameworkPropertyMetadata(1, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty VerticalOffsetProperty = DependencyProperty.Register(
        nameof(VerticalOffset),
        typeof(double),
        typeof(LineNumberGutter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty LineHeightProperty = DependencyProperty.Register(
        nameof(LineHeight),
        typeof(double),
        typeof(LineNumberGutter),
        new FrameworkPropertyMetadata(20d, FrameworkPropertyMetadataOptions.AffectsRender));

    public static readonly DependencyProperty EditorPaddingTopProperty = DependencyProperty.Register(
        nameof(EditorPaddingTop),
        typeof(double),
        typeof(LineNumberGutter),
        new FrameworkPropertyMetadata(0d, FrameworkPropertyMetadataOptions.AffectsRender));

    static LineNumberGutter()
    {
        DefaultStyleKeyProperty.OverrideMetadata(typeof(LineNumberGutter), new FrameworkPropertyMetadata(typeof(LineNumberGutter)));
    }

    public int LineCount
    {
        get => (int)GetValue(LineCountProperty);
        set => SetValue(LineCountProperty, value);
    }

    public double VerticalOffset
    {
        get => (double)GetValue(VerticalOffsetProperty);
        set => SetValue(VerticalOffsetProperty, value);
    }

    public double LineHeight
    {
        get => (double)GetValue(LineHeightProperty);
        set => SetValue(LineHeightProperty, value);
    }

    public double EditorPaddingTop
    {
        get => (double)GetValue(EditorPaddingTopProperty);
        set => SetValue(EditorPaddingTopProperty, value);
    }

    protected override void OnRender(DrawingContext drawingContext)
    {
        base.OnRender(drawingContext);

        drawingContext.DrawRectangle(Background, null, new Rect(RenderSize));

        if (LineCount <= 0 || RenderSize.Width <= 0 || RenderSize.Height <= 0)
            return;

        var dpi = VisualTreeHelper.GetDpi(this).PixelsPerDip;
        var lineHeight = LineHeight > 0 ? LineHeight : FontSize * 1.45;
        var paddingTop = EditorPaddingTop;
        var paddingRight = Padding.Right;

        var startLine = Math.Max(1, (int)Math.Floor(Math.Max(0, VerticalOffset - paddingTop) / lineHeight) + 1);
        var endLine = Math.Min(LineCount, (int)Math.Ceiling((VerticalOffset + RenderSize.Height - paddingTop) / lineHeight) + 1);
        var typeface = new Typeface(FontFamily, FontStyle, FontWeight, FontStretch);

        for (var lineNumber = startLine; lineNumber <= endLine; lineNumber++)
        {
            var formattedText = new FormattedText(
                lineNumber.ToString(CultureInfo.InvariantCulture),
                CultureInfo.CurrentUICulture,
                FlowDirection.LeftToRight,
                typeface,
                FontSize,
                Foreground,
                dpi);

            var x = Math.Max(0, RenderSize.Width - paddingRight - formattedText.Width);
            var y = paddingTop + (lineNumber - 1) * lineHeight - VerticalOffset;
            drawingContext.DrawText(formattedText, new Point(x, y));
        }
    }
}