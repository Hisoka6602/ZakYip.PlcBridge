using System;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Resources.Converters {

    /// <summary>
    /// 枚举数值等于阈值时返回 true，否则返回 false。
    /// ConverterParameter：阈值（如 "0" / "1" / "2"）。
    /// 示例：StepProgressStatus == 2 => true（Completed）。
    /// </summary>
    public sealed class EnumEqualsToBooleanConverter : IValueConverter {

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            try {
                if (value is null) {
                    return false;
                }

                var expected = ParseExpected(parameter);
                var actual = ToInt64(value);

                return actual == expected;
            }
            catch {
                // 失败降级：返回 false，避免影响 UI
                return false;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            // 单向转换
            return Binding.DoNothing;
        }

        private static long ParseExpected(object? parameter) {
            if (parameter is null) {
                return 0;
            }

            if (parameter is string s) {
                return long.TryParse(s, NumberStyles.Integer, CultureInfo.InvariantCulture, out var v) ? v : 0;
            }

            return System.Convert.ToInt64(parameter, CultureInfo.InvariantCulture);
        }

        private static long ToInt64(object value) {
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
