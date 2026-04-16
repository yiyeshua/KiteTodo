// ============================================================================
// MonthlyViewModel.cs - 月视图 ViewModel
// 管理月度日历网格数据，支持月份切换和点击查看某天详情
// ============================================================================

using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 月视图 ViewModel，以 7 列日历网格展示整月待办概览。
/// 支持月份前后切换、点击某天弹出详情面板。
/// </summary>
public partial class MonthlyViewModel : ObservableObject
{
    private readonly TodoService _todoService = new();

    /// <summary>防止 Year/Month 同时变化时重复加载的标志位</summary>
    private bool _isUpdating;

    /// <summary>当前显示的年份</summary>
    [ObservableProperty]
    private int _currentYear = DateTime.Today.Year;

    /// <summary>当前显示的月份</summary>
    [ObservableProperty]
    private int _currentMonth = DateTime.Today.Month;

    /// <summary>日历网格的所有单元格（包含空白占位和实际日期）</summary>
    [ObservableProperty]
    private ObservableCollection<DayCellInfo> _dayCells = new();

    /// <summary>月份标签（如 "2026 年 4 月"）</summary>
    [ObservableProperty]
    private string _monthLabel = string.Empty;

    /// <summary>当前选中查看详情的某一天</summary>
    [ObservableProperty]
    private DayCellInfo? _selectedDay;

    /// <summary>选中天的待办列表（显示在详情弹窗中）</summary>
    [ObservableProperty]
    private ObservableCollection<TodoItem> _selectedDayTodos = new();

    /// <summary>是否显示日详情弹窗</summary>
    [ObservableProperty]
    private bool _showDayDetail;

    /// <summary>新增待办标题（详情弹窗中的输入框）</summary>
    [ObservableProperty]
    private string _newTodoTitle = string.Empty;

    /// <summary>本月待办总数</summary>
    [ObservableProperty]
    private int _monthTotalTodos;

    /// <summary>本月已完成数</summary>
    [ObservableProperty]
    private int _monthCompletedTodos;

    public MonthlyViewModel()
    {
        LoadMonth();
    }

    // Year/Month 属性变化时自动重新加载（但 _isUpdating 为 true 时跳过，防止重复加载）
    partial void OnCurrentYearChanged(int value)
    {
        if (!_isUpdating) LoadMonth();
    }

    partial void OnCurrentMonthChanged(int value)
    {
        if (!_isUpdating) LoadMonth();
    }

    /// <summary>
    /// 加载当月数据：
    /// 1. 查询本月所有待办，按日期分组统计
    /// 2. 计算日历网格（含月初空白占位），生成 DayCellInfo 集合
    /// </summary>
    public void LoadMonth()
    {
        MonthLabel = $"{CurrentYear} 年 {CurrentMonth} 月";

        // 查询本月所有待办
        var start = new DateTime(CurrentYear, CurrentMonth, 1);
        var end = start.AddMonths(1);
        var allTodos = _todoService.GetTodosByDateRange(start, end.AddDays(-1));

        // 按日期分组
        var todosByDate = allTodos
            .GroupBy(t => t.ScheduledDate.Date)
            .ToDictionary(g => g.Key, g => g.ToList());

        MonthTotalTodos = allTodos.Count;
        MonthCompletedTodos = allTodos.Count(t => t.IsCompleted);

        var daysInMonth = DateTime.DaysInMonth(CurrentYear, CurrentMonth);
        var firstDay = new DateTime(CurrentYear, CurrentMonth, 1);
        // startOffset: 月份第一天是星期几（0=周日, 1=周一...），决定前面有几个空白格
        var startOffset = (int)firstDay.DayOfWeek;

        var cells = new ObservableCollection<DayCellInfo>();

        // 添加空白占位格（月初的空位）
        for (int i = 0; i < startOffset; i++)
            cells.Add(new DayCellInfo { IsBlank = true });

        // 添加每一天的单元格
        for (int day = 1; day <= daysInMonth; day++)
        {
            var date = new DateTime(CurrentYear, CurrentMonth, day);
            var cell = new DayCellInfo
            {
                Date = date,
                Day = day,
                IsToday = date == DateTime.Today
            };

            // 填充该天的待办统计和预览
            if (todosByDate.TryGetValue(date, out var dayTodos))
            {
                cell.TotalCount = dayTodos.Count;
                cell.CompletedCount = dayTodos.Count(t => t.IsCompleted);
                // 最多显示 3 条标题预览
                cell.TodoTitles = string.Join("\n",
                    dayTodos.Take(3).Select(t => (t.IsCompleted ? "✓ " : "○ ") + t.Title));
                if (dayTodos.Count > 3)
                    cell.TodoTitles += $"\n... 还有 {dayTodos.Count - 3} 项";
            }

            cells.Add(cell);
        }

        DayCells = cells;
        ShowDayDetail = false;
    }

