using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class EqualToToVisibilityConverter : IValueConverter {

        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (value != null && parameter != null) {
                return value.ToString() == parameter.ToString() ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }
    }
}
