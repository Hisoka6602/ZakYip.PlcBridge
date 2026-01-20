using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// 电梯任务状态
    /// </summary>
    public enum ElevatorTaskStatus {

        /// <summary>
        /// 未知
        /// </summary>
        [Description("未知")]
        Unknown = 0,

        /// <summary>
        /// 已创建
        /// </summary>
        [Description("已创建")]
        Created = 1,

        /// <summary>
        /// 执行中
        /// </summary>
        [Description("执行中")]
        Running = 2,

        /// <summary>
        /// 已完成
        /// </summary>
        [Description("已完成")]
        Completed = 3,

        /// <summary>
        /// 失败
        /// </summary>
        [Description("失败")]
        Failed = 4,

        /// <summary>
        /// 已取消
        /// </summary>
        [Description("已取消")]
        Canceled = 5
    }
}
