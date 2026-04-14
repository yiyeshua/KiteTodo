// ============================================================================
// AppSettings.cs - 应用设置数据模型
// 对应数据库中的 "settings" 集合，始终只有一条记录（Id=1），单例模式
// ============================================================================

namespace KiteTodo.Models;

/// <summary>
/// 应用全局设置实体类。数据库中只存一条（Id=1），通过 DatabaseService 读写。
/// </summary>
public class AppSettings
{
    /// <summary>固定为 1，保证数据库中只有一条设置记录</summary>
    public int Id { get; set; } = 1;

    /// <summary>主题模式："Light" 或 "Dark"</summary>
    public string ThemeMode { get; set; } = "Light";

    /// <summary>番茄钟专注时长（分钟），默认 25 分钟</summary>
    public int PomodoroDuration { get; set; } = 25;

    /// <summary>短休息时长（分钟），默认 5 分钟</summary>
    public int ShortBreakDuration { get; set; } = 5;

    /// <summary>长休息时长（分钟），默认 15 分钟</summary>
    public int LongBreakDuration { get; set; } = 15;

    /// <summary>每完成几个番茄钟后进入长休息，默认 4 轮</summary>
    public int LongBreakInterval { get; set; } = 4;

    /// <summary>是否开机自启动</summary>
    public bool LaunchAtStartup { get; set; }

    // 以下字段为历史遗留（浮窗功能已移除），保留以避免数据库迁移问题
    public bool ShowMiniWindow { get; set; }
    public double MiniWindowX { get; set; } = 100;
    public double MiniWindowY { get; set; } = 100;
    public double MiniWindowWidth { get; set; } = 220;
    public double MiniWindowHeight { get; set; } = 280;
    public double MainWindowWidth { get; set; } = 1000;
    public double MainWindowHeight { get; set; } = 680;
}
