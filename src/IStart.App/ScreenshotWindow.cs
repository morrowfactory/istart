using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace IStart;

internal sealed class ScreenshotWindow : Window
{
    private readonly BitmapSource frame;
    private readonly CaptureSurface surface;
    private readonly TextBlock status = new() { Foreground = Brushes.White, Margin = new Thickness(8), VerticalAlignment = VerticalAlignment.Center };
    private readonly Button copy = new() { Content = L.T("复制") }, save = new() { Content = L.T("保存 PNG") };
    private readonly Dictionary<CaptureTool, Button> tools = new();
    internal CaptureSurface Surface => surface;
    private readonly Canvas overlay = new();
    private readonly Border toolbar;
    private readonly WrapPanel options = new();
    private readonly TextBox textEditor = new() { Width = 220, MinHeight = 34, AcceptsReturn = true, Visibility = Visibility.Collapsed };
    private Point textOrigin;
    internal string ResultMessage { get; private set; } = L.T("已取消截屏。");

    internal ScreenshotWindow(BitmapSource frame, System.Drawing.Rectangle bounds)
    {
        this.frame = frame;
        surface = new CaptureSurface(frame);
        Title = L.T("iStart 截屏 · Esc 取消");
        WindowStyle = WindowStyle.None;
        ResizeMode = ResizeMode.NoResize;
        ShowInTaskbar = false;
        Topmost = true;
        Background = Brushes.Black;
        var grid = new Grid();
        grid.Children.Add(new Viewbox { Stretch = Stretch.Fill, Child = surface });
        var bar = new WrapPanel { MaxWidth = 620 };
        var rows = new StackPanel();
        rows.Children.Add(bar);
        rows.Children.Add(options);
        rows.Children.Add(status);
        toolbar = new Border { Child = rows, Background = Brushes.White, BorderBrush = Brushes.LightGray, BorderThickness = new Thickness(1), CornerRadius = new CornerRadius(8), Padding = new Thickness(4) };
        status.Foreground = Brushes.DimGray;
        overlay.Children.Add(toolbar);
        overlay.Children.Add(textEditor);
        grid.Children.Add(overlay);
        void Button(string label, string symbol, Action action)
        {
            var button = new Button { Content = symbol, ToolTip = label, Width = 32, Height = 30, Padding = new Thickness(2), Background = Brushes.White, Foreground = Brushes.DimGray, FontSize = 18, BorderThickness = new Thickness(0) };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => action();
            bar.Children.Add(button);
        }
        foreach (var (tool, label) in new[] { (CaptureTool.Select, L.T("调整")), (CaptureTool.Rectangle, L.T("方框")), (CaptureTool.Ellipse, L.T("圆圈")), (CaptureTool.Arrow, L.T("箭头")), (CaptureTool.Pen, L.T("画笔")), (CaptureTool.Mosaic, L.T("马赛克")), (CaptureTool.Text, L.T("文字")) })
        {
            var data = tool switch
            {
                CaptureTool.Select => "M2,7 L2,2 7,2 M13,2 L18,2 18,7 M18,13 L18,18 13,18 M7,18 L2,18 2,13",
                CaptureTool.Rectangle => "M3,3 L17,3 17,17 3,17 Z",
                CaptureTool.Ellipse => "M10,2 A8,8 0 1 1 9.99,2",
                CaptureTool.Arrow => "M3,17 L17,3 M7,3 L17,3 17,13",
                CaptureTool.Pen => "M3,17 L5,12 14,3 17,6 8,15 Z M12,5 L15,8",
                CaptureTool.Mosaic => "M2,2 L18,2 18,18 2,18 Z M2,10 L18,10 M10,2 L10,18 M3,3 L9,3 9,9 3,9 Z M11,11 L17,11 17,17 11,17 Z",
                _ => "M3,3 L17,3 M10,3 L10,18 M6,18 L14,18"
            };
            var icon = new System.Windows.Shapes.Path { Data = Geometry.Parse(data), Stroke = Brushes.DimGray, StrokeThickness = 1.5, Width = 18, Height = 18, Stretch = Stretch.Uniform };
            var button = new Button { Content = icon, ToolTip = label, Width = 32, Height = 30, Padding = new Thickness(6), BorderThickness = new Thickness(0) };
            System.Windows.Automation.AutomationProperties.SetName(button, label);
            button.Click += (_, _) => SetTool(tool);
            tools.Add(tool, button);
            bar.Children.Add(button);
        }
        var colors = new[] { (L.T("红色"), Colors.Red), (L.T("蓝色"), Colors.DodgerBlue), (L.T("绿色"), Colors.LimeGreen), (L.T("橙色"), Colors.Orange), (L.T("黑色"), Colors.Black), (L.T("白色"), Colors.White) };
        var palette = new ComboBox { Width = 82, Margin = new Thickness(6, 3, 3, 3) };
        palette.ItemsSource = colors.Select(item => item.Item1).ToArray();
        palette.SelectedIndex = 0;
        palette.SelectionChanged += (_, _) => { if (palette.SelectedIndex >= 0) surface.Color = colors[palette.SelectedIndex].Item2; };
        System.Windows.Automation.AutomationProperties.SetName(palette, L.T("标注颜色"));
        options.Children.Add(palette);
        var widths = new ComboBox { ItemsSource = new[] { L.T("细线"), L.T("中线"), L.T("粗线") }, SelectedIndex = 1, Width = L.Current.Language == "en" ? 88 : 60, Margin = new Thickness(3) };
        widths.SelectionChanged += (_, _) => surface.StrokeWidth = new[] { 2d, 4d, 7d }[widths.SelectedIndex];
        System.Windows.Automation.AutomationProperties.SetName(widths, L.T("标注线宽"));
        options.Children.Add(widths);
        Button(L.T("撤销（Ctrl+Z）"), "↶", () => surface.Undo());
        Button(L.T("重做（Ctrl+Y）"), "↷", () => surface.Redo());
        Button(L.T("清空标注"), "⌫", () => surface.ClearMarks());
        copy.Click += async (_, _) => await Copy();
        save.Click += (_, _) => Save();
        bar.Children.Add(save);
        Button(L.T("取消（Esc）"), "×", Close);
        bar.Children.Add(copy);
        Content = grid;
        copy.Content = "✓";
        save.Content = "↓";
        foreach (var (button, label) in new[] { (copy, L.T("完成并复制（Ctrl+C / Enter）")), (save, L.T("保存（Ctrl+S）")) })
        {
            button.ToolTip = label; button.Width = 32; button.Height = 30; button.FontSize = 18;
            button.Padding = new Thickness(2); button.Background = Brushes.White; button.Foreground = Brushes.DodgerBlue; button.BorderThickness = new Thickness(0);
            System.Windows.Automation.AutomationProperties.SetName(button, label);
        }
        textEditor.LostKeyboardFocus += (_, _) => CommitText();
        textEditor.PreviewKeyDown += (_, e) => { if (e.Key == Key.Enter && Keyboard.Modifiers != ModifierKeys.Shift) { CommitText(); surface.Focus(); e.Handled = true; } };
        surface.TextRequested += point =>
        {
            CommitText();
            textOrigin = point;
            Canvas.SetLeft(textEditor, Math.Clamp(point.X / frame.PixelWidth * overlay.ActualWidth, 0, Math.Max(0, overlay.ActualWidth - textEditor.Width)));
            Canvas.SetTop(textEditor, point.Y / frame.PixelHeight * overlay.ActualHeight);
            textEditor.Visibility = Visibility.Visible;
            textEditor.Focus();
        };
        overlay.SizeChanged += (_, _) => UpdateStatus();
        SetTool(CaptureTool.Select);
        surface.Changed += UpdateStatus;
        surface.CompleteRequested += async () => await Copy();
        surface.BackRequested += () => { if (surface.HasSelection) { surface.ResetSelection(); SetTool(CaptureTool.Select); } else Close(); };
        UpdateStatus();
        SourceInitialized += (_, _) => WinInput.SetWindowPos(new WindowInteropHelper(this).Handle, -1, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x40);
        PreviewKeyDown += async (_, e) =>
        {
            if (e.Key == Key.Escape) { Close(); e.Handled = true; }
            else if (textEditor.IsKeyboardFocusWithin) return;
            else if (e.Key == Key.Z && Keyboard.Modifiers == ModifierKeys.Control) { surface.Undo(); e.Handled = true; }
            else if (e.Key == Key.C && Keyboard.Modifiers == ModifierKeys.Control) { await Copy(); e.Handled = true; }
            else if (e.Key == Key.S && Keyboard.Modifiers == ModifierKeys.Control) { Save(); e.Handled = true; }
            else if (e.Key == Key.Y && Keyboard.Modifiers == ModifierKeys.Control) { surface.Redo(); e.Handled = true; }
            else if (e.Key == Key.Enter && Keyboard.Modifiers == ModifierKeys.None) { await Copy(); e.Handled = true; }
        };
        Loaded += (_, _) => { WinInput.SetWindowPos(new WindowInteropHelper(this).Handle, -1, bounds.X, bounds.Y, bounds.Width, bounds.Height, 0x40); Activate(); surface.Focus(); };
    }

