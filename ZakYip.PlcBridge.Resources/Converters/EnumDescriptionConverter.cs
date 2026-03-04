using System;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Windows.Data;
using System.Globalization;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Collections.Concurrent;

namespace ZakYip.PlcBridge.Resources.Converters {

    /// <summary>
    /// 将枚举值转换为 Description 文本。
    /// </summary>
    public sealed class EnumDescriptionConverter : IValueConverter {
        private static readonly ConcurrentDictionary<(Type EnumType, object Value), string> Cache = new();

        public object Convert(object value, Type targetType, object parameter, CultureInfo culture) {
            try {
                if (value is null) {
                    return string.Empty;
                }

                var enumType = value.GetType();
                if (!enumType.IsEnum) {
                    return string.Empty;
                }

                return Cache.GetOrAdd((enumType, value), static key => ResolveDescription(key.EnumType, key.Value));
            }
            catch {
                // 失败降级：返回空字符串，避免影响 UI
                return string.Empty;
            }
        }

        public object ConvertBack(object value, Type targetType, object parameter, CultureInfo culture) {
            // 单向转换
            return Binding.DoNothing;
        }

        private static string ResolveDescription(Type enumType, object enumValue) {
            var name = Enum.GetName(enumType, enumValue);
            if (string.IsNullOrWhiteSpace(name)) {
                return enumValue.ToString() ?? string.Empty;
            }

            var field = enumType.GetField(name, BindingFlags.Public | BindingFlags.Static);
            if (field is null) {
                return enumValue.ToString() ?? string.Empty;
            }

            var attr = field.GetCustomAttribute<DescriptionAttribute>(inherit: false);
            if (attr is null || string.IsNullOrWhiteSpace(attr.Description)) {
                return name;
            }

            return attr.Description;
        }
    }
}
