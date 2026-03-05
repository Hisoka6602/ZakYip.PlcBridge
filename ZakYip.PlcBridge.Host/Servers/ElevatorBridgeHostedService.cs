using System;
using System.Linq;
using System.Text;
using Newtonsoft.Json;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using ZakYip.PlcBridge.Drivers;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Models;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.SignalR;
using ZakYip.PlcBridge.Core.Utilities;
using ZakYip.PlcBridge.Core.Models.Elevator;

namespace ZakYip.PlcBridge.Host.Servers {

    public class ElevatorBridgeHostedService : BackgroundService {
        private readonly ILogger<ElevatorBridgeHostedService> _logger;
        private readonly SafeExecutor _safeExecutor;
        private readonly IPlcManager _plcManager;
        private readonly IElevatorApiClient _elevatorApiClient;
        private readonly IPlcBridgeMessageBroadcaster _plcBridgeMessageBroadcaster;
        private readonly IOptionsMonitor<ElevatorHandshakeDbOptions> _options;

        /// <summary>
        /// 电梯呼叫信号字节偏移
        /// </summary>
        private int _callElevatorSignalByteOffset = 0;

        /// <summary>
        /// 电梯呼叫信号位偏移
        /// </summary>
        private int _callElevatorSignalBitOffset = 0;

        /// <summary>
        /// 进料完成信号字节偏移
        /// </summary>
        private int _infeedDoneSignalByteOffset = 0;

        /// <summary>
        /// 进料完成信号位偏移
        /// </summary>
        private int _infeedDoneSignalBitOffset = 0;

        public ElevatorBridgeHostedService(ILogger<ElevatorBridgeHostedService> logger,
            SafeExecutor safeExecutor, IPlcManager plcManager,
            IElevatorApiClient elevatorApiClient, IPlcBridgeMessageBroadcaster plcBridgeMessageBroadcaster,
            IOptionsMonitor<ElevatorHandshakeDbOptions> options) {
            _logger = logger;
            _safeExecutor = safeExecutor;
            _plcManager = plcManager;
            _elevatorApiClient = elevatorApiClient;
            _plcBridgeMessageBroadcaster = plcBridgeMessageBroadcaster;
            _options = options;

            //DB更改事件
            _plcManager.DbBoolsChanged += (sender, args) => {
                _ = _safeExecutor.ExecuteAsync(async () => {
                    await Task.Yield();
                    _logger.LogInformation($"检测到信号变更,{JsonConvert.SerializeObject(args, Formatting.Indented)}");
                    var changes = args.Changes.Where(w =>
                        w.DbNumber.Equals(_options.CurrentValue.DbNumber)).ToList();
                    if (changes.Count < 1) return;
                    //呼叫电梯信号 CallElevatorSignal
                    var hasCallElevatorSignal = changes.Any(a => a.ByteOffset == _callElevatorSignalByteOffset &&
                                                                 a.BitOffset == _callElevatorSignalBitOffset &&
                                                                 a.NewState == PlcIoSignalState.High);
                    if (hasCallElevatorSignal) {
                        //检测到呼叫电梯信号，执行呼叫电梯逻辑
                        _logger.LogInformation($"检测到呼叫电梯信号，执行呼叫电梯逻辑");

                        //读数据
                        var itemCode = string.Empty;
                        var batchNo = string.Empty;
                        var boxQuantity = 0;
                        var callElevatorLayer = 0;
                        var callElevatorUseLayer = 0;
                        var uniqueGuid = DateTimeOffset.Now.ToUnixTimeMilliseconds();
                        //物料编号
                        var itemCodeOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.ItemCode);
                        if (itemCodeOptions is not null) {
                            itemCode = await _plcManager.ReadStringAsync(new PlcStringAddress {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = itemCodeOptions.ByteOffset,
                                Kind = PlcStringKind.FixedAscii,
                                MaxLength = itemCodeOptions.MaxStringLength ?? 0
                            });
                        }
                        else {
                            _logger.LogError($"未配置物料编号地址");
                        }

                        //批次
                        var batchNoOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.BatchNo);
                        if (batchNoOptions is not null) {
                            batchNo = await _plcManager.ReadStringAsync(new PlcStringAddress {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = batchNoOptions.ByteOffset,
                                Kind = PlcStringKind.FixedAscii,
                                MaxLength = batchNoOptions.MaxStringLength ?? 0
                            });
                        }
                        else {
                            _logger.LogError($"未配置批次地址");
                        }
                        //箱子数量
                        var boxQuantityOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.BoxQuantity);
                        if (boxQuantityOptions is not null) {
                            boxQuantity = await _plcManager.ReadInt16Async(new PlcInt32Address {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = boxQuantityOptions.ByteOffset
                            }) ?? 0;
                        }
                        else {
                            _logger.LogError($"未配置箱子数量地址");
                        }
                        //叫电梯楼层
                        var callElevatorLayerOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.CallElevatorLayer);
                        if (callElevatorLayerOptions is not null) {
                            callElevatorLayer = await _plcManager.ReadInt16Async(new PlcInt32Address {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = callElevatorLayerOptions.ByteOffset
                            }) ?? 0;
                        }
                        else {
                            _logger.LogError($"未配置叫电梯楼层地址");
                        }
                        //叫电梯使用层数
                        var callElevatorUseLayerOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.CallElevatorUseLayer);
                        if (callElevatorUseLayerOptions is not null) {
                            callElevatorUseLayer = await _plcManager.ReadInt16Async(new PlcInt32Address {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = callElevatorUseLayerOptions.ByteOffset
                            }) ?? 0;
                        }
                        else {
                            _logger.LogError($"未配置叫电梯使用层数地址");
                        }

