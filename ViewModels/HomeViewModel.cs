// ============================================================================
// HomeViewModel.cs - 首页（今日待办）的 ViewModel
//
// 【WPF MVVM 模式说明】
// ViewModel 是 View（界面）和 Model（数据）之间的桥梁。
// View（HomePage.xaml）通过数据绑定（Binding）自动从 ViewModel 读取数据，
// 用户操作通过 Command 传回 ViewModel 处理，ViewModel 再调用 Service 更新数据。
//
// [ObservableProperty] 特性：CommunityToolkit.Mvvm 提供的代码生成器，
// 自动为 _fieldName 生成 public FieldName 属性 + PropertyChanged 通知。
// 例如 _selectedDate 会自动生成 SelectedDate 属性。
//
// [RelayCommand] 特性：自动为方法生成 ICommand 实现。
// 例如 GoToToday() 方法会生成 GoToTodayCommand 属性，XAML 中可绑定调用。
// ============================================================================

using System.Collections.ObjectModel;
using System.Text;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Models;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

/// <summary>
/// 首页周栏中的单日数据（用于横向周一~周日快速切换栏）
/// </summary>
public class WeekDay
{
    /// <summary>星期名称（如 "周一"）</summary>
    public string DayName { get; set; } = string.Empty;

    /// <summary>日期标签（如 "04-13"）</summary>
    public string DateLabel { get; set; } = string.Empty;

    /// <summary>该日的完整日期</summary>
    public DateTime Date { get; set; }

    /// <summary>是否是今天（用于高亮显示）</summary>
    public bool IsToday { get; set; }

    /// <summary>是否是当前选中的日期</summary>
    public bool IsSelected { get; set; }
}

/// <summary>
/// 首页 ViewModel，管理「今日待办」视图的所有数据和操作。
/// 功能：日期切换、周栏导航、待办增删改查、快速添加。
/// </summary>
public partial class HomeViewModel : ObservableObject
{
    private readonly TodoService _todoService = new();
    private readonly DatabaseService _db = DatabaseService.Instance;

    /// <summary>预定义标签列表</summary>
    public static readonly string[] DefaultTags = ["基站", "底盘", "逻辑", "App", "Sdk"];

    /// <summary>当前选中的日期（改变时自动刷新待办列表和周栏）</summary>
    [ObservableProperty]
    private DateTime _selectedDate = DateTime.Today;

    /// <summary>当前日期的待办列表（绑定到 ListBox）</summary>
    [ObservableProperty]
    private ObservableCollection<TodoItem> _todos = new();

    /// <summary>快速添加输入框的文本</summary>
    [ObservableProperty]
    private string _newTodoTitle = string.Empty;

    /// <summary>当天已完成数</summary>
    [ObservableProperty]
    private int _completedCount;

    /// <summary>当天总数</summary>
    [ObservableProperty]
    private int _totalCount;

    /// <summary>当天待验证数</summary>
    [ObservableProperty]
    private int _verificationCount;

    /// <summary>周栏数据（周一到周日的 7 个 WeekDay）</summary>
    [ObservableProperty]
    private ObservableCollection<WeekDay> _weekDays = new();

    /// <summary>导出周报后的提示消息</summary>
    [ObservableProperty]
    private string _exportMessage = string.Empty;

    /// <summary>当前筛选标签（null 表示"全部"）</summary>
    [ObservableProperty]
    private string? _selectedTag;

    /// <summary>可用标签列表（预定义 + 自定义）</summary>
    [ObservableProperty]
    private ObservableCollection<string> _availableTags = new();

    public HomeViewModel()
    {
        LoadAvailableTags();
        UpdateWeekBar();
        LoadTodos();
    }

    /// <summary>
    /// 当 SelectedDate 属性变化时自动调用（由 CommunityToolkit 代码生成器触发）。
    /// 刷新待办列表和周栏显示。
    /// </summary>
    partial void OnSelectedDateChanged(DateTime value)
    {
        LoadTodos();
        UpdateWeekBar();
    }

    /// <summary>当筛选标签变化时刷新待办列表</summary>
    partial void OnSelectedTagChanged(string? value)
    {
        LoadTodos();
    }

    /// <summary>加载可用标签列表（预定义 + 自定义）</summary>
    public void LoadAvailableTags()
    {
        var settings = _db.GetSettings();
        settings.CustomTags ??= new List<string>();
        var tags = new List<string>(DefaultTags);
        foreach (var t in settings.CustomTags)
        {
            if (!tags.Contains(t))
                tags.Add(t);
        }
        AvailableTags = new ObservableCollection<string>(tags);
    }

