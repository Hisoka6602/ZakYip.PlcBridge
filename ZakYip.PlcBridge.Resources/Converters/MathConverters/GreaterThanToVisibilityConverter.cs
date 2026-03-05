namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class GreaterThanToVisibilityConverter : NumericComparisonToVisibilityConverterBase {
        protected override bool IsMatched(int comparisonResult) {
            return comparisonResult > 0;
        }
    }
}
