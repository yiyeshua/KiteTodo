// ============================================================================
// HomePage.xaml.cs - 首页（今日待办）代码后置
//
// 处理首页的各种用户交互事件，将操作委托给 HomeViewModel 处理。
// 包含：日期切换、待办增删改查、编辑对话框、快速添加等。
//
// 【事件处理模式说明】
// XAML 中的 Click="OnXxx" 会调用这里的 OnXxx 方法，
// 方法内部再调用 ViewModel 的 Command 或公共方法。
// 这是 WPF code-behind 的标准做法。
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.Models;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

public partial class HomePage : Page
{
    private readonly HomeViewModel _vm = new();
    private TodoItem? _editingItem;

    public HomePage()
    {
        InitializeComponent();
        DataContext = _vm;
        InitEditReminderCombos();
    }

    private void InitEditReminderCombos()
    {
        for (int h = 0; h < 24; h++)
            EditReminderHour.Items.Add($"{h:D2}");
        EditReminderHour.SelectedIndex = 9;

        for (int m = 0; m < 60; m += 5)
            EditReminderMinute.Items.Add($"{m:D2}");
        EditReminderMinute.SelectedIndex = 0;
    }

    private void OnPreviousDay(object sender, RoutedEventArgs e) => _vm.GoToPreviousDayCommand.Execute(null);
    private void OnNextDay(object sender, RoutedEventArgs e) => _vm.GoToNextDayCommand.Execute(null);
    private void OnGoToToday(object sender, RoutedEventArgs e) => _vm.GoToTodayCommand.Execute(null);

    private void OnPreviousWeek(object sender, RoutedEventArgs e) => _vm.GoToPreviousWeekCommand.Execute(null);
    private void OnNextWeek(object sender, RoutedEventArgs e) => _vm.GoToNextWeekCommand.Execute(null);

