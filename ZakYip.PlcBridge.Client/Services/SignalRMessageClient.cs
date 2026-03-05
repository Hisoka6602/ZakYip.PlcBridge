using System;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Client.Enums;
using ZakYip.PlcBridge.Client.Events;
using ZakYip.PlcBridge.Client.Options;
using Microsoft.AspNetCore.SignalR.Client;

namespace ZakYip.PlcBridge.Client.Services {

    /// <summary>
    /// SignalR 消息客户端实现。
    /// </summary>
    public sealed class SignalRMessageClient : ISignalRMessageClient {
        private static readonly object?[] EmptyArgs = Array.Empty<object?>();

        private readonly ILogger<SignalRMessageClient> _logger;
        private readonly SignalRConnectionOptions _options;
        private readonly IRetryPolicy? _retryPolicy;

        private HubConnection? _connection;

        private int _isDisposed;
        private int _isInitialized;

        private volatile ConnectionStatus _connectionStatus = ConnectionStatus.Disconnected;
        private volatile string? _connectionId;

        /// <summary>
        /// 是否启用自动重连。
        /// </summary>
        public bool IsAutoReconnectEnabled => _options.IsAutoReconnectEnabled;

        /// <summary>
        /// 当前连接状态。
        /// </summary>
        public ConnectionStatus ConnectionStatus => _connectionStatus;

        /// <summary>
        /// 当前连接参数。
        /// </summary>
        public SignalRConnectionOptions ConnectionOptions => _options;

        /// <summary>
        /// 当前连接标识（SignalR ConnectionId）。
        /// </summary>
        public string? ConnectionId => _connectionId;

        /// <summary>
        /// 连接状态变更事件。
        /// </summary>
        public event EventHandler<SignalRConnectionStatusChangedEventArgs>? ConnectionStatusChanged;

        /// <summary>
        /// 接收消息事件。
        /// </summary>
        public event EventHandler<SignalRMessageReceivedEventArgs>? MessageReceived;

        /// <summary>
        /// 构造函数。
        /// </summary>
        public SignalRMessageClient(
            SignalRConnectionOptions options,
            ILogger<SignalRMessageClient> logger) {
            _options = options ?? throw new ArgumentNullException(nameof(options));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));