    internal void SetTool(CaptureTool tool)
    {
        CommitText();
        surface.Tool = tool;
        options.Visibility = tool == CaptureTool.Select ? Visibility.Collapsed : Visibility.Visible;
        foreach (var pair in tools)
        {
            pair.Value.FontWeight = pair.Key == tool ? FontWeights.Bold : FontWeights.Normal;
            pair.Value.Background = pair.Key == tool ? Brushes.LightBlue : Brushes.White;
        }
        UpdateStatus();
    }
    private void CommitText()
    {
        if (textEditor.Visibility != Visibility.Visible) return;
        var text = textEditor.Text;
        textEditor.Visibility = Visibility.Collapsed;
        textEditor.Clear();
        if (!string.IsNullOrWhiteSpace(text)) surface.AddText(textOrigin, text);
    }
    private void UpdateStatus()
    {
        copy.IsEnabled = save.IsEnabled = surface.HasSelection;
        status.Text = surface.HasSelection ? L.F("{0:0} × {1:0} 像素 · Esc 取消", surface.Selection.Width, surface.Selection.Height) : L.T("拖选范围，再选择标注工具 · Esc 取消");
        if (overlay.ActualWidth < 1 || overlay.ActualHeight < 1) return;
        toolbar.MaxWidth = Math.Max(100, overlay.ActualWidth - 16);
        toolbar.Measure(new Size(toolbar.MaxWidth, double.PositiveInfinity));
        var size = toolbar.DesiredSize;
        var bottom = surface.HasSelection ? surface.Selection.Bottom / frame.PixelHeight * overlay.ActualHeight : 0;
        var right = surface.HasSelection ? surface.Selection.Right / frame.PixelWidth * overlay.ActualWidth : size.Width;
        var top = bottom + 8;
        if (top + size.Height > overlay.ActualHeight - 8) top = Math.Max(8, surface.Selection.Top / frame.PixelHeight * overlay.ActualHeight - size.Height - 8);
        Canvas.SetLeft(toolbar, Math.Clamp(right - size.Width, 8, Math.Max(8, overlay.ActualWidth - size.Width - 8)));
        Canvas.SetTop(toolbar, top);
    }
    private async Task Copy()
    {
        CommitText();
        if (!surface.HasSelection || !copy.IsEnabled) return;
        copy.IsEnabled = false;
        try
        {
            var image = Screenshot.Render(frame, surface.Selection, surface.Marks);
            for (var attempt = 0; ; attempt++)
            {
                try { Clipboard.SetImage(image); break; }
                catch (System.Runtime.InteropServices.ExternalException) when (attempt < 2) { await Task.Delay(120); if (!IsVisible) return; }
            }
            ResultMessage = L.T("截屏已复制，可在其他软件粘贴。");
            Close();
        }
        catch (System.Runtime.InteropServices.ExternalException) { status.Text = L.T("剪贴板被占用，请重试复制或保存图片。"); }
        finally { copy.IsEnabled = surface.HasSelection; }
    }
    private void Save()
    {
        CommitText();
        if (!surface.HasSelection) return;
        var dialog = new Microsoft.Win32.SaveFileDialog { Title = L.T("保存截屏"), FileName = L.F("截屏-{0:yyyyMMdd-HHmmss}.png", DateTime.Now), Filter = L.T("PNG 图片|*.png"), DefaultExt = ".png" };
        if (dialog.ShowDialog(this) != true) return;
        try
        {
            Screenshot.Save(Screenshot.Render(frame, surface.Selection, surface.Marks), dialog.FileName);
            ResultMessage = L.T("截屏已保存。");
            Close();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException) { status.Text = L.T("保存失败，请选择可写入的位置后重试。"); }
    }
}

