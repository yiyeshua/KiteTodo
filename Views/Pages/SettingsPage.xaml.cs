// ============================================================================
// SettingsPage.xaml.cs — 设置页面的代码后台 (Code-Behind)
// ============================================================================
// 功能说明：
//   应用设置页面，允许用户配置各项偏好设置（如主题、番茄钟时长、提醒开关等）。
//   设置数据通过 SettingsViewModel 加载和保存到本地数据库。
//
// WPF 知识点：
//   - 设置项在 XAML 中通过 {Binding PropertyName} 双向绑定到 ViewModel 的属性
//   - 用户修改控件值（如滑块、开关）时，绑定会自动更新 ViewModel 属性
//   - 点击"保存"按钮时，ViewModel 将当前属性值持久化到数据库
// ============================================================================

using System.Windows;
using System.Windows.Controls;
using KiteTodo.ViewModels;

namespace KiteTodo.Views.Pages;

/// <summary>
/// 设置页面，用于配置应用偏好并保存到本地数据库。
/// </summary>
public partial class SettingsPage : Page
{
    /// <summary>设置页面的 ViewModel 实例，包含所有设置项的数据和保存逻辑</summary>
    private readonly SettingsViewModel _vm = new();

    public SettingsPage()
    {
        InitializeComponent();  // 加载 SettingsPage.xaml
        DataContext = _vm;       // 绑定 ViewModel，XAML 中的 {Binding} 指向 _vm 的属性
    }

    /// <summary>点击"保存设置"按钮，将当前设置持久化到数据库</summary>
    private void OnSave(object sender, RoutedEventArgs e) => _vm.SaveSettingsCommand.Execute(null);
}
