# KiteTodo — 风筝待办

.NET 8 + WPF 桌面效率工具，MVVM 架构，LiteDB 本地数据库。

## 常用命令

```bash
dotnet build              # 编译
dotnet run                # 运行调试
dotnet test               # 当前无测试
# 发布：双击 publish.bat 或手动 dotnet publish -c Release -r win-x64 --self-contained ...
```

编译报错 `CS2012 文件被占用` 时：先关闭运行中的 KiteTodo.exe 再编译。

## 架构速览

```
View (XAML + code-behind)  ←Binding→  ViewModel ([ObservableProperty] / [RelayCommand])  →  Service  →  DatabaseService (LiteDB 单例)
```

- **无 DI 容器**：Service 和 ViewModel 通过 `new` 或静态 `Instance` / `GetSharedInstance()` 获取
- **Code-behind 模式**：XAML 事件在 `.xaml.cs` 中处理，再调用 ViewModel 方法；ViewModel 不引用任何控件
- **ViewModel 基类**：`ObservableObject`（CommunityToolkit.Mvvm），字段用 `_camelCase`，属性自动生成为 `PascalCase`

## 核心技术与约定

| 项 | 说明 |
|---|------|
| **UI 框架** | WPF-UI 3.0.5（`FluentWindow`, `NavigationView`, `CardControl`, `SymbolIcon`） |
| **数据库** | LiteDB，文件在 `%LOCALAPPDATA%\KiteTodo\kitetodo.db`，NoSQL 无需迁移 |
| **主题** | `{DynamicResource CustomXxx}` 12 个自定义画笔，`App.ApplyTheme()` 切换亮/暗 |
| **导航** | `MainWindow.xaml` 中 `NavigationView.MenuItems` 注册 `TargetPageType`，页面是 WPF `Page` |
| **系统托盘** | `H.NotifyIcon`，关闭窗口最小化到托盘，`ShutdownMode=OnExplicitShutdown` |
| **LibVLC** | 运行时嵌入为 ZIP 资源，首次使用时自动解压到 `%LOCALAPPDATA%\KiteTodo\RuntimeCache\libvlc\` |
| **WebView2** | 用于 Markdown 预览和环境音，通过 `EnsureCoreWebView2Async()` 初始化，`NavigateToString()` 注入 HTML |
| **发布** | 自包含单文件 ~77MB（启压缩），WPF 不支持 `PublishTrimmed`（NETSDK1168） |

## 关键文件位置

| 目录/文件 | 内容 |
|-----------|------|
| `App.xaml` | 全局资源定义（12 个 CustomXxx 画笔） |
| `App.xaml.cs` | 启动初始化、主题切换、托盘、浮标、番茄钟桌面提醒 |
| `MainWindow.xaml` | 侧边栏导航菜单注册 |
| `MainWindow.xaml.cs` | 导航启动、禁用 Frame 内置滚动（见下方） |
| `Models/` | 纯数据类，对应 LiteDB 集合 |
| `Services/` | 业务逻辑 + 数据访问，`DatabaseService` 是唯一下层 |
| `ViewModels/` | 页面 ViewModel（`HomeViewModel`, `PomodoroViewModel` 等） |
| `Views/Pages/` | WPF Page（导航页面） |
| `Views/Tools/` | UserControl（工具集合内嵌的子工具） |
| `Converters/` | IValueConverter 实现（BoolToVisibility 等） |
| `Helpers/` | 跨页面通信静态类 |
| `Controls/` | 自定义 WPF 控件 |
| `Views/FloatWindow/` | 浮标窗口 |

## 务必注意的坑

### NavigationView 的 Frame 内置滚动
`MainWindow.xaml.cs` 中 `DisableFrameInternalScroll()` 遍历禁用 NavigationView 内部 Frame 的 ScrollViewer。**不这样做会导致 Page 内的 ScrollViewer 失效。** 新增页面如需滚动，应在 Page 内部自己放 ScrollViewer。

### 页面切换会销毁实例
WPF-UI `NavigationView` 导航时会销毁旧 Page。需要跨页面保持状态的组件必须用静态单例：
- `PomodoroViewModel.Instance` — 静态单例
- `NoiseAndRadioToolView.GetSharedInstance()` — 返回静态单例
- `AmbientSoundToolView.GetSharedInstance()` — 同上

工具视图重新挂载时需处理 reparenting：检查 `view.Parent` 并解除旧父级再设置新 Content。

### 跨页面通信
WPF-UI 不支持参数化导航。使用 `Helpers/` 中的静态类传递数据：
- `FocusRequest` — 页面 A 请求页面 B 聚焦某个项
- `SearchNavigationRequest` — 搜索结果请求跳转到目标页面

### LibVLC 初始化顺序
首次使用前必须先调用 `NoiseAndRadioToolView` 中的 `LibVlcEnsureInitializedAsync()`，它会设置 `SetDllDirectory` 指向运行时缓存目录，否则 `new LibVLC()` 会失败。

### WPF-UI SymbolIcon 命名
格式为 `IconName` + 数字尺寸，如 `MusicNote220`（不是 `MusicNote224`）、`CalendarDay24`。可使用但需验证实际存在的图标名称。

### XAML 设计器报错
WPF-UI 自定义控件导致的设计器兼容性问题，不影响编译和运行，直接 `dotnet run` 即可。

## 代码风格

- 所有文件和注释使用中文，专有名词（类名、方法名、API）保留英文
- Code-behind 方法命名：`OnXxx` 处理事件，调用 ViewModel 的 `Xxx()` 方法
- 在 `ToolCollectionPage.xaml.cs` 中新增工具：添加到 `LoadFrameworkData()` 的 `_tools` 列表
- LiteDB 新增字段无需迁移，NoSQL 自动处理
- 新增导航页：① 创建 Page + ViewModel → ② 在 `MainWindow.xaml` 注册 `NavigationViewItem`
