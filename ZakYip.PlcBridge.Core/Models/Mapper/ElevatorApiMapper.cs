using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using ZakYip.PlcBridge.Core.Models.Elevator;

namespace ZakYip.PlcBridge.Core.Models.Mapper {

    internal static class ElevatorApiMapper {

        public static ElevatorTaskQueryResult ToQueryResult(
            ElevatorApiEnvelopeDto<ElevatorTaskResDataDto> dto,
            long durationMs,
            string? traceId) {
            if (!dto.IsSuccess) {
                return new ElevatorTaskQueryResult {
                    IsSuccess = false,
                    ErrorCode = ElevatorApiErrorCode.RemoteRejected,
                    ErrorMessage = dto.ErrMsg ?? "电梯系统返回失败",
                    DurationMs = durationMs,
                    Task = null,
                    TraceId = traceId
                };
            }

            var data = dto.ResData;
            if (data is null) {
                return new ElevatorTaskQueryResult {
                    IsSuccess = false,
                    ErrorCode = ElevatorApiErrorCode.Unknown,
                    ErrorMessage = "电梯系统返回数据为空",
                    DurationMs = durationMs,
                    Task = null,
                    TraceId = traceId
                };
            }

            // 注释：Status="4" 未提供协议语义，避免强行映射为枚举导致误判
            var snapshot = new ElevatorTaskSnapshot {
                TaskId = null,
                Status = ElevatorTaskStatus.Unknown,
                StatusMessage = $"RawStatus={data.Status ?? "null"}; RawElevatorStatus={data.ElevatorStatus ?? "null"}",
                ItemCode = null,
                BatchNo = null,
                CallLayer = null,
                UseLayer = null,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };

            // 注释：可将 ErpGuid 作为 UniqueKey 或 TaskId 的候选（按业务选择其一）
            // snapshot = snapshot with { TaskId = data.ErpGuid };

            return new ElevatorTaskQueryResult {
                IsSuccess = true,
                ErrorCode = ElevatorApiErrorCode.None,
                ErrorMessage = null,
                DurationMs = durationMs,
                Task = snapshot,
                TraceId = traceId
            };
        }
    }
}
