using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Enums;

namespace ZakYip.PlcBridge.Client.Events {
    /// <summary>
    /// SignalR 连接状态变更事件参数。
    /// </summary>
    public readonly record struct SignalRConnectionStatusChangedEventArgs {
        /// <summary>
        /// 旧状态。
        /// </summary>
        public required ConnectionStatus PreviousStatus { get; init; }

        /// <summary>
        /// 新状态。
        /// </summary>
        public required ConnectionStatus CurrentStatus { get; init; }

        /// <summary>
        /// 连接标识（可能为空）。
        /// </summary>
        public string? ConnectionId { get; init; }

        /// <summary>
        /// 状态变更原因（可选）。
        /// </summary>
        public string? Reason { get; init; }

        /// <summary>
        /// 发生时间（UTC）。
        /// </summary>
        public required DateTimeOffset OccurredAt { get; init; }
    }
}