                        //测试输出
                        _logger.LogInformation($"物料编号: {itemCode}, 批次: {batchNo}, 箱子数量: {boxQuantity}, 叫电梯楼层: {callElevatorLayer}, 叫电梯使用层数: {callElevatorUseLayer}, 唯一值Guid: {uniqueGuid}");
                        var elevatorCallRequest = new ElevatorCallRequest {
                            ErpGuid = uniqueGuid.ToString() ?? string.Empty,
                            ItemCode = itemCode ?? string.Empty,
                            Layer = callElevatorLayer,
                            Num = callElevatorUseLayer,
                            BoxQty = boxQuantity,
                            BatchNo = batchNo
                        };
                        var result = await _elevatorApiClient.CallElevatorAsync(elevatorCallRequest);
                        if (!result.IsSuccess) {
                            //写呼叫电梯失败

                            var callElevatorFailedSignalOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.CallElevatorFailedSignal);

                            if (callElevatorFailedSignalOptions is not null) {
                                var writeItems = new List<PlcDbBoolWriteItem> {
                                    new() {
                                        DbNumber = _options.CurrentValue.DbNumber,
                                        ByteOffset = callElevatorFailedSignalOptions.ByteOffset,
                                        BitOffset = callElevatorFailedSignalOptions.BitOffset ?? 0,
                                        State = PlcIoSignalState.High
                                    }
                                };
                                await _plcManager.WriteDbBoolsAsync(writeItems);
                            }
                        }
                        else {
                            //写唯一值Guid赋值
                            var uniqueGuidOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.UniqueGuid);
                            if (uniqueGuidOptions is not null) {
                                await _plcManager.WriteStringAsync(new PlcStringAddress {
                                    Area = PlcDataArea.Db,
                                    DbNumber = _options.CurrentValue.DbNumber,
                                    ByteOffset = uniqueGuidOptions.ByteOffset,
                                    Kind = PlcStringKind.FixedAscii,
                                    MaxLength = uniqueGuidOptions.MaxStringLength ?? 0
                                }, uniqueGuid.ToString());
                            }
                            else {
                                _logger.LogError($"未配置唯一值Guid地址");
                            }

