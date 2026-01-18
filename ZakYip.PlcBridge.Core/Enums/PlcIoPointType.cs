using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// PLC IO 点位类型
    /// </summary>
    public enum PlcIoPointType {

        /// <summary>数字量输入</summary>
        [Description("数字量输入")]
        DigitalInput = 0,

        /// <summary>数字量输出</summary>
        [Description("数字量输出")]
        DigitalOutput = 1,

        /// <summary>模拟量输入</summary>
        [Description("模拟量输入")]
        AnalogInput = 2,

        /// <summary>模拟量输出</summary>
        [Description("模拟量输出")]
        AnalogOutput = 3,
    }
}