    /// <summary>点击某天时，加载该天的完整待办列表并显示详情弹窗</summary>
    public void SelectDay(DayCellInfo day)
    {
        if (day.IsBlank) return;
        SelectedDay = day;
        var todos = _todoService.GetTodosByDate(day.Date);
        SelectedDayTodos = new ObservableCollection<TodoItem>(todos);
        ShowDayDetail = true;
    }

    /// <summary>关闭日详情弹窗</summary>
    public void CloseDayDetail()
    {
        ShowDayDetail = false;
        NewTodoTitle = string.Empty;
    }

    /// <summary>在选中日期新增一条待办</summary>
    [RelayCommand]
    private void AddTodoToSelectedDay()
    {
        if (SelectedDay == null || string.IsNullOrWhiteSpace(NewTodoTitle)) return;

        var todo = new TodoItem
        {
            Title = NewTodoTitle.Trim(),
            ScheduledDate = SelectedDay.Date
        };
        _todoService.Add(todo);
        NewTodoTitle = string.Empty;

        // 刷新详情列表
        var todos = _todoService.GetTodosByDate(SelectedDay.Date);
        SelectedDayTodos = new ObservableCollection<TodoItem>(todos);

        // 刷新该日历单元格的统计
        SelectedDay.TotalCount = todos.Count;
        SelectedDay.CompletedCount = todos.Count(t => t.IsCompleted);
        SelectedDay.TodoTitles = string.Join("\n",
            todos.Take(3).Select(t => (t.IsCompleted ? "✓ " : "○ ") + t.Title));
        if (todos.Count > 3)
            SelectedDay.TodoTitles += $"\n... 还有 {todos.Count - 3} 项";

        // 刷新月统计
        var start = new DateTime(CurrentYear, CurrentMonth, 1);
        var end = start.AddMonths(1).AddDays(-1);
        var allTodos = _todoService.GetTodosByDateRange(start, end);
        MonthTotalTodos = allTodos.Count;
        MonthCompletedTodos = allTodos.Count(t => t.IsCompleted);
    }

    /// <summary>切换到上一月</summary>
    [RelayCommand]
    private void PreviousMonth()
    {
        _isUpdating = true; // 防止 Year 和 Month 各触发一次 LoadMonth
        if (CurrentMonth == 1) { CurrentMonth = 12; CurrentYear--; }
        else { CurrentMonth--; }
        _isUpdating = false;
        LoadMonth();
    }

    /// <summary>切换到下一月</summary>
    [RelayCommand]
    private void NextMonth()
    {
        _isUpdating = true;
        if (CurrentMonth == 12) { CurrentMonth = 1; CurrentYear++; }
        else { CurrentMonth++; }
        _isUpdating = false;
        LoadMonth();
    }

    /// <summary>跳转回当前月</summary>
    [RelayCommand]
    private void GoToCurrentMonth()
    {
        _isUpdating = true;
        CurrentYear = DateTime.Today.Year;
        CurrentMonth = DateTime.Today.Month;
        _isUpdating = false;
        LoadMonth();
    }
}

/// <summary>
/// 月视图日历中单个格子的数据模型。
/// 包含日期、待办统计、完成率等信息。
/// </summary>
public partial class DayCellInfo : ObservableObject
{
    /// <summary>该格对应的日期</summary>
    public DateTime Date { get; set; }

    /// <summary>日期数字（1~31）</summary>
    public int Day { get; set; }

    /// <summary>是否为空白占位格（月初前面的空位）</summary>
    public bool IsBlank { get; set; }

    /// <summary>是否是今天</summary>
    public bool IsToday { get; set; }

    /// <summary>该天待办总数</summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>该天已完成数</summary>
    [ObservableProperty]
    private int _completedCount;

    /// <summary>待办标题预览文本（最多 3 条，用于 Tooltip 和格内预览）</summary>
    public string? TodoTitles { get; set; }

    /// <summary>完成率（0.0~1.0），用于进度条宽度计算</summary>
    public double CompletionRate => TotalCount > 0 ? (double)CompletedCount / TotalCount : 0;

    /// <summary>是否有待办</summary>
    public bool HasTodos => TotalCount > 0;

    /// <summary>完成统计摘要（如 "3/5"）</summary>
    public string Summary => HasTodos ? $"{CompletedCount}/{TotalCount}" : string.Empty;

    /// <summary>完成百分比文本（如 "60%"）</summary>
    public string ProgressPercent => HasTodos ? $"{(int)(CompletionRate * 100)}%" : string.Empty;
}
