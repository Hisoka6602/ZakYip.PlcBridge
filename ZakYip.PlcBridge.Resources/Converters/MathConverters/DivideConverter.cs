using System.Windows.Data;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class DivideConverter : IValueConverter {

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is IConvertible convertibleValue && double.TryParse(parameter?.ToString(), out double divisor) && divisor != 0) {
                double val = convertibleValue.ToDouble(null);
                return (int)Math.Round(val / divisor);
            }
            return value;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }
    }
}
