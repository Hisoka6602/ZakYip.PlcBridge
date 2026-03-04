using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Enums;
using ZakYip.PlcBridge.Client.Events;
using ZakYip.PlcBridge.Client.Options;

namespace ZakYip.PlcBridge.Client.Services {

    /// <summary>
    /// SignalR 消息客户端契约。
    /// </summary>
    public interface ISignalRMessageClient : IDisposable {
        //-----------字段（属性）-----------

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        bool IsAutoReconnectEnabled { get; }

        /// <summary>
        /// 当前连接状态。
        /// </summary>
        ConnectionStatus ConnectionStatus { get; }

        /// <summary>
        /// 当前连接参数。
        /// </summary>
        SignalRConnectionOptions ConnectionOptions { get; }

        /// <summary>
        /// 当前连接标识（SignalR ConnectionId）。
        /// </summary>
        string? ConnectionId { get; }

        //-----------事件-----------

        /// <summary>
        /// 连接状态变更事件。
        /// </summary>
        event EventHandler<SignalRConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        /// <summary>
        /// 接收消息事件。
        /// </summary>
        event EventHandler<SignalRMessageReceivedEventArgs>? MessageReceived;

        //-----------方法-----------

        /// <summary>
        /// 连接（支持自动重连）。
        /// </summary>
        ValueTask ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 断开连接。
        /// </summary>
        ValueTask DisconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 推送订阅（需要响应）。
        /// 约定：methodName 为 Hub 方法名，request 为请求载荷，返回值为响应载荷。
        /// </summary>
        ValueTask<SignalRInvokeResponse> InvokeAsync(
            string methodName,
            object? request,
            CancellationToken cancellationToken = default);
    }
}
