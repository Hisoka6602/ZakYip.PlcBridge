using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Options {
    /// <summary>
    /// 离线使用限制配置（按累计运行时长判定）
    /// </summary>
    public sealed record class UsageLimitOptions {
        /// <summary>
        /// 是否启用
        /// </summary>
        public bool IsEnabled { get; init; } = true;

        /// <summary>
        /// 最大累计可用时长（毫秒）
        /// </summary>
        public long MaxTotalRunTimeMs { get; init; } = 168L * 60 * 60 * 1000;

        /// <summary>
        /// 落盘间隔（毫秒）
        /// </summary>
        public int CheckpointIntervalMs { get; init; } = 15_000;

        /// <summary>
        /// 连续存储失败次数上限（超过则拒绝运行）
        /// </summary>
        public int MaxConsecutiveStoreFailures { get; init; } = 3;

        /// <summary>
        /// 产品标识（用于生成存储路径与加密熵）
        /// </summary>
        public string ProductKey { get; init; } = "ZakYip.PlcBridge";
    }
}
