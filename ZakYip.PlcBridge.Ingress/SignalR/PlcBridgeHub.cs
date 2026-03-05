using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Logging;
using System.Collections.Concurrent;
using ZakYip.PlcBridge.Core.SignalR;
using System.Text.RegularExpressions;
using ZakYip.PlcBridge.Core.Models.SignalR;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Enums;
using ZakYip.PlcBridge.Core.Utilities;
using System.Text.Json;

namespace ZakYip.PlcBridge.Ingress.SignalR {

    /// <summary>
    /// PlcBridge Hub。
    /// </summary>
    public sealed class PlcBridgeHub : Hub<IPlcBridgeClient> {
        private readonly ILogger<PlcBridgeHub> _logger;
        private readonly IServiceProvider _serviceProvider;

        public PlcBridgeHub(
            ILogger<PlcBridgeHub> logger,
            IServiceProvider serviceProvider) {
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _serviceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        }

        /// <summary>
        /// 客户端订阅 topic（加入分组）。
        /// methodName = "Subscribe"
        /// request = topic(string)
        /// </summary>
        public async Task<InvokeAckResponse> Subscribe(string topic, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(topic)) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    ErrorMessage = "topic 为空。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            await Groups.AddToGroupAsync(Context.ConnectionId, topic, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("订阅成功。ConnectionId={ConnectionId}, Topic={Topic}", Context.ConnectionId, topic);

            return new InvokeAckResponse {
                IsSuccess = true,
                ErrorMessage = null,
                RespondedAt = DateTimeOffset.Now
            };
        }

        /// <summary>
        /// 客户端取消订阅 topic（退出分组）。
        /// methodName = "Unsubscribe"
        /// request = topic(string)
        /// </summary>
        public async Task<InvokeAckResponse> Unsubscribe(string topic, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(topic)) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    ErrorMessage = "topic 为空。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            await Groups.RemoveFromGroupAsync(Context.ConnectionId, topic, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("取消订阅成功。ConnectionId={ConnectionId}, Topic={Topic}", Context.ConnectionId, topic);

