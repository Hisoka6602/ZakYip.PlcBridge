using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// PLC 字符串格式
    /// </summary>
    public enum PlcStringKind {

        /// <summary>S7 STRING（前 2 字节：最大长度/当前长度）</summary>
        [Description("S7 STRING（前 2 字节：最大长度/当前长度）")]
        SiemensString = 0,

        /// <summary>固定长度 ASCII（无头部）</summary>
        [Description("固定长度 ASCII（无头部）")]
        FixedAscii = 1,

        /// <summary>固定长度 UTF8（无头部）</summary>
        [Description("固定长度 UTF8（无头部）")]
        FixedUtf8 = 2
    }
}
