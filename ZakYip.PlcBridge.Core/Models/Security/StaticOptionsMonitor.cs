using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using Microsoft.Extensions.Options;

namespace ZakYip.PlcBridge.Core.Models.Security {

    /// <summary>
    /// 固定 OptionsMonitor（用于硬编码配置）
    /// </summary>
    public sealed class StaticOptionsMonitor<TOptions> : IOptionsMonitor<TOptions> where TOptions : class {
        private readonly TOptions _current;

        public StaticOptionsMonitor(TOptions current) {
            _current = current;
        }

        public TOptions CurrentValue => _current;

        public TOptions Get(string? name) => _current;

        public IDisposable OnChange(Action<TOptions, string?> listener) => NoopDisposable.Instance;

        private sealed class NoopDisposable : IDisposable {
            public static readonly NoopDisposable Instance = new();

            public void Dispose() {
            }
        }
    }
}
