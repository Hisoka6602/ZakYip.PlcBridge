using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Options {
    /// <summary>
    /// S7 连接参数
    /// </summary>
    public sealed record class S7ConnectionOptions {
        /// <summary>
        /// 配置节名称
        /// </summary>
        public const string SectionName = "S7Connection";

        /// <summary>
        /// 连接名称（用于日志/诊断区分，可为空）
        /// </summary>
        public string? Name { get; init; }

        /// <summary>
        /// PLC IP 地址或主机名
        /// </summary>
        public required string Host { get; init; } = string.Empty;

        /// <summary>
        /// PLC 端口（ISO-on-TCP 通常为 102）
        /// </summary>
        public int Port { get; init; } = 102;

        /// <summary>
        /// 机架号（S7-1200/1500 常用 0）
        /// </summary>
        public int Rack { get; init; } = 0;

        /// <summary>
        /// 槽号（S7-1200/1500 常用 1）
        /// </summary>
        public int Slot { get; init; } = 1;

        /// <summary>
        /// CPU 型号（用于兼容策略与默认参数选择）
        /// </summary>
        public S7CpuType CpuType { get; init; } = S7CpuType.S71200;

        /// <summary>
        /// 连接超时（毫秒）
        /// </summary>
        public int ConnectTimeoutMs { get; init; } = 1500;

        /// <summary>
        /// 读超时（毫秒）
        /// </summary>
        public int ReadTimeoutMs { get; init; } = 1500;

        /// <summary>
        /// 写超时（毫秒）
        /// </summary>
        public int WriteTimeoutMs { get; init; } = 1500;
        /// <summary>
        /// 轮询周期（毫秒），用于监测连接状态
        /// </summary>
        public int MonitorPollIntervalMs { get; init; } = 20;
        /// <summary>
        /// 是否启用自动重连
        /// </summary>
        public bool IsAutoReconnectEnabled { get; init; } = true;

        /// <summary>
        /// 初始重连延迟（毫秒）
        /// </summary>
        public int ReconnectInitialDelayMs { get; init; } = 500;

        /// <summary>
        /// 最大重连延迟（毫秒）
        /// </summary>
        public int ReconnectMaxDelayMs { get; init; } = 10_000;

        /// <summary>
        /// 最大重连次数（0 或负数表示不限）
        /// </summary>
        public int ReconnectMaxAttempts { get; init; } = 0;
    }
}
