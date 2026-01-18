using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Models {
    /// <summary>
    /// DB 块 Bool 点位定义
    /// </summary>
    public readonly record struct PlcDbBoolPoint {
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
        /// 点位标识（可为空，用于日志/上报区分）
        /// </summary>
        public string? Tag { get; init; }
    }
}
