using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Core.SignalR;

namespace ZakYip.PlcBridge.Ingress.SignalR {

    /// <summary>
    /// SignalR 广播器实现。
    /// </summary>
    public sealed class PlcBridgeMessageBroadcaster : IPlcBridgeMessageBroadcaster {
        private readonly IHubContext<PlcBridgeHub, IPlcBridgeClient> _hubContext;
        private readonly ILogger<PlcBridgeMessageBroadcaster> _logger;

        public PlcBridgeMessageBroadcaster(
            IHubContext<PlcBridgeHub, IPlcBridgeClient> hubContext,
            ILogger<PlcBridgeMessageBroadcaster> logger) {
            _hubContext = hubContext ?? throw new ArgumentNullException(nameof(hubContext));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async ValueTask BroadcastAsync(string topic, string payloadJson, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(topic)) {
                throw new ArgumentException("topic 为空。", nameof(topic));
            }

            payloadJson ??= string.Empty;

            try {
                await _hubContext.Clients.All.Receive(topic, payloadJson, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "SignalR 广播失败。Topic={Topic}", topic);
            }
        }

        public async ValueTask BroadcastToTopicAsync(string topic, string payloadJson, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(topic)) {
                throw new ArgumentException("topic 为空。", nameof(topic));
            }

            payloadJson ??= string.Empty;

            try {
                // 主题即 GroupName：订阅者会被加入同名 Group
                await _hubContext.Clients.Group(topic).Receive(topic, payloadJson, cancellationToken).ConfigureAwait(false);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "SignalR 分组广播失败。Topic={Topic}", topic);
            }
        }
    }
}
