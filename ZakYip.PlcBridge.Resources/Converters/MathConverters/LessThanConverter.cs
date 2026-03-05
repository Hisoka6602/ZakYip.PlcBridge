namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class LessThanConverter : NumericComparisonConverterBase {
        protected override bool IsMatched(int comparisonResult) {
            return comparisonResult < 0;
        }
    }
}
