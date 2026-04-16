// ============================================================================
// BacklogPage.xaml.cs — 事项池页面的代码后台
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using KiteTodo.Models;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 事项池页面，管理待排期事项。
/// 支持筛选、快速添加、编辑、标记状态、转待办排期。
/// </summary>
public partial class BacklogPage : Page
{
    private readonly BacklogViewModel _vm = new();
    private BacklogItem? _editingItem;
    private BacklogItem? _schedulingItem;

    public BacklogPage()
    {
        InitializeComponent();
        DataContext = _vm;
        LoadCategoryItems();
    }

    private void LoadCategoryItems()
    {
        EditCategory.Items.Clear();
        foreach (var tag in HomeViewModel.DefaultTags)
            EditCategory.Items.Add(tag);

        var settings = Services.DatabaseService.Instance.GetSettings();
        if (settings.CustomTags != null)
        {
            foreach (var tag in settings.CustomTags)
                EditCategory.Items.Add(tag);
        }
    }

    // ========== 筛选 ==========

    private void OnFilterChanged(object sender, RoutedEventArgs e)
    {
        if (sender == FilterAll)
            _vm.FilterStatus = -1;
        else if (sender == FilterActive)
            _vm.FilterStatus = -2;
        else if (sender == FilterPending)
            _vm.FilterStatus = BacklogItem.StatusPending;
        else if (sender == FilterWaiting)
            _vm.FilterStatus = BacklogItem.StatusWaiting;
        else if (sender == FilterScheduled)
            _vm.FilterStatus = BacklogItem.StatusScheduled;
    }

    // ========== 快速添加 ==========

    private void OnQuickAdd(object sender, RoutedEventArgs e)
    {
        _vm.QuickAddCommand.Execute(null);
        QuickAddBox.Focus();
    }

    private void OnQuickAddKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.Enter)
        {
            _vm.QuickAddCommand.Execute(null);
            e.Handled = true;
        }
    }

    // ========== 右键菜单 ==========

    private void OnItemContextMenuOpening(object sender, RoutedEventArgs e)
    {
        if (sender is ContextMenu menu && menu.PlacementTarget is FrameworkElement fe
            && fe.DataContext is BacklogItem item)
        {
            // 索引 2=标记待处理, 3=标记等待他人, 4=排期
            if (menu.Items[2] is MenuItem pending)
                pending.IsEnabled = item.Status != BacklogItem.StatusPending;
            if (menu.Items[3] is MenuItem waiting)
                waiting.IsEnabled = item.Status != BacklogItem.StatusWaiting;
            // 已排期的不可再操作
            if (item.IsScheduled)
            {
                if (menu.Items[2] is MenuItem p) p.IsEnabled = false;
                if (menu.Items[3] is MenuItem w) w.IsEnabled = false;
                if (menu.Items[4] is MenuItem s) s.IsEnabled = false;
            }
        }
    }

    private void OnEditItem(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is BacklogItem item)
            OpenEditDialog(item);
    }

    private void OnItemMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
    {
        if (e.ClickCount == 2 && sender is Border border && border.DataContext is BacklogItem item)
        {
            OpenEditDialog(item);
            e.Handled = true;
        }
    }

    private void OnMarkPending(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is BacklogItem item)
            _vm.MarkPendingCommand.Execute(item);
    }

    private void OnMarkWaiting(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is BacklogItem item)
        {
            // 直接打开编辑框让用户填写等待说明
            OpenEditDialog(item);
            EditStatus.SelectedIndex = 1; // 等待他人
        }
    }

    private void OnScheduleItem(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is BacklogItem item)
            OpenScheduleDialog(item);
    }

    private void OnDeleteItem(object sender, RoutedEventArgs e)
    {
        if (sender is MenuItem mi && mi.DataContext is BacklogItem item)
            _vm.DeleteItemCommand.Execute(item);
    }

    // ========== 编辑对话框 ==========

    private void OpenEditDialog(BacklogItem item)
    {
        _editingItem = item;
        EditTitle.Text = item.Title;
        EditDescription.Text = item.Description ?? string.Empty;
        EditPriority.SelectedIndex = item.Priority;
        EditStatus.SelectedIndex = item.Status < 2 ? item.Status : 0;
        EditWaitingFor.Text = item.WaitingFor ?? string.Empty;

        // 分类
        EditCategory.Text = item.Category ?? string.Empty;

        EditOverlay.Visibility = Visibility.Visible;
    }

    private void OnSaveEdit(object sender, RoutedEventArgs e)
    {
        if (_editingItem == null) return;

        _editingItem.Title = EditTitle.Text.Trim();
        _editingItem.Description = string.IsNullOrWhiteSpace(EditDescription.Text) ? null : EditDescription.Text.Trim();
        _editingItem.Priority = EditPriority.SelectedIndex;
        _editingItem.Status = EditStatus.SelectedIndex;
        _editingItem.Category = EditCategory.Text?.Trim() ?? string.Empty;
        _editingItem.WaitingFor = string.IsNullOrWhiteSpace(EditWaitingFor.Text) ? null : EditWaitingFor.Text.Trim();

        _vm.SaveItem(_editingItem);
        EditOverlay.Visibility = Visibility.Collapsed;
        _editingItem = null;
    }

    private void OnCancelEdit(object sender, RoutedEventArgs e)
    {
        EditOverlay.Visibility = Visibility.Collapsed;
        _editingItem = null;
    }

    private void OnOverlayClick(object sender, MouseButtonEventArgs e)
    {
        EditOverlay.Visibility = Visibility.Collapsed;
        _editingItem = null;
    }

    // ========== 排期对话框 ==========

    private void OpenScheduleDialog(BacklogItem item)
    {
        _schedulingItem = item;
        ScheduleDatePicker.SelectedDate = DateTime.Today;
        ScheduleOverlay.Visibility = Visibility.Visible;
    }

    private void OnConfirmSchedule(object sender, RoutedEventArgs e)
    {
        if (_schedulingItem == null || ScheduleDatePicker.SelectedDate == null) return;
        _vm.DoSchedule(_schedulingItem, ScheduleDatePicker.SelectedDate.Value);
        ScheduleOverlay.Visibility = Visibility.Collapsed;
        _schedulingItem = null;
    }

    private void OnCancelSchedule(object sender, RoutedEventArgs e)
    {
        ScheduleOverlay.Visibility = Visibility.Collapsed;
        _schedulingItem = null;
    }

    private void OnScheduleOverlayClick(object sender, MouseButtonEventArgs e)
    {
        ScheduleOverlay.Visibility = Visibility.Collapsed;
        _schedulingItem = null;
    }
}
