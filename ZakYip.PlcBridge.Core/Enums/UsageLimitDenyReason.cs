using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// 使用限制拒绝原因
    /// </summary>
    public enum UsageLimitDenyReason {

        /// <summary>无</summary>
        [Description("无")]
        None = 0,

        /// <summary>已超过累计可用时长</summary>
        [Description("已超过累计可用时长")]
        QuotaExceeded = 1,

        /// <summary>状态损坏或疑似被篡改</summary>
        [Description("状态损坏或疑似被篡改")]
        StateTampered = 2,

        /// <summary>状态存储不可用</summary>
        [Description("状态存储不可用")]
        StoreUnavailable = 3
    }
}
