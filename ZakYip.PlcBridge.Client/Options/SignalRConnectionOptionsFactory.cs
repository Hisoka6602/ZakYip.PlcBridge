using System;
using System.Collections.Generic;
using Microsoft.Extensions.Configuration;

namespace ZakYip.PlcBridge.Client.Options {

    /// <summary>
    /// SignalRConnectionOptions 构建器（避免引入 Binder）。
    /// </summary>
    internal static class SignalRConnectionOptionsFactory {
        private const string SectionName = "SignalRConnectionOptions";

        public static SignalRConnectionOptions Create(IConfiguration configuration) {
            if (configuration is null) {
                throw new ArgumentNullException(nameof(configuration), "配置对象为空。");
            }

            var section = configuration.GetSection(SectionName);
            if (!section.Exists()) {
                throw new InvalidOperationException($"配置缺失：{SectionName}。");
            }

            var hubUrlRaw = section["HubUrl"];
            if (string.IsNullOrWhiteSpace(hubUrlRaw)) {
                throw new InvalidOperationException("配置缺失：SignalRConnectionOptions:HubUrl。");
            }

            var headers = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            var headersSection = section.GetSection("Headers");
            if (headersSection.Exists()) {
                foreach (var child in headersSection.GetChildren()) {
                    if (!string.IsNullOrWhiteSpace(child.Key) && child.Value is not null) {
                        headers[child.Key] = child.Value;
                    }
                }
            }

            // 只读取已知字段；其余字段按 Options 类型自行扩展
            var options = new SignalRConnectionOptions {
                HubUrl = new Uri(hubUrlRaw, UriKind.Absolute),
                IsAutoReconnectEnabled = OptionParsingHelper.ParseBool(section["IsAutoReconnectEnabled"]),
                AccessToken = section["AccessToken"],
                Headers = headers.Count == 0 ? null : headers
            };

            return options;
        }
    }
}
