using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 呼叫电梯请求体
    /// </summary>
    public sealed record class ElevatorCallRequest {
        /// <summary>
        /// ERP 唯一标识
        /// </summary>
        [JsonPropertyName("erpGuid")]
        public required string ErpGuid { get; init; } = string.Empty;

        /// <summary>
        /// 物料编码
        /// </summary>
        [JsonPropertyName("itemCode")]
        public required string ItemCode { get; init; } = string.Empty;

        /// <summary>
        /// 批次号
        /// </summary>
        [JsonPropertyName("batchNo")]
        public string? BatchNo { get; init; }

        /// <summary>
        /// 楼层
        /// </summary>
        [JsonPropertyName("layer")]
        public required int Layer { get; init; }

        /// <summary>
        /// 数量
        /// </summary>
        [JsonPropertyName("num")]
        public required int Num { get; init; }

        /// <summary>
        /// 箱数
        /// </summary>
        [JsonPropertyName("boxQty")]
        public required int BoxQty { get; init; }
    }
}
