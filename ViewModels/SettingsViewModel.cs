// ============================================================================
// SettingsViewModel.cs - 设置页面 ViewModel
// 管理应用设置的读取、修改、保存，以及深色模式即时切换
// ============================================================================

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using KiteTodo.Services;

namespace KiteTodo.ViewModels;

public partial class SettingsViewModel : ObservableObject
{
    private readonly DatabaseService _db = DatabaseService.Instance;

    [ObservableProperty]
    private double _pomodoroDuration;

    [ObservableProperty]
    private double _shortBreakDuration;

    [ObservableProperty]
    private double _longBreakDuration;

    [ObservableProperty]
    private double _longBreakInterval;

    [ObservableProperty]
    private bool _launchAtStartup;

    [ObservableProperty]
    private bool _isDarkMode;

    [ObservableProperty]
    private double _toastDurationSeconds;

    [ObservableProperty]
    private bool _showFocusStartDialog;

    [ObservableProperty]
    private bool _enableCompletionSound;

    [ObservableProperty]
    private string _completionSoundName = "Default";

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private string _newTagText = string.Empty;

    /// <summary>可选提示音列表</summary>
    public List<string> AvailableSounds { get; } = new() { "Default", "Chime", "Bell", "Ding" };

    /// <summary>自定义标签列表（包含默认 + 用户添加的）</summary>
    public ObservableCollection<string> CustomTags { get; } = new();

    /// <summary>默认标签（不可删除）</summary>
    private static readonly HashSet<string> _defaultTags = new(HomeViewModel.DefaultTags);

    /// <summary>数据库存储路径</summary>
    public string DataPath { get; } = Path.Combine(
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        "KiteTodo");

    public SettingsViewModel()
    {
        LoadSettings();
    }

    private void LoadSettings()
    {
        var s = _db.GetSettings();
        PomodoroDuration = s.PomodoroDuration;
        ShortBreakDuration = s.ShortBreakDuration;
        LongBreakDuration = s.LongBreakDuration;
        LongBreakInterval = s.LongBreakInterval;
        LaunchAtStartup = s.LaunchAtStartup;
        IsDarkMode = s.ThemeMode == "Dark";
        ToastDurationSeconds = s.ToastDurationSeconds;
        ShowFocusStartDialog = s.ShowFocusStartDialog;
        EnableCompletionSound = s.EnableCompletionSound;
        CompletionSoundName = s.CompletionSoundName;

        CustomTags.Clear();
        // 加载默认标签 + 自定义标签
        foreach (var tag in HomeViewModel.DefaultTags)
            CustomTags.Add(tag);
        foreach (var tag in s.CustomTags)
        {
            if (!CustomTags.Contains(tag))
                CustomTags.Add(tag);
        }
    }

    partial void OnIsDarkModeChanged(bool value)
    {
        // Immediately apply theme
        var theme = value ? "Dark" : "Light";
        if (Application.Current is App app)
        {
            app.ApplyTheme(theme);
        }
    }

    [RelayCommand]
    private void SaveSettings()
    {
        var s = _db.GetSettings();
        s.PomodoroDuration = (int)PomodoroDuration;
        s.ShortBreakDuration = (int)ShortBreakDuration;
        s.LongBreakDuration = (int)LongBreakDuration;
        s.LongBreakInterval = (int)LongBreakInterval;
        s.LaunchAtStartup = LaunchAtStartup;
        s.ThemeMode = IsDarkMode ? "Dark" : "Light";
        s.ToastDurationSeconds = (int)ToastDurationSeconds;
        s.ShowFocusStartDialog = ShowFocusStartDialog;
        s.EnableCompletionSound = EnableCompletionSound;
        s.CompletionSoundName = CompletionSoundName;
        s.CustomTags = CustomTags.Where(t => !_defaultTags.Contains(t)).ToList();
        _db.SaveSettings(s);

        UpdateStartupRegistration();
        StatusMessage = "设置已保存！";
    }

    [RelayCommand]
    private void AddTag()
    {
        var tag = NewTagText?.Trim();
        if (!string.IsNullOrEmpty(tag) && !CustomTags.Contains(tag))
        {
            CustomTags.Add(tag);
            NewTagText = string.Empty;
        }
    }

    [RelayCommand]
    private void RemoveTag(string tag)
    {
        if (!_defaultTags.Contains(tag))
            CustomTags.Remove(tag);
    }

    /// <summary>是否为默认标签（不可删除）</summary>
    public bool IsDefaultTag(string tag) => _defaultTags.Contains(tag);

    [RelayCommand]
    private void OpenDataFolder()
    {
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = DataPath,
                UseShellExecute = true
            });
        }
        catch { }
    }

    private void UpdateStartupRegistration()
    {
        try
        {
            var key = Microsoft.Win32.Registry.CurrentUser.OpenSubKey(
                @"SOFTWARE\Microsoft\Windows\CurrentVersion\Run", true);
            if (key == null) return;

            if (LaunchAtStartup)
            {
                var exePath = Environment.ProcessPath;
                if (exePath != null)
                    key.SetValue("KiteTodo", $"\"{exePath}\"");
            }
            else
            {
                key.DeleteValue("KiteTodo", false);
            }
        }
        catch
        {
            // Silently fail if registry access denied
        }
    }
}
