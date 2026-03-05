using System.Globalization;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public abstract class BinaryArithmeticConverterBase : IValueConverter {
        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            if (value is not IConvertible convertibleValue || !double.TryParse(parameter?.ToString(), out var operand)) {
                return value;
            }

            var currentValue = convertibleValue.ToDouble(null);
            if (!TryCalculate(currentValue, operand, out var calculatedValue)) {
                return value;
            }

            return (int)Math.Round(calculatedValue);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            throw new NotImplementedException();
        }

        protected abstract bool TryCalculate(double value, double operand, out double result);
    }
}
