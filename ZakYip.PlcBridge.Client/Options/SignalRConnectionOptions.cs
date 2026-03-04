using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Options {

    /// <summary>
    /// SignalR 连接参数。
    /// </summary>
    public sealed class SignalRConnectionOptions {

        /// <summary>
        /// Hub 地址。
        /// </summary>
        public required Uri HubUrl { get; init; }

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        public bool IsAutoReconnectEnabled { get; init; } = true;

        /// <summary>
        /// 访问令牌（可选）。
        /// </summary>
        public string? AccessToken { get; init; }

        /// <summary>
        /// 自定义请求头（可选）。
        /// </summary>
        public IReadOnlyDictionary<string, string>? Headers { get; init; }

        /// <summary>
        /// 自动重连延迟序列（可选）。为空时使用实现内部默认策略。
        /// </summary>
        public IReadOnlyList<TimeSpan>? ReconnectDelays { get; init; }
    }
}
