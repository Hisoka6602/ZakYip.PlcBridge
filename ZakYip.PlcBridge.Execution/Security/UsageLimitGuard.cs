using System;
using System.Linq;
using System.Text;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.Models.Security;

namespace ZakYip.PlcBridge.Execution.Security {

    /// <summary>
    /// 使用限制守卫实现（按累计运行时长）
    /// </summary>
    public sealed class UsageLimitGuard : IUsageLimitGuard {
        private readonly ILogger<UsageLimitGuard> _logger;
        private readonly IOptionsMonitor<UsageLimitOptions> _options;
        private readonly IUsageStateStore _store;

        private readonly SemaphoreSlim _gate = new(1, 1);

        private UsageState? _state;
        private long _lastTicks;
        private long _lastSavedUsedMs;
        private int _consecutiveStoreFailures;

        public UsageLimitGuard(
            ILogger<UsageLimitGuard> logger,
            IOptionsMonitor<UsageLimitOptions> options,
            IUsageStateStore store) {
            _logger = logger;
            _options = options;
            _store = store;
        }

        public async ValueTask<UsageLimitDecision> EvaluateAsync(CancellationToken cancellationToken = default) {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _options.CurrentValue;
                if (!opt.IsEnabled) {
                    return new UsageLimitDecision {
                        IsAllowed = true,
                        DenyReason = UsageLimitDenyReason.None,
                        Remaining = null,
                        Message = "使用限制已禁用"
                    };
                }

                var initOk = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
                if (!initOk) {
                    return new UsageLimitDecision {
                        IsAllowed = false,
                        DenyReason = UsageLimitDenyReason.StoreUnavailable,
                        Remaining = null,
                        Message = "使用状态存储不可用"
                    };
                }

                ApplyElapsed();

                if (_consecutiveStoreFailures >= opt.MaxConsecutiveStoreFailures) {
                    return new UsageLimitDecision {
                        IsAllowed = false,
                        DenyReason = UsageLimitDenyReason.StoreUnavailable,
                        Remaining = null,
                        Message = "使用状态连续落盘失败，拒绝运行"
                    };
                }

                var max = opt.MaxTotalRunTimeMs;
                var used = _state!.TotalUsedMs;

                if (used >= max) {
                    return new UsageLimitDecision {
                        IsAllowed = false,
                        DenyReason = UsageLimitDenyReason.QuotaExceeded,
                        Remaining = TimeSpan.Zero,
                        Message = "累计可用时长已耗尽"
                    };
                }

                var remainingMs = max - used;
                return new UsageLimitDecision {
                    IsAllowed = true,
                    DenyReason = UsageLimitDenyReason.None,
                    Remaining = TimeSpan.FromMilliseconds(remainingMs),
                    Message = null
                };
            }
            catch (Exception ex) {
                _logger.LogError(ex, "使用限制判定异常");
                return new UsageLimitDecision {
                    IsAllowed = false,
                    DenyReason = UsageLimitDenyReason.StateTampered,
                    Remaining = null,
                    Message = "使用限制判定异常"
                };
            }
            finally {
                _gate.Release();
            }
        }

        public async ValueTask HeartbeatAsync(CancellationToken cancellationToken = default) {
            await _gate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _options.CurrentValue;
                if (!opt.IsEnabled) return;

                var initOk = await EnsureLoadedAsync(cancellationToken).ConfigureAwait(false);
                if (!initOk) return;

                ApplyElapsed();

                var checkpointIntervalMs = Math.Max(1_000, opt.CheckpointIntervalMs);
                var used = _state!.TotalUsedMs;

                // 以“累计已用时长增长”作为落盘触发条件（与单调时钟一致）
                if (used - _lastSavedUsedMs < checkpointIntervalMs) {
                    return;
                }

                await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
            }
            finally {
                _gate.Release();
            }
        }

        private async ValueTask<bool> EnsureLoadedAsync(CancellationToken cancellationToken) {
            if (_state is not null) return true;

            try {
                var loaded = await _store.TryLoadAsync(cancellationToken).ConfigureAwait(false);
                _state = loaded ?? new UsageState();

                // 首次初始化时立即落盘一次，确保重装后仍能继续追踪
                _state.LastCheckpoint = DateTimeOffset.Now;
                _state.Sequence = Math.Max(_state.Sequence, 0);

                _lastTicks = StopwatchMonotonicClock.GetTimestamp();
                _lastSavedUsedMs = _state.TotalUsedMs;

                var saved = await SaveCoreAsync(cancellationToken).ConfigureAwait(false);
                return saved;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "加载使用状态失败");
                return false;
            }
        }

        private void ApplyElapsed() {
            var now = StopwatchMonotonicClock.GetTimestamp();
            if (_lastTicks == 0) {
                _lastTicks = now;
                return;
            }

            var deltaTicks = now - _lastTicks;
            if (deltaTicks <= 0) return;

            var deltaMs = StopwatchMonotonicClock.ToMilliseconds(deltaTicks);
            if (deltaMs <= 0) {
                _lastTicks = now;
                return;
            }

            _lastTicks = now;

            // 仅按单调时钟累计，系统时间变化不影响结果
            _state!.TotalUsedMs += deltaMs;
        }

        private async ValueTask<bool> SaveCoreAsync(CancellationToken cancellationToken) {
            try {
                _state!.Sequence++;
                _state.LastCheckpoint = DateTimeOffset.Now;

                var ok = await _store.TrySaveAsync(_state, cancellationToken).ConfigureAwait(false);
                if (!ok) {
                    _consecutiveStoreFailures++;
                    _logger.LogError("使用状态落盘失败，连续失败次数: {Count}", _consecutiveStoreFailures);
                    return false;
                }

                _consecutiveStoreFailures = 0;
                _lastSavedUsedMs = _state.TotalUsedMs;
                return true;
            }
            catch (Exception ex) {
                _consecutiveStoreFailures++;
                _logger.LogError(ex, "使用状态落盘异常，连续失败次数: {Count}", _consecutiveStoreFailures);
                return false;
            }
        }
    }
}
