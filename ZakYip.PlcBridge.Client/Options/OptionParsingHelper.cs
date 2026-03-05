namespace ZakYip.PlcBridge.Client.Options {
    internal static class OptionParsingHelper {
        public static bool ParseBool(string? value, bool defaultValue = false) {
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        public static int ParsePositiveInt(string? value, int defaultValue) {
            return int.TryParse(value, out var result) && result > 0 ? result : defaultValue;
        }
    }
}
