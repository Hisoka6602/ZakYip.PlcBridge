using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Attributes;

namespace ZakYip.PlcBridge.Client.Enums {

    /// <summary>
    /// 操作结果状态枚举。
    /// </summary>
    public enum OperationResultStatus {

        /// <summary>
        /// 成功
        /// </summary>
        [Description("成功"), WinLottiesPath("SuccessAnimationPath")]
        Success = 0,

        /// <summary>
        /// 失败
        /// </summary>
        [Description("失败"), WinLottiesPath("FailedAnimationPath")]
        Failure = 1
    }
}
