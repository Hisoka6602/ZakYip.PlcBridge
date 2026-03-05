using System.Globalization;
using System.Windows;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public abstract class NumericComparisonToVisibilityConverterBase : IValueConverter {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            if (NumericConversionHelper.TryConvertToDouble(value, culture, out var numericValue) &&
                parameter is not null &&
                double.TryParse(parameter.ToString(), NumberStyles.Any, culture, out var threshold)) {
                return IsMatched(numericValue.CompareTo(threshold)) ? Visibility.Visible : Visibility.Collapsed;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }

        protected abstract bool IsMatched(int comparisonResult);
    }
}
