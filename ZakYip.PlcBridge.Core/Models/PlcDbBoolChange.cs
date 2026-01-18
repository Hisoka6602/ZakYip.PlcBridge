using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models {
    /// <summary>
    /// DB 块 Bool 变更明细
    /// </summary>
    public readonly record struct PlcDbBoolChange {
        /// <summary>
        /// DB 编号
        /// </summary>
        public required int DbNumber { get; init; }

        /// <summary>
        /// 字节偏移
        /// </summary>
        public required int ByteOffset { get; init; }

        /// <summary>
        /// Bit 偏移（0~7）
        /// </summary>
        public required int BitOffset { get; init; }

        /// <summary>
        /// 旧电平
        /// </summary>
        public required PlcIoSignalState OldState { get; init; }

        /// <summary>
        /// 新电平
        /// </summary>
        public required PlcIoSignalState NewState { get; init; }

        /// <summary>
        /// 点位标识（可为空）
        /// </summary>
        public string? Tag { get; init; }
    }
}
