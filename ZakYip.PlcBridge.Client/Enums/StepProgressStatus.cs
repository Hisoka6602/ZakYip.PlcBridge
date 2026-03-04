using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Enums {

    /// <summary>
    /// 步骤进度状态枚举。
    /// </summary>
    public enum StepProgressStatus {

        /// <summary>
        /// 未开始。
        /// </summary>
        [Description("未开始")]
        NotStarted = 0,

        /// <summary>
        /// 等待中。
        /// </summary>
        [Description("等待中")]
        Waiting = 1,

        /// <summary>
        /// 已完成。
        /// </summary>
        [Description("已完成")]
        Completed = 2
    }
}
