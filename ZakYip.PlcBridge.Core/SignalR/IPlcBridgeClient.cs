using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.SignalR {

    /// <summary>
    /// Hub -> Client 的回调契约（与客户端 conn.On("Receive") 对齐）。
    /// </summary>
    public interface IPlcBridgeClient {

        /// <summary>
        /// 服务端推送消息。
        /// </summary>
        Task Receive(string topic, string payloadJson, CancellationToken cancellationToken = default);
    }
}
