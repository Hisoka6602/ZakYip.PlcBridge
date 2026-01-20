using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 电梯任务查询请求体
    /// </summary>
    public sealed record class ElevatorTaskQueryRequest {
        /// <summary>
        /// ERP 唯一标识
        /// </summary>
        [JsonPropertyName("erpGuid")]
        public required string ErpGuid { get; init; } = string.Empty;

        /// <summary>
        /// 状态码（查询条件）
        /// </summary>
        [JsonPropertyName("status")]
        public required int Status { get; init; }
    }
}
