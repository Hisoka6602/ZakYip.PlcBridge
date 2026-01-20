using System;
using System.Linq;
using System.Text;
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
    /// ProgramData 文件存储
    /// </summary>
    public sealed class FileUsageStateStore : IUsageStateStore {

        private static readonly JsonSerializerOptions JsonOptions = new() {
            WriteIndented = false
        };

        private readonly ILogger<FileUsageStateStore> _logger;
        private readonly IOptionsMonitor<UsageLimitOptions> _options;
        private readonly IStateProtector _protector;

        private readonly string _path;
        private readonly string _bakPath;

        public FileUsageStateStore(
            ILogger<FileUsageStateStore> logger,
            IOptionsMonitor<UsageLimitOptions> options,
            IStateProtector protector) {
            _logger = logger;
            _options = options;
            _protector = protector;

            var baseDir = Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData);
            var product = Sanitize(options.CurrentValue.ProductKey);
            var dir = Path.Combine(baseDir, "ZakYip", product);

            _path = Path.Combine(dir, "usage.state");
            _bakPath = Path.Combine(dir, "usage.state.bak");
        }

        public async ValueTask<UsageState?> TryLoadAsync(CancellationToken cancellationToken = default) {
            try {
                var a = await TryLoadFromPathAsync(_path, cancellationToken).ConfigureAwait(false);
                var b = await TryLoadFromPathAsync(_bakPath, cancellationToken).ConfigureAwait(false);

                if (a is null) return b;
                if (b is null) return a;

                return a.Sequence >= b.Sequence ? a : b;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "读取使用状态文件失败");
                return null;
            }
        }

        public async ValueTask<bool> TrySaveAsync(UsageState state, CancellationToken cancellationToken = default) {
            try {
                var baseDir = Path.GetDirectoryName(_path);
                if (!string.IsNullOrWhiteSpace(baseDir)) {
                    Directory.CreateDirectory(baseDir);
                }

                // 主文件
                if (!await SaveToPathAsync(_path, state, cancellationToken).ConfigureAwait(false)) {
                    return false;
                }

                // 备份文件
                _ = await SaveToPathAsync(_bakPath, state, cancellationToken).ConfigureAwait(false);
                return true;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "写入使用状态文件失败");
                return false;
            }
        }

        private async ValueTask<UsageState?> TryLoadFromPathAsync(string path, CancellationToken cancellationToken) {
            if (!File.Exists(path)) {
                return null;
            }

            byte[] protectedBytes;
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite, 4096, FileOptions.SequentialScan)) {
                protectedBytes = new byte[fs.Length];
                var read = 0;
                while (read < protectedBytes.Length) {
                    var n = await fs.ReadAsync(protectedBytes.AsMemory(read, protectedBytes.Length - read), cancellationToken).ConfigureAwait(false);
                    if (n <= 0) break;
                    read += n;
                }

                if (read != protectedBytes.Length) {
                    return null;
                }
            }

            var plain = _protector.Unprotect(protectedBytes);
            var state = JsonSerializer.Deserialize<UsageState>(plain, JsonOptions);
            return state;
        }

        private async ValueTask<bool> SaveToPathAsync(string path, UsageState state, CancellationToken cancellationToken) {
            var plain = JsonSerializer.SerializeToUtf8Bytes(state, JsonOptions);
            var protectedBytes = _protector.Protect(plain);

            var tmp = $"{path}.{Guid.NewGuid():N}.tmp";
            await File.WriteAllBytesAsync(tmp, protectedBytes, cancellationToken).ConfigureAwait(false);

            // 原子替换，避免断电/强杀造成半写入
            File.Move(tmp, path, true);
            return true;
        }

        private static string Sanitize(string? value) {
            var s = string.IsNullOrWhiteSpace(value) ? "ZakYip.PlcBridge" : value.Trim();
            foreach (var c in Path.GetInvalidFileNameChars()) {
                s = s.Replace(c, '_');
            }
            return s;
        }
    }
}
