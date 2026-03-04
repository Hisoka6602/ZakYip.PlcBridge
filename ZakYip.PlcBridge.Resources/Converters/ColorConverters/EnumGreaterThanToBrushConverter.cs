using System;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Resources.Converters.ColorConverters {

    /// <summary>
    /// 枚举数值大于阈值时返回指定 Brush，否则返回另一种 Brush。
    /// ConverterParameter 格式："{threshold}|{greaterColor}|{elseColor}"
    /// 示例："0|#32CD32|#A9A9A9"（>0 绿色，否则灰色）
    /// </summary>
    public sealed class EnumGreaterThanToBrushConverter : IValueConverter {
        private static readonly BrushConverter BrushConverter = new();

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            try {
                if (value is null) {
                    return Brushes.Transparent;
                }

                var (threshold, greaterBrush, elseBrush) = ParseParameter(parameter);

                // 将 enum / 数值统一转为 long 比较，避免装箱后的类型分支
                var numericValue = ToInt64(value);
                var result = numericValue > threshold ? greaterBrush : elseBrush;

                // 尽量冻结，降低 WPF 渲染开销
                if (result is { CanFreeze: true, IsFrozen: false }) {
                    result.Freeze();
                }

                return result;
            }
            catch {
                // 失败降级：不抛异常影响 UI
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            // 单向转换
            return Binding.DoNothing;
        }

        private static (long Threshold, Brush GreaterBrush, Brush ElseBrush) ParseParameter(object? parameter) {
            if (parameter is not string text || string.IsNullOrWhiteSpace(text)) {
                return (0, Brushes.Transparent, Brushes.Transparent);
            }

            var parts = text.Split('|', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries);
            if (parts.Length != 3) {
                return (0, Brushes.Transparent, Brushes.Transparent);
            }

            var threshold = long.TryParse(parts[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out var t) ? t : 0;
            var greaterBrush = ParseBrush(parts[1]);
            var elseBrush = ParseBrush(parts[2]);

            return (threshold, greaterBrush, elseBrush);
        }

        private static Brush ParseBrush(string token) {
            var brush = BrushConverter.ConvertFromString(token) as Brush;
            return brush ?? Brushes.Transparent;
        }

        private static long ToInt64(object value) {
            // enum 先转为 underlying 类型再转 long
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