internal sealed class CaptureSurface : FrameworkElement
{
    private readonly BitmapSource frame;
    internal List<CaptureMark> Marks { get; } = [];
    private readonly Stack<CaptureMark> undone = new();
    internal Rect Selection { get; private set; } = Rect.Empty;
    internal bool HasSelection => !Selection.IsEmpty && Selection.Width >= 2 && Selection.Height >= 2;
    internal CaptureTool Tool { get; set; }
    internal Color Color { get; set; } = Colors.Red;
    internal double StrokeWidth { get; set; } = 4;
    internal event Action? Changed;
    internal event Action<Point>? TextRequested;
    internal event Action? CompleteRequested, BackRequested;
    internal void AddText(Point point, string text)
    {
        undone.Clear();
        Marks.Add(new(CaptureTool.Text, point, point, Color, StrokeWidth) { Text = text });
        Refresh();
    }
    private Point start;
    private Rect original;
    private int edges;
    private bool dragging, moving;
    private CaptureMark? pending;
    internal CaptureSurface(BitmapSource frame)
    {
        this.frame = frame;
        Width = frame.PixelWidth;
        Height = frame.PixelHeight;
        Focusable = true;
        Cursor = Cursors.Cross;
    }
    private Point Clamp(Point p) => new(Math.Clamp(Math.Round(p.X), 0, Width), Math.Clamp(Math.Round(p.Y), 0, Height));
    private void Refresh() { InvalidateVisual(); Changed?.Invoke(); }
    internal void Undo() { if (Marks.Count > 0) { undone.Push(Marks[^1]); Marks.RemoveAt(Marks.Count - 1); } Refresh(); }
    internal void Redo() { if (undone.TryPop(out var mark)) Marks.Add(mark); Refresh(); }
    internal void ClearMarks() { Marks.Clear(); undone.Clear(); Refresh(); }
    internal void ResetSelection()
    {
        dragging = false;
        pending = null;
        ReleaseMouseCapture();
        Marks.Clear(); undone.Clear(); Selection = Rect.Empty;
        Refresh();
    }
    protected override void OnMouseRightButtonDown(MouseButtonEventArgs e) { BackRequested?.Invoke(); e.Handled = true; }
    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        Focus();
        if (e.ClickCount == 2 && Tool == CaptureTool.Select && HasSelection && Selection.Contains(e.GetPosition(this)))
        { CompleteRequested?.Invoke(); e.Handled = true; return; }
        if (!BeginDrag(e.GetPosition(this))) return;
        dragging = CaptureMouse();
        e.Handled = true;
    }
    internal bool BeginDrag(Point point)
    {
        start = Clamp(point);
        original = Selection;
        edges = 0;
        moving = false;
        if (Tool != CaptureTool.Select)
        {
            if (!HasSelection || !Selection.Contains(start)) return false;
            if (Tool == CaptureTool.Text) { TextRequested?.Invoke(start); return false; }
            pending = new(Tool, start, start, Color, StrokeWidth) { Points = [start] };
        }
        else
        {
            if (HasSelection)
            {
                const double tolerance = 10;
                if (Math.Abs(start.X - Selection.Left) <= tolerance && start.Y >= Selection.Top - tolerance && start.Y <= Selection.Bottom + tolerance) edges |= 1;
                if (Math.Abs(start.X - Selection.Right) <= tolerance && start.Y >= Selection.Top - tolerance && start.Y <= Selection.Bottom + tolerance) edges |= 2;
                if (Math.Abs(start.Y - Selection.Top) <= tolerance && start.X >= Selection.Left - tolerance && start.X <= Selection.Right + tolerance) edges |= 4;
                if (Math.Abs(start.Y - Selection.Bottom) <= tolerance && start.X >= Selection.Left - tolerance && start.X <= Selection.Right + tolerance) edges |= 8;
                moving = edges == 0 && Selection.Contains(start);
            }
            if (edges == 0 && !moving) Selection = new Rect(start, start);
        }
        dragging = true;
        Refresh();
        return true;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        DragTo(e.GetPosition(this));
    }
    internal void DragTo(Point position)
    {
        if (!dragging) return;
        var point = Clamp(position);
        if (pending is not null)
        {
            point = new(Math.Clamp(point.X, Selection.Left, Selection.Right), Math.Clamp(point.Y, Selection.Top, Selection.Bottom));
            pending = pending with { End = point, Points = pending.Tool == CaptureTool.Pen ? [.. pending.Points, point] : pending.Points };
        }
        else if (moving)
            Selection = new Rect(Math.Clamp(original.X + point.X - start.X, 0, Width - original.Width), Math.Clamp(original.Y + point.Y - start.Y, 0, Height - original.Height), original.Width, original.Height);
        else if (edges != 0)
            Selection = Screenshot.Between(new((edges & 1) != 0 ? point.X : original.Left, (edges & 4) != 0 ? point.Y : original.Top),
                new((edges & 2) != 0 ? point.X : original.Right, (edges & 8) != 0 ? point.Y : original.Bottom));
        else Selection = Screenshot.Between(start, point);
        Refresh();
    }
    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        EndDrag();
        ReleaseMouseCapture();
    }
    internal void EndDrag()
    {
        if (!dragging) return;
        dragging = false;
        if (pending is not null && ((pending.End - pending.Start).Length >= 2 || pending.Tool == CaptureTool.Pen && pending.Points.Length > 2)) { undone.Clear(); Marks.Add(pending); }
        pending = null;
        Refresh();
    }
    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (!dragging) return;
        dragging = false;
        pending = null;
        Refresh();
    }
    protected override void OnRender(DrawingContext dc)
    {
        dc.DrawImage(frame, new Rect(0, 0, Width, Height));
        var dim = new SolidColorBrush(Color.FromArgb(120, 0, 0, 0));
        if (!HasSelection) dc.DrawRectangle(dim, null, new Rect(0, 0, Width, Height));
        else
        {
            dc.DrawRectangle(dim, null, new Rect(0, 0, Width, Selection.Top));
            dc.DrawRectangle(dim, null, new Rect(0, Selection.Bottom, Width, Height - Selection.Bottom));
            dc.DrawRectangle(dim, null, new Rect(0, Selection.Top, Selection.Left, Selection.Height));
            dc.DrawRectangle(dim, null, new Rect(Selection.Right, Selection.Top, Width - Selection.Right, Selection.Height));
            dc.PushClip(new RectangleGeometry(Selection));
            foreach (var mark in Marks) Screenshot.DrawMark(dc, mark, frame);
            if (pending is not null) Screenshot.DrawMark(dc, pending, frame);
            dc.Pop();
            dc.DrawRectangle(null, new Pen(Brushes.DodgerBlue, 2), Selection);
            foreach (var p in new[] { Selection.TopLeft, Selection.TopRight, Selection.BottomLeft, Selection.BottomRight,
                new Point(Selection.Left + Selection.Width / 2, Selection.Top), new(Selection.Left + Selection.Width / 2, Selection.Bottom),
                new(Selection.Left, Selection.Top + Selection.Height / 2), new(Selection.Right, Selection.Top + Selection.Height / 2) })
                dc.DrawRectangle(Brushes.White, new Pen(Brushes.DodgerBlue, 1), new Rect(p.X - 4, p.Y - 4, 8, 8));
        }
    }
}
