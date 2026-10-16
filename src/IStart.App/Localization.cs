using System.ComponentModel;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Markup;

namespace IStart;

internal static class L
{
    internal sealed class LanguageState : INotifyPropertyChanged
    {
        public string Language { get; private set; } = Resolve("System", SystemCulture());
        public event PropertyChangedEventHandler? PropertyChanged;
        internal void Set(string language) { Language = language; PropertyChanged?.Invoke(this, new(nameof(Language))); }
    }
    internal static readonly LanguageState Current = new();
    internal static readonly IReadOnlyDictionary<string, string> English = ReadEnglish();
    internal static string Normalize(string? language) => language is "zh-CN" or "en" ? language : "System";
    internal static string Resolve(string? language, CultureInfo system) => Normalize(language) switch
    {
        "zh-CN" => "zh-CN", "en" => "en", _ => system.TwoLetterISOLanguageName == "zh" ? "zh-CN" : "en"
    };
    [DllImport("kernel32.dll")] private static extern ushort GetUserDefaultUILanguage();
    private static CultureInfo SystemCulture() => CultureInfo.GetCultureInfo(GetUserDefaultUILanguage());
    internal static void Apply(string? language) => Current.Set(Resolve(language, SystemCulture()));
    internal static string T(string source) => Current.Language == "en" && English.TryGetValue(source, out var translated) ? translated : source;
    internal static string F(string source, params object?[] args) => string.Format(CultureInfo.GetCultureInfo(Current.Language == "en" ? "en-US" : "zh-CN"), T(source), args);
    private static IReadOnlyDictionary<string, string> ReadEnglish()
    {
        using var stream = typeof(L).Assembly.GetManifestResourceStream("IStart.Strings.en.json")!;
        return JsonSerializer.Deserialize<Dictionary<string, string>>(stream)!;
    }

    private sealed record Text(string Source, object?[] Args);
    private sealed class TextConverter : IValueConverter
    {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var text = (Text)parameter;
            return text.Args.Length == 0 ? T(text.Source) : F(text.Source, text.Args);
        }
        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
    }
    internal static Binding Binding(string source, params object?[] args) => new(nameof(LanguageState.Language))
    {
        Source = Current, Mode = BindingMode.OneWay, Converter = new TextConverter(), ConverterParameter = new Text(source, args)
    };
    internal static void Bind(DependencyObject target, DependencyProperty property, string source, params object?[] args)
        => BindingOperations.SetBinding(target, property, Binding(source, args));

    // 设置面板只包含产品文字和固定选项；用户文件名、软件名及网址不进入此遍历。
    internal static void BindSettings(DependencyObject root)
    {
        var visited = new HashSet<DependencyObject>();
        void Visit(DependencyObject item)
        {
            if (!visited.Add(item)) return;
            void Translate(DependencyProperty property)
            {
                if (BindingOperations.IsDataBound(item, property) || item.GetValue(property) is not string text) return;
                var source = English.ContainsKey(text) ? text : English.FirstOrDefault(pair => pair.Value == text).Key;
                if (source is not null) Bind(item, property, source);
            }
            if (item is Window) Translate(Window.TitleProperty);
            if (item is TextBlock) Translate(TextBlock.TextProperty);
            if (item is ContentControl) Translate(ContentControl.ContentProperty);
            if (item is FrameworkElement) Translate(FrameworkElement.ToolTipProperty);
            Translate(System.Windows.Automation.AutomationProperties.NameProperty);
            foreach (var child in LogicalTreeHelper.GetChildren(item).OfType<DependencyObject>()) Visit(child);
            if (item is ItemsControl list) foreach (var child in list.Items.OfType<DependencyObject>()) Visit(child);
        }
        Visit(root);
    }
}

public sealed class TrExtension(string text) : MarkupExtension
{
    public override object ProvideValue(IServiceProvider serviceProvider) => L.Binding(text).ProvideValue(serviceProvider);
}