    private void OnWeekDayClick(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is WeekDay day)
        {
            _vm.SelectedDate = day.Date;
        }
    }

    private void OnToggleComplete(object sender, RoutedEventArgs e)
    {
        if (sender is CheckBox cb && cb.DataContext is TodoItem item)
            _vm.ToggleCompleteCommand.Execute(item);
    }

    private void OnDeleteTodo(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is TodoItem item)
            _vm.DeleteTodoCommand.Execute(item);
    }

    private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter && !string.IsNullOrWhiteSpace(QuickAddBox.Text))
        {
            _vm.QuickAdd(QuickAddBox.Text);
            QuickAddBox.Text = string.Empty;
        }
    }

    private void OnAddTodo(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(QuickAddBox.Text))
        {
            _vm.QuickAdd(QuickAddBox.Text);
            QuickAddBox.Text = string.Empty;
        }
    }

    // === Edit Dialog ===

    /// <summary>
    /// 双击待办卡片时打开编辑对话框（与点击编辑按钮效果相同）
    /// </summary>
    private void OnTodoDoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is TodoItem item)
        {
            OpenEditDialog(item);
            e.Handled = true;  // 防止事件继续冒泡
        }
    }

    private void OnEditTodo(object sender, RoutedEventArgs e)
    {
        if (sender is FrameworkElement el && el.DataContext is TodoItem item)
        {
            OpenEditDialog(item);
        }
    }

    /// <summary>
    /// 打开编辑对话框，填充当前待办的数据到表单控件中。
    /// </summary>
    private void OpenEditDialog(TodoItem item)
    {
        _editingItem = item;
        EditTitle.Text = item.Title;
        EditDescription.Text = item.Description ?? string.Empty;

        // Set priority combo
        EditPriority.SelectedIndex = item.Priority;

        // Set tag combo
        EditCategory.Items.Clear();
        EditCategory.Items.Add("");  // 空选项（无标签）
        foreach (var tag in _vm.AvailableTags)
            EditCategory.Items.Add(tag);
        EditCategory.Text = item.Category ?? string.Empty;

        // Set progress combo (index 0=0%, 1=10%, ..., 10=100%)
        EditProgress.SelectedIndex = Math.Clamp(item.Progress, 0, 100) / 10;

        // Set verification checkbox
        EditNeedsVerification.IsChecked = item.NeedsVerification;

        // Set reminder time
        if (item.ReminderTime.HasValue)
        {
            EditReminderDate.SelectedDate = item.ReminderTime.Value.Date;
            EditReminderHour.SelectedIndex = item.ReminderTime.Value.Hour;
            // Round minute to nearest 5
            var minuteIndex = item.ReminderTime.Value.Minute / 5;
            if (minuteIndex < EditReminderMinute.Items.Count)
                EditReminderMinute.SelectedIndex = minuteIndex;
        }
        else
        {
            EditReminderDate.SelectedDate = _vm.SelectedDate;
            EditReminderHour.SelectedIndex = 9;
            EditReminderMinute.SelectedIndex = 0;
        }

        EditOverlay.Visibility = Visibility.Visible;
    }

    private void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (_editingItem == null) return;

        _editingItem.Title = EditTitle.Text.Trim();
        _editingItem.Description = string.IsNullOrWhiteSpace(EditDescription.Text) ? null : EditDescription.Text.Trim();
        _editingItem.Priority = EditPriority.SelectedIndex;
        _editingItem.Category = (EditCategory.Text ?? string.Empty).Trim();

        // 如果用户输入了新标签，自动添加到自定义标签
        if (!string.IsNullOrEmpty(_editingItem.Category))
            _vm.AddCustomTag(_editingItem.Category);

        // 进度处理 + 与完成状态双向联动
        var newProgress = EditProgress.SelectedIndex * 10;
        _editingItem.Progress = newProgress;
        if (newProgress == 100 && !_editingItem.IsCompleted)
        {
            // 进度设为100%，自动标记完成
            _editingItem.CompletedAt = DateTime.Now;
        }
        else if (newProgress < 100 && _editingItem.IsCompleted)
        {
            // 进度从100%降低，自动取消完成
            _editingItem.CompletedAt = null;
        }

        // 待验证状态
        _editingItem.NeedsVerification = EditNeedsVerification.IsChecked == true;

        // Parse reminder time
        if (EditReminderDate.SelectedDate.HasValue &&
            EditReminderHour.SelectedIndex >= 0 &&
            EditReminderMinute.SelectedIndex >= 0)
        {
            var date = EditReminderDate.SelectedDate.Value.Date;
            var hour = EditReminderHour.SelectedIndex;
            var minute = EditReminderMinute.SelectedIndex * 5;
            _editingItem.ReminderTime = date.AddHours(hour).AddMinutes(minute);
        }

        _vm.UpdateTodo(_editingItem);
        _editingItem = null;
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e)
    {
        _editingItem = null;
        EditOverlay.Visibility = Visibility.Collapsed;
    }

    private void OnClearReminder(object sender, RoutedEventArgs e)
    {
        EditReminderDate.SelectedDate = null;
    }

    // ---- 导出周报功能 ----

    /// <summary>
    /// 点击"导出周报"按钮时触发。
    /// 生成格式化的周报文本，复制到系统剪贴板，并显示 3 秒的提示消息。
    /// </summary>
    private void OnExportWeeklyReport(object sender, RoutedEventArgs e)
    {
        var report = _vm.GenerateWeeklyReport();
        Clipboard.SetText(report);
        _vm.ExportMessage = "已复制到剪贴板";

        var timer = new System.Windows.Threading.DispatcherTimer
        {
            Interval = TimeSpan.FromSeconds(3)
        };
        timer.Tick += (_, _) =>
        {
            _vm.ExportMessage = string.Empty;
            timer.Stop();
        };
        timer.Start();
    }

    // ---- 标签筛选 ----

    private void OnTagFilterClick(object sender, RoutedEventArgs e)
    {
        if (sender is Button btn && btn.Content is string tag)
        {
            _vm.SelectedTag = tag;
            UpdateTagFilterVisuals();
        }
    }

    private void OnClearTagFilter(object sender, RoutedEventArgs e)
    {
        _vm.SelectedTag = null;
        UpdateTagFilterVisuals();
    }

    private void OnAddCustomTag(object sender, RoutedEventArgs e)
    {
        var dialog = new Window
        {
            Title = "添加自定义标签",
            Width = 300, Height = 150,
            WindowStartupLocation = WindowStartupLocation.CenterOwner,
            Owner = Window.GetWindow(this),
            ResizeMode = ResizeMode.NoResize
        };
        var sp = new StackPanel { Margin = new Thickness(16) };
        var tb = new TextBox { Margin = new Thickness(0, 0, 0, 12) };
        sp.Children.Add(new TextBlock { Text = "输入标签名称:", Margin = new Thickness(0, 0, 0, 8) });
        sp.Children.Add(tb);
        var btnOk = new Button { Content = "确定", HorizontalAlignment = HorizontalAlignment.Right, Padding = new Thickness(16, 4, 16, 4) };
        btnOk.Click += (_, _) => { dialog.DialogResult = true; dialog.Close(); };
        sp.Children.Add(btnOk);
        dialog.Content = sp;
        tb.Focus();

        if (dialog.ShowDialog() == true && !string.IsNullOrWhiteSpace(tb.Text))
        {
            _vm.AddCustomTag(tb.Text.Trim());
        }
    }

    /// <summary>更新标签筛选栏的视觉状态（选中高亮）</summary>
    private void UpdateTagFilterVisuals()
    {
        var isAll = _vm.SelectedTag == null;
        TagFilterAll.Background = isAll
            ? new System.Windows.Media.SolidColorBrush(System.Windows.Media.Color.FromRgb(0, 120, 212))
            : (System.Windows.Media.Brush)FindResource("CustomSubtleBg");
        TagFilterAll.Foreground = isAll
            ? System.Windows.Media.Brushes.White
            : (System.Windows.Media.Brush)FindResource("CustomFg");
    }

    // ---- 右键菜单：移动待办到本周其他天 ----

    /// <summary>
    /// 右键菜单打开时动态生成"移动到"子菜单项。
    /// 遍历当前周栏的 7 天，排除待办当前所在的那一天，其余天作为可选目标。
    /// </summary>
    private void OnTodoContextMenuOpening(object sender, ContextMenuEventArgs e)
    {
        if (sender is not FrameworkElement element || element.DataContext is not TodoItem todoItem)
            return;

        var contextMenu = element.ContextMenu;
        if (contextMenu == null) return;

        // 找到"移动到"菜单项（ContextMenu 中的第一项）
        if (contextMenu.Items[0] is not MenuItem moveToItem) return;

        // 清除旧的子菜单项
        moveToItem.Items.Clear();

        // 遍历周栏中的 7 天，为每一天生成子菜单（排除待办当前所在天）
        foreach (var day in _vm.WeekDays)
        {
            if (day.Date.Date == todoItem.ScheduledDate.Date)
                continue;  // 跳过当前天

            var menuItem = new MenuItem
            {
                Header = $"{day.DayName} ({day.DateLabel})",
                Tag = day.Date  // 将目标日期存在 Tag 中
            };
            menuItem.Click += (s, _) =>
            {
                if (s is MenuItem mi && mi.Tag is DateTime targetDate)
                {
                    _vm.MoveTodo(todoItem, targetDate);
                }
            };
            moveToItem.Items.Add(menuItem);
        }

        // 更新"待验证"菜单项文本
        if (contextMenu.Items[3] is MenuItem verifyItem)
        {
            verifyItem.Header = todoItem.NeedsVerification ? "取消待验证" : "标记待验证";
        }
    }

    // ---- 右键菜单：开始专注 ----

    /// <summary>
    /// 右键点击"开始专注"：将待办信息写入 FocusRequest，然后导航到番茄钟页面。
    /// </summary>
    private void OnStartFocus(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement el
            && el.DataContext is TodoItem item)
        {
            Helpers.FocusRequest.PendingTodoId = item.Id;
            Helpers.FocusRequest.PendingTodoTitle = item.Title;

            // 获取 MainWindow 并导航到番茄钟页面
            if (Window.GetWindow(this) is MainWindow mainWindow)
            {
                mainWindow.NavigateTo(typeof(PomodoroPage));
            }
        }
    }

    /// <summary>右键切换待验证状态</summary>
    private void OnToggleVerification(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.Parent is ContextMenu cm
            && cm.PlacementTarget is FrameworkElement el
            && el.DataContext is TodoItem item)
        {
            _vm.ToggleVerificationCommand.Execute(item);
        }
    }
}
