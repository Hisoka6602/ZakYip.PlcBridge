using System;

namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    internal static class NumericConversionHelper {
        public static bool TryConvertToDouble(object value, IFormatProvider formatProvider, out double numericValue) {
            try {
                if (value is IConvertible convertibleValue) {
                    numericValue = convertibleValue.ToDouble(formatProvider);
                    return true;
                }
            }
            catch (FormatException) {
                // 非数字输入，按转换失败处理。
            }
            catch (InvalidCastException) {
                // 不可转换类型，按转换失败处理。
            }
            catch (OverflowException) {
                // 超出 double 范围，按转换失败处理。
            }

            numericValue = default;
            return false;
        }
    }
}
