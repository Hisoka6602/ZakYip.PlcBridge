using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ZakYip.PlcBridge.Core.Models.Elevator {
    /// <summary>
    /// 电梯接口统一回包（回调）外层结构
    /// </summary>
    public sealed record class ElevatorApiEnvelopeDto<TResData> {
        /// <summary>
        /// 错误码（"0" 通常表示成功，实际以对接协议为准）
        /// </summary>
        [JsonPropertyName("ErrCode")]
        public string? ErrCode { get; init; }

        /// <summary>
        /// 错误信息
        /// </summary>
        [JsonPropertyName("ErrMsg")]
        public string? ErrMsg { get; init; }

        /// <summary>
        /// 数量字段（若无语义，可仅用于诊断）
        /// </summary>
        [JsonPropertyName("Total")]
        public int Total { get; init; }

        /// <summary>
        /// 是否成功
        /// </summary>
        [JsonPropertyName("Success")]
        public bool Success { get; init; }

        /// <summary>
        /// 业务数据
        /// </summary>
        [JsonPropertyName("ResData")]
        public TResData? ResData { get; init; }
    }
}