    /// <summary>添加自定义标签</summary>
    public void AddCustomTag(string tag)
    {
        if (string.IsNullOrWhiteSpace(tag)) return;
        tag = tag.Trim();
        var settings = _db.GetSettings();
        settings.CustomTags ??= new List<string>();
        if (DefaultTags.Contains(tag) || settings.CustomTags.Contains(tag)) return;
        settings.CustomTags.Add(tag);
        _db.SaveSettings(settings);
        LoadAvailableTags();
    }

    /// <summary>
    /// 根据当前选中日期计算所在周的周一~周日，更新周栏数据。
    /// </summary>
    private void UpdateWeekBar()
    {
        var today = DateTime.Today;
        // 计算选中日期所在周的周一
        var dow = SelectedDate.DayOfWeek;
        var diff = dow == DayOfWeek.Sunday ? 6 : (int)dow - 1;
        var monday = SelectedDate.Date.AddDays(-diff);

        string[] dayNames = ["周一", "周二", "周三", "周四", "周五", "周六", "周日"];
        var days = new ObservableCollection<WeekDay>();
        for (int i = 0; i < 7; i++)
        {
            var date = monday.AddDays(i);
            days.Add(new WeekDay
            {
                DayName = dayNames[i],
                DateLabel = date.ToString("MM-dd"),
                Date = date,
                IsToday = date == today,
                IsSelected = date == SelectedDate.Date
            });
        }
        WeekDays = days;
    }

    /// <summary>从数据库加载当前选中日期的待办（支持标签筛选）</summary>
    public void LoadTodos()
    {
        var items = _todoService.GetTodosByDateAndTag(SelectedDate, SelectedTag);
        Todos = new ObservableCollection<TodoItem>(items);
        TotalCount = items.Count;
        CompletedCount = items.Count(x => x.IsCompleted);
        VerificationCount = items.Count(x => x.NeedsVerification);
    }

    /// <summary>
    /// 切换待办的完成/未完成状态，并与进度双向联动。
    /// 勾选完成 → Progress=100, CompletedAt=Now
    /// 取消完成 → Progress=0, CompletedAt=null
    /// </summary>
    [RelayCommand]
    private void ToggleComplete(TodoItem item)
    {
        var todo = _todoService.GetById(item.Id);
        if (todo == null) return;

        if (todo.IsCompleted)
        {
            // 取消完成：清除完成时间，进度归零
            todo.CompletedAt = null;
            todo.Progress = 0;
        }
        else
        {
            // 标记完成：设置完成时间，进度满格
            todo.CompletedAt = DateTime.Now;
            todo.Progress = 100;
        }

        _todoService.Update(todo);
        LoadTodos();
    }

    /// <summary>切换待验证状态</summary>
    [RelayCommand]
    private void ToggleVerification(TodoItem item)
    {
        var todo = _todoService.GetById(item.Id);
        if (todo == null) return;
        todo.NeedsVerification = !todo.NeedsVerification;
        _todoService.Update(todo);
        LoadTodos();
    }

    /// <summary>通过表单添加新待办</summary>
    [RelayCommand]
    private void AddTodo()
    {
        if (string.IsNullOrWhiteSpace(NewTodoTitle)) return;

        var todo = new TodoItem
        {
            Title = NewTodoTitle.Trim(),
            ScheduledDate = SelectedDate.Date,
            SortOrder = Todos.Count // 排在末尾
        };

        _todoService.Add(todo);
        NewTodoTitle = string.Empty;
        LoadTodos();
    }

    /// <summary>删除待办</summary>
    [RelayCommand]
    private void DeleteTodo(TodoItem item)
    {
        _todoService.Delete(item.Id);
        LoadTodos();
    }

    /// <summary>更新待办（编辑对话框保存时调用）</summary>
    public void UpdateTodo(TodoItem item)
    {
        _todoService.Update(item);
        LoadTodos();
    }

    /// <summary>
    /// 将待办事项移动到指定日期（修改 ScheduledDate 后更新数据库并刷新视图）
    /// </summary>
    public void MoveTodo(TodoItem item, DateTime targetDate)
    {
        var todo = _todoService.GetById(item.Id);
        if (todo == null) return;
        todo.ScheduledDate = targetDate.Date;
        _todoService.Update(todo);
        LoadTodos();
    }

    // --- 日期导航命令 ---

