namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class GreaterThanConverter : NumericComparisonConverterBase {
        protected override bool IsMatched(int comparisonResult) {
            return comparisonResult > 0;
        }
    }
}
