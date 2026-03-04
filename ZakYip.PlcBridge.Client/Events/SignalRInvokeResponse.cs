using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Events {
    /// <summary>
    /// SignalR 调用响应。
    /// </summary>
    public readonly record struct SignalRInvokeResponse {
        /// <summary>
        /// 是否成功。
        /// </summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// 响应载荷（可选）。
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// 错误信息（可选）。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 响应时间（UTC）。
        /// </summary>
        public required DateTimeOffset RespondedAt { get; init; }
    }
}
