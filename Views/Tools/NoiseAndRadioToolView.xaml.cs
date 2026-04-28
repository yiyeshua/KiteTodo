using System.Collections.ObjectModel;
using System.ComponentModel;
using System.IO;
using System.IO.Compression;
using System.Net.Http;
using System.Runtime.InteropServices;
using System.Runtime.CompilerServices;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using KiteTodo.Services;
using LibVLCSharp.Shared;
using VlcMediaPlayer = LibVLCSharp.Shared.MediaPlayer;

namespace KiteTodo.Views.Tools;

public partial class NoiseAndRadioToolView : UserControl, IDisposable
{
    private const int PageSize = 10;
    private const string EmbeddedLibVlcArchiveName = "KiteTodo.libvlc-win-x64.zip";
    private const string RuntimeCacheProductFolderName = "KiteTodo";
    private static NoiseAndRadioToolView? _sharedInstance;
    private static bool _libVlcInitialized;

    [DllImport("kernel32", SetLastError = true, CharSet = CharSet.Unicode)]
    private static extern bool SetDllDirectory(string? lpPathName);

    private sealed class RadioPreset : INotifyPropertyChanged
    {
        private bool _isCurrent;
        private bool _isPaused;
        private bool _isFavorite;
        private bool _isPinned;

        public required string Name { get; init; }
        public required string Description { get; init; }
        public required string SourceUrl { get; init; }
        public required bool IsBuiltIn { get; init; }

