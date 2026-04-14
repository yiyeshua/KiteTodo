# KiteTodo - 风筝待办

一款基于 C# + WPF (.NET 8) 开发的轻量级 Windows 桌面待办事项管理工具。

---

## 目录

- [功能概览](#功能概览)
- [技术栈](#技术栈)
- [环境要求](#环境要求)
- [快速开始](#快速开始)
- [项目结构](#项目结构)
- [架构设计](#架构设计)
  - [MVVM 模式详解](#mvvm-模式详解)
  - [数据流向](#数据流向)
- [核心概念（WPF 入门必读）](#核心概念wpf-入门必读)
  - [XAML 是什么](#xaml-是什么)
  - [数据绑定 (Data Binding)](#数据绑定-data-binding)
  - [ObservableProperty 和 RelayCommand](#observableproperty-和-relaycommand)
  - [DynamicResource 主题系统](#dynamicresource-主题系统)
  - [代码后台 (Code-Behind) vs ViewModel](#代码后台-code-behind-vs-viewmodel)
- [各模块详解](#各模块详解)
  - [Models — 数据模型](#models--数据模型)
  - [Services — 服务层](#services--服务层)
  - [ViewModels — 视图模型](#viewmodels--视图模型)
  - [Views — 界面层](#views--界面层)
  - [Converters — 值转换器](#converters--值转换器)
- [数据存储](#数据存储)
- [系统托盘](#系统托盘)
- [打包发布](#打包发布)
- [常见维护操作](#常见维护操作)
  - [如何添加一个新页面](#如何添加一个新页面)
  - [如何添加新的待办字段](#如何添加新的待办字段)
  - [如何修改主题颜色](#如何修改主题颜色)
  - [如何新增一个设置项](#如何新增一个设置项)
- [依赖库说明](#依赖库说明)
- [常见问题](#常见问题)

---

## 功能概览

| 功能 | 说明 |
|------|------|
| **今日待办** | 首页，添加/编辑/删除/完成待办，支持优先级、截止日期、提醒时间 |
| **周视图** | 以周一~周日 7 列展示本周待办，支持快速添加/删除，可导出周报 |
| **月度总览** | 日历网格展示整月待办概览，点击某天查看详情 |
| **专注计时** | 番茄钟功能（25分钟专注 + 5分钟休息），支持暂停/继续/取消 |
| **数据导出** | 将待办数据导出为 JSON/CSV 文件 |
| **设置** | 主题切换（浅色/深色）、番茄钟时长配置、提醒开关 |
| **系统托盘** | 关闭窗口后最小化到托盘，右键可显示主窗口或退出 |
| **到期提醒** | 后台定时检查待办截止时间，到期时弹出 Windows 通知 |

---

## 技术栈

| 技术 | 版本 | 用途 |
|------|------|------|
| .NET | 8.0 | 运行时框架 |
| WPF | (内含于 .NET 8) | Windows 桌面 UI 框架 |
| WPF-UI | 3.0.5 | Fluent Design 风格控件库（现代化 UI） |
| CommunityToolkit.Mvvm | 8.3.2 | MVVM 框架，提供属性变更通知和命令绑定 |
| LiteDB | 5.0.21 | 嵌入式 NoSQL 本地数据库 |
| H.NotifyIcon | 2.4.1 | 系统托盘图标和菜单 |
| Microsoft.Toolkit.Uwp.Notifications | 7.1.3 | Windows Toast 通知 |

---

## 环境要求

- **操作系统**：Windows 10 (1809+) 或 Windows 11
- **开发环境**：
  - [.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)
  - Visual Studio 2022 (推荐) 或 VS Code + C# 扩展
- **运行环境**（使用发布后的 exe）：
  - 自包含模式：无需额外安装，双击即可运行
  - 框架依赖模式：需安装 .NET 8 Desktop Runtime

---

## 快速开始

### 开发调试

```bash
# 克隆项目后，进入项目目录
cd KiteTodo

# 还原依赖包
dotnet restore

# 运行（调试模式）
dotnet run

# 或者构建后运行
dotnet build
```

### 打包发布

项目根目录下有一个 `publish.bat` 一键打包脚本：

```bash
# 双击运行 publish.bat，或在命令行执行：
publish.bat
```

脚本会自动：
1. 结束已运行的 KiteTodo 进程
2. 清理旧的编译产物
3. 发布为自包含单文件（含压缩，约 77MB）
4. 将产物复制到项目根目录为 `KiteTodo-Release.exe`

---

## 项目结构

```
KiteTodo/
├── KiteTodo.csproj          # 项目配置文件（依赖包、编译选项）
├── app.ico                  # 应用程序图标
├── publish.bat              # 一键打包脚本
│
├── App.xaml                 # 应用级资源定义（主题、全局样式）
├── App.xaml.cs              # 应用启动逻辑（托盘、主题切换、窗口管理）
│
├── MainWindow.xaml          # 主窗口布局（导航栏 + 内容区）
├── MainWindow.xaml.cs       # 主窗口代码后台（导航事件处理）
│
├── Models/                  # 数据模型（纯数据类）
│   ├── TodoItem.cs          #   待办事项模型
│   ├── PomodoroRecord.cs    #   番茄钟记录模型
│   └── AppSettings.cs       #   应用设置模型
│
├── Services/                # 服务层（数据操作、业务逻辑）
│   ├── DatabaseService.cs   #   数据库连接管理（LiteDB 单例）
│   ├── TodoService.cs       #   待办的增删改查
│   ├── ExportService.cs     #   数据导出（JSON/CSV）
│   └── ReminderService.cs   #   到期提醒（后台定时检查）
│
├── ViewModels/              # 视图模型（连接界面和数据）
│   ├── HomeViewModel.cs     #   首页 ViewModel
│   ├── WeeklyViewModel.cs   #   周视图 ViewModel
│   ├── MonthlyViewModel.cs  #   月视图 ViewModel
│   ├── PomodoroViewModel.cs #   番茄钟 ViewModel
│   ├── ExportViewModel.cs   #   导出页 ViewModel
│   └── SettingsViewModel.cs #   设置页 ViewModel
│
├── Views/Pages/             # 页面（XAML 界面 + 代码后台）
│   ├── HomePage.xaml/.cs    #   今日待办页面
│   ├── WeeklyPage.xaml/.cs  #   周视图页面
│   ├── MonthlyPage.xaml/.cs #   月度总览页面
│   ├── PomodoroPage.xaml/.cs#   番茄钟页面
│   ├── ExportPage.xaml/.cs  #   数据导出页面
│   └── SettingsPage.xaml/.cs#   设置页面
│
└── Converters/              # 值转换器（XAML 绑定时的数据格式转换）
    └── BoolConverters.cs    #   多个 bool 相关的转换器
```

---

## 架构设计

### MVVM 模式详解

本项目使用 **MVVM (Model-View-ViewModel)** 架构模式，这是 WPF 开发的标准模式。三层各自的职责如下：

```
┌─────────────────────────────────────────────────────────────┐
│  View (视图层)                                               │
│  XAML 文件定义界面长什么样，Code-Behind 处理界面事件           │
│  例: HomePage.xaml + HomePage.xaml.cs                        │
│                                                             │
│         ↕  数据绑定 (Binding)  +  事件回调                   │
│                                                             │
│  ViewModel (视图模型层)                                      │
│  包含页面需要的数据属性和操作命令，是 View 和 Model 的桥梁    │
│  例: HomeViewModel.cs                                       │
│                                                             │
│         ↕  调用 Service 方法                                 │
│                                                             │
│  Model + Service (模型层 + 服务层)                           │
│  Model 定义数据结构，Service 负责数据的读写和业务逻辑         │
│  例: TodoItem.cs + TodoService.cs                           │
└─────────────────────────────────────────────────────────────┘
```

**为什么用 MVVM？**
- **关注点分离**：界面代码和业务逻辑分开，修改界面不影响逻辑，反之亦然
- **可测试性**：ViewModel 不依赖界面，可以单独写单元测试
- **数据绑定**：WPF 的绑定机制天然支持 MVVM，界面自动跟随数据更新

### 数据流向

以"用户添加一个待办"为例：

```
用户点击"添加"按钮
    ↓
HomePage.xaml.cs 的 OnAddTodo 事件被触发
    ↓
调用 HomeViewModel.AddTodo() 方法
    ↓
ViewModel 调用 TodoService.Insert(todoItem)
    ↓
TodoService 通过 DatabaseService 将数据写入 LiteDB
    ↓
ViewModel 更新 ObservableCollection<TodoItem>（待办列表）
    ↓
WPF 绑定机制自动通知界面刷新列表显示
```

---

## 核心概念（WPF 入门必读）

### XAML 是什么

XAML (Extensible Application Markup Language) 是 WPF 用来描述界面的标记语言，类似 HTML。

```xml
<!-- 一个简单的按钮 -->
<Button Content="点击我" Click="OnButtonClick" />

<!-- 一个文本绑定到 ViewModel 的 Title 属性 -->
<TextBlock Text="{Binding Title}" />
```

每个 `.xaml` 文件都有一个对应的 `.xaml.cs` 文件（称为"代码后台"），用来处理事件逻辑。

### 数据绑定 (Data Binding)

数据绑定是 WPF 最核心的机制，它让界面元素自动跟随数据变化。

```xml
<!-- XAML 中 -->
<TextBlock Text="{Binding UserName}" />
```

```csharp
// ViewModel 中
[ObservableProperty]
private string _userName = "张三";

// 当代码中修改 UserName = "李四" 时，界面上的文本会自动变成"李四"
```

**绑定方向**：
- `{Binding Name}` — 单向绑定（数据 → 界面）
- `{Binding Name, Mode=TwoWay}` — 双向绑定（数据 ↔ 界面，常用于输入框）

**DataContext**：
每个页面在构造函数中设置 `DataContext = _vm`，这告诉 WPF "这个页面中所有的 `{Binding}` 都去 `_vm` 这个对象上找属性"。

### ObservableProperty 和 RelayCommand

这是 `CommunityToolkit.Mvvm` 库提供的两个关键特性，大幅减少 MVVM 样板代码。

**[ObservableProperty]**：自动生成带通知的属性

```csharp
// 你只需要写这一行：
[ObservableProperty]
private string _title = "";

// 编译器会自动生成：
// public string Title
// {
//     get => _title;
//     set => SetProperty(ref _title, value);  // 自动触发 UI 更新
// }
```

注意：字段名用 `_camelCase`（下划线+小驼峰），自动生成的属性名是 `PascalCase`（大驼峰）。

**[RelayCommand]**：自动生成可绑定的命令

```csharp
// 你只需要写方法：
[RelayCommand]
private void Save()
{
    // 保存逻辑
}

// 编译器自动生成：
// public IRelayCommand SaveCommand { get; }
// 在 XAML 中可以这样绑定：<Button Command="{Binding SaveCommand}" />
```

### DynamicResource 主题系统

项目使用 `DynamicResource` 实现主题切换（浅色/深色）。

在 `App.xaml` 中定义了 12 个自定义颜色资源：

```xml
<SolidColorBrush x:Key="CustomWindowBg" Color="#F3F3F3"/>    <!-- 窗口背景色 -->
<SolidColorBrush x:Key="CustomCardBg" Color="#F0F4F8"/>      <!-- 卡片背景色 -->
<SolidColorBrush x:Key="CustomFg" Color="#222222"/>           <!-- 主文字颜色 -->
<!-- ... 共 12 个 -->
```

在 XAML 中使用：
```xml
<Border Background="{DynamicResource CustomCardBg}">
```

切换主题时，`App.xaml.cs` 中的 `ApplyTheme()` 方法会替换这些资源的颜色值，界面会自动刷新。

**DynamicResource vs StaticResource**：
- `StaticResource`：加载时解析一次，之后不变
- `DynamicResource`：运行时可动态替换，主题切换必须用这个

### 代码后台 (Code-Behind) vs ViewModel

本项目中，每个页面有两个 C# 文件：

| 文件 | 职责 | 示例 |
|------|------|------|
| `xxxPage.xaml.cs` (Code-Behind) | 处理 XAML 事件回调，做"事件→ViewModel"的桥接 | `OnAddTodo` → `_vm.AddTodo()` |
| `xxxViewModel.cs` (ViewModel) | 包含业务数据和逻辑，不知道界面长什么样 | 管理待办列表、调用数据库 |

**原则**：Code-Behind 越薄越好，尽量把逻辑放在 ViewModel 中。

---

## 各模块详解

### Models -- 数据模型

纯数据类，定义了应用中使用的数据结构。每个类对应数据库中的一个集合（类似数据库中的表）。

| 文件 | 说明 | 关键字段 |
|------|------|----------|
| `TodoItem.cs` | 待办事项 | Id, Title, Description, IsCompleted, DueDate, Priority, ReminderTime, CreatedAt |
| `PomodoroRecord.cs` | 番茄钟记录 | Id, StartTime, EndTime, DurationMinutes, IsCompleted |
| `AppSettings.cs` | 应用设置 | Id, ThemeMode, FocusDuration, ShortBreakDuration, EnableReminder |

### Services -- 服务层

负责数据操作和业务逻辑，被 ViewModel 调用。

| 文件 | 说明 |
|------|------|
| `DatabaseService.cs` | LiteDB 数据库连接管理。使用**单例模式**，全局只有一个数据库连接实例。数据库文件路径：`%LOCALAPPDATA%\KiteTodo\kitetodo.db` |
| `TodoService.cs` | 待办事项的 CRUD 操作（增删改查）。所有读写最终通过 DatabaseService 操作 LiteDB |
| `ExportService.cs` | 数据导出功能，支持 JSON 和 CSV 两种格式 |
| `ReminderService.cs` | 到期提醒服务。启动一个后台定时器（默认每分钟检查一次），发现到期待办时弹出 Windows Toast 通知 |

### ViewModels -- 视图模型

每个页面对应一个 ViewModel，包含页面所需的数据和操作。

| 文件 | 对应页面 | 核心功能 |
|------|----------|----------|
| `HomeViewModel.cs` | 今日待办 | 管理待办列表、添加/编辑/删除/完成、筛选排序 |
| `WeeklyViewModel.cs` | 周视图 | 按周组织待办、前/后翻周、快速添加、生成周报 |
| `MonthlyViewModel.cs` | 月度总览 | 生成日历网格（7x6）、按天聚合待办、选中日期查看详情 |
| `PomodoroViewModel.cs` | 番茄钟 | 计时状态机（空闲→专注→休息）、暂停/继续/取消、记录统计 |
| `ExportViewModel.cs` | 数据导出 | 生成预览、保存到文件 |
| `SettingsViewModel.cs` | 设置 | 加载/保存设置、主题切换 |

### Views -- 界面层

每个页面由一对文件组成：`.xaml`（界面布局）+ `.xaml.cs`（事件处理代码后台）。

| 页面 | 功能要点 |
|------|----------|
| `HomePage` | 最复杂的页面。包含待办列表、新增/编辑表单、优先级选择、日期选择器、筛选标签 |
| `WeeklyPage` | 7 列布局，每列代表一天。支持拖拽式快速添加和一键导出周报 |
| `MonthlyPage` | 日历网格 + 弹出式详情浮层。使用事件冒泡控制浮层开关 |
| `PomodoroPage` | 圆形计时器显示 + 4 个动态显示/隐藏的操作按钮 |
| `ExportPage` | 格式选择 + 预览区 + 导出按钮 |
| `SettingsPage` | 各设置项通过双向绑定到 ViewModel 属性 |

### Converters -- 值转换器

`IValueConverter` 是 WPF 绑定系统中的数据格式转换工具。当绑定的数据类型与界面控件期望的类型不同时使用。

`BoolConverters.cs` 中包含以下转换器：

| 转换器 | 功能 | 使用场景 |
|--------|------|----------|
| `BoolToVisibility` | bool → Visible/Collapsed | 控制元素的显示/隐藏 |
| `InverseBoolToVisibility` | bool → Collapsed/Visible | 反向控制显示/隐藏 |
| `BoolToStrikethrough` | bool → Strikethrough/None | 已完成的待办显示删除线 |
| `BoolToOpacity` | bool → 0.5/1.0 | 已完成的待办变半透明 |
| `PriorityToBrush` | 优先级数字 → 颜色画刷 | 不同优先级显示不同颜色 |

---

## 数据存储

本项目使用 **LiteDB** 嵌入式数据库，无需安装任何数据库服务。

- **数据库文件位置**：`%LOCALAPPDATA%\KiteTodo\kitetodo.db`
  - 即 `C:\Users\<用户名>\AppData\Local\KiteTodo\kitetodo.db`
- **数据库类型**：NoSQL 文档数据库（类似 MongoDB，但嵌入在应用中）
- **连接模式**：`Connection=shared`（允许多线程并发读写）
- **数据集合**（类似 SQL 中的表）：
  - `TodoItem` — 待办事项
  - `PomodoroRecord` — 番茄钟记录
  - `AppSettings` — 应用设置

**备份数据**：直接复制 `kitetodo.db` 文件即可。

**重置数据**：删除 `kitetodo.db` 文件，应用会自动重建空数据库。

---

## 系统托盘

应用使用 `H.NotifyIcon` 库实现系统托盘功能，在 `App.xaml.cs` 中配置。

**行为**：
- 关闭主窗口 → 隐藏到系统托盘（不退出应用）
- 左键点击托盘图标 → 显示主窗口
- 右键点击托盘图标 → 弹出菜单（"显示主窗口" / "退出"）

**技术细节**：
- 使用 `Dispatcher.BeginInvoke()` 而非 `Dispatcher.Invoke()` 来处理托盘事件回调
- 原因：Win32 弹出菜单在回调时仍持有焦点，同步调用会导致窗口无法激活
- 使用 `Topmost = true → false` 技巧强制将窗口提到前台

---

## 打包发布

### 使用脚本（推荐）

```bash
# 双击 publish.bat 或命令行运行
publish.bat
```

### 手动打包

```bash
dotnet publish KiteTodo.csproj -c Release -r win-x64 --self-contained true ^
    -p:PublishSingleFile=true ^
    -p:IncludeNativeLibrariesForSelfExtract=true ^
    -p:EnableCompressionInSingleFile=true ^
    -p:DebugType=none ^
    -p:DebugSymbols=false
```

**参数说明**：

| 参数 | 说明 |
|------|------|
| `-c Release` | 使用 Release 配置编译 |
| `-r win-x64` | 目标平台为 Windows 64 位 |
| `--self-contained true` | 自包含模式，打包 .NET 运行时 |
| `PublishSingleFile` | 打包为单个 exe 文件 |
| `EnableCompressionInSingleFile` | 压缩单文件（~77MB，否则~185MB） |
| `DebugType=none` | 不生成调试符号文件（.pdb） |

**注意**：WPF 项目不支持 `PublishTrimmed=true`（IL 裁剪），会报错 NETSDK1168。

---

## 常见维护操作

### 如何添加一个新页面

以添加一个"统计"页面为例：

**第 1 步：创建 ViewModel**

在 `ViewModels/` 目录新建 `StatisticsViewModel.cs`：

```csharp
using CommunityToolkit.Mvvm.ComponentModel;

namespace KiteTodo.ViewModels;

public partial class StatisticsViewModel : ObservableObject
{
    // [ObservableProperty] 声明数据属性
    [ObservableProperty]
    private string _summary = "";

    // [RelayCommand] 声明命令
    [RelayCommand]
    private void LoadData()
    {
        // 加载统计数据的逻辑
    }
}
```

**第 2 步：创建 Page**

在 `Views/Pages/` 目录新建 `StatisticsPage.xaml`：

```xml
<Page x:Class="KiteTodo.Views.Pages.StatisticsPage"
      xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
      xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
      xmlns:ui="http://schemas.lepo.co/wpfui/2022/xaml">

    <Grid Margin="24">
        <TextBlock Text="{Binding Summary}" FontSize="16" />
    </Grid>
</Page>
```

新建 `StatisticsPage.xaml.cs`：

```csharp
using System.Windows.Controls;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

public partial class StatisticsPage : Page
{
    private readonly StatisticsViewModel _vm = new();

    public StatisticsPage()
    {
        InitializeComponent();
        DataContext = _vm;
    }
}
```

**第 3 步：注册到导航栏**

在 `MainWindow.xaml` 的 `NavigationView.MenuItems` 中添加：

```xml
<ui:NavigationViewItem Content="统计" TargetPageType="{x:Type pages:StatisticsPage}">
    <ui:NavigationViewItem.Icon>
        <ui:SymbolIcon Symbol="DataBarVertical24"/>
    </ui:NavigationViewItem.Icon>
</ui:NavigationViewItem>
```

### 如何添加新的待办字段

以添加"标签 (Tag)"字段为例：

1. **修改 Model**：在 `Models/TodoItem.cs` 中添加属性
   ```csharp
   public string Tag { get; set; } = "";
   ```

2. **修改 ViewModel**：在 `HomeViewModel.cs` 中添加对应的可编辑属性
   ```csharp
   [ObservableProperty]
   private string _editTag = "";
   ```

3. **修改 View**：在 `HomePage.xaml` 的编辑表单区域添加输入控件
   ```xml
   <TextBox Text="{Binding EditTag, Mode=TwoWay}" />
   ```

4. **无需修改数据库**：LiteDB 是 NoSQL 数据库，自动处理新字段，无需迁移

### 如何修改主题颜色

1. 打开 `App.xaml`，修改 `<SolidColorBrush x:Key="CustomXxx">` 的颜色值（这是浅色主题的默认值）
2. 打开 `App.xaml.cs`，找到 `ApplyTheme()` 方法，修改深色主题对应的颜色值
3. 所有使用 `{DynamicResource CustomXxx}` 的界面元素都会自动更新

### 如何新增一个设置项

以添加"自动保存间隔"为例：

1. **添加数据字段**：在 `Models/AppSettings.cs` 中添加
   ```csharp
   public int AutoSaveInterval { get; set; } = 5;  // 默认 5 分钟
   ```

2. **添加 ViewModel 属性**：在 `ViewModels/SettingsViewModel.cs` 中添加
   ```csharp
   [ObservableProperty]
   private int _autoSaveInterval = 5;
   ```
   并在 `LoadSettings()` 和 `SaveSettings()` 方法中添加对应的读写逻辑

3. **添加界面控件**：在 `Views/Pages/SettingsPage.xaml` 中添加
   ```xml
   <ui:CardControl Header="自动保存间隔（分钟）">
       <ui:NumberBox Value="{Binding AutoSaveInterval, Mode=TwoWay}" Minimum="1" Maximum="60" />
   </ui:CardControl>
   ```

---

## 依赖库说明

### WPF-UI (3.0.5)

提供 Windows 11 Fluent Design 风格的 WPF 控件。本项目使用的主要控件：

| 控件 | 用途 |
|------|------|
| `FluentWindow` | 主窗口（带 Mica 材质效果） |
| `NavigationView` | 左侧导航栏 |
| `TitleBar` | 自定义标题栏 |
| `CardControl` | 卡片式容器 |
| `SymbolIcon` | Fluent 图标 |
| `NumberBox` | 数字输入框 |

### CommunityToolkit.Mvvm (8.3.2)

微软官方的 MVVM 工具库，提供：
- `ObservableObject` — ViewModel 基类
- `[ObservableProperty]` — 自动生成带通知的属性
- `[RelayCommand]` — 自动生成命令
- `ObservableCollection<T>` — 带通知的集合（增删元素时自动刷新界面）

### LiteDB (5.0.21)

嵌入式 NoSQL 文档数据库，特点：
- 无需安装数据库服务，零配置
- 数据存储为 BSON 格式（类似 JSON）
- 支持 LINQ 查询
- 单文件存储，便于备份和迁移

### H.NotifyIcon (2.4.1)

系统托盘图标库，支持：
- 托盘图标显示
- 左键/右键菜单
- 气泡通知

### Microsoft.Toolkit.Uwp.Notifications (7.1.3)

Windows Toast 通知库，用于发送系统级通知（待办到期提醒）。

---

## 常见问题

**Q: 数据存在哪里？换电脑怎么办？**

A: 数据存储在 `%LOCALAPPDATA%\KiteTodo\kitetodo.db`。换电脑时，复制这个文件到新电脑的相同路径即可。

**Q: 程序关闭后数据会丢失吗？**

A: 不会。所有数据实时保存到 LiteDB 数据库文件中，关闭程序不影响数据。

**Q: 为什么打包后的 exe 有 77MB 这么大？**

A: 因为使用了自包含模式，exe 中包含了完整的 .NET 8 运行时。WPF 项目不支持 IL 裁剪，所以这是目前能做到的最小体积（启用了压缩）。如果目标电脑已安装 .NET 8 Desktop Runtime，可以使用框架依赖模式打包（约 10-20MB）。

**Q: 编译时报错 `CSC : error CS2012` 文件被占用**

A: KiteTodo.exe 正在运行中。先关闭程序（或通过任务管理器结束 KiteTodo.exe 进程），再重新编译。

**Q: 打开项目后 XAML 设计器报错**

A: 这是 WPF-UI 自定义控件导致的设计器兼容性问题。不影响编译和运行。直接 `dotnet run` 即可正常运行。

**Q: 如何查看项目中使用了哪些图标？**

A: WPF-UI 使用 Fluent System Icons，在代码中搜索 `Symbol=` 可以找到所有使用的图标名称。图标列表可参考 [Fluent UI System Icons](https://github.com/microsoft/fluentui-system-icons)。
