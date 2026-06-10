using System;
using System.Globalization;
using System.Windows.Data;

namespace PCleaner.ViewModels;

public class CumulativeSizeConverter : IMultiValueConverter
{
    public object Convert(object[] values, Type targetType, object parameter, CultureInfo culture)
    {
        long red = values.Length > 0 && values[0] is long r ? r : 0;
        long yellow = values.Length > 1 && values[1] is long y ? y : 0;
        long green = values.Length > 2 && values[2] is long g ? g : 0;

        string? type = parameter as string;
        return type switch
        {
            "Red" => (double)red,
            "Yellow" => (double)(red + yellow),
            "Green" => (double)(red + yellow + green),
            _ => 0.0
        };
    }

    public object[] ConvertBack(object value, Type[] targetTypes, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
