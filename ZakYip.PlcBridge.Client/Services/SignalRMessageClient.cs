using System;
using System.Buffers;
using System.Threading;
using System.Diagnostics;
using System.Threading.Tasks;
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
                await conn.StartAsync(cancellationToken).ConfigureAwait(false);

                _connectionId = conn.ConnectionId;
                SetStatus(ConnectionStatus.Connected, reason: null);
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
                object? result;
                if (request is null) {
                    // 0 参数：避免分配
                    result = await conn.InvokeCoreAsync<object?>(methodName, EmptyArgs, cancellationToken).ConfigureAwait(false);
                }
                else {
                    // 1 参数：使用 ArrayPool 降低 GC 压力
                    var pool = ArrayPool<object?>.Shared;
                    var args = pool.Rent(1);
                    args[0] = request;

                    try {
                        result = await conn.InvokeCoreAsync<object?>(methodName, args.AsSpan(0, 1).ToArray(), cancellationToken).ConfigureAwait(false);
                        // 说明：InvokeCoreAsync 需要 object?[]；此处 ToArray 会分配。
                        // 若追求极致性能，可改用自定义 HubProtocol 或封装 HubConnection（需要更深层改造）。
                    }
                    finally {
                        args[0] = null;
                        pool.Return(args, clearArray: false);
                    }
                }

                return new SignalRInvokeResponse {
                    IsSuccess = true,
                    Payload = result,
                    ErrorMessage = null,
                    RespondedAt = DateTimeOffset.Now
                };
            }
            catch (OperationCanceledException) {
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
                return Task.CompletedTask;
            };

            conn.Reconnected += connectionId => {
                _connectionId = connectionId;
                SetStatus(ConnectionStatus.Connected, reason: null);
                return Task.CompletedTask;
            };

            conn.Closed += ex => {
                _connectionId = null;

                if (ex is null) {
                    SetStatus(ConnectionStatus.Disconnected, reason: null);
                }
                else {
                    SetStatus(ConnectionStatus.Faulted, ex.Message);
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
