using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using System.Text.Json;
using System.Diagnostics;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Net.Http.Headers;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Core.Events;
using System.Text.Json.Serialization;
using ZakYip.PlcBridge.Core.Utilities;
using ZakYip.PlcBridge.Core.Models.Elevator;
using JsonSerializer = System.Text.Json.JsonSerializer;

namespace ZakYip.PlcBridge.Ingress {

    /// <summary>
    /// 基于 HttpClient 的电梯 API 客户端实现
    /// </summary>
    public sealed class HttpElevatorApiClient : IElevatorApiClient {
        private const string CallElevatorPath = "/api/wterp/erptofullinkdt";
        private const string ReportInfeedDonePath = "/api/wterp/erptodtStatus";
        private const string QueryTaskPath = "/api/wterp/DtStatusToErp";

        private static readonly JsonSerializerOptions JsonOptions = new() {
            PropertyNamingPolicy = null,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };

        private readonly HttpClient _httpClient;
        private readonly ILogger<HttpElevatorApiClient> _logger;

        public HttpElevatorApiClient(HttpClient httpClient,
            ILogger<HttpElevatorApiClient> logger) {
            _httpClient = httpClient;
            _logger = logger;

            if (_httpClient.BaseAddress is null) {
                throw new InvalidOperationException("HttpElevatorApiClient 需要配置 HttpClient.BaseAddress（示例：http://172.16.4.108:8800）");
            }
        }

        /// <summary>
        /// 异常事件（用于隔离异常，不影响上层调用链）
        /// </summary>
        public event EventHandler<ElevatorApiFaultedEventArgs>? Faulted;

        public ValueTask DisposeAsync() => ValueTask.CompletedTask;

        public async ValueTask<ElevatorApiResult> CallElevatorAsync(
            ElevatorCallRequest request,
            CancellationToken cancellationToken = default) {
            var payload = JsonSerializer.Serialize(request, JsonOptions);
            var snapshot = await PostAsync(CallElevatorPath, payload, cancellationToken).ConfigureAwait(false);

            if (!snapshot.IsTransportOk) {
                return BuildApiResultFail(snapshot, ElevatorApiErrorCode.NetworkError, snapshot.FailMessage);
            }

            var env = DeserializeEnvelope<JsonElement>(snapshot.ResponsePayload, out var parseError);
            if (env is null) {
                return BuildApiResultFail(snapshot, ElevatorApiErrorCode.Unknown, parseError ?? "电梯接口返回无法解析");
            }

            var isOk = IsEnvelopeSuccess(env);
            var errMsg = env.ErrMsg ?? (isOk ? null : $"电梯接口返回失败，ErrCode={env.ErrCode ?? "null"}");

            var elevatorApiResult = new ElevatorApiResult {
                IsSuccess = isOk,
                ErrorCode = isOk ? ElevatorApiErrorCode.None : ElevatorApiErrorCode.RemoteRejected,
                ErrorMessage = errMsg,
                DurationMs = snapshot.DurationMs,
                TraceId = snapshot.TraceId,

                RequestPayload = snapshot.RequestPayload,
                ResponsePayload = snapshot.ResponsePayload,
                Curl = snapshot.Curl
            };
            _logger.LogInformation($"呼叫电梯请求:{JsonConvert.SerializeObject(elevatorApiResult, Formatting.Indented)}");
            ElevatorRuntimeState.ErpGuid = request.ErpGuid;
            return elevatorApiResult;
        }

        public async ValueTask<ElevatorApiResult> ReportInfeedDoneAsync(
            ElevatorInfeedDoneRequest request,
            CancellationToken cancellationToken = default) {
            var payload = JsonSerializer.Serialize(request, JsonOptions);
            var snapshot = await PostAsync(ReportInfeedDonePath, payload, cancellationToken).ConfigureAwait(false);

            if (!snapshot.IsTransportOk) {
                return BuildApiResultFail(snapshot, ElevatorApiErrorCode.NetworkError, snapshot.FailMessage);
            }

            var env = DeserializeEnvelope<JsonElement>(snapshot.ResponsePayload, out var parseError);
            if (env is null) {
                return BuildApiResultFail(snapshot, ElevatorApiErrorCode.Unknown, parseError ?? "电梯接口返回无法解析");
            }

            var isOk = IsEnvelopeSuccess(env);
            var errMsg = env.ErrMsg ?? (isOk ? null : $"电梯接口返回失败，ErrCode={env.ErrCode ?? "null"}");
            var elevatorApiResult = new ElevatorApiResult {
                IsSuccess = isOk,
                ErrorCode = isOk ? ElevatorApiErrorCode.None : ElevatorApiErrorCode.RemoteRejected,
                ErrorMessage = errMsg,
                DurationMs = snapshot.DurationMs,
                TraceId = snapshot.TraceId,

                RequestPayload = snapshot.RequestPayload,
                ResponsePayload = snapshot.ResponsePayload,
                Curl = snapshot.Curl
            };
            _logger.LogInformation($"入库执行完成上报:{JsonConvert.SerializeObject(elevatorApiResult, Formatting.Indented)}");
            ElevatorRuntimeState.ClearErpGuid();
            return elevatorApiResult;
        }

