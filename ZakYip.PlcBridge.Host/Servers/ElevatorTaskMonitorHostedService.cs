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
using ZakYip.PlcBridge.Core.Utilities;
using ZakYip.PlcBridge.Core.Models.Elevator;

namespace ZakYip.PlcBridge.Host.Servers {

    public class ElevatorTaskMonitorHostedService : BackgroundService {
        private readonly ILogger<ElevatorTaskMonitorHostedService> _logger;
        private readonly SafeExecutor _safeExecutor;
        private readonly IElevatorApiClient _elevatorApiClient;
        private readonly IPlcManager _plcManager;
        private readonly IOptionsMonitor<ElevatorHandshakeDbOptions> _options;

        public ElevatorTaskMonitorHostedService(ILogger<ElevatorTaskMonitorHostedService> logger,
            SafeExecutor safeExecutor,
            IElevatorApiClient elevatorApiClient,
            IPlcManager plcManager,
            IOptionsMonitor<ElevatorHandshakeDbOptions> options) {
            _logger = logger;
            _safeExecutor = safeExecutor;
            _elevatorApiClient = elevatorApiClient;
            _plcManager = plcManager;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            while (!stoppingToken.IsCancellationRequested) {
                //查询状态
                await _safeExecutor.ExecuteAsync(async () => {
                    if (string.IsNullOrEmpty(ElevatorRuntimeState.ErpGuid)) {
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
                                ElevatorRuntimeState.ClearErpGuid();
                            }
                        }
                    }
                    catch (Exception e) {
                        _logger.LogError($"电梯接口响应数据解析异常:{e}");
                    }
                }, "电梯任务查询监控");
                await Task.Delay(1000, stoppingToken);
            }

            Console.WriteLine("跳出循环");
        }
    }
}
