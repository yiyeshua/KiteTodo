// ============================================================================
// BacklogViewModel.cs — 事项池页面的 ViewModel
// 管理待排期事项的列表、新增、编辑、删除、筛选和转待办功能
// ============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 事项池页面 ViewModel，管理待排期事项。
/// </summary>
public partial class BacklogViewModel : ObservableObject
{
    private readonly BacklogService _backlogService = new();

    /// <summary>当前列表（筛选后的结果）</summary>
    [ObservableProperty]
    private ObservableCollection<BacklogItem> _items = new();

    /// <summary>待处理事项数</summary>
    [ObservableProperty]
    private int _pendingCount;

    /// <summary>等待他人事项数</summary>
    [ObservableProperty]
    private int _waitingCount;

    /// <summary>已排期事项数</summary>
    [ObservableProperty]
    private int _scheduledCount;

    /// <summary>当前筛选状态（-2=活跃事项, -1=全部, 0=待处理, 1=等待他人, 2=已排期）</summary>
    [ObservableProperty]
    private int _filterStatus = -1;

    /// <summary>快速添加标题</summary>
    [ObservableProperty]
    private string _newTitle = string.Empty;

    public BacklogViewModel()
    {
        LoadItems();
    }

    partial void OnFilterStatusChanged(int value)
    {
        LoadItems();
    }

    /// <summary>加载事项列表</summary>
    public void LoadItems()
    {
        List<BacklogItem> list;
        if (FilterStatus == -1)
            list = _backlogService.GetAll();
        else if (FilterStatus == -2)
            list = _backlogService.GetActive();
        else
            list = _backlogService.GetByStatus(FilterStatus);

        Items = new ObservableCollection<BacklogItem>(list);

        // 统计
        var all = _backlogService.GetAll();
        PendingCount = all.Count(x => x.Status == BacklogItem.StatusPending);
        WaitingCount = all.Count(x => x.Status == BacklogItem.StatusWaiting);
        ScheduledCount = all.Count(x => x.Status == BacklogItem.StatusScheduled);
    }

    /// <summary>快速添加事项</summary>
    [RelayCommand]
    private void QuickAdd()
    {
        if (string.IsNullOrWhiteSpace(NewTitle)) return;
        _backlogService.Add(new BacklogItem { Title = NewTitle.Trim() });
        NewTitle = string.Empty;
        LoadItems();
    }

    /// <summary>删除事项</summary>
    [RelayCommand]
    private void DeleteItem(BacklogItem item)
    {
        _backlogService.Delete(item.Id);
        LoadItems();
    }

    /// <summary>切换为"等待他人"状态</summary>
    [RelayCommand]
    private void MarkWaiting(BacklogItem item)
    {
        item.Status = BacklogItem.StatusWaiting;
        _backlogService.Update(item);
        LoadItems();
    }

    /// <summary>切换为"待处理"状态</summary>
    [RelayCommand]
    private void MarkPending(BacklogItem item)
    {
        item.Status = BacklogItem.StatusPending;
        item.WaitingFor = null;
        _backlogService.Update(item);
        LoadItems();
    }

    /// <summary>排期 → 转为待办</summary>
    [RelayCommand]
    private void ScheduleItem(BacklogItem item)
    {
        // 由 Page code-behind 处理日期选择后调用 DoSchedule
    }

    /// <summary>在选定日期排期</summary>
    public void DoSchedule(BacklogItem item, DateTime date)
    {
        _backlogService.ConvertToTodo(item.Id, date);
        LoadItems();
    }

    /// <summary>保存编辑</summary>
    public void SaveItem(BacklogItem item)
    {
        _backlogService.Update(item);
        LoadItems();
    }
}