                            var elevatorArrivedSignalOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.ElevatorArrivedSignal);
                            if (elevatorArrivedSignalOptions is not null) {
                                var writeItems = new List<PlcDbBoolWriteItem> {
                                    new() {
                                        DbNumber = _options.CurrentValue.DbNumber,
                                        ByteOffset = elevatorArrivedSignalOptions.ByteOffset,
                                        BitOffset = elevatorArrivedSignalOptions.BitOffset ?? 0,
                                        State = PlcIoSignalState.Low
                                    }
                                };
                                await _plcManager.WriteDbBoolsAsync(writeItems);
                                _logger.LogInformation($"更改电梯到位信号为低");
                            }

                            await _plcBridgeMessageBroadcaster.BroadcastAsync(HubMethodNames.NotifyElevatorCallRequested,
                                 JsonConvert.SerializeObject(elevatorCallRequest));
                            ElevatorRuntimeState.UpdateProgress(HubMethodNames.NotifyElevatorCallRequested,
                                JsonConvert.SerializeObject(elevatorCallRequest));
                        }
                    }

                    //进料完成信号InfeedDoneSignal
                    var hasInfeedDoneSignal = changes.Any(a => a.ByteOffset == _infeedDoneSignalByteOffset &&
                                                               a.BitOffset == _infeedDoneSignalBitOffset &&
                                                               a.NewState == PlcIoSignalState.High);
                    if (hasInfeedDoneSignal) {
                        //检测到进料完成信号，执行呼叫电梯逻辑
                        var uniqueGuid = string.Empty;
                        //唯一值Guid
                        var uniqueGuidOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                            f.Role == ElevatorHandshakeFieldRole.UniqueGuid);
                        if (uniqueGuidOptions is not null) {
                            uniqueGuid = await _plcManager.ReadStringAsync(new PlcStringAddress {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = uniqueGuidOptions.ByteOffset,
                                Kind = PlcStringKind.FixedAscii,
                                MaxLength = uniqueGuidOptions.MaxStringLength ?? 0
                            });
                        }
                        else {
                            _logger.LogError($"未配置唯一值Guid地址");
                        }

                        var elevatorInfeedDoneRequest = new ElevatorInfeedDoneRequest {
                            ErpGuid = uniqueGuid ?? string.Empty,
                            Status = 4
                        };
                        var result = await _elevatorApiClient.ReportInfeedDoneAsync(elevatorInfeedDoneRequest);

                        //测试输出
                        _logger.LogInformation($"进料完成信号-唯一值Guid: {uniqueGuid}, 执行完成状态: {result.IsSuccess}");
                        //更改电梯到位信号为低

                        if (!result.IsSuccess) {
                            //写进料失败
                            var infeedFailedSignalOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.InfeedFailedSignal);

                            if (infeedFailedSignalOptions is not null) {
                                var writeItems = new List<PlcDbBoolWriteItem> {
                                    new() {
                                        DbNumber = _options.CurrentValue.DbNumber,
                                        ByteOffset = infeedFailedSignalOptions.ByteOffset,
                                        BitOffset = infeedFailedSignalOptions.BitOffset ?? 0,
                                        State = PlcIoSignalState.High
                                    }
                                };
                                await _plcManager.WriteDbBoolsAsync(writeItems);
                            }
                        }
                        else {
                            var elevatorArrivedSignalOptions = _options.CurrentValue.Fields.FirstOrDefault(f =>
                                f.Role == ElevatorHandshakeFieldRole.ElevatorArrivedSignal);
                            if (elevatorArrivedSignalOptions is not null) {
                                var writeItems = new List<PlcDbBoolWriteItem> {
                                    new() {
                                        DbNumber = _options.CurrentValue.DbNumber,
                                        ByteOffset = elevatorArrivedSignalOptions.ByteOffset,
                                        BitOffset = elevatorArrivedSignalOptions.BitOffset ?? 0,
                                        State = PlcIoSignalState.Low
                                    }
                                };
                                await _plcManager.WriteDbBoolsAsync(writeItems);
                                _logger.LogInformation($"更改电梯到位信号为低");
                            }
                            await _plcBridgeMessageBroadcaster.BroadcastAsync(HubMethodNames.NotifyFeedingCompleted,
                                JsonConvert.SerializeObject(elevatorInfeedDoneRequest));
                            ElevatorRuntimeState.UpdateProgress(HubMethodNames.NotifyFeedingCompleted,
                                JsonConvert.SerializeObject(elevatorInfeedDoneRequest));
                        }
                    }
                }, "PLC DB Bool 变化处理");
            };

            _plcManager.Faulted += async (sender, args) => {
                _logger.LogError($"plcManager异常:{args.Exception}");
                await _plcBridgeMessageBroadcaster.BroadcastAsync(HubMethodNames.NotifyS7ConnectionStatusChanged,
                    nameof(PlcStatus.Faulted));
            };
            _plcManager.StatusChanged += async (sender, args) => {
                _logger.LogInformation($"PLC连接状态变更:旧状态-{args.OldStatus},新状态-{args.NewStatus}");
                await _plcBridgeMessageBroadcaster.BroadcastAsync(HubMethodNames.NotifyS7ConnectionStatusChanged,
                    nameof(args.NewStatus));
            };
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            await EnvironmentHelper.DelayAfterBootAsync(TimeSpan.FromSeconds(20), stoppingToken).ConfigureAwait(false);

            await _safeExecutor.ExecuteAsync(async () => {
                var callElevatorSignal = _options.CurrentValue.Fields.FirstOrDefault(f => f is { Role: ElevatorHandshakeFieldRole.CallElevatorSignal, ValueType: PlcDbValueType.Bool });

                _callElevatorSignalByteOffset = callElevatorSignal?.ByteOffset ?? 0;

                _callElevatorSignalBitOffset = callElevatorSignal?.BitOffset ?? 0;

                var infeedDoneSignal = _options.CurrentValue.Fields.FirstOrDefault(f => f is { Role: ElevatorHandshakeFieldRole.InfeedDoneSignal, ValueType: PlcDbValueType.Bool });
                _infeedDoneSignalByteOffset = infeedDoneSignal?.ByteOffset ?? 0;
                _infeedDoneSignalBitOffset = infeedDoneSignal?.BitOffset ?? 0;

                var isInitialized = await _plcManager.InitializeAsync(stoppingToken);
                var initializeRetryDelay = TimeSpan.FromSeconds(5);
                while (!isInitialized) {
                    _logger.LogWarning("PLC初始化失败，将在{RetryDelaySeconds}秒后重试连接", initializeRetryDelay.TotalSeconds);
                    try {
                        await Task.Delay(initializeRetryDelay, stoppingToken);
                    }
                    catch (OperationCanceledException) {
                        return;
                    }

                    isInitialized = await _plcManager.ReconnectAsync(stoppingToken);
                }
                var optionsList = _options.CurrentValue.Fields.
                    Where(w => w.ValueType == PlcDbValueType.Bool)
                    .Select(s => new PlcDbBoolPoint {
                        DbNumber = _options.CurrentValue.DbNumber,
                        ByteOffset = s.ByteOffset,
                        BitOffset = s.BitOffset ?? 0,
                    })
                    .ToList();
                if (optionsList.Count < 1) {
                    _logger.LogError($"监控点位数量小于1,无法正常使用");
                }
                await _plcManager.SetMonitoredDbBoolPointsAsync(optionsList, stoppingToken);

                var serializeObject = JsonConvert.SerializeObject(_plcManager.MonitoredDbBoolPoints);
                _logger.LogInformation($"当前监控点位列表:{serializeObject}");
                _logger.LogInformation($"启动字典监控");
            },
                "PLC 初始化与监控点位设置");

            while (!stoppingToken.IsCancellationRequested) {
                await Task.Delay(1000, stoppingToken);
            }
        }
    }
}
