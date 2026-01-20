using System;
using System.Linq;
using System.Text;
using Microsoft.Win32;
using System.Text.Json;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Options;
using ZakYip.PlcBridge.Core.Models.Security;

namespace ZakYip.PlcBridge.Execution.Store {

    /// <summary>
    /// HKLM 注册表存储（始终使用 64 位视图；失败不抛异常）
    /// </summary>
    public sealed class RegistryUsageStateStore : IUsageStateStore, IDisposable {

        private static readonly JsonSerializerOptions JsonOptions = new() {
            WriteIndented = false
        };

        private readonly ILogger<RegistryUsageStateStore> _logger;
        private readonly IOptionsMonitor<UsageLimitOptions> _options;
        private readonly IStateProtector _protector;

        private readonly string _subKeyPath;

        // 注释：始终使用 64 位视图，避免 x86/x64 产生双份状态
        private readonly RegistryKey _hklm64;

        public RegistryUsageStateStore(
            ILogger<RegistryUsageStateStore> logger,
            IOptionsMonitor<UsageLimitOptions> options,
            IStateProtector protector) {
            _logger = logger;
            _options = options;
            _protector = protector;

            var product = options.CurrentValue.ProductKey?.Trim();
            if (string.IsNullOrWhiteSpace(product)) product = "ZakYip.PlcBridge";

            _subKeyPath = $@"Software\ZakYip\{product}";

            _hklm64 = RegistryKey.OpenBaseKey(RegistryHive.LocalMachine, RegistryView.Registry64);
        }

        public ValueTask<UsageState?> TryLoadAsync(CancellationToken cancellationToken = default) {
            try {
                using var key = _hklm64.OpenSubKey(_subKeyPath, false);
                if (key is null) return ValueTask.FromResult<UsageState?>(null);

                var raw = key.GetValue("UsageState") as byte[];
                if (raw is null || raw.Length == 0) return ValueTask.FromResult<UsageState?>(null);

                var plain = _protector.Unprotect(raw);
                var state = JsonSerializer.Deserialize<UsageState>(plain, JsonOptions);
                return ValueTask.FromResult<UsageState?>(state);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "读取注册表使用状态失败");
                return ValueTask.FromResult<UsageState?>(null);
            }
        }

        public ValueTask<bool> TrySaveAsync(UsageState state, CancellationToken cancellationToken = default) {
            try {
                using var key = _hklm64.CreateSubKey(_subKeyPath, true);
                if (key is null) return ValueTask.FromResult(false);

                var plain = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
                var protectedBytes = _protector.Protect(plain);

                key.SetValue("UsageState", protectedBytes, RegistryValueKind.Binary);
                return ValueTask.FromResult(true);
            }
            catch (Exception ex) {
                _logger.LogError(ex, "写入注册表使用状态失败");
                return ValueTask.FromResult(false);
            }
        }

        public void Dispose() {
            _hklm64.Dispose();
        }
    }
}