        public async ValueTask<ElevatorApiResult> QueryTaskAsync(
            ElevatorTaskQueryRequest request,
            CancellationToken cancellationToken = default) {
            var payload = JsonSerializer.Serialize(request, JsonOptions);
            var snapshot = await PostAsync(QueryTaskPath, payload, cancellationToken).ConfigureAwait(false);

            if (!snapshot.IsTransportOk) {
                return BuildTaskQueryResultFail(snapshot, ElevatorApiErrorCode.NetworkError, snapshot.FailMessage);
            }

            var env = DeserializeEnvelope<ElevatorTaskResDataDto>(snapshot.ResponsePayload, out var parseError);
            if (env is null) {
                return BuildTaskQueryResultFail(snapshot, ElevatorApiErrorCode.Unknown, parseError ?? "电梯接口返回无法解析");
            }

            var isOk = IsEnvelopeSuccess(env);
            if (!isOk) {
                var errMsg = env.ErrMsg ?? $"电梯接口返回失败，ErrCode={env.ErrCode ?? "null"}";
                return BuildTaskQueryResultFail(snapshot, ElevatorApiErrorCode.RemoteRejected, errMsg);
            }

            // 注释：Status/ElevatorStatus 协议含义未提供，优先保留原始码值，避免错误映射
            var res = env.ResData;

            var elevatorApiResult = new ElevatorApiResult {
                IsSuccess = true,
                ErrorCode = ElevatorApiErrorCode.None,
                ErrorMessage = null,
                DurationMs = snapshot.DurationMs,
                TraceId = snapshot.TraceId,

                RequestPayload = snapshot.RequestPayload,
                ResponsePayload = snapshot.ResponsePayload,
                Curl = snapshot.Curl,
            };

            _logger.LogInformation($"电梯任务查询:{JsonConvert.SerializeObject(elevatorApiResult, Formatting.Indented)}");

            return elevatorApiResult;
        }

        private async ValueTask<HttpSnapshot> PostAsync(string relativePath, string requestPayload, CancellationToken cancellationToken) {
            var started = Stopwatch.GetTimestamp();
            var url = new Uri(_httpClient.BaseAddress!, relativePath);

            try {
                using var req = new HttpRequestMessage(HttpMethod.Post, url) {
                    Content = new StringContent(requestPayload, Encoding.UTF8, "application/json")
                };
                req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

                using var resp = await _httpClient.SendAsync(req, HttpCompletionOption.ResponseHeadersRead, cancellationToken)
                    .ConfigureAwait(false);

                var traceId = TryGetTraceId(resp) ?? Activity.Current?.TraceId.ToString();
                var respText = await resp.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);

                var durationMs = ElapsedMs(started);

                if (!resp.IsSuccessStatusCode) {
                    var msg = $"HTTP 状态码异常：{(int)resp.StatusCode} {resp.ReasonPhrase}";
                    RaiseFault(new InvalidOperationException(msg), msg);

                    return new HttpSnapshot {
                        IsTransportOk = false,
                        FailMessage = msg,
                        DurationMs = durationMs,
                        TraceId = traceId,

                        RequestPayload = requestPayload,
                        ResponsePayload = respText,
                        Curl = BuildCurl(url, requestPayload)
                    };
                }

                return new HttpSnapshot {
                    IsTransportOk = true,
                    FailMessage = null,
                    DurationMs = durationMs,
                    TraceId = traceId,

                    RequestPayload = requestPayload,
                    ResponsePayload = respText,
                    Curl = BuildCurl(url, requestPayload)
                };
            }
            catch (OperationCanceledException oce) {
                var durationMs = ElapsedMs(started);
                var msg = cancellationToken.IsCancellationRequested
                    ? "请求已取消"
                    : "请求超时或被中断";

                RaiseFault(oce, msg);

                return new HttpSnapshot {
                    IsTransportOk = false,
                    FailMessage = msg,
                    DurationMs = durationMs,
                    TraceId = Activity.Current?.TraceId.ToString(),

                    RequestPayload = requestPayload,
                    ResponsePayload = string.Empty,
                    Curl = BuildCurl(url, requestPayload)
                };
            }
            catch (Exception ex) {
                var durationMs = ElapsedMs(started);
                var msg = "电梯接口调用异常";

                RaiseFault(ex, msg);

                return new HttpSnapshot {
                    IsTransportOk = false,
                    FailMessage = $"{msg}：{ex.Message}",
                    DurationMs = durationMs,
                    TraceId = Activity.Current?.TraceId.ToString(),

                    RequestPayload = requestPayload,
                    ResponsePayload = string.Empty,
                    Curl = BuildCurl(url, requestPayload)
                };
            }
        }

