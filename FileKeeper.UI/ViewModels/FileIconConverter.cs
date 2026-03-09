using Avalonia.Data.Converters;
using System;
using System.Globalization;

namespace FileKeeper.UI.ViewModels;

public class FileIconConverter : IValueConverter
{
    public object? Convert(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        // No-op for now as icons are replaced by emojis in XAML
        return null;
    }

    public object? ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
