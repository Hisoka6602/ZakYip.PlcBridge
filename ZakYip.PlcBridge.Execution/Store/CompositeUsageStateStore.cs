using System;
using System.Linq;
using System.Text;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Core.Models.Security;

namespace ZakYip.PlcBridge.Execution.Store {

    /// <summary>
    /// 组合存储：从多个副本读取，按 Sequence 选主；保存时双写/多写
    /// </summary>
    public sealed class CompositeUsageStateStore : IUsageStateStore {
        private readonly ILogger<CompositeUsageStateStore> _logger;
        private readonly IUsageStateStore[] _stores;

        public CompositeUsageStateStore(ILogger<CompositeUsageStateStore> logger, params IUsageStateStore[] stores) {
            _logger = logger;
            _stores = stores ?? [];
        }

        public async ValueTask<UsageState?> TryLoadAsync(CancellationToken cancellationToken = default) {
            UsageState? best = null;

            foreach (var store in _stores) {
                try {
                    var state = await store.TryLoadAsync(cancellationToken).ConfigureAwait(false);
                    if (state is null) continue;

                    if (best is null || state.Sequence > best.Sequence) {
                        best = state;
                    }
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "读取使用状态副本异常");
                }
            }

            return best;
        }

        public async ValueTask<bool> TrySaveAsync(UsageState state, CancellationToken cancellationToken = default) {
            var anyOk = false;

            foreach (var store in _stores) {
                try {
                    var ok = await store.TrySaveAsync(state, cancellationToken).ConfigureAwait(false);
                    anyOk |= ok;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "写入使用状态副本异常");
                }
            }

            return anyOk;
        }
    }
}
