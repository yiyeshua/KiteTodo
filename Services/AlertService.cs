using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace KiteTodo.Services;

/// <summary>
/// 桌面提醒服务：在屏幕正中弹出半屏大小的醒目提示窗口。
/// </summary>
public static class AlertService
{
    /// <summary>播放完成提示音</summary>
    public static void PlayCompletionSound(string soundName)
    {
        try
        {
            switch (soundName)
            {
                case "Chime":
                    SystemSounds.Exclamation.Play();
                    break;
                case "Bell":
                    SystemSounds.Asterisk.Play();
                    break;
                case "Ding":
                    SystemSounds.Hand.Play();
                    break;
                default:
                    SystemSounds.Beep.Play();
                    break;
            }
        }
        catch { }
    }
    /// <summary>
    /// 弹出居中大号提示窗口，点击任意位置或指定秒数后自动关闭。
    /// </summary>
    public static void ShowOverlayToast(string message, string? subMessage = null, int durationSeconds = 5)
    {
        try
        {
            var screen = SystemParameters.WorkArea;
            var width = screen.Width * 0.85;
            var height = screen.Height * 0.75;

            var toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                Width = width,
                Height = height,
                Left = screen.Left + (screen.Width - width) / 2,
                Top = screen.Top + (screen.Height - height) / 2,
                ResizeMode = ResizeMode.NoResize
            };

            // 内容面板
            var border = new Border
            {
                CornerRadius = new CornerRadius(36),
                Background = new SolidColorBrush(Color.FromArgb(240, 30, 30, 30)),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 60,
                    ShadowDepth = 0,
                    Opacity = 0.5,
                    Color = Colors.Black
                },
                Cursor = Cursors.Hand
            };

            var stack = new StackPanel
            {
                VerticalAlignment = VerticalAlignment.Center,
                HorizontalAlignment = HorizontalAlignment.Center
            };

            // 主文字
            stack.Children.Add(new TextBlock
            {
                Text = message,
                FontSize = 96,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                TextAlignment = TextAlignment.Center,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(60, 0, 60, 0)
            });

            // 副文字
            if (!string.IsNullOrEmpty(subMessage))
            {
                stack.Children.Add(new TextBlock
                {
                    Text = subMessage,
                    FontSize = 36,
                    Foreground = new SolidColorBrush(Color.FromArgb(180, 255, 255, 255)),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    Margin = new Thickness(60, 24, 60, 0)
                });
            }

            // 关闭提示
            stack.Children.Add(new TextBlock
            {
                Text = "点击任意位置关闭",
                FontSize = 20,
                Foreground = new SolidColorBrush(Color.FromArgb(100, 255, 255, 255)),
                HorizontalAlignment = HorizontalAlignment.Center,
                Margin = new Thickness(0, 48, 0, 0)
            });

            border.Child = stack;
            toast.Content = border;

            // 点击关闭
            toast.MouseDown += (_, _) => toast.Close();

            // 淡入动画
            toast.Opacity = 0;
            toast.Loaded += (_, _) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(300))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            // 指定时间后自动淡出关闭
            var autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            autoClose.Tick += (_, _) =>
            {
                autoClose.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(500));
                fadeOut.Completed += (_, _) => toast.Close();
                toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            autoClose.Start();

            toast.Show();
        }
        catch
        {
            // 静默忽略
        }
    }

    /// <summary>
    /// 在屏幕右下角显示一个轻量提示，适合轻反馈而非强提醒。
    /// </summary>
    public static void ShowCornerToast(string message, int durationSeconds = 2)
    {
        try
        {
            var screen = SystemParameters.WorkArea;
            const double width = 300;
            const double height = 72;
            const double margin = 20;

            var toast = new Window
            {
                WindowStyle = WindowStyle.None,
                AllowsTransparency = true,
                Background = Brushes.Transparent,
                Topmost = true,
                ShowInTaskbar = false,
                ShowActivated = false,
                Width = width,
                Height = height,
                Left = screen.Right - width - margin,
                Top = screen.Bottom - height - margin,
                ResizeMode = ResizeMode.NoResize
            };

            var border = new Border
            {
                CornerRadius = new CornerRadius(14),
                Background = new SolidColorBrush(Color.FromArgb(242, 40, 44, 52)),
                Padding = new Thickness(18, 14, 18, 14),
                Effect = new System.Windows.Media.Effects.DropShadowEffect
                {
                    BlurRadius = 18,
                    ShadowDepth = 4,
                    Opacity = 0.24,
                    Color = Colors.Black
                }
            };

            border.Child = new TextBlock
            {
                Text = message,
                FontSize = 14,
                Foreground = Brushes.White,
                VerticalAlignment = VerticalAlignment.Center,
                TextWrapping = TextWrapping.Wrap
            };

            toast.Content = border;

            toast.Opacity = 0;
            toast.Loaded += (_, _) =>
            {
                var fadeIn = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(180))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut }
                };
                toast.BeginAnimation(UIElement.OpacityProperty, fadeIn);
            };

            var autoClose = new DispatcherTimer { Interval = TimeSpan.FromSeconds(durationSeconds) };
            autoClose.Tick += (_, _) =>
            {
                autoClose.Stop();
                var fadeOut = new DoubleAnimation(1, 0, TimeSpan.FromMilliseconds(220));
                fadeOut.Completed += (_, _) => toast.Close();
                toast.BeginAnimation(UIElement.OpacityProperty, fadeOut);
            };
            autoClose.Start();

            toast.Show();
        }
        catch
        {
            // 静默忽略
        }
    }
}
