using Microsoft.Extensions.Configuration;

namespace ZakYip.PlcBridge.Client.Options {
    internal static class LogCleanupOptionsFactory {
        private const string SectionName = "LogCleanup";

        public static LogCleanupOptions Create(IConfiguration configuration) {
            if (configuration is null) {
                throw new ArgumentNullException(nameof(configuration), "配置对象为空。");
            }

            var section = configuration.GetSection(SectionName);

            return new LogCleanupOptions {
                Enabled = TryGetBool(section["Enabled"], defaultValue: true),
                RetentionDays = TryGetInt(section["RetentionDays"], defaultValue: 2),
                CheckIntervalHours = TryGetInt(section["CheckIntervalHours"], defaultValue: 1),
                LogDirectory = section["LogDirectory"] ?? "logs"
            };
        }

        private static bool TryGetBool(string? value, bool defaultValue) {
            return bool.TryParse(value, out var result) ? result : defaultValue;
        }

        private static int TryGetInt(string? value, int defaultValue) {
            return int.TryParse(value, out var result) && result > 0 ? result : defaultValue;
        }
    }
}
