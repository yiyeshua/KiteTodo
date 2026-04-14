namespace KiteTodo.Helpers;

/// <summary>
/// 跨页面通信：从首页"开始专注"传递待办信息到番茄钟页面。
/// 使用静态属性，因为项目无 DI 容器，NavigationView 不支持传参导航。
/// </summary>
public static class FocusRequest
{
    /// <summary>待绑定的待办 ID（null 表示无请求）</summary>
    public static int? PendingTodoId { get; set; }

    /// <summary>待绑定的待办标题（用于显示）</summary>
    public static string? PendingTodoTitle { get; set; }

    /// <summary>清除待处理请求</summary>
    public static void Clear()
    {
        PendingTodoId = null;
        PendingTodoTitle = null;
    }
}
