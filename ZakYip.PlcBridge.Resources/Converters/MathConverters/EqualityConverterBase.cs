using System.Globalization;
using System.Windows.Data;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public abstract class EqualityConverterBase : IValueConverter {
        public object Convert(object value, Type targetType, object? parameter, CultureInfo culture) {
            var isEqual = value is not null && parameter is not null && value.ToString() == parameter.ToString();
            return MapResult(isEqual);
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            return Binding.DoNothing;
        }

        protected abstract object MapResult(bool isEqual);
    }
}
