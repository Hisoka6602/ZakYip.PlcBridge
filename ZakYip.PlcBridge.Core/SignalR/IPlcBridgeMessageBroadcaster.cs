using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.SignalR {

    /// <summary>
    /// 向 SignalR 客户端广播消息的应用服务抽象。
    /// </summary>
    public interface IPlcBridgeMessageBroadcaster {

        /// <summary>
        /// 广播到所有客户端。
        /// </summary>
        ValueTask BroadcastAsync(string topic, string payloadJson, CancellationToken cancellationToken = default);

        /// <summary>
        /// 广播到某个主题分组（订阅模型）。
        /// </summary>
        ValueTask BroadcastToTopicAsync(string topic, string payloadJson, CancellationToken cancellationToken = default);
    }
}
