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
    /// 将布尔值转换为 Brush。
    /// ConverterParameter 格式："{trueColor}|{falseColor}"
    /// 颜色可为：#RRGGBB/#AARRGGBB 或颜色名（如 Red）或任意 Brush 字符串表示。
    /// </summary>
    public sealed class BoolToBrushConverter : IValueConverter {

        // 缓存解析结果，减少 BrushConverter 反复解析带来的开销
        private static readonly BrushConverter BrushConverter = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            try {
                var isTrue = value is true;

                var (trueBrush, falseBrush) = ParseParameter(parameter);
                return isTrue ? trueBrush : falseBrush;
            }
            catch {
                // 转换失败时返回透明，避免影响调用链
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            // 单向转换，不支持反向
            return Binding.DoNothing;
        }

        private static (Brush TrueBrush, Brush FalseBrush) ParseParameter(object parameter) {
            if (parameter is not string text || string.IsNullOrWhiteSpace(text)) {
                return (Brushes.Transparent, Brushes.Transparent);
            }

            var separatorIndex = text.IndexOf('|');
            if (separatorIndex <= 0 || separatorIndex >= text.Length - 1) {
                return (Brushes.Transparent, Brushes.Transparent);
            }

            var trueToken = text[..separatorIndex].Trim();
            var falseToken = text[(separatorIndex + 1)..].Trim();

            var trueBrush = ParseBrush(trueToken);
            var falseBrush = ParseBrush(falseToken);

            return (trueBrush, falseBrush);
        }

        private static Brush ParseBrush(string token) {
            // Brushes.* 快路径：常用颜色直接命中，避免 BrushConverter
            if (TryGetKnownBrush(token, out var brush)) {
                return brush;
            }

            var parsed = BrushConverter.ConvertFromString(token) as Brush;
            return parsed ?? Brushes.Transparent;
        }

        private static bool TryGetKnownBrush(string token, out Brush? brush) {
            // 注意：不使用反射，减少启动与运行期开销
            // 常用颜色可以按需补充
            brush = token switch {
                "Transparent" => Brushes.Transparent,
                "White" => Brushes.White,
                "Black" => Brushes.Black,
                "Red" => Brushes.Red,
                "Green" => Brushes.Green,
                "Blue" => Brushes.Blue,
                "Gray" => Brushes.Gray,
                "Yellow" => Brushes.Yellow,
                _ => null
            };

            return brush is not null;
        }
    }
}
