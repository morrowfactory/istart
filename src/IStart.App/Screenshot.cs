using System.IO;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Drawing = System.Drawing;

namespace IStart;

internal enum CaptureTool { Select, Arrow, Ellipse, Rectangle, Pen, Mosaic, Text }
internal sealed record CaptureMark(CaptureTool Tool, Point Start, Point End, Color Color, double Width)
{
    internal Point[] Points { get; init; } = [];
    internal string Text { get; init; } = "";
}

internal static class Screenshot
{
    internal static BitmapSource Capture(Drawing.Rectangle bounds)
    {
        if (bounds.Width <= 0 || bounds.Height <= 0 || (long)bounds.Width * bounds.Height > 40_000_000)
            throw new InvalidOperationException("此屏幕尺寸暂不支持截屏。");
        using var bitmap = new Drawing.Bitmap(bounds.Width, bounds.Height, Drawing.Imaging.PixelFormat.Format32bppArgb);
        using (var graphics = Drawing.Graphics.FromImage(bitmap))
            graphics.CopyFromScreen(bounds.Location, Drawing.Point.Empty, bounds.Size, Drawing.CopyPixelOperation.SourceCopy);
        var data = bitmap.LockBits(new Drawing.Rectangle(0, 0, bounds.Width, bounds.Height), Drawing.Imaging.ImageLockMode.ReadOnly, Drawing.Imaging.PixelFormat.Format32bppArgb);
        try
        {
            var image = BitmapSource.Create(bounds.Width, bounds.Height, 96, 96, PixelFormats.Bgr32, null, data.Scan0, data.Stride * bounds.Height, data.Stride);
            image.Freeze();
            return image;
        }
        finally { bitmap.UnlockBits(data); }
    }

    internal static Rect Between(Point first, Point second) => new(first, second);
    internal static Int32Rect Crop(Rect selection, int width, int height)
    {
        var left = Math.Clamp((int)Math.Floor(selection.Left), 0, width);
        var top = Math.Clamp((int)Math.Floor(selection.Top), 0, height);
        var right = Math.Clamp((int)Math.Ceiling(selection.Right), left, width);
        var bottom = Math.Clamp((int)Math.Ceiling(selection.Bottom), top, height);
        if (right - left < 2 || bottom - top < 2) throw new InvalidOperationException("请先拖选有效截屏范围。");
        return new(left, top, right - left, bottom - top);
    }

    internal static void DrawMark(DrawingContext context, CaptureMark mark, BitmapSource? frame = null)
    {
        var pen = new Pen(new SolidColorBrush(mark.Color), mark.Width) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Round };
        if (mark.Tool == CaptureTool.Text)
            context.DrawText(new FormattedText(mark.Text, System.Globalization.CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface("Microsoft YaHei UI"), 14 + mark.Width * 2, pen.Brush, 1), mark.Start);
        else if (mark.Tool == CaptureTool.Pen)
        {
            var points = mark.Points;
            for (var i = 1; i < points.Length; i++) context.DrawLine(pen, points[i - 1], points[i]);
        }
        else if (mark.Tool == CaptureTool.Mosaic && frame is not null)
        {
            var region = Between(mark.Start, mark.End);
            var block = (int)Math.Max(8, mark.Width * 3);
            var source = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0);
            var pixel = new byte[4];
            for (var y = (int)region.Top; y < region.Bottom; y += block)
                for (var x = (int)region.Left; x < region.Right; x += block)
                {
                    source.CopyPixels(new Int32Rect(Math.Clamp(x + block / 2, 0, frame.PixelWidth - 1), Math.Clamp(y + block / 2, 0, frame.PixelHeight - 1), 1, 1), pixel, 4, 0);
                    context.DrawRectangle(new SolidColorBrush(Color.FromRgb(pixel[2], pixel[1], pixel[0])), null,
                        new Rect(x, y, Math.Min(block, region.Right - x), Math.Min(block, region.Bottom - y)));
                }
        }
        else if (mark.Tool == CaptureTool.Rectangle) context.DrawRectangle(null, pen, Between(mark.Start, mark.End));
        else if (mark.Tool == CaptureTool.Ellipse)
        {
            var bounds = Between(mark.Start, mark.End);
            context.DrawEllipse(null, pen, new(bounds.Left + bounds.Width / 2, bounds.Top + bounds.Height / 2), bounds.Width / 2, bounds.Height / 2);
        }
        else if (mark.Tool == CaptureTool.Arrow)
        {
            context.DrawLine(pen, mark.Start, mark.End);
            var direction = mark.Start - mark.End;
            if (direction.Length < 1) return;
            direction.Normalize();
            var head = direction * Math.Max(12, mark.Width * 4);
            var side = new Vector(-direction.Y, direction.X) * Math.Max(5, mark.Width * 1.8);
            context.DrawLine(pen, mark.End, mark.End + head + side);
            context.DrawLine(pen, mark.End, mark.End + head - side);
        }
    }

    internal static BitmapSource Render(BitmapSource frame, Rect selection, IEnumerable<CaptureMark> marks)
    {
        var crop = Crop(selection, frame.PixelWidth, frame.PixelHeight);
        var visual = new DrawingVisual();
        using (var context = visual.RenderOpen())
        {
            context.PushTransform(new TranslateTransform(-crop.X, -crop.Y));
            context.DrawImage(frame, new Rect(0, 0, frame.PixelWidth, frame.PixelHeight));
            foreach (var mark in marks) DrawMark(context, mark, frame);
            context.Pop();
        }
        var result = new RenderTargetBitmap(crop.Width, crop.Height, 96, 96, PixelFormats.Pbgra32);
        result.Render(visual);
        result.Freeze();
        return result;
    }

    internal static void Save(BitmapSource image, string path)
    {
        var encoder = new PngBitmapEncoder();
        encoder.Frames.Add(BitmapFrame.Create(image));
        // 编码完成后才替换用户选择的文件，保存失败不留下半张图片。
        using var encoded = new MemoryStream();
        encoder.Save(encoded);
        var temporary = Path.Combine(Path.GetDirectoryName(Path.GetFullPath(path))!, ".istart-screenshot-" + Guid.NewGuid().ToString("N") + ".tmp");
        try { File.WriteAllBytes(temporary, encoded.ToArray()); File.Move(temporary, path, true); }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
