using System.IO;
using Microsoft.Extensions.Logging;
using ZakYip.PlcBridge.Client.Options;

namespace ZakYip.PlcBridge.Client.Services {
    /// <summary>
    /// 客户端日志清理服务。
    /// </summary>
    public sealed class LogCleanupService {
        private readonly ILogger<LogCleanupService> _logger;
        private readonly LogCleanupOptions _options;

        private readonly object _gate = new();
        private CancellationTokenSource? _cts;
        private Task? _cleanupTask;

        public LogCleanupService(
            ILogger<LogCleanupService> logger,
            LogCleanupOptions options) {
            _logger = logger;
            _options = options;
        }

        public void Start() {
            if (!_options.Enabled) {
                _logger.LogInformation("客户端日志清理服务已禁用。");
                return;
            }

            lock (_gate) {
                if (_cleanupTask is not null) {
                    return;
                }

                _cts = new CancellationTokenSource();
                _cleanupTask = Task.Run(() => RunAsync(_cts.Token));
            }
        }

        public async Task StopAsync() {
            Task? cleanupTask;
            CancellationTokenSource? cts;

            lock (_gate) {
                cleanupTask = _cleanupTask;
                cts = _cts;
                _cleanupTask = null;
                _cts = null;
            }

            if (cleanupTask is null || cts is null) {
                return;
            }

            cts.Cancel();
            try {
                await cleanupTask.ConfigureAwait(false);
            }
            catch (OperationCanceledException) {
                // ignore
            }
            finally {
                cts.Dispose();
            }
        }

        private async Task RunAsync(CancellationToken cancellationToken) {
            _logger.LogInformation("客户端日志清理服务已启动，保留天数: {RetentionDays}天，检查间隔: {CheckIntervalHours}小时",
                _options.RetentionDays, _options.CheckIntervalHours);

            await CleanupOldLogsAsync().ConfigureAwait(false);

            while (!cancellationToken.IsCancellationRequested) {
                try {
                    await Task.Delay(TimeSpan.FromHours(_options.CheckIntervalHours), cancellationToken).ConfigureAwait(false);
                    await CleanupOldLogsAsync().ConfigureAwait(false);
                }
                catch (OperationCanceledException) {
                    _logger.LogInformation("客户端日志清理服务正在停止。");
                    break;
                }
                catch (Exception ex) {
                    _logger.LogError(ex, "客户端日志定期清理异常。");
                }
            }
        }

        private async Task CleanupOldLogsAsync() {
            var logDirectory = _options.LogDirectory;

            if (!Path.IsPathRooted(logDirectory)) {
                logDirectory = Path.Combine(AppContext.BaseDirectory, logDirectory);
            }

            if (!Directory.Exists(logDirectory)) {
                _logger.LogWarning("日志目录不存在: {LogDirectory}", logDirectory);
                return;
            }

            var cutoffDate = DateTime.Now.AddDays(-_options.RetentionDays);
            var (deletedCount, failedCount) = await CleanupDirectoryAsync(logDirectory, cutoffDate).ConfigureAwait(false);

            var archiveDirectory = Path.Combine(logDirectory, "archives");
            if (Directory.Exists(archiveDirectory)) {
                var (archiveDeleted, archiveFailed) = await CleanupDirectoryAsync(archiveDirectory, cutoffDate).ConfigureAwait(false);
                deletedCount += archiveDeleted;
                failedCount += archiveFailed;
            }

            _logger.LogInformation("客户端日志清理完成，删除文件数: {DeletedCount}，失败数: {FailedCount}", deletedCount, failedCount);
        }

        private async Task<(int DeletedCount, int FailedCount)> CleanupDirectoryAsync(string directory, DateTime cutoffDate) {
            return await Task.Run(() => {
                var deletedCount = 0;
                var failedCount = 0;

                foreach (var file in Directory.GetFiles(directory, "*.log")) {
                    try {
                        var fileInfo = new FileInfo(file);
                        if (fileInfo.LastWriteTime < cutoffDate) {
                            fileInfo.Delete();
                            deletedCount++;
                        }
                    }
                    catch (Exception ex) {
                        _logger.LogWarning(ex, "删除日志文件失败: {File}", file);
                        failedCount++;
                    }
                }

                return (deletedCount, failedCount);
            }).ConfigureAwait(false);
        }
    }
}
