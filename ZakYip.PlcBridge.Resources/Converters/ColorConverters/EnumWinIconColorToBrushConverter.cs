using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows.Data;
using System.Globalization;
using System.Windows.Media;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;
using ZakYip.PlcBridge.Resources.Attributes;

namespace ZakYip.PlcBridge.Resources.Converters.ColorConverters {

    /// <summary>
    /// 将枚举值转换为 WinIconColor 对应的 Brush。
    /// </summary>
    public sealed class EnumWinIconColorToBrushConverter : IValueConverter {
        private static readonly BrushConverter BrushConverter = new();

        // 以 (enumType, enumValue) 为 key 缓存 Brush，避免重复反射/解析
        private static readonly ConcurrentDictionary<(Type EnumType, object Value), Brush> Cache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            try {
                if (value is null) {
                    return Brushes.Transparent;
                }

                var enumType = value.GetType();
                if (!enumType.IsEnum) {
                    return Brushes.Transparent;
                }

                return Cache.GetOrAdd((enumType, value), static key => ResolveBrush(key.EnumType, key.Value));
            }
            catch {
                // 失败降级：不抛异常影响 UI 线程
                return Brushes.Transparent;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            // 单向转换
            return Binding.DoNothing;
        }

        private static Brush ResolveBrush(Type enumType, object enumValue) {
            var name = Enum.GetName(enumType, enumValue);
            if (string.IsNullOrWhiteSpace(name)) {
                return Brushes.Transparent;
            }

            var field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field is null) {
                return Brushes.Transparent;
            }

            var attr = field.GetCustomAttribute<WinIconColorAttribute>(inherit: false);
            if (attr is null || string.IsNullOrWhiteSpace(attr.ColorHex)) {
                return Brushes.Transparent;
            }

            var brush = BrushConverter.ConvertFromString(attr.ColorHex) as Brush;
            if (brush is null) {
                return Brushes.Transparent;
            }

            // 冻结 Brush，减少 WPF 渲染线程负担
            if (brush.CanFreeze) {
                brush.Freeze();
            }

            return brush;
        }
    }
}
