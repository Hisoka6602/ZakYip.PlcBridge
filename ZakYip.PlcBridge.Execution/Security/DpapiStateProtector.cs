using System;
using System.Linq;
using System.Text;
using ZakYip.PlcBridge.Core;
using System.Threading.Tasks;
using System.Collections.Generic;
using System.Security.Cryptography;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Options;

namespace ZakYip.PlcBridge.Execution.Security {

    /// <summary>
    /// DPAPI 保护器（LocalMachine）
    /// </summary>
    public sealed class DpapiStateProtector : IStateProtector {
        private readonly byte[] _entropy;

        public DpapiStateProtector(IOptionsMonitor<UsageLimitOptions> options) {
            var key = options.CurrentValue.ProductKey ?? "ZakYip.PlcBridge";
            _entropy = Encoding.UTF8.GetBytes($"UsageLimit|{key}|Entropy");
        }

        public byte[] Protect(ReadOnlySpan<byte> plain) {
            // DPAPI 需要 byte[]，该分配规模很小且落盘频率可控
            return ProtectedData.Protect(plain.ToArray(), _entropy, DataProtectionScope.LocalMachine);
        }

        public byte[] Unprotect(ReadOnlySpan<byte> protectedData) {
            return ProtectedData.Unprotect(protectedData.ToArray(), _entropy, DataProtectionScope.LocalMachine);
        }
    }
}
