namespace ZakYip.PlcBridge.Resources.Converters.MathConverters {

    public class EqualToToVisibilityConverter : EqualityConverterBase {
        protected override object MapResult(bool isEqual) {
            return isEqual ? System.Windows.Visibility.Visible : System.Windows.Visibility.Collapsed;
        }
    }
}