        private ElevatorApiResult BuildApiResultFail(HttpSnapshot snapshot, ElevatorApiErrorCode code, string? message) {
            _logger.LogInformation($"电梯接口调用失败:{JsonConvert.SerializeObject(snapshot, Formatting.Indented)}");
            return new ElevatorApiResult {
                IsSuccess = false,
                ErrorCode = code,
                ErrorMessage = message ?? "电梯接口调用失败",
                DurationMs = snapshot.DurationMs,
                TraceId = snapshot.TraceId,

                RequestPayload = snapshot.RequestPayload,
                ResponsePayload = snapshot.ResponsePayload,
                Curl = snapshot.Curl
            };
        }

        private ElevatorApiResult BuildTaskQueryResultFail(HttpSnapshot snapshot, ElevatorApiErrorCode code, string? message) {
            _logger.LogError($"电梯任务查询失败:{JsonConvert.SerializeObject(snapshot, Formatting.Indented)}");

            return new ElevatorApiResult {
                IsSuccess = false,
                ErrorCode = code,
                ErrorMessage = message ?? "电梯接口调用失败",
                DurationMs = snapshot.DurationMs,
                TraceId = snapshot.TraceId,
                RequestPayload = snapshot.RequestPayload,
                ResponsePayload = snapshot.ResponsePayload,
                Curl = snapshot.Curl,
            };
        }

        private void RaiseFault(Exception ex, string message) {
            try {
                _logger.LogError(ex, "{Message}", message);
                Faulted?.Invoke(this, new ElevatorApiFaultedEventArgs {
                    Message = message,
                    Exception = ex,
                    OccurredAt = DateTimeOffset.Now
                });
            }
            catch {
                // 注释：事件订阅方异常不影响主调用链
            }
        }

        private static long ElapsedMs(long startedTicks) {
            var delta = Stopwatch.GetTimestamp() - startedTicks;
            if (delta <= 0) return 0;
            return delta * 1000L / Stopwatch.Frequency;
        }

        private static string? TryGetTraceId(HttpResponseMessage resp) {
            if (resp.Headers.TryGetValues("TraceId", out var values)) {
                foreach (var v in values) {
                    if (!string.IsNullOrWhiteSpace(v)) return v;
                }
            }
            return null;
        }

        private static ElevatorApiEnvelopeDto<TResData>? DeserializeEnvelope<TResData>(string json, out string? error) {
            error = null;

            if (string.IsNullOrWhiteSpace(json)) {
                error = "电梯接口返回为空";
                return null;
            }

            try {
                return JsonSerializer.Deserialize<ElevatorApiEnvelopeDto<TResData>>(json, JsonOptions);
            }
            catch (Exception ex) {
                error = $"电梯接口返回解析失败：{ex.Message}";
                return null;
            }
        }

        private static bool IsEnvelopeSuccess<TResData>(ElevatorApiEnvelopeDto<TResData> env) {
            if (!env.Success) return false;

            var code = env.ErrCode?.Trim();
            if (string.IsNullOrWhiteSpace(code)) return true;

            return string.Equals(code, "0", StringComparison.OrdinalIgnoreCase)
                   || string.Equals(code, "00", StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildCurl(Uri url, string jsonPayload) {
            // 注释：提供 PowerShell/类 Unix 与 CMD 两种写法，便于复现问题
            var unix = $"curl -X POST \"{url}\" -H \"Content-Type: application/json\" --data-raw '{EscapeForSingleQuotes(jsonPayload)}'";
            var cmdJson = EscapeForWindowsCmd(jsonPayload);
            var cmd = $"chcp 65001>nul & curl -X POST \"{url}\" -H \"Content-Type: application/json\" --data-raw \"{cmdJson}\"";
            return unix + Environment.NewLine + cmd;
        }

        private static string EscapeForSingleQuotes(string s) {
            // 注释：单引号字符串内遇到单引号时采用最通用的拼接写法
            return s.Replace("'", "'\"'\"'", StringComparison.Ordinal);
        }

        private static string EscapeForWindowsCmd(string s) {
            // 注释：CommandLineToArgvW 解析规则下，反斜杠+引号可用于转义
            return s.Replace("\\", "\\\\", StringComparison.Ordinal)
                    .Replace("\"", "\\\"", StringComparison.Ordinal);
        }

        private sealed record class HttpSnapshot {
            public required bool IsTransportOk { get; init; }
            public string? FailMessage { get; init; }
            public required long DurationMs { get; init; }
            public string? TraceId { get; init; }

            public required string RequestPayload { get; init; }
            public required string ResponsePayload { get; init; }
            public required string Curl { get; init; }
        }
    }
}
