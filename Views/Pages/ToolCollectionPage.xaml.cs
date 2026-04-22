using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using KiteTodo.Views.Tools;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 工具集合页面：提供工作台入口和工具宿主区域。
/// 工具卡片只是入口，实际工具界面由注册表按需创建并加载。
/// </summary>
public partial class ToolCollectionPage : Page
{
    private sealed class ToolEntry
    {
        public required string Key { get; init; }
        public required string Badge { get; init; }
        public required string Title { get; init; }
        public required string Subtitle { get; init; }
        public required Brush AccentBrush { get; init; }
        public required Func<FrameworkElement> ViewFactory { get; init; }
    }

    private readonly ObservableCollection<ToolEntry> _tools = [];
    private readonly Dictionary<string, FrameworkElement> _toolViewCache = [];
    private ToolEntry? _activeTool;

    public ToolCollectionPage()
    {
        InitializeComponent();
        ToolEntriesItemsControl.ItemsSource = _tools;
        LoadFrameworkData();
    }

    private void LoadFrameworkData()
    {
        _tools.Clear();
        _tools.Add(new ToolEntry
        {
            Key = "tool-1",
            Badge = "1",
            Title = "位值计算器",
            Subtitle = "30 位 bit 实时换算十进制和十六进制",
            AccentBrush = CreateBrush("#36C86B"),
            ViewFactory = static () => new BitValueCalculatorView()
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-2",
            Badge = "2",
            Title = "工具2",
            Subtitle = "预留入口",
            AccentBrush = CreateBrush("#3B82F6"),
            ViewFactory = () => BuildPlaceholderToolView("工具2", "这里预留给工具2，后面按同样方式接入。")
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-3",
            Badge = "3",
            Title = "工具3",
            Subtitle = "预留入口",
            AccentBrush = CreateBrush("#F59E0B"),
            ViewFactory = () => BuildPlaceholderToolView("工具3", "这里预留给工具3，后面按同样方式接入。")
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-4",
            Badge = "4",
            Title = "工具4",
            Subtitle = "预留入口",
            AccentBrush = CreateBrush("#8B5CF6"),
            ViewFactory = () => BuildPlaceholderToolView("工具4", "这里预留给工具4，后面按同样方式接入。")
        });

        ShowWorkbench();
    }

    private static Brush CreateBrush(string hex)
    {
        return (Brush)new BrushConverter().ConvertFrom(hex)!;
    }

    private void OnToolEntryClick(object sender, RoutedEventArgs e)
    {
        if (sender is not Button { Tag: ToolEntry tool })
            return;

        ShowTool(tool);
    }

    private void OnBackToWorkbench(object sender, RoutedEventArgs e)
    {
        ShowWorkbench();
    }

    private void ShowWorkbench()
    {
        _activeTool = null;
        ToolHostHeader.Visibility = Visibility.Collapsed;
        ToolHostBorder.Visibility = Visibility.Collapsed;
        WorkbenchScrollViewer.Visibility = Visibility.Visible;
        ToolHostContent.Content = null;
    }

    private void ShowTool(ToolEntry tool)
    {
        _activeTool = tool;
        CurrentToolTitleText.Text = tool.Title;
        CurrentToolSubtitleText.Text = tool.Subtitle;
        ToolHostHeader.Visibility = Visibility.Visible;
        ToolHostBorder.Visibility = Visibility.Visible;
        WorkbenchScrollViewer.Visibility = Visibility.Collapsed;

        if (!_toolViewCache.TryGetValue(tool.Key, out var view))
        {
            view = tool.ViewFactory();
            _toolViewCache[tool.Key] = view;
        }

        ToolHostContent.Content = view;
    }

    private static FrameworkElement BuildPlaceholderToolView(string title, string description)
    {
        var card = new Border
        {
            Margin = new Thickness(18),
            Padding = new Thickness(24),
            CornerRadius = new CornerRadius(14),
            BorderThickness = new Thickness(1),
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#D9E1EC")!,
            Background = Brushes.White
        };

        var stack = new StackPanel();
        stack.Children.Add(new TextBlock
        {
            Text = title,
            FontSize = 22,
            FontWeight = FontWeights.SemiBold,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#1F2937")!
        });
        stack.Children.Add(new TextBlock
        {
            Text = description,
            Margin = new Thickness(0, 10, 0, 0),
            FontSize = 13,
            TextWrapping = TextWrapping.Wrap,
            Foreground = (Brush)new BrushConverter().ConvertFrom("#64748B")!
        });
        stack.Children.Add(new Border
        {
            Margin = new Thickness(0, 22, 0, 0),
            Padding = new Thickness(18, 16, 18, 16),
            CornerRadius = new CornerRadius(12),
            Background = (Brush)new BrushConverter().ConvertFrom("#F8FAFC")!,
            BorderBrush = (Brush)new BrushConverter().ConvertFrom("#E2E8F0")!,
            BorderThickness = new Thickness(1),
            Child = new TextBlock
            {
                Text = "这里已经是独立工具视图区域，不是单纯的 MessageBox 占位。后面工具1、工具2、工具3都可以各自挂自己的页面或控件。",
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                Foreground = (Brush)new BrushConverter().ConvertFrom("#475569")!
            }
        });

        card.Child = stack;
        return card;
    }
}