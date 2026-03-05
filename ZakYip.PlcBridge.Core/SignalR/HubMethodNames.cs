using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.SignalR {

    /// <summary>
    /// Hub 方法名常量。
    /// </summary>
    public static class HubMethodNames {

        /// <summary>
        /// Hub -> Client 推送方法名（必须与客户端 "Receive" 严格一致）。
        /// </summary>
        public const string Receive = "Receive";

        /// <summary>
        /// Client -> Hub：发布消息。
        /// </summary>
        public const string Publish = "Publish";

        /// <summary>
        /// Client -> Hub：加入主题分组。
        /// </summary>
        public const string Subscribe = "Subscribe";

        /// <summary>
        /// Client -> Hub：退出主题分组。
        /// </summary>
        public const string Unsubscribe = "Unsubscribe";

        // ---------------------------
        // Hub -> Client：业务事件推送（建议使用 Notify 前缀）
        // ---------------------------

        /// <summary>
        /// S7 连接状态改变通知。
        /// </summary>
        public const string NotifyS7ConnectionStatusChanged = "NotifyS7ConnectionStatusChanged";

        /// <summary>
        /// 电梯呼叫通知（服务端已发起呼叫或状态变更）。
        /// </summary>
        public const string NotifyElevatorCallRequested = "NotifyElevatorCallRequested";

        /// <summary>
        /// 电梯到位通知。
        /// </summary>
        public const string NotifyElevatorArrived = "NotifyElevatorArrived";

        /// <summary>
        /// 进料完成通知。
        /// </summary>
        public const string NotifyFeedingCompleted = "NotifyFeedingCompleted";
    }
}