            return new InvokeAckResponse {
                IsSuccess = true,
                ErrorMessage = null,
                RespondedAt = DateTimeOffset.Now
            };
        }

        /// <summary>
        /// 客户端发布消息（服务端可选择广播到订阅者）。
        /// methodName = "Publish"
        /// request = { topic, payloadJson }
        /// </summary>
        public async Task<InvokeAckResponse> Publish(PublishRequest request, CancellationToken cancellationToken = default) {
            if (string.IsNullOrWhiteSpace(request.Topic)) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    ErrorMessage = "Topic 为空。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            var payloadJson = request.PayloadJson ?? string.Empty;

            // 默认：广播到该 Topic 分组（订阅模型）
            await Clients.Group(request.Topic).Receive(request.Topic, payloadJson, cancellationToken).ConfigureAwait(false);

            return new InvokeAckResponse {
                IsSuccess = true,
                ErrorMessage = null,
                RespondedAt = DateTimeOffset.Now
            };
        }

        public override async Task OnConnectedAsync() {
            _logger.LogInformation("连接建立。ConnectionId={ConnectionId}", Context.ConnectionId);

            var client = Clients.Client(Context.ConnectionId);
            var cancellationToken = Context.ConnectionAborted;

            var plcManager = _serviceProvider.GetService(typeof(IPlcManager)) as IPlcManager;
            var s7StatusPayload = plcManager is null
                ? nameof(PlcStatus.Disconnected)
                : plcManager.Status.ToString();

            try {
                await client.Receive(HubMethodNames.NotifyS7ConnectionStatusChanged, s7StatusPayload, cancellationToken).ConfigureAwait(false);

                var currentErpGuid = ElevatorRuntimeState.ErpGuid;
                if (!string.IsNullOrWhiteSpace(currentErpGuid)) {
                    var callTaskPayloadJson = JsonSerializer.Serialize(new { erpGuid = currentErpGuid });
                    await client.Receive(HubMethodNames.NotifyElevatorCallRequested, callTaskPayloadJson, cancellationToken).ConfigureAwait(false);
                }

                var latestProgress = ElevatorRuntimeState.LatestProgressSnapshot;
                if (latestProgress is { } progressSnapshot &&
                    !string.IsNullOrWhiteSpace(progressSnapshot.Topic) &&
                    !string.IsNullOrWhiteSpace(progressSnapshot.PayloadJson)) {
                    await client.Receive(progressSnapshot.Topic, progressSnapshot.PayloadJson, cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                _logger.LogDebug("在初始状态推送期间连接已取消。ConnectionId={ConnectionId}", Context.ConnectionId);
            }
            catch (Exception ex) {
                _logger.LogWarning(ex, "在初始状态推送期间发生异常。ConnectionId={ConnectionId}", Context.ConnectionId);
            }

            await base.OnConnectedAsync();
        }

        public override Task OnDisconnectedAsync(Exception? exception) {
            if (exception is null) {
                _logger.LogInformation("连接断开。ConnectionId={ConnectionId}", Context.ConnectionId);
            }
            else {
                _logger.LogWarning(exception, "连接异常断开。ConnectionId={ConnectionId}", Context.ConnectionId);
            }

            return base.OnDisconnectedAsync(exception);
        }

        /// <summary>
        /// 发布请求载荷（事件载荷要求：record struct 或 record class，命名以 EventArgs 结尾只适用于事件；这里是请求模型）。
        /// </summary>
        public readonly record struct PublishRequest {
            public required string Topic { get; init; }
            public string? PayloadJson { get; init; }
        }

        /// <summary>
        /// 命令处理委托：
        /// - IServiceProvider：用于解析现有服务（HttpClient/PlcManager/Options 等）
        /// - connectionId：当前连接标识
        /// - request：客户端请求体（与客户端 InvokeAsync 传入一致）
        /// </summary>
        public delegate ValueTask<InvokeAckResponse> PlcBridgeInvokeHandler(
            IServiceProvider serviceProvider,
            string connectionId,
            object? request,
            CancellationToken cancellationToken);

        private static readonly ConcurrentDictionary<string, PlcBridgeInvokeHandler> Handlers
            = new(StringComparer.OrdinalIgnoreCase);

        /// <summary>
        /// 注册命令处理器（在 Program.cs 启动期调用一次即可）。
        /// </summary>
        public static void RegisterInvokeHandler(string commandName, PlcBridgeInvokeHandler handler) {
            if (string.IsNullOrWhiteSpace(commandName)) {
                throw new ArgumentException("命令名为空。", nameof(commandName));
            }

            if (handler is null) {
                throw new ArgumentNullException(nameof(handler));
            }

            Handlers[commandName] = handler;
        }

        /// <summary>
        /// 统一入口：客户端 InvokeAsync("InvokeCommand", new { CommandName="xxx", Request=... })
        /// 服务端按 CommandName 从注册表分发，并返回 InvokeAckResponse（含 Payload）。
        /// </summary>
        public Task<InvokeAckResponse> Invoke(InvokeEnvelope? request, CancellationToken cancellationToken = default) {
            return InvokeCommand(request, cancellationToken);
        }

        /// <summary>
        /// Client -> Hub 统一命令调用入口。
        /// </summary>
        public async Task<InvokeAckResponse> InvokeCommand(InvokeEnvelope? request, CancellationToken cancellationToken = default) {
            if (request is null || string.IsNullOrWhiteSpace(request.CommandName)) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = "CommandName 为空。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            if (!Handlers.TryGetValue(request.CommandName, out var handler)) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = $"未注册命令：{request.CommandName}",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            try {
                var result = await handler(_serviceProvider, Context.ConnectionId, request.Request, cancellationToken)
                    .ConfigureAwait(false);

                // 兜底补全：handler 没填的字段由 Hub 补齐，避免返回不完整
                return result with {
                    RespondedAt = result.RespondedAt == default ? DateTimeOffset.Now : result.RespondedAt,
                    ErrorMessage = result.IsSuccess ? null : result.ErrorMessage
                };
            }
            catch (OperationCanceledException) {
                return new InvokeAckResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = "请求已取消。",
                    RespondedAt = DateTimeOffset.Now
                };
            }
            catch (Exception ex) {
                _logger.LogError(ex, "Invoke 执行失败。Command={CommandName}", request.CommandName);

                return new InvokeAckResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = ex.Message,
                    RespondedAt = DateTimeOffset.Now
                };
            }
        }
    }
}
