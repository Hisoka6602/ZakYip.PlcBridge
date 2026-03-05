using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Models;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.SignalR;
using ZakYip.PlcBridge.Core.Utilities;
using ZakYip.PlcBridge.Ingress.SignalR;
using ZakYip.PlcBridge.Core.Models.SignalR;
using ZakYip.PlcBridge.Core.Models.Elevator;

namespace ZakYip.PlcBridge.Host.Servers {

    public class ElevatorTaskMonitorHostedService : BackgroundService {
        private readonly ILogger<ElevatorTaskMonitorHostedService> _logger;
        private readonly SafeExecutor _safeExecutor;
        private readonly IElevatorApiClient _elevatorApiClient;
        private readonly IPlcManager _plcManager;
        private readonly IPlcBridgeMessageBroadcaster _plcBridgeMessageBroadcaster;
        private readonly IOptionsMonitor<ElevatorHandshakeDbOptions> _options;
        private int _queryExecutableSignalBitOffset = 0;
        private int _queryExecutableSignalByteOffset = 0;
        private bool _isQueryExecutable = false;
        private static int _invokeHandlersRegistered;

        public ElevatorTaskMonitorHostedService(ILogger<ElevatorTaskMonitorHostedService> logger,
            SafeExecutor safeExecutor,
            IElevatorApiClient elevatorApiClient,
            IPlcManager plcManager,
            IPlcBridgeMessageBroadcaster plcBridgeMessageBroadcaster,
            IOptionsMonitor<ElevatorHandshakeDbOptions> options) {
            _logger = logger;
            _safeExecutor = safeExecutor;
            _elevatorApiClient = elevatorApiClient;
            _plcManager = plcManager;
            _plcBridgeMessageBroadcaster = plcBridgeMessageBroadcaster;
            _options = options;

            _plcManager.DbBoolsChanged += (sender, args) => {
                var changes = args.Changes.Where(w =>
                    w.DbNumber.Equals(_options.CurrentValue.DbNumber)).ToList();
                if (changes.Count < 1) return;
                //可查询执行信号变化

                var plcDbBoolChange = changes.FirstOrDefault(a => a.ByteOffset == _queryExecutableSignalByteOffset &&
                                                                  a.BitOffset == _queryExecutableSignalBitOffset);

                _isQueryExecutable = plcDbBoolChange.NewState == PlcIoSignalState.High;
            };
            RegisterPlcBridgeInvokeHandlersOnce();
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var queryExecutableSignal = _options.CurrentValue.Fields.FirstOrDefault(f => f is { Role: ElevatorHandshakeFieldRole.QueryExecutableSignal, ValueType: PlcDbValueType.Bool });
            _queryExecutableSignalByteOffset = queryExecutableSignal?.ByteOffset ?? 0;
            _queryExecutableSignalBitOffset = queryExecutableSignal?.BitOffset ?? 0;
            while (!stoppingToken.IsCancellationRequested) {
                //查询状态
                await _safeExecutor.ExecuteAsync(async () => {
                    if (string.IsNullOrEmpty(ElevatorRuntimeState.ErpGuid) || !_isQueryExecutable) {
                        return;
                    }
                    ElevatorApiResult elevatorApiResult;
                    try {
                        elevatorApiResult = await _elevatorApiClient.QueryTaskAsync(new ElevatorTaskQueryRequest {
                            ErpGuid = ElevatorRuntimeState.ErpGuid,
                            Status = 2
                        }, stoppingToken);
                    }
                    catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) {
                        _logger.LogError($"电梯接口响应超时");
                        return;
                    }
                    if (!elevatorApiResult.IsSuccess || string.IsNullOrEmpty(elevatorApiResult.ResponsePayload)) {
                        return;
                    }

                    try {
                        var envelopeDto = JsonConvert.DeserializeObject<ElevatorApiEnvelopeDto<ElevatorTaskResDataDto>>(
                            elevatorApiResult.ResponsePayload);
                        var statusText = envelopeDto?.ResData?.Status;
                        if (int.TryParse(statusText, out var status) && status == 2) {
                            //电梯到位
                            var elevatorArrivedSignalOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.ElevatorArrivedSignal);
                            if (elevatorArrivedSignalOptions is not null) {
                                var writeItems = new List<PlcDbBoolWriteItem>
                                {
                                    new()
                                    {
                                        DbNumber = _options.CurrentValue.DbNumber,
                                        ByteOffset = elevatorArrivedSignalOptions.ByteOffset,
                                        BitOffset = elevatorArrivedSignalOptions.BitOffset ?? 0,
                                        State = PlcIoSignalState.High
                                    }
                                };
                                await _plcManager.WriteDbBoolsAsync(writeItems, stoppingToken);
                                _logger.LogInformation($"更改电梯到位信号为高");
                                var payloadJson = JsonConvert.SerializeObject(elevatorApiResult);
                                await _plcBridgeMessageBroadcaster.BroadcastAsync(HubMethodNames.NotifyElevatorArrived,
                                     payloadJson, stoppingToken);
                                ElevatorRuntimeState.UpdateProgress(HubMethodNames.NotifyElevatorArrived, payloadJson);
                            }
                        }
                    }
                    catch (Exception e) {
                        _logger.LogError($"电梯接口响应数据解析异常:{e}");
                    }
                }, "电梯任务查询监控");
                await Task.Delay(10000, stoppingToken);
            }

