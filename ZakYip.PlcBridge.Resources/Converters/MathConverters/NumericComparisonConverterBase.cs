using System;
using System.Globalization;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public abstract class NumericComparisonConverterBase : IValueConverter {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (TryConvertToDouble(value, culture, out var numericValue) &&
                parameter is not null &&
                double.TryParse(parameter.ToString(), NumberStyles.Any, culture, out var threshold)) {
                return IsMatched(numericValue.CompareTo(threshold));
            }

            return false;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }

        protected abstract bool IsMatched(int comparisonResult);

        private static bool TryConvertToDouble(object value, IFormatProvider formatProvider, out double numericValue) {
            try {
                if (value is IConvertible convertibleValue) {
                    numericValue = convertibleValue.ToDouble(formatProvider);
                    return true;
                }
            }
            catch (FormatException) { }
            catch (InvalidCastException) { }
            catch (OverflowException) { }

            numericValue = default;
            return false;
        }
    }
}
