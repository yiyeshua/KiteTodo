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
            Title = "二维码助手",
            Subtitle = "二维码生成、中心 Logo 叠加与图片解析",
            AccentBrush = CreateBrush("#3B82F6"),
            ViewFactory = static () => new QrToolView()
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-3",
            Badge = "3",
            Title = "网络电台",
            Subtitle = "分页电台列表、双击播放暂停与新增源校验",
            AccentBrush = CreateBrush("#F59E0B"),
            ViewFactory = static () => NoiseAndRadioToolView.GetSharedInstance()
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-4",
            Badge = "4",
            Title = "文件摘要校验",
            Subtitle = "计算 MD5、SHA1、SHA256、SHA384、SHA512",
            AccentBrush = CreateBrush("#8B5CF6"),
            ViewFactory = static () => new FileHashToolView()
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-5",
            Badge = "5",
            Title = "导航大全",
            Subtitle = "分类浏览常用在线工具，并支持内嵌网页预览",
            AccentBrush = CreateBrush("#EC4899"),
            ViewFactory = static () => new NavigationDirectoryToolView()
        });
        _tools.Add(new ToolEntry
        {
            Key = "tool-6",
            Badge = "6",
            Title = "环境音",
            Subtitle = "雨声、海浪、篝火等自然混音，助眠放松专注",
            AccentBrush = CreateBrush("#10B981"),
            ViewFactory = AmbientSoundToolView.GetSharedInstance
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

        // 返回工作台时分离当前工具视图（单例工具不会被销毁）
        if (ToolHostContent.Content is FrameworkElement currentView)
        {
            ToolHostContent.Content = null;
            // 从逻辑树中移除，但单例引用保持其存活
            if (currentView.Parent is ContentControl)
                ((ContentControl)currentView.Parent).Content = null;
        }
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
            try
            {
                view = tool.ViewFactory();
                _toolViewCache[tool.Key] = view;
            }
            catch (Exception ex)
            {
                MessageBox.Show($"打开工具失败：{ex.Message}", tool.Title, MessageBoxButton.OK, MessageBoxImage.Warning);
                ShowWorkbench();
                return;
            }
        }

        // 处理单例工具在页面切换后重新挂载的情况
        if (view.Parent is ContentControl oldHost && oldHost != ToolHostContent)
            oldHost.Content = null;

        ToolHostContent.Content = view;
    }
}