# KiteTodo - 风筝待办

一款基于 .NET 8 + WPF 的 Windows 桌面效率工具，集待办管理、笔记编辑、专注计时、流程图设计、实用工具于一体。

---

## 功能概览

| 功能 | 说明 |
|------|------|
| **今日待办** | 首页，添加/编辑/删除/完成待办，支持优先级、进度、标签分类、子任务、周期重复、周栏快速切换日期、一键生成周报 |
| **月度总览** | 日历网格展示整月待办概览，实时显示每日完成率，点击某天查看详情 |
| **事项池** | 待排期事项缓冲区，支持待处理/等待他人/已排期三种状态，可与待办双向转换 |
| **专注计时** | 番茄钟（25分钟专注+5分钟休息），支持暂停/继续/取消、绑定待办、心情打卡、历史记录 |
| **全局搜索** | 跨待办、事项池、笔记三集合搜索，点击结果直接跳转 |
| **流程设计** | 流程图绘制工具，支持节点拖拽、连线、导出图片，保存到数据库或文件 |
| **工具集合** | 位值计算器、二维码助手、网络电台、文件摘要校验、导航大全、环境音、手绘白板 |
| **笔记本** | 基于 AINote 集成，CodeMirror 6 编辑器，Markdown/纯文本双模式，文件树管理，多标签页，模板系统，AI 辅助写作 |
| **数据导出** | 待办导出 Markdown/纯文本，数据备份/恢复，旧笔记迁移 |
| **设置** | 主题切换（浅色/深色）、番茄钟时长、自定义标签、AI 配置 |
| **系统托盘** | 关闭窗口最小化到托盘，左键恢复，右键菜单（显示主窗口/随机电台/浮标/退出） |
| **到期提醒** | 后台每 30 秒检查待办提醒，右下角弹出通知 |

---

## 技术栈

| 技术 | 用途 |
|------|------|
| .NET 8 + WPF | 桌面框架 |
| WPF-UI 3.0.5 | Fluent Design 控件库 |
| CommunityToolkit.Mvvm 8.3.2 | MVVM 框架 |
| LiteDB 5.0.21 | 嵌入式 NoSQL 数据库 |
| WebView2 | 笔记本前端承载（AINote React 应用） |
| CodeMirror 6 (via AINote) | Markdown/纯文本编辑器 |
| LibVLCSharp 3.9.4 | 网络电台播放 |
| Markdig 0.41.3 | Markdown 渲染 |
| QRCoder + ZXing.Net | 二维码生成与解析 |
| H.NotifyIcon 2.4.1 | 系统托盘 |

---

## 快速开始

```bash
# 开发运行
dotnet run

# 打包发布（输出 KiteTodo-Release.exe 到根目录）
dotnet publish -c Release -r win-x64 --self-contained true \
    -p:PublishSingleFile=true \
    -p:IncludeNativeLibrariesForSelfExtract=true \
    -p:EnableCompressionInSingleFile=true \
    -p:DebugType=none -p:DebugSymbols=false
```

