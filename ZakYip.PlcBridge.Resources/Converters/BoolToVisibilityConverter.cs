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
    /// 布尔值转 Visibility：true=Visible，false=Collapsed（默认）。
    /// ConverterParameter 可选：传入 "Hidden" 则 false=Hidden。
    /// </summary>
    public sealed class BoolToVisibilityConverter : IValueConverter {

        public object Convert(object? value, Type targetType, object parameter, CultureInfo culture) {
            var flag = value is true;
            if (flag) {
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