        public bool IsCurrent
        {
            get => _isCurrent;
            set
            {
                if (_isCurrent == value)
                    return;

                _isCurrent = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusLabel));
            }
        }

        public bool IsPaused
        {
            get => _isPaused;
            set
            {
                if (_isPaused == value)
                    return;

                _isPaused = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(StatusLabel));
            }
        }

        public bool IsFavorite
        {
            get => _isFavorite;
            set
            {
                if (_isFavorite == value)
                    return;

                _isFavorite = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarkerLabel));
            }
        }

        public bool IsPinned
        {
            get => _isPinned;
            set
            {
                if (_isPinned == value)
                    return;

                _isPinned = value;
                OnPropertyChanged();
                OnPropertyChanged(nameof(MarkerLabel));
            }
        }

        public string StatusLabel => IsCurrent ? (IsPaused ? "已暂停" : "播放中") : (IsBuiltIn ? "预设" : "新增");
        public string MarkerLabel => IsPinned && IsFavorite ? "置顶 ★" : IsPinned ? "置顶" : IsFavorite ? "收藏" : string.Empty;
        public string SourceKey => NormalizeSourceKey(SourceUrl);

        public event PropertyChangedEventHandler? PropertyChanged;

        private void OnPropertyChanged([CallerMemberName] string? propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }
    }

    private sealed class SavedRadioPreset
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string SourceUrl { get; set; } = string.Empty;
    }

    private sealed class RadioSettings
    {
        public List<SavedRadioPreset> CustomPresets { get; set; } = [];
        public List<string> FavoriteSourceKeys { get; set; } = [];
        public List<string> PinnedSourceKeys { get; set; } = [];
        public List<string> HiddenBuiltInSourceKeys { get; set; } = [];
        public List<SavedRadioPreset> RecentPresets { get; set; } = [];
        public List<RenamedRadioPreset> RenamedPresets { get; set; } = [];
    }

    private sealed class RenamedRadioPreset
    {
        public string SourceKey { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
    }

    private readonly record struct AddPresetRequest(string Name, string SourceUrl);

    private LibVLC? _libVlc;
    private VlcMediaPlayer? _player;
    private Media? _currentMedia;
    private readonly object _playerSync = new();
    private readonly HttpClient _httpClient = new() { Timeout = TimeSpan.FromSeconds(12) };
    private readonly ObservableCollection<RadioPreset> _allRadioPresets = [];
    private readonly ObservableCollection<RadioPreset> _pagedRadioPresets = [];
    private readonly string _settingsFilePath;

    private RadioSettings _settings = new();
    private string? _currentPlayableSource;
    private string? _currentDisplaySource;
    private string? _currentSourceKey;
    private int _pageIndex;
    private bool _isCurrentPaused;
    private bool _isStartingPlayback;
    private bool _favoritesViewOnly;
    private bool _disposed;

    public static NoiseAndRadioToolView GetSharedInstance()
    {
        if (_sharedInstance == null || _sharedInstance._disposed)
            _sharedInstance = new NoiseAndRadioToolView();

        return _sharedInstance;
    }

    public NoiseAndRadioToolView()
    {
        InitializeComponent();

        EnsureLibVlcRuntime();

        _settingsFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "KiteTodo",
            "radio-presets.json");

        Directory.CreateDirectory(Path.GetDirectoryName(_settingsFilePath)!);

        RadioPresetsListBox.ItemsSource = _pagedRadioPresets;
        LoadSettings();
        ReloadRadioPresets();
        UpdatePlaybackActionButton();

        CurrentRadioUrlTextBox.Text = "等待选择电台";
    }

    private static void EnsureLibVlcRuntime()
    {
        if (_libVlcInitialized)
            return;

        string? runtimeDirectory;
        try
        {
            runtimeDirectory = ResolveLibVlcRuntimeDirectory();
            runtimeDirectory ??= TryExtractEmbeddedLibVlcRuntime();
        }
        catch (Exception ex)
        {
            var cacheRoot = GetLibVlcRuntimeCacheRoot();
            throw new InvalidOperationException($"网络电台运行库初始化失败。程序会尝试将内置 VLC 运行库释放到 {cacheRoot}，但当前未成功。请检查该目录写入权限，或改用安装版。", ex);
        }

        if (runtimeDirectory == null)
            throw new DirectoryNotFoundException("未找到 VLC 播放内核目录，且内置运行库也未能释放。请重新发布程序，或改用安装版。")
            {
                Data = { ["LibVlcRuntime"] = "libvlc\\win-x64" }
            };

        PrepareNativeLibrarySearchPath(runtimeDirectory);

        Core.Initialize(runtimeDirectory);
        _libVlcInitialized = true;
    }

    private static void PrepareNativeLibrarySearchPath(string runtimeDirectory)
    {
        SetDllDirectory(runtimeDirectory);

        var currentPath = Environment.GetEnvironmentVariable("PATH") ?? string.Empty;
        var normalizedRuntime = Path.GetFullPath(runtimeDirectory).TrimEnd(Path.DirectorySeparatorChar);

        var containsRuntime = currentPath
            .Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries)
            .Any(path => string.Equals(
                Path.GetFullPath(path).TrimEnd(Path.DirectorySeparatorChar),
                normalizedRuntime,
                StringComparison.OrdinalIgnoreCase));

        if (!containsRuntime)
            Environment.SetEnvironmentVariable("PATH", runtimeDirectory + Path.PathSeparator + currentPath);
    }

    private static string? ResolveLibVlcRuntimeDirectory()
    {
        var runtimeFolderName = Environment.Is64BitProcess ? Path.Combine("libvlc", "win-x64") : Path.Combine("libvlc", "win-x86");
        var baseDirectory = AppContext.BaseDirectory;
        var currentDirectory = Environment.CurrentDirectory;
        var candidateDirectories = new[]
        {
            Path.Combine(baseDirectory, runtimeFolderName),
            Path.Combine(currentDirectory, runtimeFolderName),
            Path.Combine(Directory.GetParent(baseDirectory)?.FullName ?? string.Empty, runtimeFolderName),
            Path.Combine(Directory.GetParent(currentDirectory)?.FullName ?? string.Empty, runtimeFolderName)
        };

        foreach (var candidate in candidateDirectories.Distinct(StringComparer.OrdinalIgnoreCase))
        {
            if (File.Exists(Path.Combine(candidate, "libvlc.dll")) &&
                File.Exists(Path.Combine(candidate, "libvlccore.dll")))
            {
                return candidate;
            }
        }

        return null;
    }

    private static string? TryExtractEmbeddedLibVlcRuntime()
    {
        if (!Environment.Is64BitProcess)
            return null;

        var cacheRoot = GetLibVlcRuntimeCacheRoot();
        var extractionDirectory = Path.Combine(cacheRoot, GetCurrentRuntimeCacheVersionFolderName());

        if (IsLibVlcRuntimeDirectory(extractionDirectory))
        {
            CleanupOldRuntimeCaches(cacheRoot, extractionDirectory);
            return extractionDirectory;
        }

        if (Directory.Exists(extractionDirectory))
        {
            try
            {
                Directory.Delete(extractionDirectory, recursive: true);
            }
            catch
            {
            }
        }

        Directory.CreateDirectory(extractionDirectory);

        var assembly = typeof(NoiseAndRadioToolView).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedLibVlcArchiveName);
        if (stream == null)
            return null;

        ZipFile.ExtractToDirectory(stream, extractionDirectory, overwriteFiles: true);

        if (!IsLibVlcRuntimeDirectory(extractionDirectory))
            return null;

        CleanupOldRuntimeCaches(cacheRoot, extractionDirectory);
        return extractionDirectory;
    }

    private static string GetLibVlcRuntimeCacheRoot()
    {
        return Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            RuntimeCacheProductFolderName,
            "RuntimeCache",
            "libvlc",
            Environment.Is64BitProcess ? "win-x64" : "win-x86");
    }

    private static string GetCurrentRuntimeCacheVersionFolderName()
    {
        var version = typeof(NoiseAndRadioToolView).Assembly.GetName().Version?.ToString() ?? "1.0.0.0";
        return $"vlc-{version}";
    }

    private static void CleanupOldRuntimeCaches(string cacheRoot, string activeRuntimeDirectory)
    {
        if (!Directory.Exists(cacheRoot))
            return;

        foreach (var directory in Directory.GetDirectories(cacheRoot))
        {
            if (string.Equals(
                Path.GetFullPath(directory).TrimEnd(Path.DirectorySeparatorChar),
                Path.GetFullPath(activeRuntimeDirectory).TrimEnd(Path.DirectorySeparatorChar),
                StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            try
            {
                Directory.Delete(directory, recursive: true);
            }
            catch
            {
            }
        }
    }

    private static bool IsLibVlcRuntimeDirectory(string candidate)
    {
        return File.Exists(Path.Combine(candidate, "libvlc.dll")) &&
               File.Exists(Path.Combine(candidate, "libvlccore.dll")) &&
               Directory.Exists(Path.Combine(candidate, "plugins"));
    }

    private VlcMediaPlayer EnsurePlayer()
    {
        lock (_playerSync)
        {
            if (_player != null)
                return _player;

            try
            {
                _libVlc ??= new LibVLC("--quiet");
                _player = new VlcMediaPlayer(_libVlc);
                _player.Playing += OnPlayerPlaying;
                _player.Paused += OnPlayerPaused;
                _player.Stopped += OnPlayerStopped;
                _player.EndReached += OnPlayerEndReached;
                _player.EncounteredError += OnPlayerEncounteredError;
                _player.Volume = (int)Math.Round(VolumeSlider.Value);
                return _player;
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException("当前系统无法初始化媒体播放组件。", ex);
            }
        }
    }

    private static List<SavedRadioPreset> GetBuiltInPresets()
    {
        return
        [
            new SavedRadioPreset { Name = "WALM Contemporary Christian", Description = "Christian", SourceUrl = "https://icecast1.walmradio.com:8443/walm_opus" },
            new SavedRadioPreset { Name = "WALM Traditional Christian", Description = "Classical", SourceUrl = "https://icecast1.walmradio.com:8443/walm2_opus" },
            new SavedRadioPreset { Name = "WALM Christmas Vinyl", Description = "Christmas", SourceUrl = "https://icecast3.walmradio.com:8443/christmas_opus" },
            new SavedRadioPreset { Name = "WALM Classic Vinyl", Description = "Classic", SourceUrl = "https://icecast3.walmradio.com:8443/classic_opus" },
            new SavedRadioPreset { Name = "WALM Jazz", Description = "Jazz", SourceUrl = "https://icecast2.walmradio.com:8443/jazz_opus" },
            new SavedRadioPreset { Name = "WALM OTR", Description = "Old Time Radio", SourceUrl = "https://icecast2.walmradio.com:8443/otr_opus" },
            new SavedRadioPreset { Name = "SomaFM Groove Salad", Description = "氛围 / Downtempo", SourceUrl = "https://somafm.com/groovesalad.pls" },
            new SavedRadioPreset { Name = "SomaFM Drone Zone", Description = "深空 / 环境氛围", SourceUrl = "https://somafm.com/dronezone.pls" },
            new SavedRadioPreset { Name = "SomaFM Deep Space One", Description = "深空电子 / 实验氛围", SourceUrl = "https://somafm.com/deepspaceone.pls" },
            new SavedRadioPreset { Name = "SomaFM Secret Agent", Description = "Lounge / 复古特工", SourceUrl = "https://somafm.com/secretagent.pls" },
            new SavedRadioPreset { Name = "SomaFM Illinois Street Lounge", Description = "Vintage Lounge / 轻松办公", SourceUrl = "https://somafm.com/illstreet.pls" },
            new SavedRadioPreset { Name = "SomaFM Lush", Description = "女性人声 / Dream Pop", SourceUrl = "https://somafm.com/lush.pls" },
            new SavedRadioPreset { Name = "SomaFM Beat Blender", Description = "节拍电子 / Breaks", SourceUrl = "https://somafm.com/beatblender.pls" },
            new SavedRadioPreset { Name = "SomaFM Sonic Universe", Description = "Jazz / Groove / Fusion", SourceUrl = "https://somafm.com/sonicuniverse.pls" },
            new SavedRadioPreset { Name = "SomaFM PopTron", Description = "Indie Pop / 电子流行", SourceUrl = "https://somafm.com/poptron.pls" },
            new SavedRadioPreset { Name = "SomaFM DEF CON Radio", Description = "黑客大会 / Talk & Music", SourceUrl = "https://somafm.com/defcon.pls" }
        ];
    }

    private void LoadSettings()
    {
        if (!File.Exists(_settingsFilePath))
        {
            _settings = new RadioSettings();
            return;
        }

        try
        {
            _settings = JsonSerializer.Deserialize<RadioSettings>(File.ReadAllText(_settingsFilePath)) ?? new RadioSettings();
            _settings.CustomPresets ??= [];
            _settings.FavoriteSourceKeys ??= [];
            _settings.PinnedSourceKeys ??= [];
            _settings.HiddenBuiltInSourceKeys ??= [];
            _settings.RecentPresets ??= [];
            _settings.RenamedPresets ??= [];
        }
        catch
        {
            _settings = new RadioSettings();
        }
    }

    private void SaveSettings()
    {
        var json = JsonSerializer.Serialize(_settings, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_settingsFilePath, json);
    }

    private void ReloadRadioPresets(string? preferredSourceKey = null)
    {
        preferredSourceKey ??= _currentSourceKey ?? (RadioPresetsListBox.SelectedItem as RadioPreset)?.SourceKey;

        var ordered = BuildOrderedPresets(_favoritesViewOnly);

        _allRadioPresets.Clear();
        foreach (var preset in ordered)
            _allRadioPresets.Add(preset);

        MovePageToPreset(preferredSourceKey);
        UpdatePagedPresets(preferredSourceKey);
        ApplyPlaybackMarker();
        UpdateFavoritesToggleButton();
    }

    private List<RadioPreset> BuildOrderedPresets(bool favoritesOnly)
    {
        var favoriteSet = _settings.FavoriteSourceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var pinnedSet = _settings.PinnedSourceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var hiddenSet = _settings.HiddenBuiltInSourceKeys.ToHashSet(StringComparer.OrdinalIgnoreCase);
        var renamedMap = _settings.RenamedPresets
            .Where(item => !string.IsNullOrWhiteSpace(item.SourceKey) && !string.IsNullOrWhiteSpace(item.Name))
            .GroupBy(item => item.SourceKey, StringComparer.OrdinalIgnoreCase)
            .ToDictionary(group => group.Key, group => group.Last().Name, StringComparer.OrdinalIgnoreCase);

        var presets = new List<RadioPreset>();

        foreach (var preset in GetBuiltInPresets())
        {
            var sourceKey = NormalizeSourceKey(preset.SourceUrl);
            if (hiddenSet.Contains(sourceKey))
                continue;

            presets.Add(CreatePreset(preset, true, favoriteSet, pinnedSet, renamedMap));
        }

        foreach (var preset in _settings.CustomPresets)
        {
            var sourceKey = NormalizeSourceKey(preset.SourceUrl);
            if (presets.Any(item => item.SourceKey == sourceKey))
                continue;

            presets.Add(CreatePreset(preset, false, favoriteSet, pinnedSet, renamedMap));
        }

        var filtered = favoritesOnly
            ? presets.Where(item => item.IsFavorite)
            : presets;

        return filtered
            .OrderByDescending(item => item.IsPinned)
            .ThenByDescending(item => item.IsFavorite)
            .ThenBy(item => item.IsBuiltIn ? 0 : 1)
            .ThenBy(item => item.Name, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static RadioPreset CreatePreset(
        SavedRadioPreset preset,
        bool isBuiltIn,
        HashSet<string> favoriteSet,
        HashSet<string> pinnedSet,
        Dictionary<string, string> renamedMap)
    {
        var sourceKey = NormalizeSourceKey(preset.SourceUrl);
        return new RadioPreset
        {
            Name = renamedMap.TryGetValue(sourceKey, out var renamed) ? renamed : preset.Name,
            Description = BuildCompactDescription(preset.Description, isBuiltIn),
            SourceUrl = preset.SourceUrl,
            IsBuiltIn = isBuiltIn,
            IsFavorite = favoriteSet.Contains(sourceKey),
            IsPinned = pinnedSet.Contains(sourceKey)
        };
    }

    private static string BuildCompactDescription(string? description, bool isBuiltIn)
    {
        var compact = string.IsNullOrWhiteSpace(description)
            ? string.Empty
            : description.Trim()
                .Replace(" / ", "/", StringComparison.Ordinal)
                .Replace(" /", "/", StringComparison.Ordinal)
                .Replace("/ ", "/", StringComparison.Ordinal)
                .Replace(" & ", "&", StringComparison.Ordinal);

        if (!string.IsNullOrWhiteSpace(compact))
            return compact;

        return isBuiltIn ? "网络电台" : "自定义源";
    }

    private void MovePageToPreset(string? sourceKey)
    {
        if (string.IsNullOrWhiteSpace(sourceKey))
            return;

        var index = _allRadioPresets
            .Select((preset, index) => new { preset, index })
            .FirstOrDefault(entry => entry.preset.SourceKey == sourceKey)?.index;

        if (!index.HasValue)
            return;

        _pageIndex = index.Value / PageSize;
    }

    private void UpdatePagedPresets(string? preferredSourceKey = null)
    {
        var totalPages = GetTotalPages();
        if (_pageIndex >= totalPages)
            _pageIndex = Math.Max(0, totalPages - 1);

        _pagedRadioPresets.Clear();
        foreach (var preset in _allRadioPresets.Skip(_pageIndex * PageSize).Take(PageSize))
            _pagedRadioPresets.Add(preset);

        PageInfoTextBlock.Text = $"第 {_pageIndex + 1} / {totalPages} 页，共 {_allRadioPresets.Count} 条";
        PreviousPageButton.IsEnabled = _pageIndex > 0;
        NextPageButton.IsEnabled = _pageIndex < totalPages - 1;

        var selected = _pagedRadioPresets.FirstOrDefault(item => item.SourceKey == preferredSourceKey);
        if (selected != null)
            RadioPresetsListBox.SelectedItem = selected;
        else if (_pagedRadioPresets.Count > 0)
            RadioPresetsListBox.SelectedIndex = 0;
        else
            RadioPresetsListBox.SelectedItem = null;

        UpdateFavoritesEmptyState();
    }

    private int GetTotalPages()
    {
        return Math.Max(1, (int)Math.Ceiling(_allRadioPresets.Count / (double)PageSize));
    }

    private void ApplyPlaybackMarker()
    {
        foreach (var preset in _allRadioPresets)
        {
            preset.IsCurrent = preset.SourceKey == _currentSourceKey;
            preset.IsPaused = preset.IsCurrent && _isCurrentPaused;
        }

        UpdatePlaybackActionButton();
    }

    private void UpdatePlaybackActionButton()
    {
        if (_isStartingPlayback)
        {
            PlaybackToggleButton.IsEnabled = false;
            PlaybackToggleButton.Content = "正在启动...";
            return;
        }

        if (_currentPlayableSource == null || _player == null)
        {
            PlaybackToggleButton.IsEnabled = RadioPresetsListBox.SelectedItem is RadioPreset;
            PlaybackToggleButton.Content = "开始播放";
            return;
        }

        PlaybackToggleButton.IsEnabled = true;
        PlaybackToggleButton.Content = _isCurrentPaused ? "继续播放" : "暂停播放";
    }

    private void UpdateFavoritesToggleButton()
    {
        if (FavoritesToggleButton == null)
            return;

        FavoritesToggleButton.Content = _favoritesViewOnly ? "全部列表" : "收藏列表";
        FavoritesToggleButton.Appearance = _favoritesViewOnly ? Wpf.Ui.Controls.ControlAppearance.Primary : Wpf.Ui.Controls.ControlAppearance.Secondary;
    }

    private void UpdateFavoritesEmptyState()
    {
        if (FavoritesEmptyStateBorder == null)
            return;

        FavoritesEmptyStateBorder.Visibility = _favoritesViewOnly && _pagedRadioPresets.Count == 0
            ? Visibility.Visible
            : Visibility.Collapsed;
    }

    private void OnRadioPresetSelectionChanged(object sender, SelectionChangedEventArgs e)
    {
        if (RadioPresetsListBox.SelectedItem is not RadioPreset preset)
        {
            UpdatePlaybackActionButton();
            return;
        }

        CurrentRadioNameTextBlock.Text = preset.Name;
        CurrentRadioUrlTextBox.Text = preset.SourceUrl;
        if (preset.SourceKey != _currentSourceKey)
            PlaybackStatusTextBlock.Text = "双击该电台开始播放";

        UpdatePlaybackActionButton();
    }

    private async void OnRadioPresetMouseDoubleClick(object sender, MouseButtonEventArgs e)
    {
        if (RadioPresetsListBox.SelectedItem is not RadioPreset preset)
            return;

        if (_currentSourceKey == preset.SourceKey && _currentPlayableSource != null)
        {
            if (_player == null)
            {
                await PlayRadioAsync(preset);
                return;
            }

            if (_isCurrentPaused)
            {
                _player.Play();
                _isCurrentPaused = false;
                PlaybackStatusTextBlock.Text = "正在播放";
            }
            else
            {
                _player.Pause();
                _isCurrentPaused = true;
                PlaybackStatusTextBlock.Text = "已暂停";
            }

            ApplyPlaybackMarker();
            return;
        }

        await PlayRadioAsync(preset);
    }

    private async void OnTogglePlayback(object sender, RoutedEventArgs e)
    {
        if (_player == null || _currentPlayableSource == null)
        {
            if (RadioPresetsListBox.SelectedItem is RadioPreset selectedPreset)
            {
                await PlayRadioAsync(selectedPreset);
            }

            return;
        }

        if (_isCurrentPaused)
        {
            _player.Play();
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "正在播放";
        }
        else
        {
            _player.Pause();
            _isCurrentPaused = true;
            PlaybackStatusTextBlock.Text = "已暂停";
        }

        ApplyPlaybackMarker();
    }

    private void OnPreviousPage(object sender, RoutedEventArgs e)
    {
        if (_pageIndex <= 0)
            return;

        _pageIndex--;
        UpdatePagedPresets();
    }

    private void OnNextPage(object sender, RoutedEventArgs e)
    {
        if (_pageIndex >= GetTotalPages() - 1)
            return;

        _pageIndex++;
        UpdatePagedPresets();
    }

    private void OnToggleFavoritesView(object sender, RoutedEventArgs e)
    {
        _favoritesViewOnly = !_favoritesViewOnly;
        _pageIndex = 0;
        ReloadRadioPresets();
    }

    private void OnToggleFavoritePreset(object sender, RoutedEventArgs e)
    {
        if (TryGetPresetFromSender(sender, out var preset))
            ToggleFavoritePreset(preset);
    }

    private void OnTogglePinPreset(object sender, RoutedEventArgs e)
    {
        if (TryGetPresetFromSender(sender, out var preset))
            TogglePinPreset(preset);
    }

    private void OnRenamePreset(object sender, RoutedEventArgs e)
    {
        if (TryGetPresetFromSender(sender, out var preset))
            RenamePreset(preset);
    }

    private void OnDeletePreset(object sender, RoutedEventArgs e)
    {
        if (TryGetPresetFromSender(sender, out var preset))
            DeletePreset(preset);
    }

    private void OnRestoreDefaultPresets(object sender, RoutedEventArgs e)
    {
        if (_settings.HiddenBuiltInSourceKeys.Count == 0)
        {
            MessageBox.Show("当前没有被删除的默认电台。", "恢复默认电台", MessageBoxButton.OK, MessageBoxImage.Information);
            return;
        }

        _settings.HiddenBuiltInSourceKeys.Clear();
        SaveSettings();
        ReloadRadioPresets();
    }

    private static bool TryGetPresetFromSender(object sender, out RadioPreset preset)
    {
        preset = null!;
        if (sender is MenuItem { DataContext: RadioPreset dataContext })
        {
            preset = dataContext;
            return true;
        }

        return false;
    }

    private void ToggleFavoritePreset(RadioPreset preset)
    {
        var sourceKey = preset.SourceKey;
        if (_settings.FavoriteSourceKeys.Any(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase)))
            _settings.FavoriteSourceKeys.RemoveAll(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));
        else
            _settings.FavoriteSourceKeys.Add(sourceKey);

        SaveSettings();
        ReloadRadioPresets(sourceKey);
    }

    private void TogglePinPreset(RadioPreset preset)
    {
        var sourceKey = preset.SourceKey;
        if (_settings.PinnedSourceKeys.Any(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase)))
            _settings.PinnedSourceKeys.RemoveAll(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));
        else
            _settings.PinnedSourceKeys.Add(sourceKey);

        SaveSettings();
        ReloadRadioPresets(sourceKey);
    }

    private void RenamePreset(RadioPreset preset)
    {
        if (!TryShowRenamePresetDialog(preset.Name, out var newName))
            return;

        var sourceKey = preset.SourceKey;
        var trimmedName = newName.Trim();
        var existingCustom = _settings.CustomPresets.FirstOrDefault(item => NormalizeSourceKey(item.SourceUrl) == sourceKey);
        if (existingCustom != null)
        {
            existingCustom.Name = trimmedName;
        }

        _settings.RenamedPresets.RemoveAll(item => item.SourceKey.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));
        _settings.RenamedPresets.Add(new RenamedRadioPreset
        {
            SourceKey = sourceKey,
            Name = trimmedName
        });

        SaveSettings();
        ReloadRadioPresets(sourceKey);
    }

    private void DeletePreset(RadioPreset preset)
    {
        if (MessageBox.Show($"确定删除电台“{preset.Name}”吗？", "删除电台", MessageBoxButton.YesNo, MessageBoxImage.Question) != MessageBoxResult.Yes)
            return;

        var sourceKey = preset.SourceKey;
        if (preset.IsBuiltIn)
        {
            if (!_settings.HiddenBuiltInSourceKeys.Any(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase)))
                _settings.HiddenBuiltInSourceKeys.Add(sourceKey);
        }
        else
        {
            _settings.CustomPresets.RemoveAll(item => NormalizeSourceKey(item.SourceUrl) == sourceKey);
        }

        _settings.FavoriteSourceKeys.RemoveAll(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));
        _settings.PinnedSourceKeys.RemoveAll(item => item.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));
        _settings.RenamedPresets.RemoveAll(item => item.SourceKey.Equals(sourceKey, StringComparison.OrdinalIgnoreCase));

        SaveSettings();
        ReloadRadioPresets();
    }

    private async void OnAddPreset(object sender, RoutedEventArgs e)
    {
        if (!TryShowAddPresetDialog(out var request))
            return;

        var sourceKey = NormalizeSourceKey(request.SourceUrl);
        if (HasExistingPreset(sourceKey, out var existingName))
        {
            MessageBox.Show($"该电台源已存在：{existingName}", "新增电台源", MessageBoxButton.OK, MessageBoxImage.Information);
            MovePageToPreset(sourceKey);
            UpdatePagedPresets(sourceKey);
            return;
        }

        try
        {
            PlaybackStatusTextBlock.Text = "正在校验新增电台源...";
            await ResolvePlayableStreamAsync(request.SourceUrl);
        }
        catch (Exception ex)
        {
            PlaybackStatusTextBlock.Text = "新增源校验失败";
            MessageBox.Show($"该源暂时不可用或无法解析：{ex.Message}", "新增电台源", MessageBoxButton.OK, MessageBoxImage.Warning);
            return;
        }

        _settings.CustomPresets.Add(new SavedRadioPreset
        {
            Name = string.IsNullOrWhiteSpace(request.Name) ? BuildPresetName(request.SourceUrl) : request.Name.Trim(),
            Description = "自定义源",
            SourceUrl = request.SourceUrl.Trim()
        });

        SaveSettings();
        ReloadRadioPresets(sourceKey);
        PlaybackStatusTextBlock.Text = "新增源已保存，双击列表项即可播放";
    }

    private bool HasExistingPreset(string sourceKey, out string existingName)
    {
        var existingPreset = _allRadioPresets.FirstOrDefault(item => item.SourceKey == sourceKey);
        if (existingPreset != null)
        {
            existingName = existingPreset.Name;
            return true;
        }

        var customPreset = _settings.CustomPresets.FirstOrDefault(item => NormalizeSourceKey(item.SourceUrl) == sourceKey);
        if (customPreset != null)
        {
            existingName = customPreset.Name;
            return true;
        }

        var builtInPreset = GetBuiltInPresets().FirstOrDefault(item => NormalizeSourceKey(item.SourceUrl) == sourceKey);
        if (builtInPreset != null)
        {
            existingName = builtInPreset.Name;
            return true;
        }

        existingName = string.Empty;
        return false;
    }

    private bool TryShowAddPresetDialog(out AddPresetRequest request)
    {
        request = default;

        var owner = Window.GetWindow(this);
        var nameBox = new TextBox
        {
            MinWidth = 340,
            Margin = new Thickness(0, 8, 0, 0)
        };
        var urlBox = new TextBox
        {
            MinWidth = 340,
            Margin = new Thickness(0, 8, 0, 0)
        };

        var hintText = new TextBlock
        {
            Text = "请输入公开电台源地址，支持 m3u / m3u8 / pls 或直接流媒体地址。名称可选，不填则自动生成。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.DimGray
        };

        var nameLabel = new TextBlock
        {
            Text = "显示名称（可选）",
            Margin = new Thickness(0, 12, 0, 0),
            FontWeight = FontWeights.SemiBold
        };

        var urlLabel = new TextBlock
        {
            Text = "源地址",
            Margin = new Thickness(0, 12, 0, 0),
            FontWeight = FontWeights.SemiBold
        };

        var okButton = new Button { Content = "校验并保存", Width = 96, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new Button { Content = "取消", Width = 76, IsCancel = true };
        var pasteButton = new Button { Content = "粘贴地址", Width = 88, Margin = new Thickness(0, 0, 8, 0) };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 16, 0, 0)
        };
        buttonPanel.Children.Add(pasteButton);
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(hintText);
        root.Children.Add(nameLabel);
        root.Children.Add(nameBox);
        root.Children.Add(urlLabel);
        root.Children.Add(urlBox);
        root.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = "新增电台源",
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 440,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false
        };

        pasteButton.Click += (_, _) =>
        {
            if (!Clipboard.ContainsText())
                return;

            urlBox.Text = Clipboard.GetText().Trim();
            urlBox.SelectAll();
            urlBox.Focus();
        };

        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(urlBox.Text))
            {
                MessageBox.Show(dialog, "请先输入电台源地址。", "新增电台源", MessageBoxButton.OK, MessageBoxImage.Information);
                urlBox.Focus();
                return;
            }

            dialog.DialogResult = true;
        };

        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) => urlBox.Focus();

        if (dialog.ShowDialog() != true)
            return false;

        request = new AddPresetRequest(nameBox.Text, urlBox.Text);
        return true;
    }

    private bool TryShowRenamePresetDialog(string initialName, out string result)
    {
        result = string.Empty;

        var owner = Window.GetWindow(this);
        var inputBox = new TextBox
        {
            MinWidth = 320,
            Margin = new Thickness(0, 8, 0, 0),
            Text = initialName
        };

        var hintText = new TextBlock
        {
            Text = "输入新的电台显示名称。",
            TextWrapping = TextWrapping.Wrap,
            FontSize = 12,
            Foreground = Brushes.DimGray
        };

        var okButton = new Button { Content = "保存", Width = 76, Margin = new Thickness(0, 0, 8, 0), IsDefault = true };
        var cancelButton = new Button { Content = "取消", Width = 76, IsCancel = true };

        var buttonPanel = new StackPanel
        {
            Orientation = Orientation.Horizontal,
            HorizontalAlignment = HorizontalAlignment.Right,
            Margin = new Thickness(0, 14, 0, 0)
        };
        buttonPanel.Children.Add(okButton);
        buttonPanel.Children.Add(cancelButton);

        var root = new StackPanel { Margin = new Thickness(16) };
        root.Children.Add(hintText);
        root.Children.Add(inputBox);
        root.Children.Add(buttonPanel);

        var dialog = new Window
        {
            Title = "重命名电台",
            Content = root,
            SizeToContent = SizeToContent.WidthAndHeight,
            MinWidth = 420,
            ResizeMode = ResizeMode.NoResize,
            WindowStartupLocation = owner == null ? WindowStartupLocation.CenterScreen : WindowStartupLocation.CenterOwner,
            Owner = owner,
            ShowInTaskbar = false
        };

        okButton.Click += (_, _) =>
        {
            if (string.IsNullOrWhiteSpace(inputBox.Text))
            {
                MessageBox.Show(dialog, "请输入新的电台名称。", "重命名电台", MessageBoxButton.OK, MessageBoxImage.Information);
                inputBox.Focus();
                return;
            }

            dialog.DialogResult = true;
        };
        cancelButton.Click += (_, _) => dialog.DialogResult = false;
        dialog.Loaded += (_, _) =>
        {
            inputBox.Focus();
            inputBox.SelectAll();
        };

        if (dialog.ShowDialog() != true)
            return false;

        result = inputBox.Text;
        return true;
    }

    public Task<bool> PlayRandomRadioAsync()
    {
        return PlayRandomRadioInternalAsync(skipCurrentPreset: false);
    }

    public Task<bool> PlayNextRandomRadioAsync()
    {
        return PlayRandomRadioInternalAsync(skipCurrentPreset: true);
    }

    private async Task<bool> PlayRandomRadioInternalAsync(bool skipCurrentPreset)
    {
        if (_isStartingPlayback)
            return false;

        var candidates = BuildOrderedPresets(false);
        if (candidates.Count == 0)
        {
            MessageBox.Show("当前没有可用电台可供随机播放。", "网络电台", MessageBoxButton.OK, MessageBoxImage.Information);
            return false;
        }

        var shuffled = candidates
            .OrderBy(_ => Random.Shared.Next())
            .ToList();

        if (!string.IsNullOrWhiteSpace(_currentSourceKey) && shuffled.Count > 1)
        {
            if (skipCurrentPreset)
            {
                shuffled = shuffled
                    .Where(item => item.SourceKey != _currentSourceKey)
                    .ToList();
            }
            else
            {
                shuffled = shuffled
                    .OrderBy(item => item.SourceKey == _currentSourceKey ? 1 : 0)
                    .ThenBy(_ => Random.Shared.Next())
                    .ToList();
            }
        }

        foreach (var preset in shuffled)
        {
            if (await TryPlayRadioAsync(preset, showErrorMessage: false))
            {
                MovePageToPreset(preset.SourceKey);
                UpdatePagedPresets(preset.SourceKey);
                PlaybackStatusTextBlock.Text = skipCurrentPreset
                    ? $"已切换随机台：{preset.Name}"
                    : $"已随机播放：{preset.Name}";
                AlertService.ShowCornerToast(
                    skipCurrentPreset
                        ? $"已切换到：{preset.Name}"
                        : $"随机电台：{preset.Name}",
                    2);
                ApplyPlaybackMarker();
                return true;
            }
        }

        PlaybackStatusTextBlock.Text = skipCurrentPreset ? "下一随机台失败" : "随机电台失败";
        ApplyPlaybackMarker();
        MessageBox.Show(
            skipCurrentPreset
                ? "尝试切换到其他随机电台，但当前没有成功播放的新电台。请稍后重试或手动选择。"
                : "随机尝试了多个电台，但当前都无法播放。请稍后重试或手动选择其他电台。",
            "网络电台",
            MessageBoxButton.OK,
            MessageBoxImage.Warning);
        return false;
    }

    private Task PlayRadioAsync(RadioPreset preset)
    {
        return TryPlayRadioAsync(preset, showErrorMessage: true);
    }

    private async Task<bool> TryPlayRadioAsync(RadioPreset preset, bool showErrorMessage)
    {
        if (_isStartingPlayback)
            return false;

        try
        {
            _isStartingPlayback = true;
            PlaybackStatusTextBlock.Text = "正在解析电台源...";
            UpdatePlaybackActionButton();
            CurrentRadioNameTextBlock.Text = preset.Name;
            _currentDisplaySource = preset.SourceUrl;

            var resolvedUrl = await ResolvePlayableStreamAsync(preset.SourceUrl);
            _currentPlayableSource = resolvedUrl;
            _currentSourceKey = preset.SourceKey;
            _isCurrentPaused = false;

            await Task.Run(() =>
            {
                var player = EnsurePlayer();
                var libVlc = _libVlc ?? throw new InvalidOperationException("VLC 播放内核尚未初始化。");
                OpenAndPlay(libVlc, player, resolvedUrl);
            });

            CurrentRadioUrlTextBox.Text = resolvedUrl.Equals(preset.SourceUrl, StringComparison.OrdinalIgnoreCase)
                ? resolvedUrl
                : $"原始地址：{preset.SourceUrl}{Environment.NewLine}{Environment.NewLine}实际播放地址：{resolvedUrl}";
            PlaybackStatusTextBlock.Text = "正在连接网络电台";
            ApplyPlaybackMarker();
            return true;
        }
        catch (Exception ex)
        {
            _currentPlayableSource = null;
            _currentSourceKey = null;
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "电台播放失败";
            ApplyPlaybackMarker();
            if (showErrorMessage)
                MessageBox.Show($"电台播放失败：{ex.Message}", "网络电台", MessageBoxButton.OK, MessageBoxImage.Warning);

            return false;
        }
        finally
        {
            _isStartingPlayback = false;
            UpdatePlaybackActionButton();
        }
    }

    private void OpenAndPlay(LibVLC libVlc, VlcMediaPlayer player, string source)
    {
        lock (_playerSync)
        {
            _currentMedia?.Dispose();
            _currentMedia = new Media(libVlc, source, FromType.FromLocation);
            _currentMedia.AddOption(":http-reconnect=true");
            _currentMedia.AddOption(":http-user-agent=Mozilla/5.0");
            _currentMedia.AddOption(":network-caching=1200");

            if (!player.Play(_currentMedia))
                throw new InvalidOperationException("播放器没有接受该音频流。可能是流媒体源不兼容或暂时不可用。");
        }
    }

    private async Task<string> ResolvePlayableStreamAsync(string sourceInput)
    {
        var trimmed = sourceInput.Trim();
        if (!Uri.TryCreate(trimmed, UriKind.Absolute, out var sourceUri))
            throw new InvalidOperationException("音频源地址格式不正确。请填写 http/https 地址。") ;

        var lower = sourceUri.AbsolutePath.ToLowerInvariant();
        if (!lower.EndsWith(".m3u") && !lower.EndsWith(".m3u8") && !lower.EndsWith(".pls"))
            return sourceUri.AbsoluteUri;

        var content = await _httpClient.GetStringAsync(sourceUri);
        var parsed = lower.EndsWith(".pls")
            ? ParsePls(content)
            : ParseM3u(content, sourceUri);

        return parsed ?? throw new InvalidOperationException("没有从播放列表中解析出可用的音频流地址。");
    }

    private static string? ParsePls(string content)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            if (!line.StartsWith("File", StringComparison.OrdinalIgnoreCase))
                continue;

            var separatorIndex = line.IndexOf('=');
            if (separatorIndex < 0 || separatorIndex >= line.Length - 1)
                continue;

            var value = line[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(value))
                return value;
        }

        return null;
    }

    private static string? ParseM3u(string content, Uri sourceUri)
    {
        using var reader = new StringReader(content);
        while (reader.ReadLine() is { } line)
        {
            var trimmed = line.Trim();
            if (string.IsNullOrWhiteSpace(trimmed) || trimmed.StartsWith('#'))
                continue;

            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var absoluteUri))
                return absoluteUri.AbsoluteUri;

            if (Uri.TryCreate(sourceUri, trimmed, out var relativeUri))
                return relativeUri.AbsoluteUri;
        }

        return null;
    }

    private void OnVolumeChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (_player != null)
            _player.Volume = (int)Math.Round(e.NewValue);
    }

    private void OnPlayerPlaying(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "正在播放";
            ApplyPlaybackMarker();
        });
    }

    private void OnPlayerPaused(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _isCurrentPaused = true;
            PlaybackStatusTextBlock.Text = "已暂停";
            ApplyPlaybackMarker();
        });
    }

    private void OnPlayerStopped(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _currentMedia?.Dispose();
            _currentMedia = null;
            _currentPlayableSource = null;
            _currentSourceKey = null;
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "已停止";
            ApplyPlaybackMarker();
        });
    }

    private void OnPlayerEndReached(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _currentMedia?.Dispose();
            _currentMedia = null;
            _currentPlayableSource = null;
            _currentSourceKey = null;
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "播放结束";
            ApplyPlaybackMarker();
        });
    }

    private void OnPlayerEncounteredError(object? sender, EventArgs e)
    {
        Dispatcher.Invoke(() =>
        {
            _currentMedia?.Dispose();
            _currentMedia = null;
            _currentPlayableSource = null;
            _currentSourceKey = null;
            _isCurrentPaused = false;
            PlaybackStatusTextBlock.Text = "播放失败";
            ApplyPlaybackMarker();
            MessageBox.Show("音频播放失败。该电台源可能受限、暂时不可用，或当前编码格式不被底层解码器接受。", "网络电台", MessageBoxButton.OK, MessageBoxImage.Warning);
        });
    }

    private static string BuildPresetName(string sourceUrl)
    {
        if (Uri.TryCreate(sourceUrl, UriKind.Absolute, out var uri))
            return string.IsNullOrWhiteSpace(uri.Host) ? "自定义电台" : uri.Host;

        return "自定义电台";
    }

    private static string NormalizeSourceKey(string sourceUrl)
    {
        return sourceUrl.Trim().ToLowerInvariant();
    }

    public void Dispose()
    {
        if (_disposed)
            return;

        _disposed = true;
        if (ReferenceEquals(_sharedInstance, this))
            _sharedInstance = null;

        if (_player != null)
        {
            _player.Playing -= OnPlayerPlaying;
            _player.Paused -= OnPlayerPaused;
            _player.Stopped -= OnPlayerStopped;
            _player.EndReached -= OnPlayerEndReached;
            _player.EncounteredError -= OnPlayerEncounteredError;
            _player.Stop();
            _player.Dispose();
        }

        _currentMedia?.Dispose();
        _libVlc?.Dispose();
        _httpClient.Dispose();
    }
}