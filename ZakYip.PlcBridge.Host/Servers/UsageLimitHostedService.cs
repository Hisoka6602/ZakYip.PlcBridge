using System;
using System.Linq;
using System.Text;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Options;

namespace ZakYip.PlcBridge.Host.Servers {

    /// <summary>
    /// 使用限制后台服务：定时心跳 + 判定，超限则停止应用
    /// </summary>
    public sealed class UsageLimitHostedService : BackgroundService {
        private readonly ILogger<UsageLimitHostedService> _logger;
        private readonly IHostApplicationLifetime _lifetime;
        private readonly IUsageLimitGuard _guard;
        private readonly IOptionsMonitor<UsageLimitOptions> _options;

        public UsageLimitHostedService(
            ILogger<UsageLimitHostedService> logger,
            IHostApplicationLifetime lifetime,
            IUsageLimitGuard guard,
            IOptionsMonitor<UsageLimitOptions> options) {
            _logger = logger;
            _lifetime = lifetime;
            _guard = guard;
            _options = options;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken) {
            var opt = _options.CurrentValue;
            if (!opt.IsEnabled) {
                _logger.LogInformation("使用限制已禁用");
                return;
            }

            // 启动立即判定一次
            var decision = await _guard.EvaluateAsync(stoppingToken).ConfigureAwait(false);
            if (!decision.IsAllowed) {
                _logger.LogError("使用限制触发：{Message}", decision.Message ?? "拒绝运行");
                _lifetime.StopApplication();
                return;
            }

            var interval = TimeSpan.FromMilliseconds(Math.Max(1_000, opt.CheckpointIntervalMs));
            using var timer = new PeriodicTimer(interval);

            _logger.LogInformation("使用限制服务已启动，落盘间隔: {IntervalMs}ms，最大累计时长: {MaxMs}ms",
                opt.CheckpointIntervalMs, opt.MaxTotalRunTimeMs);

            while (!stoppingToken.IsCancellationRequested) {
                try {
                    var hasNext = await timer.WaitForNextTickAsync(stoppingToken).ConfigureAwait(false);
                    if (!hasNext) break;

                    await _guard.HeartbeatAsync(stoppingToken).ConfigureAwait(false);

                    decision = await _guard.EvaluateAsync(stoppingToken).ConfigureAwait(false);
                    if (!decision.IsAllowed) {
                        _logger.LogError("使用限制触发：{Message}", decision.Message ?? "拒绝运行");
                        _lifetime.StopApplication();
                        break;
                    }
                }
                catch (OperationCanceledException) {
                    break;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "使用限制服务循环异常");
                }
                finally {
                    await Task.Delay(1000, stoppingToken);
                }
            }
        }
    }
}
