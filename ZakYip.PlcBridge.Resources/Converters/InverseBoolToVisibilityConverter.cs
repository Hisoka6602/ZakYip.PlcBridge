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
    /// 布尔值转 Visibility（反转）：true=Collapsed（默认），false=Visible。
    /// ConverterParameter 可选：传入 "Hidden" 则 true=Hidden。
    /// </summary>
    public sealed class InverseBoolToVisibilityConverter : IValueConverter {

        public object Convert(object? value, Type targetType, object? parameter, CultureInfo culture) {
            var flag = value is true;

            if (!flag) {
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
