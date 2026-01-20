using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// 电梯 API 错误码
    /// </summary>
    public enum ElevatorApiErrorCode {

        /// <summary>
        /// 无错误
        /// </summary>
        [Description("无错误")]
        None = 0,

        /// <summary>
        /// 超时
        /// </summary>
        [Description("请求超时")]
        Timeout = 1,

        /// <summary>
        /// 网络异常
        /// </summary>
        [Description("网络异常")]
        NetworkError = 2,

        /// <summary>
        /// 远端返回失败
        /// </summary>
        [Description("远端返回失败")]
        RemoteRejected = 3,

        /// <summary>
        /// 参数非法
        /// </summary>
        [Description("参数非法")]
        InvalidRequest = 4,

        /// <summary>
        /// 未找到任务
        /// </summary>
        [Description("未找到任务")]
        TaskNotFound = 5,

        /// <summary>
        /// 未知错误
        /// </summary>
        [Description("未知错误")]
        Unknown = 99
    }
}
