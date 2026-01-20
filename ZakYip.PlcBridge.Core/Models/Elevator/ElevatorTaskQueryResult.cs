using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 任务查询结果
    /// </summary>
    public readonly record struct ElevatorTaskQueryResult {
        /// <summary>
        /// 是否成功
        /// </summary>
        public required bool IsSuccess { get; init; }

        /// <summary>
        /// 错误码（成功时为 None）
        /// </summary>
        public required ElevatorApiErrorCode ErrorCode { get; init; }

        /// <summary>
        /// 错误信息（成功时可为空）
        /// </summary>
        public string? ErrorMessage { get; init; }

        /// <summary>
        /// 耗时（毫秒）
        /// </summary>
        public long DurationMs { get; init; }

        /// <summary>
        /// 任务快照（成功但未找到任务时可为 null）
        /// </summary>
        public ElevatorTaskSnapshot? Task { get; init; }

        /// <summary>
        /// 追踪 Id
        /// </summary>
        public string? TraceId { get; init; }
    }
}
