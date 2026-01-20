using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models.Security {
    /// <summary>
    /// 使用限制判定结果
    /// </summary>
    public readonly record struct UsageLimitDecision {
        public required bool IsAllowed { get; init; }
        public required UsageLimitDenyReason DenyReason { get; init; }
        public TimeSpan? Remaining { get; init; }
        public string? Message { get; init; }
    }
}