            _retryPolicy = _options.IsAutoReconnectEnabled
                ? new FixedDelayRetryPolicy(Array.Empty<TimeSpan>())
                : null;
        }

        /// <summary>
        /// 连接（支持自动重连）。
        /// </summary>
        public async ValueTask ConnectAsync(CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            EnsureInitialized();

            var conn = _connection!;
            var previous = _connectionStatus;

            if (previous == ConnectionStatus.Connected) {
                return;
            }

            SetStatus(ConnectionStatus.Connecting, reason: null);

            try {
                _logger.LogInformation("SignalR 开始连接：{HubUrl}", _options.HubUrl);
                await conn.StartAsync(cancellationToken).ConfigureAwait(false);

                _connectionId = conn.ConnectionId;
                SetStatus(ConnectionStatus.Connected, reason: null);
                _logger.LogInformation("SignalR 连接成功。ConnectionId={ConnectionId}", _connectionId);
            }
            catch (OperationCanceledException) {
                // 取消属于预期流转
                SetStatus(ConnectionStatus.Disconnected, reason: "连接已取消。");
                throw;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "SignalR 连接失败。");
                SetStatus(ConnectionStatus.Faulted, reason: ex.Message);
            }
        }

        /// <summary>
        /// 断开连接。
        /// </summary>
        public async ValueTask DisconnectAsync(CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            var conn = _connection;
            if (conn is null) {
                SetStatus(ConnectionStatus.Disconnected, reason: null);
                return;
            }

            try {
                await conn.StopAsync(cancellationToken).ConfigureAwait(false);
                _logger.LogInformation("SignalR 已断开。");
            }
            catch (OperationCanceledException) {
                throw;
            }
            catch (Exception ex) {
                // 断开失败不抛出，避免影响调用链
                _logger.LogError(ex, "SignalR 断开失败。");
            }
            finally {
                _connectionId = null;
                SetStatus(ConnectionStatus.Disconnected, reason: null);
            }
        }

        /// <summary>
        /// 推送订阅（需要响应）。
        /// </summary>
        public async ValueTask<SignalRInvokeResponse> InvokeAsync(
      string methodName,
      object? request,
      CancellationToken cancellationToken = default) {
            ThrowIfDisposed();

            if (string.IsNullOrWhiteSpace(methodName)) {
                return new SignalRInvokeResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = "Hub 方法名为空。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            var conn = _connection;
            if (conn is null || _connectionStatus != ConnectionStatus.Connected) {
                return new SignalRInvokeResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = "SignalR 未连接。",
                    RespondedAt = DateTimeOffset.Now
                };
            }

            try {
                _logger.LogInformation("SignalR Invoke 请求。Method={MethodName}, Request={RequestJson}",
                    methodName, SerializeAsJson(request));
                object? result;

                if (request is null) {
                    // 0 参数：避免分配
                    result = await conn.InvokeCoreAsync<object?>(methodName, EmptyArgs, cancellationToken).ConfigureAwait(false);
                }
                else {
                    // 1 参数：InvokeCoreAsync 需要 object?[]，此处不可避免分配
                    result = await conn.InvokeCoreAsync<object?>(methodName, new object?[] { request }, cancellationToken).ConfigureAwait(false);
                }

                // ✅ 关键：若服务端返回 InvokeAckResponse 形状，则以服务端 IsSuccess 为准
                if (TryExtractInvokeAck(result, out var serverSuccess, out var serverPayload, out var serverErrorMessage, out var serverRespondedAt)) {
                    _logger.LogInformation("SignalR Invoke 响应。Method={MethodName}, IsSuccess={IsSuccess}, Error={ErrorMessage}, Payload={PayloadJson}",
                        methodName, serverSuccess, serverErrorMessage, SerializeAsJson(serverPayload));
                    return new SignalRInvokeResponse {
                        IsSuccess = serverSuccess,
                        Payload = serverPayload,
                        ErrorMessage = serverSuccess ? null : (serverErrorMessage ?? "服务端返回失败。"),
                        RespondedAt = serverRespondedAt ?? DateTimeOffset.Now
                    };
                }

                // 兜底：若服务端没有返回标准应答结构，则认为调用成功，Payload=原始返回
                _logger.LogInformation("SignalR Invoke 响应(非标准结构)。Method={MethodName}, Payload={PayloadJson}",
                    methodName, SerializeAsJson(result));
                return new SignalRInvokeResponse {
                    IsSuccess = true,
                    Payload = result,
                    ErrorMessage = null,
                    RespondedAt = DateTimeOffset.Now
                };
            }
            catch (OperationCanceledException) {
                _logger.LogWarning("SignalR Invoke 已取消。Method={MethodName}", methodName);
                return new SignalRInvokeResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = "请求已取消。",
                    RespondedAt = DateTimeOffset.Now
                };
            }
            catch (Exception ex) {
                _logger.LogError(ex, "SignalR Invoke 失败。Method={MethodName}", methodName);
                SetStatus(ConnectionStatus.Faulted, reason: ex.Message);

                return new SignalRInvokeResponse {
                    IsSuccess = false,
                    Payload = null,
                    ErrorMessage = ex.Message,
                    RespondedAt = DateTimeOffset.Now
                };
            }
        }

        public void Dispose() {
            if (Interlocked.Exchange(ref _isDisposed, 1) != 0) {
                return;
            }

            var conn = _connection;
            _connection = null;

            if (conn is null) {
                return;
            }

            try {
                conn.DisposeAsync().AsTask().GetAwaiter().GetResult();
            }
            catch (Exception ex) {
                // Dispose 不允许抛出
                _logger.LogError(ex, "SignalR 释放失败。");
            }
        }

        private void EnsureInitialized() {
            if (Interlocked.Exchange(ref _isInitialized, 1) != 0) {
                return;
            }

            var builder = new HubConnectionBuilder();

            builder.WithUrl(_options.HubUrl, httpOptions => {
                httpOptions.Transports = Microsoft.AspNetCore.Http.Connections.HttpTransportType.LongPolling;
                // AccessToken 由 Options 提供时再配置，避免每次连接分配
                if (!string.IsNullOrWhiteSpace(_options.AccessToken)) {
                    httpOptions.AccessTokenProvider = static () => Task.FromResult<string?>(null);
                    var token = _options.AccessToken;
                    httpOptions.AccessTokenProvider = () => Task.FromResult<string?>(token);
                }

                if (_options.Headers is { Count: > 0 }) {
                    foreach (var kv in _options.Headers) {
                        httpOptions.Headers[kv.Key] = kv.Value;
                    }
                }
            });

            if (_retryPolicy is not null) {
                builder.WithAutomaticReconnect(_retryPolicy);
            }

            // 说明：日志级别按需调整，默认不加额外开销
            // builder.ConfigureLogging(logging => logging.SetMinimumLevel(LogLevel.Warning));

            var conn = builder.Build();

            // 连接状态事件绑定
            conn.Reconnecting += ex => {
                _connectionId = null;
                SetStatus(ConnectionStatus.Connecting, ex?.Message);
                _logger.LogWarning(ex, "SignalR 重连中。");
                return Task.CompletedTask;
            };

            conn.Reconnected += connectionId => {
                _connectionId = connectionId;
                SetStatus(ConnectionStatus.Connected, reason: null);
                _logger.LogInformation("SignalR 重连成功。ConnectionId={ConnectionId}", connectionId);
                return Task.CompletedTask;
            };

            conn.Closed += ex => {
                _connectionId = null;

                if (ex is null) {
                    SetStatus(ConnectionStatus.Disconnected, reason: null);
                    _logger.LogInformation("SignalR 连接已关闭。");
                }
                else {
                    SetStatus(ConnectionStatus.Faulted, ex.Message);
                    _logger.LogError(ex, "SignalR 连接异常关闭。");
                }

                return Task.CompletedTask;
            };

            // 接收消息约定：
            // Hub -> Client: Receive(string topic, string payloadJson)
            // 若服务端采用其它方法名，可在 Options 增加 ReceiveMethodName 后替换此常量
            conn.On<string, string>("Receive", RaiseMessageReceived);

            _connection = conn;
        }

        private void SetStatus(ConnectionStatus status, string? reason) {
            var previous = _connectionStatus;
            if (previous == status) {
                return;
            }

            _connectionStatus = status;

            var handler = ConnectionStatusChanged;
            if (handler is null) {
                return;
            }

            try {
                handler.Invoke(this, new SignalRConnectionStatusChangedEventArgs {
                    PreviousStatus = previous,
                    CurrentStatus = status,
                    ConnectionId = _connectionId,
                    Reason = reason,
                    OccurredAt = DateTimeOffset.Now
                });
            }
            catch (Exception ex) {
                // 事件回调异常不得影响主流程
                _logger.LogError(ex, "连接状态变更事件回调失败。");
            }
        }

        private void RaiseMessageReceived(string topic, object payload) {
            var handler = MessageReceived;
            if (handler is null) {
                return;
            }

            try {
                _logger.LogInformation("SignalR 接收消息。Topic={Topic}, Payload={PayloadJson}",
                    topic, SerializeAsJson(payload));
                handler.Invoke(this, new SignalRMessageReceivedEventArgs {
                    Topic = topic,
                    Payload = payload,
                    ReceivedAt = DateTimeOffset.Now
                });
            }
            catch (Exception ex) {
                // 事件回调异常不得影响主流程
                _logger.LogError(ex, "接收消息事件回调失败。");
            }
        }

        private void ThrowIfDisposed() {
            if (Volatile.Read(ref _isDisposed) != 0) {
                throw new ObjectDisposedException(nameof(SignalRMessageClient), "对象已释放。");
            }
        }

        private static bool TryExtractInvokeAck(
    object? result,
    out bool isSuccess,
    out object? payload,
    out string? errorMessage,
    out DateTimeOffset? respondedAt) {
            isSuccess = false;
            payload = null;
            errorMessage = null;
            respondedAt = null;

            if (result is null) {
                return false;
            }

            // 形态 1：JsonElement
            if (result is System.Text.Json.JsonElement je && je.ValueKind == System.Text.Json.JsonValueKind.Object) {
                if (!je.TryGetProperty("IsSuccess", out var isSuccessProp) || isSuccessProp.ValueKind is not System.Text.Json.JsonValueKind.True and not System.Text.Json.JsonValueKind.False) {
                    return false;
                }

                isSuccess = isSuccessProp.GetBoolean();

                if (je.TryGetProperty("Payload", out var payloadProp)) {
                    // 保留 JsonElement，交由上层决定是否反序列化
                    payload = payloadProp;
                }

                if (je.TryGetProperty("ErrorMessage", out var errProp) && errProp.ValueKind == System.Text.Json.JsonValueKind.String) {
                    errorMessage = errProp.GetString();
                }

                if (je.TryGetProperty("RespondedAt", out var timeProp) && timeProp.ValueKind == System.Text.Json.JsonValueKind.String) {
                    if (DateTimeOffset.TryParse(timeProp.GetString(), out var dto)) {
                        respondedAt = dto;
                    }
                }

                return true;
            }

            // 形态 2：普通对象（反射读取属性）
            var type = result.GetType();

            var isSuccessProperty = type.GetProperty("IsSuccess");
            if (isSuccessProperty?.PropertyType != typeof(bool)) {
                return false;
            }

            isSuccess = (bool)isSuccessProperty.GetValue(result)!;

            var payloadProperty = type.GetProperty("Payload");
            payload = payloadProperty?.GetValue(result);

            var errorProperty = type.GetProperty("ErrorMessage");
            if (errorProperty?.PropertyType == typeof(string)) {
                errorMessage = (string?)errorProperty.GetValue(result);
            }

            var respondedAtProperty = type.GetProperty("RespondedAt");
            if (respondedAtProperty?.PropertyType == typeof(DateTimeOffset)) {
                respondedAt = (DateTimeOffset?)respondedAtProperty.GetValue(result);
            }

            return true;
        }

        private static string SerializeAsJson(object? value) {
            try {
                if (value is null) {
                    return "null";
                }

                return JsonSerializer.Serialize(value);
            }
            catch {
                return value?.ToString() ?? "null";
            }
        }

        /// <summary>
        /// 固定延迟重连策略（无限重连，最大退避时间 10 秒）。
        /// </summary>
        private sealed class FixedDelayRetryPolicy : IRetryPolicy {
            private static readonly TimeSpan MaxDelay = TimeSpan.FromSeconds(10);

            private readonly TimeSpan[] _delays;

            public FixedDelayRetryPolicy(IReadOnlyList<TimeSpan>? delays) {
                // 为空时采用默认退避序列：0s, 2s, 5s, 10s
                if (delays is null || delays.Count <= 0) {
                    _delays = [TimeSpan.Zero, TimeSpan.FromSeconds(2), TimeSpan.FromSeconds(5), MaxDelay];
                    return;
                }

                // 将外部传入延迟拷贝为数组，并对每项做上限裁剪（<= 10s）
                _delays = new TimeSpan[delays.Count];

                for (var i = 0; i < _delays.Length; i++) {
                    var d = delays[i];

                    // 负数归零，超上限封顶到 10 秒
                    if (d < TimeSpan.Zero) {
                        d = TimeSpan.Zero;
                    }
                    else if (d > MaxDelay) {
                        d = MaxDelay;
                    }

                    _delays[i] = d;
                }

                // 兜底：若配置全为 0 或被裁剪导致数组无有效值，确保最后至少是 10 秒封顶
                if (_delays.Length == 1 && _delays[0] == TimeSpan.Zero) {
                    _delays = [TimeSpan.Zero, MaxDelay];
                }
            }

            public TimeSpan? NextRetryDelay(RetryContext retryContext) {
                var index = retryContext.PreviousRetryCount;

                // 无限重连：永不返回 null
                // 超出序列长度后，保持最后一个值（最大退避 10 秒）
                if ((uint)index < (uint)_delays.Length) {
                    return _delays[index];
                }

                return _delays[^1];
            }
        }
    }
}
