using System.Windows;
using System.Windows.Interop;

namespace IStart;

public partial class MainWindow
{
    private bool capturing;
    private ScreenshotHotkey? screenshotHotkey;
    private void InitializeScreenshotHotkey()
    {
        SourceInitialized += (_, _) =>
        {
            if (App.IsUITest) return;
            var source = HwndSource.FromHwnd(new WindowInteropHelper(this).Handle);
            screenshotHotkey = new ScreenshotHotkey(source, async () => await Capture(true));
            screenshotHotkey.Set(store.Library.ScreenshotShortcut);
            ScreenshotButton.ToolTip = screenshotHotkey.Status;
        };
        Closed += (_, _) => screenshotHotkey?.Dispose();
    }
    private async void StartScreenshot(object sender, RoutedEventArgs e)
        => await Capture(false);
    private async Task Capture(bool fromHotkey)
    {
        if (capturing) return;
        if (OwnedWindows.Count > 0) { StatusText.Text = "请先关闭设置或升级窗口，再开始截屏。"; return; }
        capturing = true;
        ScreenshotButton.IsEnabled = false;
        var screen = fromHotkey && WinInput.GetCursorPos(out var cursor)
            ? System.Windows.Forms.Screen.FromPoint(new System.Drawing.Point(cursor.X, cursor.Y))
            : System.Windows.Forms.Screen.FromHandle(new WindowInteropHelper(this).Handle);
        Hide();
        try
        {
            // 等待菜单的隐藏帧完成，原始画面中不包含 iStart。
            await System.Windows.Threading.Dispatcher.Yield(System.Windows.Threading.DispatcherPriority.ApplicationIdle);
            await Task.Delay(220);
            var frame = Screenshot.Capture(screen.Bounds);
            var editor = new ScreenshotWindow(frame, screen.Bounds) { Owner = this, Icon = Icon };
            editor.ShowDialog();
            StatusText.Text = editor.ResultMessage;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException or System.OutOfMemoryException or System.Runtime.InteropServices.ExternalException)
        { capturing = false; StatusText.Text = "暂时无法截屏，请关闭其他截屏窗口后重试。"; ShowMenu(null); }
        finally { capturing = false; ScreenshotButton.IsEnabled = true; }
    }
}
