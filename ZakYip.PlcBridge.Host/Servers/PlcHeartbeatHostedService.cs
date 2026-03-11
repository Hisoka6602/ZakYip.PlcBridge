using ZakYip.PlcBridge.Core;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Models;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.SignalR;
using ZakYip.PlcBridge.Core.Utilities;

namespace ZakYip.PlcBridge.Host.Servers {

    public class PlcHeartbeatHostedService : BackgroundService {
        private readonly ILogger<PlcHeartbeatHostedService> _logger;
        private readonly SafeExecutor _safeExecutor;
        private readonly IElevatorApiClient _elevatorApiClient;
        private readonly IPlcManager _plcManager;
        private readonly IOptionsMonitor<ElevatorHandshakeDbOptions> _options;

        public PlcHeartbeatHostedService(ILogger<PlcHeartbeatHostedService> logger,
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
            var heartbeatSignal = _options.CurrentValue.Fields.FirstOrDefault(f =>
                f.Role == ElevatorHandshakeFieldRole.HeartbeatSignal);

            while (!stoppingToken.IsCancellationRequested) {
                if (_plcManager.Status == PlcStatus.Connected) {
                    await _safeExecutor.ExecuteAsync(async () => {
                        if (heartbeatSignal is not null) {
                            var heartbeatValue = await _plcManager.ReadInt16Async(new PlcInt32Address {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = heartbeatSignal.ByteOffset
                            }, stoppingToken);

                            if (heartbeatValue is null) {
                                _logger.LogWarning("心跳读取失败，已跳过本次写入");
                                return;
                            }

                            int writeValue = heartbeatValue == 1 ? 0 : 1; // 切换心跳信号状态

                            await _plcManager.WriteInt16Async(new PlcInt32Address {
                                Area = PlcDataArea.Db,
                                DbNumber = _options.CurrentValue.DbNumber,
                                ByteOffset = heartbeatSignal.ByteOffset
                            }, (short)writeValue, stoppingToken);
                        }

                        // 心跳检测逻辑
                    }, "心跳检测异常").ConfigureAwait(false);
                }

                await Task.Delay(TimeSpan.FromSeconds(1), stoppingToken).ConfigureAwait(false);
            }
        }
    }
}
