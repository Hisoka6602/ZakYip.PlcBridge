namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class LessThanToVisibilityConverter : NumericComparisonToVisibilityConverterBase {
        protected override bool IsMatched(int comparisonResult) {
            return comparisonResult < 0;
        }
    }
}
