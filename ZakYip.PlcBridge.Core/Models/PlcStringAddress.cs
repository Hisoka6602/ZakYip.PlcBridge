using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models {
    /// <summary>
    /// 字符串读取地址
    /// </summary>
    public readonly record struct PlcStringAddress {
        /// <summary>
        /// 数据区
        /// </summary>
        public required PlcDataArea Area { get; init; }

        /// <summary>
        /// DB 编号（仅 Area=Db 时使用；其他区可为 0）
        /// </summary>
        public required int DbNumber { get; init; }

        /// <summary>
        /// 起始字节偏移
        /// </summary>
        public required int ByteOffset { get; init; }

        /// <summary>
        /// 字符串格式
        /// </summary>
        public required PlcStringKind Kind { get; init; }

        /// <summary>
        /// 最大长度（字节/字符语义由 Kind 决定）
        /// </summary>
        public required int MaxLength { get; init; }
    }
}
