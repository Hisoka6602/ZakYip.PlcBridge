using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Resources.Attributes;

namespace ZakYip.PlcBridge.Client.Enums {

    /// <summary>
    /// 连接状态。
    /// </summary>
    public enum ConnectionStatus {

        /// <summary>
        /// 未连接。
        /// </summary>
        [Description("未连接"), WinIconColor("#A9A9A9")]
        Disconnected = 0,

        /// <summary>
        /// 连接中。
        /// </summary>
        [Description("连接中..."), WinIconColor("#FF8C00")]
        Connecting = 1,

        /// <summary>
        /// 已连接。
        /// </summary>
        [Description("已连接"), WinIconColor("#32CD32")]
        Connected = 2,

        /// <summary>
        /// 故障。
        /// </summary>
        [Description("故障"), WinIconColor("#FF4500")]
        Faulted = 3
    }
}
