using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using ZakYip.PlcBridge.Core.Events;
using ZakYip.PlcBridge.Core.Models;

namespace ZakYip.PlcBridge.Core.Manager {

    public interface IPlcManager : IDisposable {

        /// <summary>
        /// PLC 状态
        /// </summary>
        PlcStatus Status { get; }

        /// <summary>
        /// 是否已完成初始化
        /// </summary>
        bool IsInitialized { get; }

        /// <summary>
        /// 是否已建立连接
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// 当前故障码（无故障时为 null）
        /// </summary>
        int? FaultCode { get; }

        /// <summary>
        /// 当前监控的 IO 列表
        /// </summary>
        IReadOnlyList<PlcIoPoint> MonitoredIoPoints { get; }

        /// <summary>
        /// 当前监控的 DB Bool 列表
        /// </summary>
        IReadOnlyList<PlcDbBoolPoint> MonitoredDbBoolPoints { get; }

        /// <summary>
        /// PLC 状态变更事件
        /// </summary>
        event EventHandler<PlcStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// 异常事件（用于隔离异常，不影响上层调用链）
        /// </summary>
        event EventHandler<PlcFaultedEventArgs>? Faulted;

        /// <summary>
        /// 监控 IO 变动事件
        /// </summary>
        event EventHandler<PlcIoChangedEventArgs>? IoChanged;

        /// <summary>
        /// 监控 DB 块 Bool 变化事件（批量上报）
        /// </summary>
        event EventHandler<PlcDbBoolsChangedEventArgs>? DbBoolsChanged;

        /// <summary>
        /// 初始化（内部包含首次连接与必要的握手）
        /// </summary>
        ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置监控的 IO 点集合（覆盖式设置）
        /// </summary>
        ValueTask SetMonitoredIoPointsAsync(
            IReadOnlyList<PlcIoPoint> ioPoints,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 设置监控的 DB Bool 点集合（覆盖式设置）
        /// </summary>
        ValueTask SetMonitoredDbBoolPointsAsync(
            IReadOnlyList<PlcDbBoolPoint> points,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 重新连接（内部实现退避/限流，避免风暴式重连）
        /// </summary>
        ValueTask<bool> ReconnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// 写 IO 电平
        /// </summary>
        ValueTask WriteIoAsync(
            PlcIoPoint ioPoint,
            PlcIoSignalState state,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取 Int32 值（读取失败返回 null，异常通过 Faulted 事件隔离）
        /// </summary>
        ValueTask<int?> ReadInt32Async(
            PlcInt32Address address,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 读取字符串值（读取失败返回 null，异常通过 Faulted 事件隔离）
        /// </summary>
        ValueTask<string?> ReadStringAsync(
            PlcStringAddress address,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 写 DB 块的多个 Bool（允许不连续；实现端可按 DB 分组、按字节合并写入）
        /// </summary>
        ValueTask WriteDbBoolsAsync(
            IReadOnlyList<PlcDbBoolWriteItem> items,
            CancellationToken cancellationToken = default);
    }
}
