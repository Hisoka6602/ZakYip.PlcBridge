using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// S7 CPU 型号分类（用于连接参数与兼容策略）
    /// </summary>
    public enum S7CpuType {

        /// <summary>S7-200</summary>
        [Description("S7-200")]
        S7200 = 0,

        /// <summary>S7-300</summary>
        [Description("S7-300")]
        S7300 = 1,

        /// <summary>S7-400</summary>
        [Description("S7-400")]
        S7400 = 2,

        /// <summary>S7-1200</summary>
        [Description("S7-1200")]
        S71200 = 3,

        /// <summary>S7-1500</summary>
        [Description("S7-1500")]
        S71500 = 4,
    }
}
