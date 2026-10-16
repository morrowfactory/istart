using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Interop;

namespace IStart;

internal static class PresentationTest
{
    internal static async Task Run(string output)
    {
        Directory.CreateDirectory(output);
        var cover = new Window { Title = "iStart 置前验证背景", WindowState = WindowState.Maximized, Content = new TextBlock { Text = "验证完成后自动关闭", FontSize = 24 } };
        var menu = new MainWindow(new LibraryStore(Path.Combine(output, "library.json")));
        var report = new List<string>();
        try
        {
            cover.Show(); cover.Activate();
            await Task.Delay(150);
            for (var i = 0; i < 3; i++)
            {
                menu.ShowMenu(null);
                var handle = new WindowInteropHelper(menu).Handle;
                for (var wait = 0; wait < 30; wait++)
                {
                    if (menu.IsVisible && menu.Topmost && WinInput.GetForegroundWindow() == handle
                        && ((TextBox)menu.FindName("SearchBox")).IsKeyboardFocused) break;
                    await Task.Delay(100);
                }
                if (!menu.IsVisible || !menu.Topmost || WinInput.GetForegroundWindow() != handle
                    || !((TextBox)menu.FindName("SearchBox")).IsKeyboardFocused)
                    throw new InvalidOperationException($"未接入开始按钮置前失败：visible={menu.IsVisible}, topmost={menu.Topmost}, foreground={WinInput.GetForegroundWindow() == handle}, search={((TextBox)menu.FindName("SearchBox")).IsKeyboardFocused}");
                report.Add("PASS native mode logo/show => foreground, topmost, search focus");
                cover.Activate();
                WinInput.SetForegroundWindow(new WindowInteropHelper(cover).Handle);
                await Task.Delay(100);
                if (menu.IsVisible || menu.Topmost) throw new InvalidOperationException("切到其他窗口后菜单必须收起并解除浮层。");
                report.Add("PASS deactivate => hidden, not topmost");
            }
            menu.WindowState = WindowState.Minimized;
            menu.ShowMenu(null);
            await Task.Delay(350);
            if (menu.WindowState != WindowState.Normal || !menu.IsVisible || !menu.Topmost) throw new InvalidOperationException("最小化窗口必须恢复到前面。");
            report.Add("PASS minimized menu => normal, visible, topmost");
            var enable = (Button)menu.FindName("EnableMenuButton");
            if (enable.Visibility != Visibility.Visible) throw new InvalidOperationException("原生模式必须显示启用接入入口。");
            enable.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            await Wait(() => enable.Visibility == Visibility.Collapsed, "启用接入");
            menu.Hide();
            WinInput.TestKeys((0x5B, false), (0x5B, true));
            await Wait(() => menu.IsVisible && menu.Topmost && WinInput.GetForegroundWindow() == new WindowInteropHelper(menu).Handle, "Win 键显示实际菜单");
            report.Add("PASS enable button => Win key opens actual foreground menu");
            menu.Hide();
            var target = (await Task.Run(StartMenuAdapter.FindTargets).WaitAsync(TimeSpan.FromSeconds(3))).First();
            WinInput.TestClick(new() { X = (int)(target.Bounds.Left + target.Bounds.Width / 2), Y = (int)(target.Bounds.Top + target.Bounds.Height / 2) });
            await Wait(() => menu.IsVisible && menu.Topmost && WinInput.GetForegroundWindow() == new WindowInteropHelper(menu).Handle, "开始按钮显示实际菜单");
            report.Add("PASS Start click => actual foreground menu");
            File.WriteAllLines(Path.Combine(output, "presentation-result.txt"), report);
        }
        finally { menu.RequestExit("presentation-test"); cover.Close(); }
    }
    private static async Task Wait(Func<bool> condition, string operation)
    {
        for (var i = 0; i < 50; i++) { if (condition()) return; await Task.Delay(100); }
        throw new InvalidOperationException(operation + "未完成。");
    }
}
