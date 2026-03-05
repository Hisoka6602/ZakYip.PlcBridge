using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 生产订单推送请求。
    /// </summary>
    public sealed record class ProductionOrderPushRequest {
        /// <summary>
        /// 工单号。
        /// </summary>
        [JsonPropertyName("workOrderNo")]
        public required string WorkOrderNo { get; init; }

        /// <summary>
        /// 物料编号。
        /// </summary>
        [JsonPropertyName("itemCode")]
        public required string ItemCode { get; init; }

        /// <summary>
        /// 批次号。
        /// </summary>
        [JsonPropertyName("batchNo")]
        public string? BatchNo { get; init; }

        /// <summary>
        /// 计划箱数。
        /// </summary>
        [JsonPropertyName("PlanQty")]
        public required int PlannedBoxCount { get; init; }
    }
}
