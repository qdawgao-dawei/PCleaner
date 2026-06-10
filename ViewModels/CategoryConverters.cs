using System;
using System.Globalization;
using System.Windows.Data;
using System.Windows.Media;

namespace PCleaner.ViewModels;

public class CategoryToColorConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Green" => new SolidColorBrush(Color.FromRgb(39, 174, 96)), // Emerald Green
            "Yellow" => new SolidColorBrush(Color.FromRgb(243, 156, 18)), // Orange
            "Red" => new SolidColorBrush(Color.FromRgb(231, 76, 60)), // Alizarin Red
            _ => Brushes.Gray
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}

public class CategoryToTextConverter : IValueConverter
{
    public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
    {
        return value?.ToString() switch
        {
            "Green" => "🟢 可自动清理",
            "Yellow" => "🟡 需人工判断",
            "Red" => "🔴 谨慎清理",
            _ => "其他"
        };
    }

    public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
    {
        throw new NotImplementedException();
    }
}
