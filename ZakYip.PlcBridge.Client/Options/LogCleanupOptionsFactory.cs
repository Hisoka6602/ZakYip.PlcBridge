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
                Enabled = OptionParsingHelper.ParseBool(section["Enabled"], defaultValue: true),
                RetentionDays = OptionParsingHelper.ParsePositiveIntOrDefault(section["RetentionDays"], defaultValue: 2),
                CheckIntervalHours = OptionParsingHelper.ParsePositiveIntOrDefault(section["CheckIntervalHours"], defaultValue: 1),
                LogDirectory = section["LogDirectory"] ?? "logs"
            };
        }
    }
}
