namespace ZakYip.PlcBridge.Resources.Attributes {

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class WinLottiesPathAttribute : Attribute {
        public string ResourcePathName { get; }

        public WinLottiesPathAttribute(string resourcePathName) {
            ResourcePathName = resourcePathName;
        }
    }
}
