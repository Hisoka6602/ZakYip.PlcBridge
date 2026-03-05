using System.Globalization;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public abstract class NumericComparisonConverterBase : IValueConverter {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (value is IComparable comparableValue &&
                parameter is not null &&
                double.TryParse(parameter.ToString(), out var threshold)) {
                return IsMatched(comparableValue.CompareTo(threshold));
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }

        protected abstract bool IsMatched(int comparisonResult);
    }
}
