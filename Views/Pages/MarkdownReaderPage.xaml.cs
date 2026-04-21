using System.IO;
using System.Text.Json;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Navigation;
using System.Windows.Threading;
using KiteTodo.Models;
using KiteTodo.Services;
using Microsoft.Web.WebView2.Core;
using Microsoft.Win32;

namespace KiteTodo.Views.Pages;

public partial class MarkdownReaderPage : Page
{
    private const int MinZoomPercent = 85;
    private const int MaxZoomPercent = 200;
    private const int ZoomStepPercent = 10;

    private readonly MarkdownPreviewService _markdownPreviewService = new();
    private readonly DatabaseService _databaseService = DatabaseService.Instance;
    private string? _currentFilePath;
    private string _currentMarkdown = string.Empty;
    private bool _isSourceMode;
    private bool _hasUnsavedChanges;
    private bool _isInternalUpdate;
    private int _previewZoomPercent;
    private int _outlineHeadingCount;
    private bool _showOutline;
    private bool _showSyntaxHelp;
    private bool _previewReady;
    private string? _lastPreviewHtml;

    public MarkdownReaderPage()
    {
        InitializeComponent();
        var settings = _databaseService.GetSettings();
        _isSourceMode = settings.MarkdownReaderSourceMode;
        _previewZoomPercent = Math.Clamp(settings.MarkdownReaderZoomPercent, MinZoomPercent, MaxZoomPercent);
        _showOutline = settings.MarkdownReaderShowOutline;
        Loaded += OnPageLoaded;
        Unloaded += OnPageUnloaded;
        UpdateZoomUi();
        UpdateContentMode();
    }

    private void OnOpenFile(object sender, RoutedEventArgs e)
    {
        if (!ConfirmContinueWhenUnsaved())
            return;

        var dialog = new OpenFileDialog
        {
            Title = "选择 Markdown 文件",
            Filter = "Markdown files (*.md;*.markdown;*.txt)|*.md;*.markdown;*.txt|All files (*.*)|*.*",
            CheckFileExists = true,
            Multiselect = false
        };

        if (dialog.ShowDialog() == true)
            OpenMarkdownFile(dialog.FileName);
    }

    private void OnReloadFile(object sender, RoutedEventArgs e)
    {
        if (!string.IsNullOrWhiteSpace(_currentFilePath) && ConfirmContinueWhenUnsaved())
            OpenMarkdownFile(_currentFilePath);
    }

    private void OnSaveFile(object sender, RoutedEventArgs e)
    {
        SaveCurrentFile();
    }

    private void OnToggleMode(object sender, RoutedEventArgs e)
    {
        _isSourceMode = !_isSourceMode;
        SaveViewModePreference();
        UpdateContentMode();
    }

    private void OnZoomOut(object sender, RoutedEventArgs e)
    {
        ChangeZoom(-ZoomStepPercent);
    }

    private void OnZoomIn(object sender, RoutedEventArgs e)
    {
        ChangeZoom(ZoomStepPercent);
    }

    private void OnToggleOutline(object sender, RoutedEventArgs e)
    {
        _showOutline = !_showOutline;
        SaveOutlinePreference();
        UpdateOutlineUi();

        if (!string.IsNullOrWhiteSpace(_currentFilePath) && !_isSourceMode)
            RenderPreviewAsync();
    }

    private void OnToggleHelp(object sender, RoutedEventArgs e)
    {
        _showSyntaxHelp = !_showSyntaxHelp;
        UpdateSyntaxHelpUi();
    }

    private void OnCopyHelpSnippet(object sender, RoutedEventArgs e)
    {
        if (sender is not FrameworkElement element || element.Tag is not string text)
            return;

        var normalized = System.Net.WebUtility.HtmlDecode(text);
        Clipboard.SetText(normalized);
        AlertService.ShowCornerToast("语法片段已复制", 2);
    }

    private void OnSourceTextChanged(object sender, TextChangedEventArgs e)
    {
        if (_isInternalUpdate)
            return;

        _currentMarkdown = SourceTextBox.Text;
        _outlineHeadingCount = _markdownPreviewService.CountOutlineHeadings(_currentMarkdown);
        _hasUnsavedChanges = !string.Equals(_currentMarkdown, ReadCurrentFileText(), StringComparison.Ordinal);
        UpdateFileInfoText();
        UpdateOutlineUi();
        UpdateActionButtons();
    }

