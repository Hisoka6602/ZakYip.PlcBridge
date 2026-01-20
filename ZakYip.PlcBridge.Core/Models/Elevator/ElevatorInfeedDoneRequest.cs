using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 入库执行完成上报请求体
    /// </summary>
    public sealed record class ElevatorInfeedDoneRequest {
        /// <summary>
        /// ERP 唯一标识
        /// </summary>
        [JsonPropertyName("erpGuid")]
        public required string ErpGuid { get; init; } = string.Empty;

        /// <summary>
        /// 状态码
        /// </summary>
        [JsonPropertyName("status")]
        public required int Status { get; init; }
    }
}
