global using System;
global using System.Globalization;
global using System.IO;
global using System.Windows.Data;

namespace Alpha.Branding;

public sealed class InverseBooleanConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture) => value is bool busy && !busy;
    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class CountToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = value switch
        {
            int i => i,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };
        bool isNonEmpty = count > 0;
        if (parameter is string p && p.Equals("Inverse", StringComparison.OrdinalIgnoreCase))
            isNonEmpty = !isNonEmpty;
        return isNonEmpty ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}

public sealed class EmptyToVisibilityConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        int count = value switch
        {
            int i => i,
            System.Collections.ICollection c => c.Count,
            _ => 0
        };
        return count == 0 ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) => throw new NotSupportedException();
}


