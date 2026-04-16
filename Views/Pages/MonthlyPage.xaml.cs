// ============================================================================
// MonthlyPage.xaml.cs — 月视图页面的代码后台 (Code-Behind)
// ============================================================================
// 功能说明：
//   月视图页面，以日历网格（7列 x 6行）的形式展示整月的待办概览。
//   支持以下交互操作：
//   - 前一月 / 后一月 / 回到本月 的导航
//   - 点击某一天的单元格，弹出该天的待办详情浮层
//   - 点击浮层外部区域或关闭按钮来关闭详情浮层
//
// WPF 知识点：
//   - MouseButtonEventArgs 用于处理鼠标点击事件（比 RoutedEventArgs 多了鼠标按键信息）
//   - e.Handled = true 可以阻止事件继续冒泡到父元素
//   - FrameworkElement 是所有 WPF 控件的基类，可用于统一获取 DataContext
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 月视图页面，以日历网格展示每月待办概览，支持点击查看某天详情。
/// </summary>
public partial class MonthlyPage : Page
{
    /// <summary>月视图的 ViewModel 实例</summary>
    private readonly MonthlyViewModel _vm = new();

    public MonthlyPage()
    {
        InitializeComponent();  // 加载 MonthlyPage.xaml
        DataContext = _vm;       // 绑定 ViewModel
    }

    // ---- 月份导航按钮的事件回调 ----

    /// <summary>切换到上一个月</summary>
    private void OnPreviousMonth(object sender, RoutedEventArgs e) => _vm.PreviousMonthCommand.Execute(null);

    /// <summary>切换到下一个月</summary>
    private void OnNextMonth(object sender, RoutedEventArgs e) => _vm.NextMonthCommand.Execute(null);

    /// <summary>回到当前月份</summary>
    private void OnGoToCurrentMonth(object sender, RoutedEventArgs e) => _vm.GoToCurrentMonthCommand.Execute(null);

    // ---- 日历单元格交互 ----

    /// <summary>
    /// 点击日历中的某个日期单元格时触发。
    /// 通过控件的 DataContext 获取对应的 DayCellInfo 数据，然后调用 ViewModel 显示该天详情。
    /// e.Handled = true 防止事件冒泡到外层容器（否则会触发关闭详情）。
    /// </summary>
    private void OnDayCellClick(object sender, MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is DayCellInfo day)
        {
            _vm.SelectDay(day);   // 选中该天，弹出详情浮层
            e.Handled = true;     // 阻止冒泡
        }
    }

    // ---- 详情浮层关闭逻辑 ----

    /// <summary>
    /// 点击详情浮层的半透明遮罩层（外部区域）时关闭浮层。
    /// 这是一个常见的"点击空白处关闭弹窗"模式。
    /// </summary>
    private void OnCloseDayDetail(object sender, MouseButtonEventArgs e)
    {
        _vm.CloseDayDetail();
    }

    /// <summary>
    /// 点击详情浮层的内部内容区域时，阻止事件冒泡。
    /// 如果不阻止，点击事件会冒泡到遮罩层，导致浮层被意外关闭。
    /// </summary>
    private void OnDayDetailInnerClick(object sender, MouseButtonEventArgs e)
    {
        e.Handled = true;  // 阻止冒泡到遮罩层
    }

    /// <summary>点击详情浮层右上角的"关闭"按钮时触发</summary>
    private void OnCloseDayDetailBtn(object sender, RoutedEventArgs e)
    {
        _vm.CloseDayDetail();
    }

    /// <summary>点击"添加"按钮新增待办</summary>
    private void OnAddTodoClick(object sender, RoutedEventArgs e)
    {
        _vm.AddTodoToSelectedDayCommand.Execute(null);
    }

    /// <summary>输入框回车新增待办</summary>
    private void OnNewTodoKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.AddTodoToSelectedDayCommand.Execute(null);
            e.Handled = true;
        }
    }
}
