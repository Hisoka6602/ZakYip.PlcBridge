namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class AddConverter : BinaryArithmeticConverterBase {
        protected override bool TryCalculate(double value, double operand, out double result) {
            result = value + operand;
            return true;
        }
    }
}
