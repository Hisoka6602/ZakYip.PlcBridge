namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class EqualToConverter : EqualityConverterBase {
        protected override object MapResult(bool isEqual) {
            return isEqual;
        }
    }
}
