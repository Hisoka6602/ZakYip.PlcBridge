namespace ZakYip.PlcBridge.Client.Options {
    /// <summary>
    /// 客户端日志清理配置。
    /// </summary>
    public sealed record class LogCleanupOptions {
        public bool Enabled { get; init; } = true;
        public int RetentionDays { get; init; } = 2;
        public int CheckIntervalHours { get; init; } = 1;
        public string LogDirectory { get; init; } = "logs";
    }
}
