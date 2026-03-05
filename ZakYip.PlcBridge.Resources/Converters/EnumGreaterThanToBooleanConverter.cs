using System;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Resources.Converters {

    /// <summary>
    /// 枚举数值大于阈值时返回 true，否则返回 false。
    /// ConverterParameter：阈值（如 "0" / "1"）。
    /// 示例：StepProgressStatus > 0 => true（Waiting/Completed），否则 false（NotStarted）。
    /// </summary>
    public sealed class EnumGreaterThanToBooleanConverter : IValueConverter {

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            try {
                if (value is null) {
                    return false;
                }

                var threshold = EnumConverterValueHelper.ParseLongParameter(parameter);
                var numericValue = EnumConverterValueHelper.ToInt64(value);

                return numericValue > threshold;
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
