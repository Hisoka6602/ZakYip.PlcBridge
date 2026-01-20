using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 电梯 API 调用结果
    /// </summary>
    public readonly record struct ElevatorApiResult {
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
        /// 追踪 Id（用于链路追踪/排障）
        /// </summary>
        public string? TraceId { get; init; }

        /// <summary>
        /// 提交内容（建议为脱敏后的请求体/请求参数快照，仅用于诊断）
        /// </summary>
        public string? RequestPayload { get; init; }

        /// <summary>
        /// 响应内容（建议为脱敏后的响应体快照，仅用于诊断）
        /// </summary>
        public string? ResponsePayload { get; init; }

        /// <summary>
        /// curl 格式内容
        /// </summary>
        public string? Curl { get; init; }
    }
}
