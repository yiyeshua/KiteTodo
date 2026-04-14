// ============================================================================
// MainWindow.xaml.cs - 主窗口代码后置
//
// 【WPF 代码后置说明】
// 每个 .xaml 文件都有一个对应的 .xaml.cs 文件（称为 code-behind）。
// XAML 定义界面布局和外观，.cs 文件处理初始化逻辑和事件响应。
//
// 本窗口使用 WPF-UI 的 FluentWindow（带 Fluent Design 风格），
// 内置 NavigationView 实现侧边栏页面导航。
// ============================================================================

using System.ComponentModel;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media.Imaging;
using Wpf.Ui.Controls;

namespace KiteTodo;

public partial class MainWindow : FluentWindow
{
    public MainWindow()
    {
        InitializeComponent();
        SetWindowIcon();
        Loaded += (_, _) =>
        {
            NavView.Navigate(typeof(Views.Pages.HomePage));
        };
    }

    private void SetWindowIcon()
    {
        using var bitmap = new System.Drawing.Bitmap(32, 32);
        using (var g = System.Drawing.Graphics.FromImage(bitmap))
        {
            g.Clear(System.Drawing.Color.FromArgb(0, 120, 212));
            using var font = new System.Drawing.Font("Segoe UI", 18, System.Drawing.FontStyle.Bold);
            using var brush = new System.Drawing.SolidBrush(System.Drawing.Color.White);
            var size = g.MeasureString("K", font);
            g.DrawString("K", font, brush, (32 - size.Width) / 2, (32 - size.Height) / 2);
        }
        var hIcon = bitmap.GetHicon();
        Icon = Imaging.CreateBitmapSourceFromHIcon(
            hIcon, Int32Rect.Empty, BitmapSizeOptions.FromEmptyOptions());
    }

    protected override void OnClosing(CancelEventArgs e)
    {
        // Minimize to tray instead of closing
        e.Cancel = true;
        Hide();
        base.OnClosing(e);
    }

    /// <summary>公开的导航方法，供其他页面的 code-behind 调用</summary>
    public void NavigateTo(Type pageType)
    {
        NavView.Navigate(pageType);
    }
}
