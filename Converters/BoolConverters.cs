// ============================================================================
// BoolConverters.cs - XAML 数据绑定转换器集合
//
// WPF 中 XAML 绑定的值类型和 UI 属性类型经常不匹配，
// 例如 bool 值需要转换为 Visibility、删除线、透明度等，
// 转换器（IValueConverter）就是做这个桥梁的。
//
// 使用方式：在 XAML 的 Page.Resources 中声明，然后在 Binding 中引用
// 例如: Visibility="{Binding IsCompleted, Converter={StaticResource BoolToVisConverter}}"
// ============================================================================

using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace KiteTodo.Converters;

/// <summary>
/// bool → Visibility 转换器
/// true → Visible, false → Collapsed
/// 支持 parameter="Invert" 反转逻辑
/// </summary>
public class BoolToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        bool boolValue = value is bool b && b;
        if (parameter?.ToString() == "Invert") boolValue = !boolValue;
        return boolValue ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// bool → 删除线转换器
/// true（已完成）→ 显示删除线，false → 无装饰
/// </summary>
public class BoolToStrikethroughConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? TextDecorations.Strikethrough : null!;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 优先级数字 → 颜色转换器
/// 3=红色(高), 2=橙色(中), 1=蓝色(低), 0=透明(无)
/// 用于待办卡片左侧的优先级色条
/// </summary>
public class PriorityToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int priority ? priority switch
        {
            3 => new SolidColorBrush(Color.FromRgb(239, 68, 68)),   // 红色 - 高优先级
            2 => new SolidColorBrush(Color.FromRgb(249, 115, 22)),  // 橙色 - 中优先级
            1 => new SolidColorBrush(Color.FromRgb(59, 130, 246)),  // 蓝色 - 低优先级
            _ => new SolidColorBrush(Colors.Transparent)             // 无优先级
        } : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 优先级 → 卡片背景色转换器
/// 高优先级淡红色，中优先级淡橙色，其他透明
/// </summary>
public class PriorityToBgConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int priority ? priority switch
        {
            3 => new SolidColorBrush(Color.FromArgb(20, 239, 68, 68)),   // 淡红 - 高优先级
            2 => new SolidColorBrush(Color.FromArgb(15, 249, 115, 22)),  // 淡橙 - 中优先级
            _ => new SolidColorBrush(Colors.Transparent)
        } : new SolidColorBrush(Colors.Transparent);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// bool → 透明度转换器
/// true（已完成）→ 0.5 半透明，false → 1.0 完全不透明
/// </summary>
public class BoolToOpacityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is bool b && b ? 0.5 : 1.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// null/空字符串 → Collapsed 转换器
/// 用于有值时显示控件、无值时隐藏（如描述、提醒时间为空时隐藏对应区域）
/// </summary>
public class NullToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime)
            return Visibility.Visible;
        return string.IsNullOrWhiteSpace(value?.ToString()) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 值为 null 时显示（Visible）、非 null 时隐藏（Collapsed）。
/// 用于空状态提示（如未选中笔记时显示"选择或新建笔记"）。
/// </summary>
public class NullToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value == null ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
/// <summary>
/// 整数为 0 时显示、否则隐藏（用于 "暂无待办" 空状态提示）
/// </summary>
public class ZeroToVisibleConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int count && count == 0 ? Visibility.Visible : Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 完成率（0.0~1.0）→ 像素宽度转换器
/// 用于月视图中每日进度条的宽度计算
/// </summary>
public class CompletionRateToWidthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is double rate)
            return rate * 100; // 将比率转为百分比宽度（像素），受父容器约束
        return 0.0;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 优先级数字 → 中文文本转换器
/// 3="高", 2="中", 1="低", 0="无"
/// </summary>
public class PriorityToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is int priority ? priority switch
        {
            3 => "高",
            2 => "中",
            1 => "低",
            _ => "无"
        } : "无";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// DateTime → 提醒时间显示文本转换器
/// 例如: "提醒: 04-13 09:00"
/// </summary>
public class ReminderTimeConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is DateTime dt)
            return $"提醒: {dt:MM-dd HH:mm}";
        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 将进度值 (int 0-100) 转换为显示文本。
/// 例如: "30%"
/// </summary>
public class ProgressToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is int progress)
            return $"{progress}%";
        return "0%";
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 空字符串/null → Collapsed，有值 → Visible
/// 用于标签徽章：无标签时隐藏
/// </summary>
public class EmptyStringToCollapsedConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return string.IsNullOrEmpty(value as string) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}

/// <summary>
/// 默认标签 → Collapsed 转换器
/// 默认标签隐藏删除按钮，自定义标签显示删除按钮
/// </summary>
public class DefaultTagToVisibilityConverter : IValueConverter
{
    private static readonly HashSet<string> DefaultTags = new(KiteTodo.ViewModels.HomeViewModel.DefaultTags);

    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value is string tag && DefaultTags.Contains(tag) ? Visibility.Collapsed : Visibility.Visible;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
        => throw new NotImplementedException();
}
