using System.Windows;
using System.Windows.Data;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class LessThanToVisibilityConverter : IValueConverter {

        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is IComparable val && parameter != null && double.TryParse(parameter.ToString(), out var threshold)) {
                return val.CompareTo(threshold) < 0 ? Visibility.Visible : Visibility.Collapsed;
            }
            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }
    }
}
