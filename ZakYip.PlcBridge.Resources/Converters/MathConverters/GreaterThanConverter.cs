using System.Windows.Data;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class GreaterThanConverter : IValueConverter {

        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is IComparable val && parameter != null && double.TryParse(parameter.ToString(), out var threshold)) {
                return val.CompareTo(threshold) > 0;
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }
    }
}
