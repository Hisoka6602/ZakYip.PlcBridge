using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// PLC 数据区
    /// </summary>
    public enum PlcDataArea {

        /// <summary>DB 数据块</summary>
        [Description("DB 数据块")]
        Db = 0,

        /// <summary>输入区（I）</summary>
        [Description("输入区（I）")]
        Input = 1,

        /// <summary>输出区（Q）</summary>
        [Description("输出区（Q）")]
        Output = 2,

        /// <summary>内部存储区（M）</summary>
        [Description("内部存储区（M）")]
        Memory = 3
    }
}
