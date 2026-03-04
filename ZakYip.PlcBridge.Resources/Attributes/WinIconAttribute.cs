namespace ZakYip.PlcBridge.Resources.Attributes {

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class WinIconAttribute : Attribute {
        public string Icon { get; }

        public WinIconAttribute(string icon) {
            Icon = icon;
        }
    }
}
