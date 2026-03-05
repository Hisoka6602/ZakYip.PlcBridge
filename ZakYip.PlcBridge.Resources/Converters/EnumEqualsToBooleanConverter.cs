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

                var expected = EnumConverterValueHelper.ParseLongParameter(parameter);
                var actual = EnumConverterValueHelper.ToInt64(value);

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
    }
}
