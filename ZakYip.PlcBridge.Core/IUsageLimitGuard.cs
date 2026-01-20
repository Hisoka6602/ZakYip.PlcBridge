using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Models.Security;

namespace ZakYip.PlcBridge.Core {

    /// <summary>
    /// 使用限制守卫（离线限时：按累计运行时长判定）
    /// </summary>
    public interface IUsageLimitGuard {

        /// <summary>
        /// 校验当前是否允许继续运行
        /// </summary>
        ValueTask<UsageLimitDecision> EvaluateAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 运行心跳（用于定时累加运行时长并落盘）
        /// </summary>
        ValueTask HeartbeatAsync(CancellationToken cancellationToken = default);
    }
}
