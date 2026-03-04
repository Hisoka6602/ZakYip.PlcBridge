using System;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Data;
using System.Globalization;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Resources.Converters {

    /// <summary>
    /// 对象不为 null 时返回 Visible，否则返回 Collapsed（默认）。
    /// ConverterParameter 可选：传入 "Hidden" 则 null 时返回 Hidden。
    /// </summary>
    public sealed class IsNotNullToVisibilityConverter : IValueConverter {

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            var isNotNull = value is not null;

            if (isNotNull) {
                return Visibility.Visible;
            }

            return Visibility.Collapsed;
        }

        public object ConvertBack(object? value, Type targetType, object? parameter, CultureInfo culture) {
            // 单向转换
            return Binding.DoNothing;
        }
    }
}
