using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// PLC DB 字段值类型
    /// </summary>
    public enum PlcDbValueType {

        /// <summary>
        /// 布尔（位）
        /// </summary>
        [Description("Bool")]
        Bool = 1,

        /// <summary>
        /// 16 位整数（TIA Portal Int）
        /// </summary>
        [Description("Int16")]
        Int16 = 2,

        /// <summary>
        /// S7 STRING（2 字节头 + 内容）
        /// </summary>
        [Description("S7String")]
        S7String = 3,
    }
}