    private void OnPreviewKeyDown(object sender, KeyEventArgs e)
    {
        if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control)
        {
            if (_hasUnsavedChanges)
                SaveCurrentFile();

            e.Handled = true;
        }
    }

    private void OnDragOver(object sender, DragEventArgs e)
    {
        var hasFile = e.Data.GetDataPresent(DataFormats.FileDrop);
        e.Effects = hasFile ? DragDropEffects.Copy : DragDropEffects.None;
        e.Handled = true;
    }

    private void OnDropFile(object sender, DragEventArgs e)
    {
        if (!e.Data.GetDataPresent(DataFormats.FileDrop))
            return;

        if (e.Data.GetData(DataFormats.FileDrop) is not string[] files || files.Length == 0)
            return;

        if (!ConfirmContinueWhenUnsaved())
            return;

        OpenMarkdownFile(files[0]);
    }

    private void OnPageLoaded(object sender, RoutedEventArgs e)
    {
        var navigationService = NavigationService;
        if (navigationService != null)
            navigationService.Navigating += OnNavigatingAway;

        _ = EnsurePreviewReadyAsync();
    }

    private void OnPageUnloaded(object sender, RoutedEventArgs e)
    {
        var navigationService = NavigationService;
        if (navigationService != null)
            navigationService.Navigating -= OnNavigatingAway;
    }

    private void OnNavigatingAway(object? sender, NavigatingCancelEventArgs e)
    {
        if (!ConfirmContinueWhenUnsaved())
            e.Cancel = true;
    }

    private void OpenMarkdownFile(string filePath)
    {
        if (!File.Exists(filePath))
        {
            MessageBox.Show("文件不存在，可能已被移动或删除。", "打开失败", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        try
        {
            _currentMarkdown = File.ReadAllText(filePath);
            _outlineHeadingCount = _markdownPreviewService.CountOutlineHeadings(_currentMarkdown);
            _isInternalUpdate = true;
            SourceTextBox.Text = _currentMarkdown;
            _isInternalUpdate = false;

            _currentFilePath = filePath;
            _hasUnsavedChanges = false;
            UpdateFileInfoText();
            ReloadButton.IsEnabled = true;
            ToggleModeButton.IsEnabled = true;
            EmptyStatePanel.Visibility = Visibility.Collapsed;
            UpdateActionButtons();
            UpdateOutlineUi();
            UpdateContentMode();
        }
        catch (Exception ex)
        {
            _isInternalUpdate = false;
            MessageBox.Show($"Markdown 渲染失败：{ex.Message}", "打开失败", MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }

    private void UpdateContentMode()
    {
        ToggleModeButton.Content = _isSourceMode ? "查看阅读" : "查看源码";
        PreviewHost.Visibility = _isSourceMode ? Visibility.Collapsed : Visibility.Visible;
        SourceHost.Visibility = _isSourceMode ? Visibility.Visible : Visibility.Collapsed;
        UpdateOutlineUi();
        UpdateSyntaxHelpUi();

        if (!_isSourceMode && !string.IsNullOrWhiteSpace(_currentFilePath))
            RenderPreviewAsync();

        UpdateActionButtons();
    }

    private void UpdateActionButtons()
    {
        SaveButton.IsEnabled = !string.IsNullOrWhiteSpace(_currentFilePath) && _hasUnsavedChanges;
    }

    private bool SaveCurrentFile()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
            return false;

        try
        {
            _currentMarkdown = SourceTextBox.Text;
            File.WriteAllText(_currentFilePath, _currentMarkdown, new UTF8Encoding(false));
            _hasUnsavedChanges = false;
            UpdateFileInfoText();
            RenderPreviewAsync();
            UpdateActionButtons();
            AlertService.ShowCornerToast("Markdown 已保存", 2);
            return true;
        }
        catch (Exception ex)
        {
            MessageBox.Show($"保存失败：{ex.Message}", "保存失败", MessageBoxButton.OK, MessageBoxImage.Error);
            return false;
        }
    }

    private void RenderPreviewAsync()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
            return;

        _ = RenderPreviewInternalAsync(SourceTextBox.Text, _currentFilePath);
    }

    private void SaveViewModePreference()
    {
        var settings = _databaseService.GetSettings();
        settings.MarkdownReaderSourceMode = _isSourceMode;
        _databaseService.SaveSettings(settings);
    }

    private void SaveZoomPreference()
    {
        var settings = _databaseService.GetSettings();
        settings.MarkdownReaderZoomPercent = _previewZoomPercent;
        _databaseService.SaveSettings(settings);
    }

    private void SaveOutlinePreference()
    {
        var settings = _databaseService.GetSettings();
        settings.MarkdownReaderShowOutline = _showOutline;
        _databaseService.SaveSettings(settings);
    }

    private string ReadCurrentFileText()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath) || !File.Exists(_currentFilePath))
            return string.Empty;

        try
        {
            return File.ReadAllText(_currentFilePath);
        }
        catch
        {
            return string.Empty;
        }
    }

    private void UpdateFileInfoText()
    {
        if (string.IsNullOrWhiteSpace(_currentFilePath))
        {
            FileInfoText.Text = "还没有打开 Markdown 文件 · 支持 .md、.markdown、.txt";
            return;
        }

        var fileName = Path.GetFileName(_currentFilePath);
        if (_hasUnsavedChanges)
            fileName += " *";

        FileInfoText.Text = $"{fileName} · {_currentFilePath}";
    }

    private bool ConfirmContinueWhenUnsaved()
    {
        if (!_hasUnsavedChanges)
            return true;

        var result = MessageBox.Show(
            "当前 Markdown 有未保存修改。是否先保存？\n\n选择“是”会先保存，选择“否”会放弃修改，选择“取消”则停留在当前页面。",
            "未保存修改",
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Warning);

        return result switch
        {
            MessageBoxResult.Yes => SaveCurrentFile(),
            MessageBoxResult.No => true,
            _ => false
        };
    }

    private void ChangeZoom(int delta)
    {
        var nextZoom = Math.Clamp(_previewZoomPercent + delta, MinZoomPercent, MaxZoomPercent);
        if (nextZoom == _previewZoomPercent)
            return;

        _previewZoomPercent = nextZoom;
        UpdateZoomUi();
        SaveZoomPreference();

        if (!string.IsNullOrWhiteSpace(_currentFilePath) && !_isSourceMode)
            RenderPreviewAsync();
    }

    private void UpdateZoomUi()
    {
        ZoomText.Text = $"{_previewZoomPercent}%";
        ZoomOutButton.IsEnabled = _previewZoomPercent > MinZoomPercent;
        ZoomInButton.IsEnabled = _previewZoomPercent < MaxZoomPercent;
    }

    private void UpdateOutlineUi()
    {
        var canShowOutline = !_isSourceMode
            && !string.IsNullOrWhiteSpace(_currentFilePath)
            && _outlineHeadingCount >= 3;

        ToggleOutlineButton.Visibility = canShowOutline ? Visibility.Visible : Visibility.Collapsed;
        ToggleOutlineButton.Content = _showOutline ? "隐藏目录" : "显示目录";
    }

    private void UpdateSyntaxHelpUi()
    {
        var canShowSyntaxHelp = _isSourceMode && !string.IsNullOrWhiteSpace(_currentFilePath);
        ToggleHelpButton.Visibility = canShowSyntaxHelp ? Visibility.Visible : Visibility.Collapsed;

        if (!canShowSyntaxHelp)
        {
            SourceHelpPanel.Visibility = Visibility.Collapsed;
            SourceDivider.Visibility = Visibility.Collapsed;
            SourceHelpColumn.Width = new GridLength(0);
            SourceDividerColumn.Width = new GridLength(0);
            SourceEditorColumn.Width = new GridLength(1, GridUnitType.Star);
            return;
        }

        ToggleHelpButton.Content = _showSyntaxHelp ? "隐藏语法" : "显示语法";
        SourceHelpPanel.Visibility = _showSyntaxHelp ? Visibility.Visible : Visibility.Collapsed;
        SourceDivider.Visibility = _showSyntaxHelp ? Visibility.Visible : Visibility.Collapsed;
        SourceHelpColumn.Width = _showSyntaxHelp ? new GridLength(0.9, GridUnitType.Star) : new GridLength(0);
        SourceDividerColumn.Width = _showSyntaxHelp ? new GridLength(1) : new GridLength(0);
        SourceEditorColumn.Width = _showSyntaxHelp ? new GridLength(2.75, GridUnitType.Star) : new GridLength(1, GridUnitType.Star);
    }

    private async Task EnsurePreviewReadyAsync()
    {
        if (_previewReady)
            return;

        await PreviewBrowser.EnsureCoreWebView2Async();
        PreviewBrowser.CoreWebView2.Settings.AreDefaultContextMenusEnabled = true;
        PreviewBrowser.CoreWebView2.Settings.AreDevToolsEnabled = false;
        PreviewBrowser.CoreWebView2.Settings.IsStatusBarEnabled = false;
        PreviewBrowser.CoreWebView2.Settings.IsZoomControlEnabled = false;
        PreviewBrowser.CoreWebView2.WebMessageReceived -= OnPreviewWebMessageReceived;
        PreviewBrowser.CoreWebView2.WebMessageReceived += OnPreviewWebMessageReceived;
        PreviewBrowser.DefaultBackgroundColor = System.Drawing.Color.White;
        _previewReady = true;

        if (!string.IsNullOrWhiteSpace(_lastPreviewHtml))
            PreviewBrowser.NavigateToString(_lastPreviewHtml);
    }

    private async Task RenderPreviewInternalAsync(string markdown, string? filePath)
    {
        if (string.IsNullOrWhiteSpace(filePath))
            return;

        var html = _markdownPreviewService.RenderHtml(markdown, filePath, _previewZoomPercent, _showOutline);
        _lastPreviewHtml = html;

        await Dispatcher.InvokeAsync(async () =>
        {
            await EnsurePreviewReadyAsync();

            if (_lastPreviewHtml == html)
                PreviewBrowser.NavigateToString(html);
        }, DispatcherPriority.Background);
    }

    private void OnPreviewWebMessageReceived(object? sender, CoreWebView2WebMessageReceivedEventArgs e)
    {
        try
        {
            using var document = JsonDocument.Parse(e.WebMessageAsJson);
            var root = document.RootElement;
            if (!root.TryGetProperty("type", out var typeProperty))
                return;

            var type = typeProperty.GetString();
            if (!string.Equals(type, "zoom-wheel", StringComparison.Ordinal))
                return;

            if (!root.TryGetProperty("delta", out var deltaProperty))
                return;

            ChangeZoom(deltaProperty.GetInt32() * ZoomStepPercent);
        }
        catch
        {
            // ignore malformed preview messages
        }
    }
}