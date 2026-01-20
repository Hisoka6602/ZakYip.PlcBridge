using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 电梯任务快照
    /// </summary>
    public sealed record class ElevatorTaskSnapshot {
        /// <summary>
        /// 任务 Id
        /// </summary>
        public string? TaskId { get; init; }

        /// <summary>
        /// 当前状态
        /// </summary>
        public required ElevatorTaskStatus Status { get; init; }

        /// <summary>
        /// 状态描述（便于日志/前端展示）
        /// </summary>
        public string? StatusMessage { get; init; }

        /// <summary>
        /// 关联物料编号
        /// </summary>
        public string? ItemCode { get; init; }

        /// <summary>
        /// 关联批次号
        /// </summary>
        public string? BatchNo { get; init; }

        /// <summary>
        /// 呼叫楼层
        /// </summary>
        public short? CallLayer { get; init; }

        /// <summary>
        /// 使用楼层
        /// </summary>
        public short? UseLayer { get; init; }

        /// <summary>
        /// 更新时间（UTC）
        /// </summary>
        public DateTimeOffset UpdatedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    }
}
