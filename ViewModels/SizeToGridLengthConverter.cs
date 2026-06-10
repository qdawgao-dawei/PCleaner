using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace PCleaner.ViewModels;

public class SizeToGridLengthConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        if (value is long size)
        {
            // We use the size value as the star weight.
            // Grid will handle the proportionality.
            return new GridLength(Math.Max(0.0001, size), GridUnitType.Star);
        }
        return new GridLength(0, GridUnitType.Star);
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
