using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Events {
    /// <summary>
    /// SignalR 消息接收事件参数。
    /// </summary>
    public readonly record struct SignalRMessageReceivedEventArgs {
        /// <summary>
        /// 主题/方法名。
        /// </summary>
        public required string Topic { get; init; }

        /// <summary>
        /// 原始载荷。
        /// </summary>
        public required object Payload { get; init; }

        /// <summary>
        /// 接收时间（UTC）。
        /// </summary>
        public required DateTimeOffset ReceivedAt { get; init; }
    }
}
