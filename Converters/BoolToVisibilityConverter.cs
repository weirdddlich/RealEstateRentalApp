using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace RealEstateRental.Converters
{
    public class BoolToVisibilityConverter : IValueConverter
    {
        public bool Invert { get; set; }
        public bool CollapseWhenFalse { get; set; } = true;

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture)
        {
            var flag = value is bool b && b;
            if (Invert)
                flag = !flag;

            if (flag)
                return Visibility.Visible;

            return CollapseWhenFalse ? Visibility.Collapsed : Visibility.Hidden;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture)
            => throw new NotSupportedException();
    }
}