    /// <summary>切换到前一天</summary>
    [RelayCommand]
    private void GoToPreviousDay() => SelectedDate = SelectedDate.AddDays(-1);

    /// <summary>切换到后一天</summary>
    [RelayCommand]
    private void GoToNextDay() => SelectedDate = SelectedDate.AddDays(1);

    /// <summary>跳转到今天</summary>
    [RelayCommand]
    private void GoToToday() => SelectedDate = DateTime.Today;

    /// <summary>周栏切换到上一周</summary>
    [RelayCommand]
    private void GoToPreviousWeek() => SelectedDate = SelectedDate.AddDays(-7);

    /// <summary>周栏切换到下一周</summary>
    [RelayCommand]
    private void GoToNextWeek() => SelectedDate = SelectedDate.AddDays(7);

    /// <summary>快速添加待办（底部输入框回车触发）</summary>
    public void QuickAdd(string title)
    {
        if (string.IsNullOrWhiteSpace(title)) return;

        var todo = new TodoItem
        {
            Title = title.Trim(),
            ScheduledDate = SelectedDate.Date,
            SortOrder = Todos.Count
        };
        _todoService.Add(todo);
        LoadTodos();
    }

    /// <summary>
    /// 生成当前周的周报文本。
    /// 根据当前选中日期所在周（周一~周日）汇总所有待办，
    /// 分为"已完成"和"未完成"两部分，并附带统计数据。
    /// </summary>
    public string GenerateWeeklyReport()
    {
        // 计算当前选中日期所在周的周一
        var dow = SelectedDate.DayOfWeek;
        var diff = dow == DayOfWeek.Sunday ? 6 : (int)dow - 1;
        var weekStart = SelectedDate.Date.AddDays(-diff);
        var weekEnd = weekStart.AddDays(6);

        var allTodos = _todoService.GetTodosByDateRange(weekStart, weekEnd);
        var completed = allTodos.Where(t => t.IsCompleted).ToList();
        var pending = allTodos.Where(t => !t.IsCompleted).ToList();
        var needsVerify = allTodos.Where(t => t.NeedsVerification).ToList();

        var sb = new StringBuilder();
        sb.AppendLine($"周报 ({weekStart:yyyy-MM-dd} ~ {weekEnd:yyyy-MM-dd})");
        sb.AppendLine();

        // 已完成任务按天分组
        sb.AppendLine("一、本周完成工作");
        if (completed.Count > 0)
        {
            var idx = 1;
            foreach (var group in completed.GroupBy(t => t.ScheduledDate.Date).OrderBy(g => g.Key))
            {
                var dayName = group.Key.ToString("MM/dd ddd");
                foreach (var item in group.OrderBy(t => t.SortOrder))
                {
                    sb.Append($"{idx}. {item.Title} ({dayName})");
                    if (item.NeedsVerification)
                        sb.Append(" [待验证]");
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        sb.Append($" - {item.Description}");
                    sb.AppendLine();
                    idx++;
                }
            }
        }
        else
        {
            sb.AppendLine("  (无)");
        }
        sb.AppendLine();

        // 未完成任务（附带进度）
        sb.AppendLine("二、未完成/进行中");
        if (pending.Count > 0)
        {
            var idx = 1;
            foreach (var group in pending.GroupBy(t => t.ScheduledDate.Date).OrderBy(g => g.Key))
            {
                var dayName = group.Key.ToString("MM/dd ddd");
                foreach (var item in group.OrderBy(t => t.SortOrder))
                {
                    sb.Append($"{idx}. {item.Title} [{item.Progress}%] ({dayName})");
                    if (item.NeedsVerification)
                        sb.Append(" [待验证]");
                    if (!string.IsNullOrWhiteSpace(item.Description))
                        sb.Append($" - {item.Description}");
                    sb.AppendLine();
                    idx++;
                }
            }
        }
        else
        {
            sb.AppendLine("  (无)");
        }
        sb.AppendLine();

        // 统计
        var total = allTodos.Count;
        var rate = total > 0 ? (completed.Count * 100 / total) : 0;
        var avgProgress = pending.Count > 0 ? (int)pending.Average(t => t.Progress) : 0;
        sb.AppendLine("三、本周统计");
        sb.AppendLine($"总任务: {total} | 已完成: {completed.Count} | 完成率: {rate}%");
        if (pending.Count > 0)
            sb.AppendLine($"未完成平均进度: {avgProgress}%");
        if (needsVerify.Count > 0)
            sb.AppendLine($"待验证: {needsVerify.Count} 项");

        return sb.ToString();
    }
}
