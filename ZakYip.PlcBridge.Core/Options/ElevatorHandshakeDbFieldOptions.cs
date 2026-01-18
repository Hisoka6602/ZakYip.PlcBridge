using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Options {
    /// <summary>
    /// 电梯对接 DB 字段点位配置
    /// </summary>
    public sealed record class ElevatorHandshakeDbFieldOptions {
        /// <summary>
        /// 业务作用（字段含义）
        /// </summary>
        public required ElevatorHandshakeFieldRole Role { get; init; }

        /// <summary>
        /// 数据类型
        /// </summary>
        public required PlcDbValueType ValueType { get; init; }

        /// <summary>
        /// 字节偏移（TIA Portal 的“偏移量”）
        /// </summary>
        public required int ByteOffset { get; init; }

        /// <summary>
        /// 位偏移（仅 Bool 使用，0..7）
        /// </summary>
        public int? BitOffset { get; init; }

        /// <summary>
        /// 字符串最大长度（仅 S7String 使用，STRING[n] 的 n）
        /// </summary>
        public int? MaxStringLength { get; init; }

        /// <summary>
        /// 字段说明（用于日志/审计）
        /// </summary>
        public string? Remark { get; init; }
    }
}
