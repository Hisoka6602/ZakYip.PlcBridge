using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Models;

namespace ZakYip.PlcBridge.Core.Events {
    /// <summary>
    /// DB 块 Bool 变化事件载荷（批量上报）
    /// </summary>
    public readonly record struct PlcDbBoolsChangedEventArgs {
        /// <summary>
        /// 变化明细集合（同一次采样周期可能包含多个 DB 的变化）
        /// </summary>
        public required IReadOnlyList<PlcDbBoolChange> Changes { get; init; }

        /// <summary>
        /// 发生时间
        /// </summary>
        public required DateTimeOffset OccurredAt { get; init; }
    }
}
