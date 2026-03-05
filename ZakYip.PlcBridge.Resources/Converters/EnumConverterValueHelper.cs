using System;
using System.Globalization;

namespace ZakYip.PlcBridge.Resources.Converters {
    internal static class EnumConverterValueHelper {
        internal static long ParseLongParameter(object? parameter) {
            if (parameter is null) {
                return 0;
            }

            if (parameter is string s) {
                return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v)
                    ? v
                    : 0;
            }

            return System.Convert.ToInt64(parameter, CultureInfo.InvariantCulture);
        }

        internal static long ToInt64(object value) {
            var type = value.GetType();
            if (type.IsEnum) {
                var underlying = Enum.GetUnderlyingType(type);
                var raw = System.Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
                return System.Convert.ToInt64(raw, CultureInfo.InvariantCulture);
            }

            return System.Convert.ToInt64(value, CultureInfo.InvariantCulture);
        }
    }
}
