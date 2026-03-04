using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Resources.Attributes;

namespace ZakYip.PlcBridge.Client.Enums {

    /// <summary>
    /// 操作结果状态枚举。
    /// </summary>
    public enum OperationResultStatus {

        /// <summary>
        /// 成功
        /// </summary>
        [Description("推送成功"), WinLottiesPath("SuccessAnimationPath"), WinIconColor("#5A32CD32")]
        Success = 0,

        /// <summary>
        /// 失败
        /// </summary>
        [Description("推送失败"), WinLottiesPath("FailedAnimationPath"), WinIconColor("#5AFF0000")]
        Failure = 1
    }
}
