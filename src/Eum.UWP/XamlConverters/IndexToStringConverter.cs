using System;
using Windows.UI.Xaml.Data;

namespace Eum.UWP.XamlConverters;

public class IndexToStringConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, string language)
    {
        if (value is int index)
        {
            return $"{(index + 1):D2}.";
        }

        return string.Empty;
    }

    public object ConvertBack(object value, Type targetType, object parameter, string language)
    {
        if (value is int index)
        {
            return $"{index:D2}.";
        }

        return string.Empty;
    }
}
