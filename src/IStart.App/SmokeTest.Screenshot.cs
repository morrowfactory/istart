using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

internal static partial class SmokeTest
{
    private static void VerifyScreenshot(string output)
    {
        Check(ScreenshotHotkey.Binding("Alt+Shift+A") == (5u, 65u), "企业微信截图提示对应 Alt+Shift+A");
        Check(ScreenshotHotkey.Binding("Ctrl+Alt+A") == (3u, 65u) && ScreenshotHotkey.Binding("Ctrl+Shift+S") == (6u, 83u), "备用快捷键映射");
        Check(ScreenshotHotkey.Binding("关闭") == (0u, 0u) && ScreenshotHotkey.Binding("未知") == (0u, 0u), "关闭及未知组合不注册");
        var preferences = new LibraryStore(Path.Combine(output, "shortcut-fixture", "library.json"));
        Check(preferences.Library.ScreenshotShortcut == "Alt+Shift+A", "旧配置默认截屏组合");
        preferences.Change(s => s.ScreenshotShortcut = "Ctrl+Shift+S");
        Check(new LibraryStore(Path.Combine(output, "shortcut-fixture", "library.json")).Library.ScreenshotShortcut == "Ctrl+Shift+S", "截屏快捷键保存重开");
        var pixels = Enumerable.Repeat((byte)255, 320 * 240 * 4).ToArray();
        var frame = BitmapSource.Create(320, 240, 96, 96, PixelFormats.Bgra32, null, pixels, 320 * 4);
        frame.Freeze();
        var surface = new CaptureSurface(frame);
        Check(surface.BeginDrag(new(10, 20)), "开始拖选截屏区域");
        surface.DragTo(new(210, 180)); surface.EndDrag();
        Check(surface.Selection == new Rect(10, 20, 200, 160), "截屏拖选范围");
        surface.BeginDrag(new(50, 60)); surface.DragTo(new(80, 80)); surface.EndDrag();
        Check(surface.Selection == new Rect(40, 40, 200, 160), "截屏选区移动");
        surface.BeginDrag(new(40, 100)); surface.DragTo(new(20, 100)); surface.EndDrag();
        Check(surface.Selection == new Rect(20, 40, 220, 160), "截屏左边界调整大小");
        surface.BeginDrag(new(240, 200)); surface.DragTo(new(300, 230)); surface.EndDrag();
        Check(surface.Selection == new Rect(20, 40, 280, 190), "截屏角点调整大小");
        void Mark(CaptureTool tool, Point start, Point end, Color color)
        {
            surface.Tool = tool; surface.Color = color; surface.StrokeWidth = 4;
            Check(surface.BeginDrag(start), "选区内开始标注");
            surface.DragTo(end); surface.EndDrag();
        }
        Mark(CaptureTool.Arrow, new(50, 80), new(180, 80), Colors.Red);
        Mark(CaptureTool.Ellipse, new(200, 100), new(260, 160), Colors.DodgerBlue);
        Mark(CaptureTool.Rectangle, new(60, 130), new(120, 190), Colors.LimeGreen);
        Check(surface.Marks.Count == 3, "保留箭头圆圈方框及不同颜色");
        Check(!surface.BeginDrag(new(0, 0)), "拒绝在选区外开始标注");
        var result = Screenshot.Render(frame, surface.Selection, surface.Marks);
        Check(result.PixelWidth == 280 && result.PixelHeight == 190, "导出尺寸与调整后选区一致");
        var data = new byte[result.PixelWidth * result.PixelHeight * 4];
        result.CopyPixels(data, result.PixelWidth * 4, 0);
        byte[] Pixel(int x, int y) => data.Skip((y * result.PixelWidth + x) * 4).Take(4).ToArray();
        var red = Pixel(80, 40); var blue = Pixel(210, 60); var green = Pixel(40, 120);
        Check(red[2] > 240 && red[1] < 20 && red[0] < 20, "导出红色箭头像素正确");
        Check(blue[0] > 240 && blue[2] < 50, "导出蓝色圆圈像素正确");
        Check(green[1] > 180 && green[0] < 100, "导出绿色方框像素正确");
        var file = Path.Combine(output, "screenshot-annotations.png");
        Screenshot.Save(result, file);
        using (var stream = File.OpenRead(file))
        {
            var decoded = BitmapDecoder.Create(stream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad).Frames[0];
            Check(decoded.PixelWidth == 280 && decoded.PixelHeight == 190, "PNG 保存后可读取并保持尺寸");
        }
        surface.Undo(); Check(surface.Marks.Count == 2, "撤销最近标注");
        surface.Redo(); Check(surface.Marks.Count == 3, "重做恢复最近标注");
        surface.Undo();
        Mark(CaptureTool.Pen, new(80, 110), new(160, 115), Colors.Orange);
        Check(surface.Marks[^1].Points.Length == 2, "画笔保留拖动路径");
        Mark(CaptureTool.Mosaic, new(90, 150), new(130, 180), Colors.Red);
        surface.AddText(new(40, 50), "测试文字");
        var extended = Screenshot.Render(frame, surface.Selection, surface.Marks);
        Screenshot.Save(extended, Path.Combine(output, "screenshot-extra-tools.png"));
        Check(surface.Marks[^1].Text == "测试文字" && extended.PixelWidth == 280, "文字画笔马赛克共同导出");
        surface.ClearMarks(); Check(surface.Marks.Count == 0, "清空标注保留选区");
        surface.Redo(); Check(surface.Marks.Count == 0, "清空标注清除重做记录");
        Check(surface.HasSelection, "清空标注不清除选区");
        try { Screenshot.Crop(new Rect(0, 0, 1, 1), 320, 240); throw new InvalidOperationException("空范围未拒绝"); }
        catch (InvalidOperationException ex) when (ex.Message == "请先拖选有效截屏范围。") { }
        var editor = new ScreenshotWindow(frame, new System.Drawing.Rectangle(0, 0, 960, 640));
        editor.Surface.BeginDrag(new(20, 30)); editor.Surface.DragTo(new(300, 170)); editor.Surface.EndDrag();
        editor.SetTool(CaptureTool.Arrow);
        editor.Surface.BeginDrag(new(50, 80)); editor.Surface.DragTo(new(200, 120)); editor.Surface.EndDrag();
        var content = (FrameworkElement)editor.Content;
        content.Measure(new Size(960, 640)); content.Arrange(new Rect(0, 0, 960, 640)); content.UpdateLayout();
        var preview = new RenderTargetBitmap(960, 640, 96, 96, PixelFormats.Pbgra32);
        preview.Render(content);
        Screenshot.Save(preview, Path.Combine(output, "screenshot-editor.png"));
        editor.Close();
        surface.ResetSelection();
        Check(!surface.HasSelection && surface.Marks.Count == 0, "右键重选清除选区和旧标注");
        surface.Tool = CaptureTool.Select;
        surface.BeginDrag(new(10, 10)); surface.DragTo(new(100, 100));
        surface.ResetSelection(); surface.DragTo(new(150, 150)); surface.EndDrag();
        Check(!surface.HasSelection, "拖动途中右键重选停止旧手势");
    }
}
