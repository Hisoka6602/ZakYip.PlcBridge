using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 电梯任务回调数据体
    /// </summary>
    public sealed record class ElevatorTaskResDataDto {
        /// <summary>
        /// ERP 唯一标识
        /// </summary>
        [JsonPropertyName("ErpGuid")]
        public string? ErpGuid { get; init; }

        /// <summary>
        /// 任务状态（字符串编码，需协议解释）
        /// </summary>
        [JsonPropertyName("Status")]
        public string? Status { get; init; }

        /// <summary>
        /// 电梯状态（可能为空，需协议解释）
        /// </summary>
        [JsonPropertyName("ElevatorStatus")]
        public string? ElevatorStatus { get; init; }
    }
}