            Console.WriteLine("跳出循环");
        }

        private void RegisterPlcBridgeInvokeHandlersOnce() {
            if (Interlocked.Exchange(ref _invokeHandlersRegistered, 1) == 1) {
                return;
            }

            // 示例：呼叫电梯（命令名建议放常量里，例如 HubMethodNames.CallElevator）
            PlcBridgeHub.RegisterInvokeHandler("PushProductionOrder", async (sp, connectionId, req, ct) => {
                try {
                    if (req is null) {
                        _logger.LogWarning("PushProductionOrder 请求为空。ConnectionId={ConnectionId}", connectionId);
                        return new InvokeAckResponse {
                            IsSuccess = false,
                            Payload = null,
                            ErrorMessage = "请求内容为空。",
                            RespondedAt = DateTimeOffset.Now
                        };
                    }

                    var requestJson = req.ToString();
                    var productionOrderPushRequest = JsonConvert.DeserializeObject<ProductionOrderPushRequest>(requestJson!);
                    if (productionOrderPushRequest is null) {
                        _logger.LogWarning("PushProductionOrder 请求反序列化失败。ConnectionId={ConnectionId}, Request={RequestJson}",
                            connectionId, requestJson);
                        return new InvokeAckResponse {
                            IsSuccess = false,
                            Payload = null,
                            ErrorMessage = "请求格式错误。",
                            RespondedAt = DateTimeOffset.Now
                        };
                    }

                    var api = sp.GetRequiredService<IElevatorApiClient>();

                    var pushProductionOrderAsync = await api.PushProductionOrderAsync(productionOrderPushRequest, ct);

                    return new InvokeAckResponse {
                        IsSuccess = pushProductionOrderAsync.IsSuccess,
                        Payload = new { IsOk = pushProductionOrderAsync.IsSuccess, ConnectionId = connectionId },
                        ErrorMessage = pushProductionOrderAsync.IsSuccess ? null : pushProductionOrderAsync.ErrorMessage,
                        RespondedAt = DateTimeOffset.Now
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
                    _logger.LogError(ex, "PushProductionOrder 执行异常。ConnectionId={ConnectionId}", connectionId);
                    return new InvokeAckResponse {
                        IsSuccess = false,
                        Payload = null,
                        ErrorMessage = ex.Message,
                        RespondedAt = DateTimeOffset.Now
                    };
                }
            });
        }
    }
}
