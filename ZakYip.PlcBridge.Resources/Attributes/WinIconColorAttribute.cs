namespace ZakYip.PlcBridge.Resources.Attributes {

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class WinIconColorAttribute : Attribute {
        public string ColorHex { get; }

        public WinIconColorAttribute(string colorHex) {
            ColorHex = colorHex;
        }
    }
}
