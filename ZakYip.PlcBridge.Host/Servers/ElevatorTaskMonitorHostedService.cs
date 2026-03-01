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
        private int _queryExecutableSignalBitOffset = 0;
        private int _queryExecutableSignalByteOffset = 0;
        private bool _isQueryExecutable = false;

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

            _plcManager.DbBoolsChanged += (sender, args) => {
                var changes = args.Changes.Where(w =>
                    w.DbNumber.Equals(_options.CurrentValue.DbNumber)).ToList();
                if (changes.Count < 1) return;
                //可查询执行信号变化

                var plcDbBoolChange = changes.FirstOrDefault(a => a.ByteOffset == _queryExecutableSignalByteOffset &&
                                                                  a.BitOffset == _queryExecutableSignalBitOffset);

                _isQueryExecutable = plcDbBoolChange.NewState == PlcIoSignalState.High;
            };
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
                                ElevatorRuntimeState.ClearErpGuid();
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
    }
}
