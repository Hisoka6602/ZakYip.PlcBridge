using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Models.Security {
    /// <summary>
    /// 使用状态（加密存储，支持防回滚）
    /// </summary>
    public sealed record class UsageState {
        /// <summary>
        /// 版本号（用于兼容升级）
        /// </summary>
        public int Version { get; init; } = 1;

        /// <summary>
        /// 安装标识（首次生成）
        /// </summary>
        public Guid InstallId { get; init; } = Guid.NewGuid();

        /// <summary>
        /// 递增序列号（用于防回滚选主）
        /// </summary>
        public long Sequence { get; set; }

        /// <summary>
        /// 累计已用时长（毫秒）
        /// </summary>
        public long TotalUsedMs { get; set; }

        /// <summary>
        /// 最近一次落盘时间（仅用于审计）
        /// </summary>
        public DateTimeOffset LastCheckpoint { get; set; } = DateTimeOffset.Now;
    }
}