**环境要求**：Windows 10 (1809+) 或 Windows 11，[.NET 8 SDK](https://dotnet.microsoft.com/download/dotnet/8.0)

---

## 项目结构

```
KiteTodo/
├── App.xaml / App.xaml.cs           # 应用入口、主题切换、托盘、浮标
├── MainWindow.xaml / .cs            # 主窗口（导航栏 + 内容区）
├── KiteTodo.csproj                  # 项目配置
├── Resources/NoteApp/               # AINote 前端静态资源 + webview-bridge.js
│
├── Models/                          # 数据模型
│   ├── TodoItem.cs                  # 待办（含子任务、周期重复）
│   ├── BacklogItem.cs               # 事项池
│   ├── Note.cs                      # 旧版笔记（LiteDB）
│   ├── PomodoroRecord.cs            # 番茄钟记录
│   ├── FlowchartStorageItem.cs      # 流程设计
│   └── AppSettings.cs               # 全局设置
│
├── Services/                        # 业务逻辑 + 数据访问
│   ├── DatabaseService.cs           # LiteDB 单例
│   ├── TodoService.cs               # 待办 CRUD
│   ├── BacklogService.cs            # 事项池 CRUD
│   ├── NoteService.cs               # 旧笔记 CRUD
│   ├── NoteAppBridgeService.cs      # 🆕 AINote IPC 桥接（替代 Tauri 后端）
│   ├── BackupService.cs             # 🆕 数据备份/恢复/迁移
│   ├── ExportService.cs             # 待办导出
│   ├── SearchService.cs             # 全局搜索
│   ├── ReminderService.cs           # 到期提醒
│   └── AlertService.cs              # 桌面通知
│
├── ViewModels/                      # MVVM 视图模型
│   ├── HomeViewModel.cs             # 今日待办
│   ├── MonthlyViewModel.cs          # 月度总览
│   ├── BacklogViewModel.cs          # 事项池
│   ├── PomodoroViewModel.cs         # 番茄钟（静态单例）
│   ├── ExportViewModel.cs           # 数据导出
│   ├── SettingsViewModel.cs         # 设置
│   ├── NotebookViewModel.cs         # 旧记事本
│   └── NotebookProViewModel.cs      # 🆕 笔记本 Pro
│
├── Views/Pages/                     # 导航页面
│   ├── HomePage, MonthlyPage, BacklogPage
│   ├── PomodoroPage, SearchPage
│   ├── DiagramDesignerPage          # 流程设计
│   ├── ToolCollectionPage           # 工具集合工作台
│   ├── NotebookProPage              # 🆕 笔记本（WebView2 + AINote）
│   ├── ExportPage, SettingsPage
│
├── Views/Tools/                     # 内嵌工具
│   ├── BitValueCalculatorView       # 位值计算器
│   ├── QrToolView                   # 二维码助手
│   ├── NoiseAndRadioToolView        # 网络电台
│   ├── FileHashToolView             # 文件摘要校验
│   ├── NavigationDirectoryToolView  # 导航大全
│   ├── AmbientSoundToolView         # 环境音
│   └── ExcalidrawToolView           # 手绘白板
│
├── Views/                           # 浮标 + 对话框
│   ├── FloatingEntryWindow          # 桌面浮标（番茄钟状态）
│   ├── BacklogTransferDialog        # 事项转换对话框
│   └── FocusCompleteDialog          # 专注完成对话框
│
├── Converters/                      # IValueConverter
├── Helpers/                         # 跨页面通信静态类
└── Controls/                        # 自定义控件
```

---

## 架构设计

### 笔记本 Pro 架构（AINote 集成）

```
┌──────────────────────────────────────────────────────┐
│  WPF 主窗口 (MainWindow)                              │
│  └── NavigationView → NotebookProPage                │
│      └── WebView2 控件                                │
│          └── AINote React 前端                         │
│              ├── CodeMirror 6 编辑器                   │
│              ├── 文件树 / 标签页 / Markdown 预览        │
│              ├── 模板选择器 / AI 面板                   │
│              └── 设置（含 AI 配置）                     │
│                  │                                    │
│                  │ invoke() → webview-bridge.js       │
│                  │     ↓ window.chrome.webview         │
│                  │     ↓ postMessage                  │
│                  ▼                                    │
│          NoteAppBridgeService (.NET)                  │
│          ├── 文件 CRUD（替代 Tauri Rust 后端）         │
│          ├── 目录扫描 / 搜索 / 标签                    │
│          ├── AI API 调用（OpenAI 兼容 / Ollama）       │
│          └── 对话框 / 导入导出 / 备份                  │
│              │                                        │
│              ▼                                        │
│          文件系统 (%LOCALAPPDATA%\KiteTodo\notes\)     │
└──────────────────────────────────────────────────────┘
```

### 传统 MVVM 架构（待办/设置等）

```
View (XAML) ←→ ViewModel ([ObservableProperty]) ←→ Service → DatabaseService (LiteDB)
```

---

## 数据存储

| 数据类型 | 存储方式 | 路径 |
|---------|---------|------|
| 待办/事项池/番茄钟/流程图/设置 | LiteDB | `%LOCALAPPDATA%\KiteTodo\kitetodo.db` |
| 笔记本 Pro（.md/.txt 文件） | 文件系统 | `%LOCALAPPDATA%\KiteTodo\notes\` |
| 笔记本标签/配置/最近访问 | JSON 文件 | `notes\.kite-tags.json` 等 |
| AINote 前端缓存 | ZIP 解压 | `%LOCALAPPDATA%\KiteTodo\NoteAppCache\` |
| LibVLC 运行时 | ZIP 解压 | `%LOCALAPPDATA%\KiteTodo\RuntimeCache\libvlc\` |

---

## 关键注意事项

1. **WPF-UI NavigationView** 切换页面会销毁旧实例。需要跨页面保持状态的组件必须用静态单例（如 `PomodoroViewModel.Instance`）
2. **LibVLC** 首次使用前需先解压运行时到本地缓存目录
3. **AINote 前端更新**：在 AINote 目录 `npm run build` → 复制 `dist/*` 到 `Resources/NoteApp/` → 修改 `index.html` 为相对路径并添加 `webview-bridge.js` 引用 → 重新发布
4. **单文件发布**：NoteApp 资源嵌入 ZIP，运行时自动解压；`IncludeNativeLibrariesForSelfExtract` 必须开启
5. **数据备份**：导出页面提供完整备份（JSON）+ 笔记本目录备份（ZIP）+ 旧笔记一键迁移

---

## 常见维护

### 更新 AINote 集成
```bash
cd E:\tools\AINote\notes-app
npm run build
# 复制 dist/* 到 E:\tools\KiteTodo\Resources\NoteApp\
# 修改 index.html：路径改为 ./assets/xxx，添加 <script src="./webview-bridge.js">
# 重新发布 KiteTodo
```

### 发布单文件
```bash
cd E:\tools\KiteTodo
dotnet publish -c Release -r win-x64 --self-contained true \
  -p:PublishSingleFile=true \
  -p:IncludeNativeLibrariesForSelfExtract=true \
  -p:EnableCompressionInSingleFile=true \
  -p:DebugType=none -p:DebugSymbols=false
# 产物：bin\Release\net8.0-windows10.0.17763.0\win-x64\publish\KiteTodo.exe
```
