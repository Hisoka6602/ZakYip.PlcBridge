using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Events {
    /// <summary>
    /// PLC 异常事件载荷（用于隔离异常，不影响上层调用链）
    /// </summary>
    public readonly record struct PlcFaultedEventArgs {
        /// <summary>异常描述</summary>
        public required string Message { get; init; }

        /// <summary>异常对象</summary>
        public required Exception Exception { get; init; }

        /// <summary>发生时间</summary>
        public required DateTimeOffset OccurredAt { get; init; }

        /// <summary>上下文信息（可为空）</summary>
        public string? Context { get; init; }
    }
}
