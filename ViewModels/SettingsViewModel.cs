// ============================================================================
// SettingsViewModel.cs - 设置页面 ViewModel
// 管理应用设置的读取、修改、保存，以及深色模式即时切换
// ============================================================================

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
    private string _statusMessage = string.Empty;

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
        _db.SaveSettings(s);

        UpdateStartupRegistration();
        StatusMessage = "设置已保存！";
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
