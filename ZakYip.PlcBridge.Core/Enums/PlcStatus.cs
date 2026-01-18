using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// PLC 状态
    /// </summary>
    public enum PlcStatus {

        /// <summary>未初始化</summary>
        [Description("未初始化")]
        NotInitialized = 0,

        /// <summary>初始化中</summary>
        [Description("初始化中")]
        Initializing = 1,

        /// <summary>连接中</summary>
        [Description("连接中")]
        Connecting = 2,

        /// <summary>已连接</summary>
        [Description("已连接")]
        Connected = 3,

        /// <summary>已断开</summary>
        [Description("已断开")]
        Disconnected = 4,

        /// <summary>故障</summary>
        [Description("故障")]
        Faulted = 5,
    }
}
