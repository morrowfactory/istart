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
        var name = category.Id == new Guid("ffffffff-ffff-ffff-ffff-ffffffffffff") ? "panels-top-left"
            : category.Id == new Guid("eeeeeeee-eeee-eeee-eeee-eeeeeeeeeeee") ? "history"
            : category.Id == new Guid("bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb") ? "monitor"
            : category.Id == new Guid("cccccccc-cccc-cccc-cccc-cccccccccccc") ? "globe-2" : "folder";
        if (!Icons.TryGetValue(name, out var icon))
        {
            icon = new BitmapImage(new Uri("pack://application:,,,/IStart.App;component/Assets/Icons/" + name + ".png"));
            icon.Freeze();
            Icons.Add(name, icon);
        }
        return icon;
    }
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}
