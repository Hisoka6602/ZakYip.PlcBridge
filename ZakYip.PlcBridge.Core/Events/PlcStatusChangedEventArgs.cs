using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Events {
    /// <summary>
    /// PLC 状态变更事件载荷
    /// </summary>
    public readonly record struct PlcStatusChangedEventArgs {
        /// <summary>旧状态</summary>
        public required PlcStatus OldStatus { get; init; }

        /// <summary>新状态</summary>
        public required PlcStatus NewStatus { get; init; }

        /// <summary>发生时间</summary>
        public required DateTimeOffset OccurredAt { get; init; }

        /// <summary>变更原因（可为空）</summary>
        public string? Reason { get; init; }
    }
}
