using System.Globalization;
using System.Windows.Data;
using System.Windows.Media.Imaging;

namespace IStart;

public sealed class CategoryIconConverter : IValueConverter
{
    private static readonly Dictionary<string, BitmapImage> Icons = new();
    public object? Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is not MenuCategory category) return null;
        var name = category.Id == new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff") ? "all"
            : category.Id == new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") ? "recent"
            : category.Name == "常用软件" ? "apps" : "folder";
        if (!Icons.TryGetValue(name, out var icon))
        {
            icon = new BitmapImage(new Uri("pack://application:,,,/IStart.App;component/Assets/Fluent/" + name + ".png"));
            icon.Freeze();
            Icons.Add(name, icon);
        }
        return icon;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
