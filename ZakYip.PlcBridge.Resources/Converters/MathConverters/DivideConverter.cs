namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class DivideConverter : BinaryArithmeticConverterBase {
        protected override bool TryCalculate(double value, double operand, out double result) {
            if (operand == 0) {
                result = default;
                return false;
            }

            result = value / operand;
            return true;
        }
    }
}
