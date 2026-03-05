using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Models.SignalR {
    /// <summary>
    /// Hub 调用应答（与客户端 SignalRInvokeResponse 语义对齐）。
    /// </summary>
    public sealed record class InvokeAckResponse {
        /// <summary>
        /// 是否成功。
        /// </summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// 载荷（成功时返回；失败时通常为 null）。
        /// </summary>
        public object? Payload { get; init; }

        /// <summary>
        /// 错误信息。
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 响应时间。
        /// </summary>
        public required DateTimeOffset RespondedAt { get; init; }
    }
}
