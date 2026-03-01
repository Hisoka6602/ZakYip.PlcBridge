using System.Windows.Data;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class EqualToConverter : IValueConverter {

        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (value != null && parameter != null) {
                return value.ToString() == parameter.ToString();
            }
            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }
    }
}
