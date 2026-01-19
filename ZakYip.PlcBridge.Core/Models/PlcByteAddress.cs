using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Models {
    /// <summary>
    /// Byte 地址定义（契约层，不绑定具体 PLC 实现）
    /// </summary>
    public sealed record class PlcByteAddress {
        /// <summary>
        /// DB 块编号（例如 DB1）
        /// </summary>
        public int? DbNumber { get; init; }

        /// <summary>
        /// 起始字节偏移（例如 DBB10 的 10）
        /// </summary>
        public int? StartByteAdr { get; init; }
    }
}
