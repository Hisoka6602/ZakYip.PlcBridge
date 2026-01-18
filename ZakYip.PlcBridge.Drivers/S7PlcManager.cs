using S7.Net;
using System;
using System.Net;
using System.Linq;
using System.Text;
using System.Reflection;
using System.Buffers.Binary;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using ZakYip.PlcBridge.Core.Events;
using ZakYip.PlcBridge.Core.Models;
using System.Collections.Concurrent;
using ZakYip.PlcBridge.Core.Manager;
using ZakYip.PlcBridge.Core.Options;

namespace ZakYip.PlcBridge.Drivers {

    /// <summary>
    /// 基于 S7.NetPlus 的 PLC 管理器（连接、监控 IO/DB、写 IO/DB）
    /// 关键点：
    /// 1) 单 PLC 单连接：所有读写串行化，避免并发导致协议层异常
    /// 2) 监控采用批量 ReadBytesAsync 后本地拆 bit，降低通信次数与分配
    /// 3) 异常通过 Faulted 事件隔离，不影响调用链
    /// </summary>
    public sealed class S7PlcManager : IPlcManager {
        private readonly ILogger<S7PlcManager> _logger;
        private readonly IOptionsMonitor<S7ConnectionOptions> _optionsMonitor;
        private readonly IDisposable? _optionsChangeToken;

        // 生命周期串行化：初始化/重连/切换监控点位
        private readonly SemaphoreSlim _lifecycleGate = new(1, 1);

        // 通信串行化：S7netplus 单连接不适合并发请求
        private readonly SemaphoreSlim _requestGate = new(1, 1);

        private Plc? _plc;

        private int _disposed;      // 0=false, 1=true
        private int _isInitialized; // 0=false, 1=true
        private int _isConnected;   // 0=false, 1=true
        private int _status;        // PlcStatus
        private int _faultCode = -1; // -1 表示 null

        private PlcIoPoint[] _monitoredIoPoints = [];

        // DB Bool 监控（轮询路径只用预计算数据结构）
        private PlcDbBoolPoint[] _monitoredDbBoolPoints = [];

        private DbReadGroup[] _dbReadGroups = [];

        private int[] _dbPointGroupIndex = [];
        private int[] _dbPointByteIndex = [];
        private int[] _dbPointBitIndex = [];

        // 0=未知(首次采集)，1=Low，2=High
        private byte[] _dbPointSnapshot = [];

        // 将点位索引按 group 聚合，轮询时避免重复扫描
        private int[] _dbGroupPointStart = [];

        private int[] _dbGroupPointCount = [];
        private int[] _dbPointIndicesByGroup = [];

        private CancellationTokenSource? _monitorCts;
        private Task? _monitorTask;

        // 自动重连任务状态（0=未运行，1=运行中）
        private int _autoReconnectRunning;

        // 自动重连任务（用于观测/诊断，不做 await）
        private Task? _autoReconnectTask;

        // IO 快照：监控线程读写；切换监控点位时先停监控，避免并发访问
        private Dictionary<PlcIoPoint, PlcIoSignalState> _snapshot = new();

        /// <summary>
        /// 创建 `S7PlcManager` 实例，并订阅配置热更新。
        /// </summary>
        /// <remarks>
        /// - 通过 `IOptionsMonitor` 监听连接配置变更；
        /// - 一旦配置变化且当前已初始化，异步触发一次重连；
        /// - 该回调不阻塞 Options 的回调线程，避免影响配置系统。
        /// </remarks>
        public S7PlcManager(
            ILogger<S7PlcManager> logger,
            IOptionsMonitor<S7ConnectionOptions> optionsMonitor) {
            _logger = logger;
            _optionsMonitor = optionsMonitor;

            _status = (int)PlcStatus.NotInitialized;

            _optionsChangeToken = _optionsMonitor.OnChange((_, _) => {
                // 配置变更后触发重连：不阻塞 Options 回调线程
                if (Volatile.Read(ref _isInitialized) == 1 && Volatile.Read(ref _disposed) == 0) {
                    _ = Task.Run(async () => {
                        try {
                            await ReconnectAsync(CancellationToken.None).ConfigureAwait(false);
                        }
                        catch (Exception ex) {
                            RaiseFaulted("配置变更触发重连失败", ex);
                        }
                    });
                }
            });
        }

        /// <summary>
        /// 当前正在监控的 DB Bool 点位列表（只读视图）。
        /// </summary>
        public IReadOnlyList<PlcDbBoolPoint> MonitoredDbBoolPoints => _monitoredDbBoolPoints;

        /// <summary>
        /// PLC 状态变更事件（例如：初始化中 -> 已连接 / 已断开）。
        /// </summary>
        public event EventHandler<PlcStatusChangedEventArgs>? StatusChanged;

        /// <summary>
        /// 故障事件：用于上报内部异常，不向外抛出以隔离调用链。
        /// </summary>
        public event EventHandler<PlcFaultedEventArgs>? Faulted;

        /// <summary>
        /// IO 点位变动事件（输入/输出电平变化）。
        /// </summary>
        public event EventHandler<PlcIoChangedEventArgs>? IoChanged;

        /// <summary>
        /// DB Bool 点位批量变动事件（一次轮询可能产生多个变更）。
        /// </summary>
        public event EventHandler<PlcDbBoolsChangedEventArgs>? DbBoolsChanged;

        /// <summary>
        /// 当前 PLC 状态（线程安全读取）。
        /// </summary>
        public PlcStatus Status => (PlcStatus)Volatile.Read(ref _status);

        /// <summary>
        /// 是否已完成初始化（初始化只做一次，后续重连不影响该标志）。
        /// </summary>
        public bool IsInitialized => Volatile.Read(ref _isInitialized) == 1;

        /// <summary>
        /// 是否当前处于连接状态（线程安全读取）。
        /// </summary>
        public bool IsConnected => Volatile.Read(ref _isConnected) == 1;

        /// <summary>
        /// 当前故障码（无故障时为 null）。
        /// </summary>
        /// <remarks>
        /// - 使用 `-1` 作为“无故障”的内部哨兵值；
        /// - 该属性将其映射为 `null`。
        /// </remarks>
        public int? FaultCode {
            get {
                var code = Volatile.Read(ref _faultCode);
                return code < 0 ? null : code;
            }
        }

        /// <summary>
        /// 当前正在监控的 IO 点位列表（只读视图）。
        /// </summary>
        public IReadOnlyList<PlcIoPoint> MonitoredIoPoints => _monitoredIoPoints;

        /// <summary>
        /// 初始化管理器：建立第一次 PLC 连接并在需要时启动监控循环。
        /// </summary>
        /// <param name="cancellationToken">取消令牌（控制初始化/连接过程）。</param>
        /// <returns>连接成功返回 true；失败返回 false（异常通过 Faulted 事件上报）。</returns>
        public async ValueTask<bool> InitializeAsync(CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return false;
            }

            // 初始化只允许一次；重复调用直接返回当前连接状态
            if (Interlocked.CompareExchange(ref _isInitialized, 1, 0) == 1) {
                return IsConnected;
            }

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                SetStatus(PlcStatus.Initializing, "初始化开始");
                var ok = await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
                SetStatus(ok ? PlcStatus.Connected : PlcStatus.Disconnected, ok ? "初始化完成" : "初始化失败");
                return ok;
            }
            catch (Exception ex) {
                RaiseFaulted("初始化异常", ex);
                SetStatus(PlcStatus.Disconnected, "初始化异常");
                return false;
            }
            finally {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// 覆盖式设置需要监控的 IO 点集合。
        /// </summary>
        /// <remarks>
        /// 关键步骤：
        /// 1) 进入生命周期锁，确保与重连/初始化/其他设置互斥；
        /// 2) 先停止监控循环，避免轮询线程并发访问 `_snapshot`；
        /// 3) 复制上层集合为数组，防止上层集合后续被修改引发竞态；
        /// 4) 重建 IO 快照字典；
        /// 5) 若当前已连接则重新启动监控循环。
        /// </remarks>
        public async ValueTask SetMonitoredIoPointsAsync(IReadOnlyList<PlcIoPoint> ioPoints, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            ioPoints ??= [];

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                StopMonitorLoop();

                // 复制为数组，避免上层集合被修改导致竞态
                var arr = new PlcIoPoint[ioPoints.Count];
                for (var i = 0; i < ioPoints.Count; i++) {
                    arr[i] = ioPoints[i];
                }

                _monitoredIoPoints = arr;
                _snapshot = new Dictionary<PlcIoPoint, PlcIoSignalState>(arr.Length);

                if (IsConnected) {
                    StartMonitorLoop();
                }

                _logger.LogInformation("已设置监控 IO 点数量: {Count}", arr.Length);
            }
            catch (Exception ex) {
                RaiseFaulted("设置监控 IO 点集合异常", ex);
            }
            finally {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// 覆盖式设置需要监控的 DB Bool 点集合，并预计算“批量读取分组”和“点位映射表”。
        /// </summary>
        /// <remarks>
        /// 该方法属于“重计算配置”的入口，允许使用反射；轮询热路径不再反射。
        ///
        /// 复杂步骤说明：
        /// 1) 进入生命周期锁并暂停监控，避免轮询线程读写映射数组；
        /// 2) 逐点解析(反射)得到 db/byte/bit，并过滤非法点位；
        /// 3) 按 DB 计算最小/最大字节范围，形成 `DbReadGroup`（一次 ReadBytesAsync 覆盖一组点位）；
        /// 4) 为每个点位建立 point->group 的映射；
        /// 5) 将点位索引按 group 聚合，轮询时仅按 group 顺序访问点位，避免每次扫描全量点位；
        /// 6) 初始化快照数组 `_dbPointSnapshot`，用于变更检测（未知/高/低）。
        /// </remarks>
        public async ValueTask SetMonitoredDbBoolPointsAsync(IReadOnlyList<PlcDbBoolPoint> points, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            points ??= [];

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                StopMonitorLoop();

                if (points.Count == 0) {
                    ClearDbMonitorState();

                    if (IsConnected) {
                        StartMonitorLoop();
                    }

                    _logger.LogInformation("已清空监控 DB Bool 点集合");
                    return;
                }

                // 解析点位（仅此处反射），轮询路径不做反射
                var tmpPoints = new PlcDbBoolPoint[points.Count];
                var tmpDb = new int[points.Count];
                var tmpByte = new int[points.Count];
                var tmpBit = new int[points.Count];

                var validCount = 0;
                foreach (var p in points) {
                    if (!ReflectionAccessor.TryExtractDbBoolPoint(p, out var dbNumber, out var byteIndex, out var bitIndex)) {
                        RaiseFaulted("DB Bool 点位解析失败（字段缺失或命名不匹配）", new InvalidOperationException(p.GetType().FullName ?? "UnknownType"));
                        continue;
                    }

                    if (dbNumber < 1 || byteIndex < 0 || (uint)bitIndex > 7u) {
                        RaiseFaulted("DB Bool 点位参数非法（db/byte/bit 越界）", new ArgumentOutOfRangeException(p.GetType().FullName ?? "UnknownType"));
                        continue;
                    }

                    tmpPoints[validCount] = p;
                    tmpDb[validCount] = dbNumber;
                    tmpByte[validCount] = byteIndex;
                    tmpBit[validCount] = bitIndex;
                    validCount++;
                }

                if (validCount == 0) {
                    ClearDbMonitorState();

                    if (IsConnected) {
                        StartMonitorLoop();
                    }

                    _logger.LogWarning("监控 DB Bool 点位全部无效，已忽略");
                    return;
                }

                if (validCount != tmpPoints.Length) {
                    Array.Resize(ref tmpPoints, validCount);
                    Array.Resize(ref tmpDb, validCount);
                    Array.Resize(ref tmpByte, validCount);
                    Array.Resize(ref tmpBit, validCount);
                }

                // 计算每个 DB 的最小/最大字节范围，合并为批量读取组
                var ranges = new Dictionary<int, DbByteRange>(capacity: Math.Min(16, validCount));
                for (var i = 0; i < validCount; i++) {
                    var db = tmpDb[i];
                    var b = tmpByte[i];

                    if (!ranges.TryGetValue(db, out var r)) {
                        ranges[db] = new DbByteRange(b, b);
                        continue;
                    }

                    if (b < r.MinByte) r.MinByte = b;
                    if (b > r.MaxByte) r.MaxByte = b;
                    ranges[db] = r;
                }

                var groups = new DbReadGroup[ranges.Count];
                var dbToGroupIndex = new Dictionary<int, int>(ranges.Count);

                var gIndex = 0;
                foreach (var kv in ranges) {
                    var db = kv.Key;
                    var r = kv.Value;

                    var count = checked(r.MaxByte - r.MinByte + 1);
                    groups[gIndex] = new DbReadGroup(db, r.MinByte, count);
                    dbToGroupIndex[db] = gIndex;
                    gIndex++;
                }

                // 点位 -> group 映射
                var pointGroupIndex = new int[validCount];
                for (var i = 0; i < validCount; i++) {
                    pointGroupIndex[i] = dbToGroupIndex[tmpDb[i]];
                }

                // group 聚合索引构建
                // 目的：轮询时按 group 遍历，只遍历该 group 的点位，减少重复扫描。
                var groupPointCount = new int[groups.Length];
                for (var i = 0; i < validCount; i++) {
                    groupPointCount[pointGroupIndex[i]]++;
                }

                var groupPointStart = new int[groups.Length];
                var running = 0;
                for (var g = 0; g < groups.Length; g++) {
                    groupPointStart[g] = running;
                    running += groupPointCount[g];
                }

                var indicesByGroup = new int[validCount];
                var groupCursor = new int[groups.Length];
                for (var i = 0; i < validCount; i++) {
                    var g = pointGroupIndex[i];
                    var pos = groupPointStart[g] + groupCursor[g];
                    indicesByGroup[pos] = i;
                    groupCursor[g]++;
                }

                _monitoredDbBoolPoints = tmpPoints;
                _dbReadGroups = groups;

                _dbPointGroupIndex = pointGroupIndex;
                _dbPointByteIndex = tmpByte;
                _dbPointBitIndex = tmpBit;

                _dbPointSnapshot = new byte[validCount];
                _dbGroupPointStart = groupPointStart;
                _dbGroupPointCount = groupPointCount;
                _dbPointIndicesByGroup = indicesByGroup;

                if (IsConnected) {
                    StartMonitorLoop();
                }

                _logger.LogInformation("已设置监控 DB Bool 点数量: {Count}, DB组数量: {GroupCount}", validCount, groups.Length);
            }
            catch (Exception ex) {
                RaiseFaulted("设置监控 DB Bool 点集合异常", ex);
            }
            finally {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// 手动触发重连：停止监控、关闭旧连接、重新建立连接，并按需恢复监控。
        /// </summary>
        /// <remarks>
        /// - 通过生命周期锁串行化，避免与初始化/设置监控点同时发生；
        /// - 重连成功与否以返回值表示；异常通过 Faulted 上报。
        /// </remarks>
        public async ValueTask<bool> ReconnectAsync(CancellationToken cancellationToken = default) {
            return await ReconnectInternalAsync(stopMonitorLoop: true, reason: "手动重连", cancellationToken).ConfigureAwait(false);
        }

        /// <summary>
        /// 内部重连实现。
        /// </summary>
        /// <remarks>
        /// - `stopMonitorLoop=true`：用于外部显式重连/配置变更重连，先停止监控循环，确保不再访问旧连接；
        /// - `stopMonitorLoop=false`：用于监控循环内的自动重连，禁止停止监控循环，避免自等待导致死锁。
        /// </remarks>
        private async ValueTask<bool> ReconnectInternalAsync(bool stopMonitorLoop, string reason, CancellationToken cancellationToken) {
            if (Volatile.Read(ref _disposed) == 1) {
                return false;
            }

            await _lifecycleGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                SetStatus(PlcStatus.Connecting, $"重连开始: {reason}");

                if (stopMonitorLoop) {
                    StopMonitorLoop();
                }

                // 串行化关闭与重新打开，避免与 PollOnceAsync / 写入并发
                await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
                try {
                    ClosePlc();

                    var ok = await ConnectCoreAsync(cancellationToken).ConfigureAwait(false);
                    SetStatus(ok ? PlcStatus.Connected : PlcStatus.Disconnected, ok ? "重连成功" : "重连失败");
                    return ok;
                }
                finally {
                    _requestGate.Release();
                }
            }
            catch (Exception ex) {
                RaiseFaulted("重连异常", ex);
                SetStatus(PlcStatus.Disconnected, "重连异常");
                return false;
            }
            finally {
                _lifecycleGate.Release();
            }
        }

        /// <summary>
        /// 尝试启动自动重连任务（最多只允许一个重连任务运行）。
        /// </summary>
        private void TryStartAutoReconnect(string reason, CancellationToken cancellationToken) {
            var opt = _optionsMonitor.CurrentValue;
            if (!opt.IsAutoReconnectEnabled) {
                return;
            }

            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            if (IsConnected) {
                return;
            }

            if (System.Threading.Interlocked.CompareExchange(ref _autoReconnectRunning, 1, 0) != 0) {
                return;
            }

            _autoReconnectTask = Task.Run(() => AutoReconnectLoopAsync(reason, cancellationToken));
        }

        /// <summary>
        /// 自动重连循环：指数退避 + 抖动。
        /// </summary>
        private async Task AutoReconnectLoopAsync(string reason, CancellationToken cancellationToken) {
            try {
                var opt = _optionsMonitor.CurrentValue;

                var delayMs = opt.ReconnectInitialDelayMs > 0 ? opt.ReconnectInitialDelayMs : 500;
                var maxDelayMs = opt.ReconnectMaxDelayMs > 0 ? opt.ReconnectMaxDelayMs : 10_000;
                var maxAttempts = opt.ReconnectMaxAttempts;

                var attempt = 0;

                _logger.LogWarning("PLC自动重连已启动: Reason={Reason}, InitialDelay={InitialDelay}ms, MaxDelay={MaxDelay}ms, MaxAttempts={MaxAttempts}",
                    reason, delayMs, maxDelayMs, maxAttempts);

                while (!cancellationToken.IsCancellationRequested && Volatile.Read(ref _disposed) == 0) {
                    if (IsConnected) {
                        return;
                    }

                    attempt++;
                    if (maxAttempts > 0 && attempt > maxAttempts) {
                        _logger.LogError("PLC自动重连已达到最大次数，已停止: Attempts={Attempts}", attempt - 1);
                        return;
                    }

                    var ok = await ReconnectInternalAsync(stopMonitorLoop: false, reason: $"自动重连({reason})#{attempt}", cancellationToken)
                        .ConfigureAwait(false);

                    if (ok) {
                        _logger.LogInformation("PLC自动重连成功: Attempts={Attempts}", attempt);
                        return;
                    }

                    var waitMs = ApplyJitter(delayMs);
                    _logger.LogWarning("PLC自动重连失败，等待后继续: Attempts={Attempts}, NextDelay={Delay}ms", attempt, waitMs);

                    try {
                        await Task.Delay(TimeSpan.FromMilliseconds(waitMs), cancellationToken).ConfigureAwait(false);
                    }
                    catch (OperationCanceledException) {
                        return;
                    }

                    delayMs = delayMs >= maxDelayMs ? maxDelayMs : min(maxDelayMs, delayMs * 2);
                }
            }
            catch (OperationCanceledException) {
                // 正常退出
            }
            catch (Exception ex) {
                RaiseFaulted("PLC自动重连循环异常", ex);
            }
            finally {
                System.Threading.Interlocked.Exchange(ref _autoReconnectRunning, 0);
                _logger.LogInformation("PLC自动重连已停止");
            }

            static int min(int a, int b) => a < b ? a : b;
        }

        /// <summary>
        /// 对延迟做轻量抖动，避免多个实例同时重连造成尖峰。
        /// </summary>
        private static int ApplyJitter(int delayMs) {
            if (delayMs <= 0) {
                return 0;
            }

            // ±10% 抖动，最小 1ms
            var jitter = (int)(delayMs * 0.1);
            if (jitter <= 0) {
                return delayMs;
            }

            var delta = Random.Shared.Next(-jitter, jitter + 1);
            var result = delayMs + delta;
            return result <= 0 ? 1 : result;
        }

        /// <summary>
        /// 写入一个 IO 输出点的电平状态（仅允许写输出点）。
        /// </summary>
        /// <remarks>
        /// 关键点：
        /// - 写入前校验：连接状态、PLC 实例存在、点位类型必须为输出、点位号可转换成 byte/bit；
        /// - 使用 `_requestGate` 将所有 S7 请求串行化，避免 S7.NetPlus 并发引发异常；
        /// - 写超时由配置控制，超时/异常会标记断开并上报 Faulted。
        /// </remarks>
        public async ValueTask WriteIoAsync(PlcIoPoint ioPoint, PlcIoSignalState state, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            if (!IsConnected) {
                _logger.LogWarning("PLC 未连接，写 IO 被忽略: {Point}", ioPoint.Point);
                return;
            }

            var plc = _plc;
            if (plc is null) {
                _logger.LogWarning("PLC 实例为空，写 IO 被忽略: {Point}", ioPoint.Point);
                return;
            }

            if (!IsOutput(ioPoint)) {
                _logger.LogWarning("非输出点位禁止写入: {Point}, Type={Type}", ioPoint.Point, ioPoint.Type);
                return;
            }

            if (!TryToBitAddress(ioPoint.Point, out var byteIndex, out var bitIndex)) {
                RaiseFaulted($"点位编号非法，写 IO 被忽略: Point={ioPoint.Point}", new ArgumentOutOfRangeException(nameof(ioPoint.Point)));
                return;
            }

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _optionsMonitor.CurrentValue;

                using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.WriteTimeoutMs > 0) {
                    writeCts.CancelAfter(TimeSpan.FromMilliseconds(opt.WriteTimeoutMs));
                }

                await plc.WriteBitAsync(
                        DataType.Output,
                        db: 0,
                        startByteAdr: byteIndex,
                        bitAdr: bitIndex,
                        value: state == PlcIoSignalState.High,
                        writeCts.Token)
                    .ConfigureAwait(false);
            }
            catch (Exception ex) {
                MarkDisconnected("写 IO 异常");
                RaiseFaulted($"写 IO 异常: Point={ioPoint.Point}", ex);
            }
            finally {
                _requestGate.Release();
            }
        }

        /// <summary>
        /// 读取一个 Int32 值（失败返回 null；异常通过 Faulted 上报）。
        /// </summary>
        /// <remarks>
        /// - 地址对象允许不同命名字段，使用反射做兼容解析；
        /// - 使用 `ReadBytesAsync` 读取 4 字节；
        /// - S7 多字节为大端序，因此使用 `ReadInt32BigEndian`。
        /// </remarks>
        public async ValueTask<int?> ReadInt32Async(PlcInt32Address address, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return null;
            }

            if (!IsConnected) {
                _logger.LogWarning("PLC 未连接，读取 Int32 被忽略");
                return null;
            }

            var plc = _plc;
            if (plc is null) {
                _logger.LogWarning("PLC 实例为空，读取 Int32 被忽略");
                return null;
            }

            if (!ReflectionAccessor.TryExtractInt32Address(address, out var area, out var dbNumber, out var startByteAdr)) {
                RaiseFaulted("读取 Int32 地址解析失败（字段缺失或命名不匹配）", new InvalidOperationException(address.GetType().FullName ?? "UnknownType"));
                return null;
            }

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _optionsMonitor.CurrentValue;

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.ReadTimeoutMs > 0) {
                    readCts.CancelAfter(TimeSpan.FromMilliseconds(opt.ReadTimeoutMs));
                }

                var bytes = await plc.ReadBytesAsync(
                        area,
                        db: dbNumber,
                        startByteAdr: startByteAdr,
                        count: 4,
                        readCts.Token)
                    .ConfigureAwait(false);

                if (bytes.Length < 4) {
                    return null;
                }

                // S7 多字节按大端序
                return BinaryPrimitives.ReadInt32BigEndian(bytes.AsSpan(0, 4));
            }
            catch (Exception ex) {
                MarkDisconnected("读取 Int32 异常");
                RaiseFaulted("读取 Int32 异常", ex);
                return null;
            }
            finally {
                _requestGate.Release();
            }
        }

        /// <summary>
        /// 读取一个 S7 STRING 字符串（失败返回 null；异常通过 Faulted 上报）。
        /// </summary>
        /// <remarks>
        /// 当前实现读取 S7 STRING（2 字节头：最大长度/当前长度 + ASCII 内容）。
        /// - `maxLen` 上限做了保护，避免误配置导致超大读取；
        /// - 如果 PLC 返回的当前长度超过本次读取的有效数据，会截断到可用范围。
        /// </remarks>
        public async ValueTask<string?> ReadStringAsync(PlcStringAddress address, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return null;
            }

            if (!IsConnected) {
                _logger.LogWarning("PLC 未连接，读取 String 被忽略");
                return null;
            }

            var plc = _plc;
            if (plc is null) {
                _logger.LogWarning("PLC 实例为空，读取 String 被忽略");
                return null;
            }

            if (!ReflectionAccessor.TryExtractStringAddress(address, out var area, out var dbNumber, out var startByteAdr, out var maxLen)) {
                RaiseFaulted("读取 String 地址解析失败（字段缺失或命名不匹配）", new InvalidOperationException(address.GetType().FullName ?? "UnknownType"));
                return null;
            }

            if (maxLen <= 0) {
                maxLen = 254;
            }
            if (maxLen > 1024) {
                RaiseFaulted("读取 String 最大长度过大，已拒绝", new ArgumentOutOfRangeException(nameof(maxLen)));
                return null;
            }

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _optionsMonitor.CurrentValue;

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.ReadTimeoutMs > 0) {
                    readCts.CancelAfter(TimeSpan.FromMilliseconds(opt.ReadTimeoutMs));
                }

                // S7 STRING：2字节头 + 内容（ASCII）
                var count = checked(maxLen + 2);
                var bytes = await plc.ReadBytesAsync(area, db: dbNumber, startByteAdr: startByteAdr, count: count, readCts.Token).ConfigureAwait(false);

                if (bytes.Length < 2) {
                    return null;
                }

                var currentLen = bytes[1];
                if (currentLen <= 0) {
                    return string.Empty;
                }

                var available = bytes.Length - 2;
                if (currentLen > available) {
                    currentLen = (byte)available;
                }

                return Encoding.ASCII.GetString(bytes, 2, currentLen);
            }
            catch (Exception ex) {
                MarkDisconnected("读取 String 异常");
                RaiseFaulted("读取 String 异常", ex);
                return null;
            }
            finally {
                _requestGate.Release();
            }
        }

        /// <summary>
        /// 写入多个 DB Bool（允许不连续），内部采用“按 DB 分组 + 读改写合并”策略。
        /// </summary>
        /// <remarks>
        /// 复杂步骤说明：
        /// 1) 入口处解析写入项（反射兼容），过滤非法项；
        /// 2) 使用 `_requestGate` 串行化请求；
        /// 3) 将写入按 DB 分组，并对每个 DB：
        ///    - 计算该 DB 需要写入的最小/最大字节范围；
        ///    - 先 ReadBytesAsync 读出这段原始字节；
        ///    - 在本地 buffer 上按 bit 修改；
        ///    - 再 WriteBytesAsync 一次性写回；
        /// 4) 如果读出的长度不符合预期，跳过该组并通过 Faulted 上报。
        /// </remarks>
        public async ValueTask WriteDbBoolsAsync(IReadOnlyList<PlcDbBoolWriteItem> items, CancellationToken cancellationToken = default) {
            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            if (items.Count == 0) {
                return;
            }

            if (!IsConnected) {
                _logger.LogWarning("PLC 未连接，写 DB Bool 被忽略: Count={Count}", items.Count);
                return;
            }

            var plc = _plc;
            if (plc is null) {
                _logger.LogWarning("PLC 实例为空，写 DB Bool 被忽略: Count={Count}", items.Count);
                return;
            }

            // 解析写入项（仅此处反射）
            var writes = new List<DbBoolWrite>(items.Count);
            foreach (var t in items) {
                if (!ReflectionAccessor.TryExtractDbBoolWriteItem(t, out var dbNumber, out var byteIndex, out var bitIndex, out var value)) {
                    RaiseFaulted("写 DB Bool 项解析失败（字段缺失或命名不匹配）", new InvalidOperationException(t.GetType().FullName ?? "UnknownType"));
                    continue;
                }

                if (dbNumber < 1 || byteIndex < 0 || (uint)bitIndex > 7u) {
                    RaiseFaulted("写 DB Bool 项参数非法（db/byte/bit 越界）", new ArgumentOutOfRangeException(t.GetType().FullName ?? "UnknownType"));
                    continue;
                }

                writes.Add(new DbBoolWrite(dbNumber, byteIndex, bitIndex, value));
            }

            if (writes.Count == 0) {
                return;
            }

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _optionsMonitor.CurrentValue;

                using var readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.ReadTimeoutMs > 0) {
                    readCts.CancelAfter(TimeSpan.FromMilliseconds(opt.ReadTimeoutMs));
                }

                using var writeCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.WriteTimeoutMs > 0) {
                    writeCts.CancelAfter(TimeSpan.FromMilliseconds(opt.WriteTimeoutMs));
                }

                // 按 DB 分组，采用“读-改-写”合并，避免逐 bit 多次写入
                writes.Sort(static (a, b) => a.DbNumber != b.DbNumber ? a.DbNumber.CompareTo(b.DbNumber) : a.ByteIndex.CompareTo(b.ByteIndex));

                var start = 0;
                while (start < writes.Count) {
                    var db = writes[start].DbNumber;

                    var minByte = writes[start].ByteIndex;
                    var maxByte = minByte;

                    var end = start + 1;
                    while (end < writes.Count && writes[end].DbNumber == db) {
                        var b = writes[end].ByteIndex;
                        if (b < minByte) minByte = b;
                        if (b > maxByte) maxByte = b;
                        end++;
                    }

                    var count = checked(maxByte - minByte + 1);

                    var buffer = await plc.ReadBytesAsync(
                        DataType.DataBlock,
                        db: db,
                        startByteAdr: minByte,
                        count: count,
                        readCts.Token).ConfigureAwait(false);

                    if (buffer.Length != count) {
                        RaiseFaulted("写 DB Bool 读取原字节失败", new InvalidOperationException($"DB{db}, Start={minByte}, Count={count}"));
                        start = end;
                        continue;
                    }

                    for (var i = start; i < end; i++) {
                        var w = writes[i];
                        var offset = w.ByteIndex - minByte;

                        var mask = (byte)(1 << w.BitIndex);
                        if (w.Value) {
                            buffer[offset] |= mask;
                        }
                        else {
                            buffer[offset] &= (byte)~mask;
                        }
                    }

                    await plc.WriteBytesAsync(
                        DataType.DataBlock,
                        db: db,
                        startByteAdr: minByte,
                        value: buffer,
                        writeCts.Token).ConfigureAwait(false);

                    start = end;
                }
            }
            catch (Exception ex) {
                MarkDisconnected("写 DB Bool 异常");
                RaiseFaulted("写 DB Bool 异常", ex);
            }
            finally {
                _requestGate.Release();
            }
        }

        /// <summary>
        /// 释放资源：停止监控、关闭 PLC 连接、取消配置订阅，并释放信号量。
        /// </summary>
        /// <remarks>
        /// - 该方法可安全重复调用；
        /// - 会等待监控任务结束（同步等待），确保退出时不会继续访问已释放资源。
        /// </remarks>
        public void Dispose() {
            if (Interlocked.Exchange(ref _disposed, 1) == 1) {
                return;
            }

            try { _optionsChangeToken?.Dispose(); } catch { }
            try { StopMonitorLoop(); } catch { }
            try { ClosePlc(); } catch { }

            _lifecycleGate.Dispose();
            _requestGate.Dispose();
        }

        /// <summary>
        /// 建立 PLC 连接的核心逻辑：创建 `Plc` 实例、打开连接、设置状态并启动监控。
        /// </summary>
        /// <remarks>
        /// 步骤：
        /// 1) 读取最新连接配置；
        /// 2) 映射 CPU 类型并构建 `Plc`；
        /// 3) 以超时控制打开连接；
        /// 4) 成功则写入 `_plc`，标记连接状态，并启动监控；
        /// 5) 失败/异常则清理对象、标记断开与故障码，并通过 Faulted 上报异常。
        /// </remarks>
        private async Task<bool> ConnectCoreAsync(CancellationToken cancellationToken) {
            var opt = _optionsMonitor.CurrentValue;

            if (string.IsNullOrWhiteSpace(opt.Host)) {
                RaiseFaulted("PLC Host 为空，连接已取消", new ArgumentException("PLC Host 不能为空", nameof(opt.Host)));
                Volatile.Write(ref _isConnected, 0);
                Volatile.Write(ref _faultCode, -1);
                return false;
            }

            var (ip, port) = NormalizeHostAndPort(opt.Host, opt.Port);

            var cpuType = MapCpuType(opt.CpuType);
            Plc plc;
            try {
                plc = new Plc(cpuType, ip, port, (short)opt.Rack, (short)opt.Slot);
            }
            catch (Exception ex) {
                RaiseFaulted("PLC构造失败（Host/Port/Rack/Slot 可能不合法）", ex);
                Volatile.Write(ref _isConnected, 0);
                Volatile.Write(ref _faultCode, -1);
                return false;
            }

            _logger.LogInformation("PLC连接开始: Ip={Ip}, Port={Port}, Rack={Rack}, Slot={Slot}, CpuType={CpuType}", ip, port, opt.Rack, opt.Slot, opt.CpuType);

            try {
                using var connectCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                if (opt.ConnectTimeoutMs > 0) {
                    connectCts.CancelAfter(TimeSpan.FromMilliseconds(opt.ConnectTimeoutMs));
                }

                await plc.OpenAsync(connectCts.Token).ConfigureAwait(false);

                if (!plc.IsConnected) {
                    _logger.LogWarning("PLC连接失败: IsConnected=false");
                    Volatile.Write(ref _isConnected, 0);
                    Volatile.Write(ref _faultCode, -1);
                    SafeCloseAndDispose(plc);
                    return false;
                }

                _plc = plc;
                Volatile.Write(ref _isConnected, 1);

                // 无故障时必须为 null（即 _faultCode < 0）
                Volatile.Write(ref _faultCode, -1);

                StartMonitorLoop();

                _logger.LogInformation("PLC连接成功");
                return true;
            }
            catch (OperationCanceledException oce) {
                _logger.LogWarning(oce, "PLC连接取消或超时");
                SafeCloseAndDispose(plc);

                Volatile.Write(ref _isConnected, 0);
                Volatile.Write(ref _faultCode, -2);
                return false;
            }
            catch (Exception ex) {
                _logger.LogError(ex, "PLC连接异常");
                SafeCloseAndDispose(plc);

                Volatile.Write(ref _isConnected, 0);
                Volatile.Write(ref _faultCode, -1);

                RaiseFaulted("PLC连接异常", ex);
                return false;
            }
        }

        /// <summary>
        /// 规范化 Host/Port：支持 Host=IP/HostName/URI/"IP:Port"。
        /// </summary>
        private static (string Ip, int Port) NormalizeHostAndPort(string host, int defaultPort) {
            var trimmed = host.Trim();

            // 支持传入 URI（例如 http://10.0.0.1:102）
            if (Uri.TryCreate(trimmed, UriKind.Absolute, out var uri) && !string.IsNullOrWhiteSpace(uri.Host)) {
                trimmed = uri.Host;
                if (uri.Port > 0) {
                    defaultPort = uri.Port;
                }
            }

            // 支持 "IP:Port" 或 "Host:Port"（不处理 IPv6 复杂场景）
            var colonIndex = trimmed.LastIndexOf(':');
            if (colonIndex > 0 && colonIndex < trimmed.Length - 1 && trimmed.IndexOf(':') == colonIndex) {
                var hostPart = trimmed[..colonIndex];
                var portPart = trimmed[(colonIndex + 1)..];

                if (int.TryParse(portPart, out var parsedPort) && parsedPort > 0 && parsedPort <= 65535) {
                    trimmed = hostPart;
                    defaultPort = parsedPort;
                }
            }

            if (IPAddress.TryParse(trimmed, out var ipAddress)) {
                return (ipAddress.ToString(), defaultPort);
            }

            // HostName 解析为 IP：优先 IPv4
            try {
                var addresses = Dns.GetHostAddresses(trimmed);
                var ipv4 = addresses.FirstOrDefault(a => a.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork);
                if (ipv4 is not null) {
                    return (ipv4.ToString(), defaultPort);
                }

                var any = addresses.FirstOrDefault();
                if (any is not null) {
                    return (any.ToString(), defaultPort);
                }
            }
            catch {
                // ignored
            }

            throw new ArgumentException($"PLC Host 无法解析为有效 IP: {host}", nameof(host));
        }

        /// <summary>
        /// 启动监控轮询循环（如果已连接且存在监控点位且轮询间隔大于 0）。
        /// </summary>
        /// <remarks>
        /// - 若 IO/DB 均无监控点位，则不启动；
        /// - 若已存在未完成的监控任务，则不重复启动；
        /// - 启动前会先调用 `StopMonitorLoop` 清理旧任务/CTS，避免残留。
        /// </remarks>
        private void StartMonitorLoop() {
            if (Volatile.Read(ref _disposed) == 1) {
                return;
            }

            // IO/DB 均无监控点位时不启动轮询
            if (_monitoredIoPoints.Length == 0 && _monitoredDbBoolPoints.Length == 0) {
                return;
            }

            var opt = _optionsMonitor.CurrentValue;
            if (opt.MonitorPollIntervalMs <= 0) {
                _logger.LogInformation("PLC监控循环已禁用: MonitorPollIntervalMs={Interval}", opt.MonitorPollIntervalMs);
                return;
            }

            var task = _monitorTask;
            if (task is not null && !task.IsCompleted) {
                return;
            }

            StopMonitorLoop();

            _monitorCts = new CancellationTokenSource();
            _monitorTask = Task.Run(() => MonitorLoopAsync(_monitorCts.Token));
        }

        /// <summary>
        /// 停止监控轮询循环（取消 token 并等待任务退出），并清理相关资源。
        /// </summary>
        /// <remarks>
        /// - 该方法同步等待 `_monitorTask` 结束，确保不会继续访问已更新/释放的字段；
        /// - 用 try/catch 吞掉取消/等待中的异常，避免干扰调用方控制流。
        /// </remarks>
        private void StopMonitorLoop() {
            var cts = _monitorCts;
            if (cts is not null) {
                try { cts.Cancel(); } catch { }
            }

            var task = _monitorTask;
            if (task is not null) {
                try { task.GetAwaiter().GetResult(); } catch { }
            }

            try { _monitorCts?.Dispose(); } catch { }

            _monitorCts = null;
            _monitorTask = null;
        }

        /// <summary>
        /// 监控循环：按固定周期调用 `PollOnceAsync` 采集数据并触发事件。
        /// </summary>
        /// <remarks>
        /// - 使用 `PeriodicTimer` 控制采样周期；
        /// - 发生异常时标记断开并通过 Faulted 上报；
        /// - 正常取消属于正常退出路径。
        /// </remarks>
        private async Task MonitorLoopAsync(CancellationToken cancellationToken) {
            var opt = _optionsMonitor.CurrentValue;
            var intervalMs = opt.MonitorPollIntervalMs;

            _logger.LogInformation("PLC监控循环已启动: Interval={Interval}ms", intervalMs);

            try {
                using var timer = new PeriodicTimer(TimeSpan.FromMilliseconds(intervalMs));

                while (await timer.WaitForNextTickAsync(cancellationToken).ConfigureAwait(false)) {
                    // 断开时优先触发自动重连，避免高频轮询导致日志噪音与无效请求
                    if (!IsConnected) {
                        TryStartAutoReconnect("监控循环检测到断开", cancellationToken);
                        _logger.LogInformation("监控循环检测到断开");
                        continue;
                    }

                    await PollOnceAsync(cancellationToken).ConfigureAwait(false);
                }
            }
            catch (OperationCanceledException) {
                // 正常退出
            }
            catch (Exception ex) {
                MarkDisconnected("监控循环异常");
                RaiseFaulted("监控循环异常", ex);
            }
            finally {
                _logger.LogInformation("PLC监控循环已停止");
            }
        }

        /// <summary>
        /// 执行一次轮询：批量读取 IO/DB 数据，检测变化并触发相应事件。
        /// </summary>
        /// <remarks>
        /// 复杂步骤说明（IO 部分）：
        /// 1) 读取当前监控点位快照（数组引用），避免期间被替换；
        /// 2) 统计 Input/Output 所需读取的最小/最大字节范围；
        /// 3) 进入 `_requestGate` 串行化读操作；
        /// 4) 按范围批量 ReadBytesAsync（减少通讯次数）；
        /// 5) 本地按 bit 拆分计算每个点位状态，与 `_snapshot` 对比后触发 `IoChanged`。
        ///
        /// 复杂步骤说明（DB Bool 部分）：
        /// 1) 轮询按 `_dbReadGroups` 分组读取（每个 DB 一次读取一个连续范围）；
        /// 2) 对该 group 内的点位索引（`_dbPointIndicesByGroup`）逐个取 bit；
        /// 3) 与 `_dbPointSnapshot` 对比，收集变化项；
        /// 4) 如果有变化则批量触发 `DbBoolsChanged`。
        /// </remarks>
        private async Task PollOnceAsync(CancellationToken cancellationToken) {
            var ioPoints = _monitoredIoPoints;
            var dbPoints = _monitoredDbBoolPoints;

            if (ioPoints.Length == 0 && dbPoints.Length == 0) {
                return;
            }

            var plc = _plc;
            if (plc is null || !plc.IsConnected) {
                MarkDisconnected("监控读取前检测到断开");
                return;
            }

            // 计算 Input/Output 字节范围（批量读取）
            var hasInput = false;
            var hasOutput = false;

            var inMin = int.MaxValue;
            var inMax = int.MinValue;
            var outMin = int.MaxValue;
            var outMax = int.MinValue;

            foreach (var p in ioPoints) {
                if (!TryToBitAddress(p.Point, out var byteIndex, out _)) {
                    continue;
                }

                if (IsInput(p)) {
                    hasInput = true;
                    if (byteIndex < inMin) inMin = byteIndex;
                    if (byteIndex > inMax) inMax = byteIndex;
                }
                else if (IsOutput(p)) {
                    hasOutput = true;
                    if (byteIndex < outMin) outMin = byteIndex;
                    if (byteIndex > outMax) outMax = byteIndex;
                }
            }

            // 变化列表放到 gate 外触发，避免事件处理逻辑阻塞监控热路径
            List<(PlcIoPoint Point, PlcIoSignalState OldState, PlcIoSignalState NewState)>? ioChanges = null;
            List<DbBoolChange>? dbChanges = null;
            var occurredAt = DateTimeOffset.MinValue;

            await _requestGate.WaitAsync(cancellationToken).ConfigureAwait(false);
            try {
                var opt = _optionsMonitor.CurrentValue;

                // 一个轮询周期只创建一个 CTS（避免每次读都 CreateLinkedTokenSource + TryReset 的竞态）
                CancellationTokenSource? readCts = null;
                var readToken = cancellationToken;

                if (opt.ReadTimeoutMs > 0) {
                    readCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
                    readToken = readCts.Token;
                }

                try {
                    async ValueTask<byte[]> ReadBytesWithTimeoutAsync(DataType area, int db, int startByteAdr, int count) {
                        if (readCts is not null) {
                            // 通过 CancelAfter 设置本次读的超时
                            readCts.CancelAfter(TimeSpan.FromMilliseconds(opt.ReadTimeoutMs));
                        }

                        try {
                            return await plc.ReadBytesAsync(area, db: db, startByteAdr: startByteAdr, count: count, readToken).ConfigureAwait(false);
                        }
                        finally {
                            if (readCts is not null) {
                                // 关闭定时器，避免跨调用串扰
                                readCts.CancelAfter(Timeout.InfiniteTimeSpan);
                            }
                        }
                    }

                    byte[]? inputBytes = null;
                    byte[]? outputBytes = null;

                    if (hasInput) {
                        var inCount = checked(inMax - inMin + 1);
                        inputBytes = await ReadBytesWithTimeoutAsync(DataType.Input, db: 0, startByteAdr: inMin, count: inCount).ConfigureAwait(false);
                    }

                    if (hasOutput) {
                        var outCount = checked(outMax - outMin + 1);
                        outputBytes = await ReadBytesWithTimeoutAsync(DataType.Output, db: 0, startByteAdr: outMin, count: outCount).ConfigureAwait(false);
                    }

                    occurredAt = DateTimeOffset.Now;

                    // IO 变化检测（先聚合，后触发事件）
                    foreach (var p in ioPoints) {
                        if (!TryToBitAddress(p.Point, out var byteIndex, out var bitIndex)) {
                            continue;
                        }

                        var isOn = false;

                        if (IsInput(p)) {
                            if (inputBytes is null) continue;
                            var offset = byteIndex - inMin;
                            if ((uint)offset >= (uint)inputBytes.Length) continue;
                            isOn = (inputBytes[offset] & (1 << bitIndex)) != 0;
                        }
                        else if (IsOutput(p)) {
                            if (outputBytes is null) continue;
                            var offset = byteIndex - outMin;
                            if ((uint)offset >= (uint)outputBytes.Length) continue;
                            isOn = (outputBytes[offset] & (1 << bitIndex)) != 0;
                        }
                        else {
                            continue;
                        }

                        var newState = isOn ? PlcIoSignalState.High : PlcIoSignalState.Low;

                        if (_snapshot.TryGetValue(p, out var oldState)) {
                            if (oldState == newState) {
                                continue;
                            }

                            _snapshot[p] = newState;

                            ioChanges ??= new List<(PlcIoPoint, PlcIoSignalState, PlcIoSignalState)>(capacity: 8);
                            ioChanges.Add((p, oldState, newState));
                            continue;
                        }

                        _snapshot[p] = newState;

                        // 首次采集也推一次，便于上层建立初始状态
                        var initOld = newState == PlcIoSignalState.High ? PlcIoSignalState.Low : PlcIoSignalState.High;
                        ioChanges ??= new List<(PlcIoPoint, PlcIoSignalState, PlcIoSignalState)>(capacity: 8);
                        ioChanges.Add((p, initOld, newState));
                    }

                    // DB Bool 变化检测（先聚合，后触发事件）
                    if (_dbReadGroups.Length > 0 && dbPoints.Length > 0) {
                        for (var g = 0; g < _dbReadGroups.Length; g++) {
                            var group = _dbReadGroups[g];

                            var bytes = await ReadBytesWithTimeoutAsync(
                                DataType.DataBlock,
                                db: group.DbNumber,
                                startByteAdr: group.StartByteAdr,
                                count: group.Count).ConfigureAwait(false);

                            if (bytes.Length != group.Count) {
                                continue;
                            }

                            var start = _dbGroupPointStart[g];
                            var count = _dbGroupPointCount[g];

                            for (var k = 0; k < count; k++) {
                                var idx = _dbPointIndicesByGroup[start + k];

                                var byteIndex = _dbPointByteIndex[idx];
                                var bitIndex = _dbPointBitIndex[idx];

                                var offset = byteIndex - group.StartByteAdr;
                                if ((uint)offset >= (uint)bytes.Length) {
                                    continue;
                                }

                                var isOn = (bytes[offset] & (1 << bitIndex)) != 0;
                                var newSnapshot = isOn ? (byte)2 : (byte)1;

                                var oldSnapshot = _dbPointSnapshot[idx];
                                if (oldSnapshot == newSnapshot) {
                                    continue;
                                }

                                var newState = isOn ? PlcIoSignalState.High : PlcIoSignalState.Low;
                                var oldState = oldSnapshot switch {
                                    2 => PlcIoSignalState.High,
                                    1 => PlcIoSignalState.Low,
                                    _ => newState == PlcIoSignalState.High ? PlcIoSignalState.Low : PlcIoSignalState.High
                                };

                                _dbPointSnapshot[idx] = newSnapshot;

                                dbChanges ??= new List<DbBoolChange>(capacity: 8);
                                dbChanges.Add(new DbBoolChange(_monitoredDbBoolPoints[idx], oldState, newState));
                            }
                        }
                    }
                }
                finally {
                    // readCts 在上面创建，这里统一释放
                    // 注释：未使用 TryReset，避免 CTS 复用在定时器回调边界的竞态
                }
            }
            catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested) {
                // 服务停止触发的取消：不属于断线/异常
                _logger.LogInformation(ex, "监控读取已取消");
                return;
            }
            catch (OperationCanceledException ex) {
                // CancelAfter 触发：读超时
                MarkDisconnected("监控读取超时");
                RaiseFaulted("监控读取超时", ex);
                _logger.LogWarning(ex, "监控读取超时");
                return;
            }
            catch (Exception ex) {
                MarkDisconnected("监控读取异常");
                RaiseFaulted("监控读取异常", ex);
                _logger.LogError(ex, "监控读取异常");
                return;
            }
            finally {
                _requestGate.Release();
            }

            // gate 外触发事件，避免业务处理占用 requestGate
            if (ioChanges is not null) {
                foreach (var (point, oldState, newState) in ioChanges) {
                    RaiseIoChanged(point, oldState, newState, occurredAt);
                }
            }

            if (dbChanges is not null && dbChanges.Count > 0) {
                RaiseDbBoolsChanged(dbChanges, occurredAt);
            }
        }

        /// <summary>
        /// 触发 DB Bool 批量变化事件，并尽最大努力通过反射填充载荷字段。
        /// </summary>
        /// <remarks>
        /// 由于 `PlcDbBoolsChangedEventArgs` 可能是 record struct/record class 且包含 required 字段，
        /// 这里采用“先装箱 + 反射设置属性/字段”的方式进行兼容填充：
        /// - 设置 OccurredAt/Timestamp；
        /// - 尝试将 changes 映射到 Items/Changes/ChangedItems/Points 等常见集合字段；
        /// - 如果无法映射，也仍然触发事件（上层可以只依赖事件信号）。
        /// </remarks>
        private void RaiseDbBoolsChanged(List<DbBoolChange> changes, DateTimeOffset occurredAt) {
            try {
                // 兼容 record struct / record class：跳过 required 编译约束，运行期用反射尽量填充
                PlcDbBoolsChangedEventArgs args = default;
                object boxed = args;

                ReflectionAccessor.TrySet(boxed, "OccurredAt", occurredAt);
                ReflectionAccessor.TrySet(boxed, "Timestamp", occurredAt);

                // 常见集合命名：Items / Changes / ChangedItems / Points
                var mapped = ReflectionAccessor.TrySetChangesCollection(boxed, changes);

                args = (PlcDbBoolsChangedEventArgs)boxed;

                DbBoolsChanged?.Invoke(this, args);

                if (!mapped) {
                    _logger.LogDebug("DB Bool 变动事件载荷集合字段未匹配，仅已触发事件");
                }
            }
            catch (Exception ex) {
                RaiseFaulted("DB Bool变动事件回调异常", ex);
            }
        }

        /// <summary>
        /// 清空所有 DB Bool 监控相关状态（点位、分组、映射、快照）。
        /// </summary>
        private void ClearDbMonitorState() {
            _monitoredDbBoolPoints = [];
            _dbReadGroups = [];

            _dbPointGroupIndex = [];
            _dbPointByteIndex = [];
            _dbPointBitIndex = [];

            _dbPointSnapshot = [];

            _dbGroupPointStart = [];
            _dbGroupPointCount = [];
            _dbPointIndicesByGroup = [];
        }

        /// <summary>
        /// 关闭并释放当前 PLC 连接实例，并将连接标志置为断开。
        /// </summary>
        private void ClosePlc() {
            var plc = Interlocked.Exchange(ref _plc, null);
            if (plc is null) {
                return;
            }

            SafeCloseAndDispose(plc);
            Volatile.Write(ref _isConnected, 0);
        }

        /// <summary>
        /// 安全关闭并释放 `Plc`：吞掉 Close/Dispose 中的异常，避免影响调用链。
        /// </summary>
        private static void SafeCloseAndDispose(Plc plc) {
            try { plc.Close(); } catch { }
            try { (plc as IDisposable)?.Dispose(); } catch { }
        }

        /// <summary>
        /// 标记为断开状态，并更新故障码/状态事件。
        /// </summary>
        /// <param name="reason">断开原因（用于状态事件与日志）。</param>
        private void MarkDisconnected(string reason) {
            Volatile.Write(ref _isConnected, 0);
            Volatile.Write(ref _faultCode, -1);
            SetStatus(PlcStatus.Disconnected, reason);

            // 断开后尝试触发自动重连（无阻塞）
            TryStartAutoReconnect(reason, CancellationToken.None);
        }

        /// <summary>
        /// 更新 PLC 状态并触发 `StatusChanged` 事件。
        /// </summary>
        /// <remarks>
        /// - 使用 `Interlocked.Exchange` 原子更新状态；
        /// - 若状态未变化则不触发事件；
        /// - 回调异常不会向外抛出，而是通过 Faulted 上报。
        /// </remarks>
        private void SetStatus(PlcStatus newStatus, string? reason) {
            var old = (PlcStatus)Interlocked.Exchange(ref _status, (int)newStatus);
            if (old == newStatus) {
                return;
            }

            try {
                StatusChanged?.Invoke(this, new PlcStatusChangedEventArgs {
                    OldStatus = old,
                    NewStatus = newStatus,
                    Reason = reason,
                    OccurredAt = DateTimeOffset.Now
                });
            }
            catch (Exception ex) {
                RaiseFaulted("状态变更事件回调异常", ex);
            }
        }

        /// <summary>
        /// 上报故障：记录错误日志并触发 `Faulted` 事件（隔离异常，不影响调用链）。
        /// </summary>
        private void RaiseFaulted(string message, Exception exception) {
            _logger.LogError(exception, "{Message}", message);

            try {
                Faulted?.Invoke(this, new PlcFaultedEventArgs {
                    Message = message,
                    Exception = exception,
                    OccurredAt = DateTimeOffset.Now
                });
            }
            catch { }
        }

        /// <summary>
        /// 触发 IO 点位变动事件 `IoChanged`。
        /// </summary>
        private void RaiseIoChanged(PlcIoPoint ioPoint, PlcIoSignalState oldState, PlcIoSignalState newState, DateTimeOffset occurredAt) {
            try {
                IoChanged?.Invoke(this, new PlcIoChangedEventArgs {
                    IoPoint = ioPoint,
                    OldState = oldState,
                    NewState = newState,
                    OccurredAt = occurredAt
                });
            }
            catch (Exception ex) {
                RaiseFaulted("IO变动事件回调异常", ex);
            }
        }

        /// <summary>
        /// 判断点位是否为输入（数字量输入）。
        /// </summary>
        private static bool IsInput(PlcIoPoint ioPoint) => ioPoint.Type == PlcIoPointType.DigitalInput;

        /// <summary>
        /// 判断点位是否为输出（数字量输出）。
        /// </summary>
        private static bool IsOutput(PlcIoPoint ioPoint) => ioPoint.Type == PlcIoPointType.DigitalOutput;

        /// <summary>
        /// 将“点位编号”(bit offset)转换为 S7 访问所需的 byteIndex + bitIndex。
        /// </summary>
        /// <remarks>
        /// 约定：point 表示 bit 的线性编号，换算：
        /// - byteIndex = point / 8
        /// - bitIndex  = point % 8
        /// </remarks>
        private static bool TryToBitAddress(int point, out int byteIndex, out int bitIndex) {
            if (point < 0) {
                byteIndex = 0;
                bitIndex = 0;
                return false;
            }

            byteIndex = point >> 3;
            bitIndex = point & 0b111;
            return true;
        }

        /// <summary>
        /// 将项目内的 `S7CpuType` 映射为 S7.Net 的 `CpuType`。
        /// </summary>
        private static CpuType MapCpuType(S7CpuType cpuType) {
            return cpuType switch {
                S7CpuType.S7200 => CpuType.S7200,
                S7CpuType.S7300 => CpuType.S7300,
                S7CpuType.S7400 => CpuType.S7400,
                S7CpuType.S71200 => CpuType.S71200,
                S7CpuType.S71500 => CpuType.S71500,
                _ => CpuType.S71200
            };
        }

        /// <summary>
        /// DB 批量读取分组：一次读取一个 DB 中连续的字节范围。
        /// </summary>
        private readonly record struct DbReadGroup(int DbNumber, int StartByteAdr, int Count);

        /// <summary>
        /// DB 字节范围（用于计算分组的最小/最大字节偏移）。
        /// </summary>
        private struct DbByteRange {
            public int MinByte;
            public int MaxByte;

            /// <summary>
            /// 构造一个字节范围。
            /// </summary>
            public DbByteRange(int min, int max) {
                MinByte = min;
                MaxByte = max;
            }
        }

        /// <summary>
        /// DB Bool 变化项（内部使用的结构，用于批量上报）。
        /// </summary>
        private readonly record struct DbBoolChange(PlcDbBoolPoint Point, PlcIoSignalState OldState, PlcIoSignalState NewState);

        /// <summary>
        /// DB Bool 写入项（内部使用的结构，反射解析后的统一表示）。
        /// </summary>
        private readonly record struct DbBoolWrite(int DbNumber, int ByteIndex, int BitIndex, bool Value);

        /// <summary>
        /// 反射解析器：只用于低频入口（设置点位/单次读写），并带缓存。
        /// </summary>
        /// <remarks>
        /// 设计目标：
        /// - 兼容不同模型字段命名（DbNumber/Db/DB、ByteIndex/ByteOffset、BitIndex/BitOffset 等）；
        /// - 将反射开销限制在“配置阶段/单次操作”，轮询热路径不做反射；
        /// - 部分 Set 操作用 MemberCache 缓存 Property/Field，减少重复反射成本。
        /// </remarks>
        private static class ReflectionAccessor {
            private static readonly ConcurrentDictionary<(Type Type, string Name), MemberInfo?> MemberCache = new();

            /// <summary>
            /// 从 `PlcDbBoolPoint`（或同构对象）中解析出 dbNumber/byteIndex/bitIndex。
            /// </summary>
            /// <remarks>
            /// 兼容字段：
            /// - DbNumber/Db/DB
            /// - Point/Offset/BitOffset（表示 bit 的线性偏移，会自动拆分成 byte/bit）
            /// - ByteIndex/StartByteAdr/ByteAdr/ByteAddress/Byte
            /// - BitIndex/BitAdr/BitAddress/Bit
            /// </remarks>
            public static bool TryExtractDbBoolPoint(PlcDbBoolPoint point, out int dbNumber, out int byteIndex, out int bitIndex) {
                var boxed = (object)point;
                var t = boxed.GetType();

                if (!TryGetInt(boxed, t, ["DbNumber", "Db", "DB"], out dbNumber)) {
                    dbNumber = 0;
                }

                byteIndex = -1;
                bitIndex = -1;

                // 1) 优先：TIA/OPC 常见语义（ByteOffset + BitOffset）
                if (TryGetInt(boxed, t, ["ByteOffset", "ByteIndex", "StartByteAdr", "ByteAdr", "ByteAddress", "Byte"], out var b)
                    && TryGetInt(boxed, t, ["BitOffset", "BitIndex", "BitAdr", "BitAddress", "Bit"], out var bit)) {
                    // BitOffset 必须是 0~7
                    if (dbNumber > 0 && b >= 0 && (uint)bit <= 7u) {
                        byteIndex = b;
                        bitIndex = bit;
                        return true;
                    }

                    return false;
                }

                // 2) 兜底：线性 bit 偏移（只允许 Point/Offset，避免与 BitOffset 语义冲突）
                if (TryGetInt(boxed, t, ["Point", "Offset"], out var linearBitOffset)) {
                    if (dbNumber <= 0 || linearBitOffset < 0) {
                        return false;
                    }

                    byteIndex = linearBitOffset >> 3;
                    bitIndex = linearBitOffset & 0b111;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 从 `PlcDbBoolWriteItem` 解析出 db/byte/bit/value。
            /// </summary>
            /// <remarks>
            /// 兼容写入项模型：
            /// - 直接包含 DbNumber/ByteIndex/BitIndex + Value/State；
            /// - 或包含 Point/DbBoolPoint 属性指向一个点位对象；
            /// - 值字段兼容 Value/IsOn/IsHigh/IsEnabled，或 State/SignalState（枚举/布尔）。
            /// </remarks>
            public static bool TryExtractDbBoolWriteItem(PlcDbBoolWriteItem item, out int dbNumber, out int byteIndex, out int bitIndex, out bool value) {
                var boxed = (object)item;
                var t = boxed.GetType();

                // 常见：包含 Point/DbBoolPoint 属性
                if (TryGetObject(boxed, t, ["Point", "DbBoolPoint"], out var pObj) && pObj is not null) {
                    if (pObj is PlcDbBoolPoint p) {
                        if (!TryExtractDbBoolPoint(p, out dbNumber, out byteIndex, out bitIndex)) {
                            value = false;
                            return false;
                        }
                    }
                    else {
                        if (!TryExtractDbBoolPointObject(pObj, out dbNumber, out byteIndex, out bitIndex)) {
                            value = false;
                            return false;
                        }
                    }
                }
                else {
                    if (!TryGetInt(boxed, t, ["DbNumber", "Db", "DB"], out dbNumber)) dbNumber = 0;

                    // 修正：兼容 ByteOffset（正式环境常用语义）
                    if (!TryGetInt(boxed, t, ["ByteOffset", "ByteIndex", "StartByteAdr", "ByteAdr", "ByteAddress", "Byte"], out byteIndex)) {
                        byteIndex = -1;
                    }

                    // 修正：兼容 BitOffset（正式环境常用语义）
                    if (!TryGetInt(boxed, t, ["BitOffset", "BitIndex", "BitAdr", "BitAddress", "Bit"], out bitIndex)) {
                        bitIndex = -1;
                    }
                }

                if (TryGetBool(boxed, t, ["Value", "IsOn", "IsHigh", "IsEnabled"], out value)) {
                    return dbNumber > 0 && byteIndex >= 0 && (uint)bitIndex <= 7u;
                }

                if (TryGetObject(boxed, t, ["State", "SignalState"], out var stateObj) && stateObj is not null) {
                    if (stateObj is PlcIoSignalState s) {
                        value = s == PlcIoSignalState.High;
                        return dbNumber > 0 && byteIndex >= 0 && (uint)bitIndex <= 7u;
                    }

                    if (stateObj is bool b) {
                        value = b;
                        return dbNumber > 0 && byteIndex >= 0 && (uint)bitIndex <= 7u;
                    }
                }

                value = false;
                return false;
            }

            /// <summary>
            /// 从 `PlcInt32Address` 或同构对象解析出读取区域/DB/起始字节。
            /// </summary>
            /// <remarks>
            /// 兼容字段：
            /// - DataType/Area/MemoryArea（可为 DataType 或字符串）
            /// - DbNumber/Db/DB
            /// - StartByteAdr/ByteIndex/Offset/ByteAdr/ByteAddress
            /// - Address（字符串：DB1.DBD100 / DB1.DBB100 等）
            /// </remarks>
            public static bool TryExtractInt32Address(PlcInt32Address address, out DataType area, out int dbNumber, out int startByteAdr) {
                var boxed = (object)address;
                var t = boxed.GetType();

                area = DataType.DataBlock;
                dbNumber = 0;            // 关键：不再默认为 1
                startByteAdr = 0;

                if (TryGetObject(boxed, t, ["DataType", "Area", "MemoryArea"], out var areaObj) && areaObj is not null) {
                    area = MapArea(areaObj);
                }

                if (!TryGetInt(boxed, t, ["DbNumber", "Db", "DB"], out dbNumber) || dbNumber <= 0) {
                    return false;        // 关键：缺 DB 编号直接失败，避免误读 DB1
                }

                return TryGetInt(boxed, t, ["StartByteAdr", "ByteIndex", "Offset", "ByteAdr", "ByteAddress"], out startByteAdr);
            }

            /// <summary>
            /// 从 `PlcStringAddress` 或同构对象解析出读取区域/DB/起始字节/最大长度。
            /// </summary>
            /// <remarks>
            /// 兼容字段：
            /// - DataType/Area/MemoryArea（可为 DataType 或字符串）
            /// - DbNumber/Db/DB
            /// - StartByteAdr/ByteIndex/Offset/ByteAdr/ByteAddress 或 Address 字符串
            /// - MaxLength/Length/Capacity/MaxLen
            /// </remarks>
            public static bool TryExtractStringAddress(PlcStringAddress address, out DataType area, out int dbNumber, out int startByteAdr, out int maxLen) {
                var boxed = (object)address;
                var t = boxed.GetType();

                area = DataType.DataBlock;
                dbNumber = 1;
                startByteAdr = 0;
                maxLen = 254;

                if (TryGetObject(boxed, t, ["DataType", "Area", "MemoryArea"], out var areaObj) && areaObj is not null) {
                    area = MapArea(areaObj);
                }

                if (TryGetInt(boxed, t, ["DbNumber", "Db", "DB"], out var db)) {
                    dbNumber = db;
                }

                if (TryGetInt(boxed, t, ["StartByteAdr", "ByteIndex", "Offset", "ByteAdr", "ByteAddress"], out startByteAdr)) {
                    // ok
                }
                else if (TryGetObject(boxed, t, ["Address"], out var addrObj) && addrObj is string addr) {
                    if (!TryParseDbAddress(addr, out area, out dbNumber, out startByteAdr)) {
                        return false;
                    }
                }
                else {
                    return false;
                }

                if (TryGetInt(boxed, t, ["MaxLength", "Length", "Capacity", "MaxLen"], out var len)) {
                    maxLen = len;
                }

                return true;
            }

            /// <summary>
            /// 通过反射为对象设置属性/字段值（带缓存）。
            /// </summary>
            /// <remarks>
            /// - 对 DateTime/DateTimeOffset 之间做了有限转换；
            /// - 若属性不可写或类型不匹配则返回 false；
            /// - MemberCache 缓存同类型同成员名的反射结果，降低重复反射成本。
            /// </remarks>
            public static bool TrySet(object target, string name, object value) {
                var t = target.GetType();

                var mi = MemberCache.GetOrAdd((t, name), static key => {
                    var (type, memberName) = key;
                    return (MemberInfo?)type.GetProperty(memberName, BindingFlags.Public | BindingFlags.Instance)
                        ?? type.GetField(memberName, BindingFlags.Public | BindingFlags.Instance);
                });

                if (mi is PropertyInfo pi) {
                    if (!pi.CanWrite) return false;

                    if (!pi.PropertyType.IsInstanceOfType(value)) {
                        if (pi.PropertyType == typeof(DateTime) && value is DateTimeOffset dto) {
                            pi.SetValue(target, dto.LocalDateTime);
                            return true;
                        }

                        if (pi.PropertyType == typeof(DateTimeOffset) && value is DateTime dt) {
                            pi.SetValue(target, new DateTimeOffset(dt));
                            return true;
                        }

                        return false;
                    }

                    pi.SetValue(target, value);
                    return true;
                }

                if (mi is FieldInfo fi) {
                    if (!fi.FieldType.IsInstanceOfType(value)) {
                        return false;
                    }

                    fi.SetValue(target, value);
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 尝试将变化集合写入 `PlcDbBoolsChangedEventArgs` 的某个集合属性。
            /// </summary>
            /// <remarks>
            /// 策略：
            /// 1) 优先匹配常见命名：Items/Changes/ChangedItems/Points；
            /// 2) 若未匹配，再尝试所有可写属性；
            /// 3) 支持数组或常见泛型集合接口（IEnumerable/IReadOnlyList/ICollection 等）。
            /// </remarks>
            public static bool TrySetChangesCollection(object boxedArgs, List<DbBoolChange> changes) {
                var argsType = boxedArgs.GetType();
                var props = argsType.GetProperties(BindingFlags.Public | BindingFlags.Instance);

                if ((from p in props
                     where p.CanWrite
                     let n = p.Name
                     where n.Equals("Items", StringComparison.OrdinalIgnoreCase)
                                                               || n.Equals("Changes", StringComparison.OrdinalIgnoreCase)
                                                               || n.Equals("ChangedItems", StringComparison.OrdinalIgnoreCase)
                                                               || n.Equals("Points", StringComparison.OrdinalIgnoreCase)
                     select p).Any(p => TryBuildAndAssignCollection(p, boxedArgs, changes))) {
                    return true;
                }

                return props.Where(p => p.CanWrite).Any(p => TryBuildAndAssignCollection(p, boxedArgs, changes));
            }

            /// <summary>
            /// 为某个属性构建并赋值集合（数组或 List&lt;T&gt;），集合元素会根据目标元素类型做对象构建/赋值。
            /// </summary>
            private static bool TryBuildAndAssignCollection(PropertyInfo p, object boxedArgs, List<DbBoolChange> changes) {
                var pt = p.PropertyType;

                if (pt.IsArray) {
                    var itemType = pt.GetElementType();
                    if (itemType is null) return false;

                    var arr = Array.CreateInstance(itemType, changes.Count);
                    for (var i = 0; i < changes.Count; i++) {
                        arr.SetValue(BuildChangeItem(itemType, changes[i]), i);
                    }

                    p.SetValue(boxedArgs, arr);
                    return true;
                }

                var item = GetEnumerableItemType(pt);
                if (item is null) return false;

                var listType = typeof(List<>).MakeGenericType(item);
                var list = (System.Collections.IList)Activator.CreateInstance(listType)!;

                foreach (var t in changes) {
                    list.Add(BuildChangeItem(item, t));
                }

                if (!pt.IsAssignableFrom(listType)) {
                    return false;
                }

                p.SetValue(boxedArgs, list);
                return true;
            }

            /// <summary>
            /// 获取一个集合类型的元素类型（支持直接泛型、或从接口 IEnumerable&lt;T&gt; 推断）。
            /// </summary>
            private static Type? GetEnumerableItemType(Type t) {
                if (t.IsGenericType) {
                    var gd = t.GetGenericTypeDefinition();
                    if (gd == typeof(IEnumerable<>)
                        || gd == typeof(IReadOnlyList<>)
                        || gd == typeof(IList<>)
                        || gd == typeof(IReadOnlyCollection<>)
                        || gd == typeof(ICollection<>)) {
                        return t.GetGenericArguments()[0];
                    }
                }

                return (from it in t.GetInterfaces()
                        where it.IsGenericType && it.GetGenericTypeDefinition() == typeof(IEnumerable<>)
                        select it.GetGenericArguments()[0]).FirstOrDefault();
            }

            /// <summary>
            /// 将内部 `DbBoolChange` 转换为事件集合元素类型的实例。
            /// </summary>
            private static object BuildChangeItem(Type itemType, DbBoolChange c) {
                // 强类型：PlcDbBoolChange 走无反射映射，确保字段齐全
                if (itemType == typeof(PlcDbBoolChange)) {
                    var p = c.Point;

                    return new PlcDbBoolChange {
                        DbNumber = p.DbNumber,
                        ByteOffset = p.ByteOffset,
                        BitOffset = p.BitOffset,
                        OldState = c.OldState,
                        NewState = c.NewState,
                        Tag = p.Tag
                    };
                }

                // 兼容：上层如果直接用 PlcDbBoolPoint 作为变化元素
                if (itemType == typeof(PlcDbBoolPoint)) {
                    return c.Point;
                }

                // 兜底：未知元素类型，尝试反射写入常见字段
                var obj = Activator.CreateInstance(itemType)!;

                // 点位信息
                TrySet(obj, "Point", c.Point);
                TrySet(obj, "DbBoolPoint", c.Point);

                // 结构化字段（优先确保 DbNumber/ByteOffset/BitOffset/Tag）
                TrySet(obj, "DbNumber", c.Point.DbNumber);
                TrySet(obj, "ByteOffset", c.Point.ByteOffset);
                TrySet(obj, "BitOffset", c.Point.BitOffset);
                TrySet(obj, "Tag", c.Point.Tag);

                // 电平信息
                TrySet(obj, "OldState", c.OldState);
                TrySet(obj, "NewState", c.NewState);

                // 兼容布尔语义字段
                var oldValue = c.OldState == PlcIoSignalState.High;
                var newValue = c.NewState == PlcIoSignalState.High;

                TrySet(obj, "OldValue", oldValue);
                TrySet(obj, "NewValue", newValue);
                TrySet(obj, "Value", newValue);

                return obj;
            }

            /// <summary>
            /// 从任意“点位对象”解析 db/byte/bit（用于写入项中 Point/DbBoolPoint 不是强类型的情况）。
            /// </summary>
            private static bool TryExtractDbBoolPointObject(object pointObj, out int dbNumber, out int byteIndex, out int bitIndex) {
                var t = pointObj.GetType();

                if (!TryGetInt(pointObj, t, ["DbNumber", "Db", "DB"], out dbNumber)) {
                    dbNumber = 0;
                }

                byteIndex = -1;
                bitIndex = -1;

                // 1) 优先：ByteOffset + BitOffset
                if (TryGetInt(pointObj, t, ["ByteOffset", "ByteIndex", "StartByteAdr", "ByteAdr", "ByteAddress", "Byte"], out var b)
                    && TryGetInt(pointObj, t, ["BitOffset", "BitIndex", "BitAdr", "BitAddress", "Bit"], out var bit)) {
                    if (dbNumber > 0 && b >= 0 && (uint)bit <= 7u) {
                        byteIndex = b;
                        bitIndex = bit;
                        return true;
                    }

                    return false;
                }

                // 2) 兜底：线性 bit 偏移（Point/Offset）
                if (TryGetInt(pointObj, t, ["Point", "Offset"], out var linearBitOffset)) {
                    if (dbNumber <= 0 || linearBitOffset < 0) {
                        return false;
                    }

                    byteIndex = linearBitOffset >> 3;
                    bitIndex = linearBitOffset & 0b111;
                    return true;
                }

                return false;
            }

            /// <summary>
            /// 尝试从对象中读取 int 字段/属性（支持多候选名称）。
            /// </summary>
            /// <remarks>
            /// - 先尝试属性，再尝试字段；
            /// - 读取到值后用 `Convert.ToInt32` 做宽松转换；
            /// - 任一候选命中则返回 true。
            /// </remarks>
            private static bool TryGetInt(object boxed, Type t, string[] names, out int value) {
                foreach (var name in names) {
                    var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (pi is not null) {
                        var v = pi.GetValue(boxed);
                        if (v is null) continue;

                        try {
                            value = Convert.ToInt32(v);
                            return true;
                        }
                        catch {
                            continue;
                        }
                    }

                    var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (fi is not null) {
                        var v = fi.GetValue(boxed);
                        if (v is null) continue;

                        try {
                            value = Convert.ToInt32(v);
                            return true;
                        }
                        catch {
                            continue;
                        }
                    }
                }

                value = 0;
                return false;
            }

            /// <summary>
            /// 尝试从对象中读取 bool 字段/属性（支持多候选名称）。
            /// </summary>
            /// <remarks>
            /// - 仅当值类型就是 bool 时才算命中（避免字符串/数字造成语义歧义）。
            /// - 先尝试属性，再尝试字段。
            /// </remarks>
            private static bool TryGetBool(object boxed, Type t, string[] names, out bool value) {
                foreach (var name in names) {
                    var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (pi is not null) {
                        var v = pi.GetValue(boxed);
                        if (v is bool b) {
                            value = b;
                            return true;
                        }
                    }

                    var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (fi is not null) {
                        var v = fi.GetValue(boxed);
                        if (v is bool b) {
                            value = b;
                            return true;
                        }
                    }
                }

                value = false;
                return false;
            }

            /// <summary>
            /// 尝试从对象中读取任意字段/属性（支持多候选名称）。
            /// </summary>
            private static bool TryGetObject(object boxed, Type t, string[] names, out object? value) {
                foreach (var name in names) {
                    var pi = t.GetProperty(name, BindingFlags.Public | BindingFlags.Instance);
                    if (pi is not null) {
                        value = pi.GetValue(boxed);
                        return true;
                    }

                    var fi = t.GetField(name, BindingFlags.Public | BindingFlags.Instance);
                    if (fi is not null) {
                        value = fi.GetValue(boxed);
                        return true;
                    }
                }

                value = null;
                return false;
            }

            /// <summary>
            /// 将“地址区域”对象映射为 S7.Net 的 `DataType`。
            /// </summary>
            /// <remarks>
            /// - 支持直接传入 `DataType`；
            /// - 或传入字符串/枚举名称（Input/I、Output/Q、Memory/M、DataBlock/DB）。
            /// </remarks>
            private static DataType MapArea(object areaObj) {
                if (areaObj is DataType dt) {
                    return dt;
                }

                var name = areaObj.ToString() ?? string.Empty;
                if (name.Equals("Input", StringComparison.OrdinalIgnoreCase) || name.Equals("I", StringComparison.OrdinalIgnoreCase)) return DataType.Input;
                if (name.Equals("Output", StringComparison.OrdinalIgnoreCase) || name.Equals("Q", StringComparison.OrdinalIgnoreCase)) return DataType.Output;
                if (name.Equals("Memory", StringComparison.OrdinalIgnoreCase) || name.Equals("M", StringComparison.OrdinalIgnoreCase)) return DataType.Memory;
                if (name.Equals("DataBlock", StringComparison.OrdinalIgnoreCase) || name.Equals("DB", StringComparison.OrdinalIgnoreCase)) return DataType.DataBlock;

                return DataType.DataBlock;
            }

            /// <summary>
            /// 解析简化 DB 地址：DB{n}.DBB{offset} / DB{n}.DBW{offset} / DB{n}.DBD{offset}。
            /// </summary>
            /// <remarks>
            /// - 仅解析 DB data block 类型（area 固定为 DataBlock）；
            /// - 不做复杂语法支持，避免引入额外分配与复杂度；
            /// - 成功时输出 dbNumber 与 startByteAdr，并返回 true。
            /// </remarks>
            private static bool TryParseDbAddress(string address, out DataType area, out int dbNumber, out int startByteAdr) {
                area = DataType.DataBlock;
                dbNumber = 0;
                startByteAdr = 0;

                if (string.IsNullOrWhiteSpace(address)) {
                    return false;
                }

                var s = address.Trim().ToUpperInvariant();

                // 仅做 DB 解析，避免引入复杂字符串分配
                // 例：DB1.DBD100
                if (!s.StartsWith("DB", StringComparison.Ordinal)) {
                    return false;
                }

                var dot = s.IndexOf('.', StringComparison.Ordinal);
                if (dot <= 2) {
                    return false;
                }

                if (!int.TryParse(s.AsSpan(2, dot - 2), out dbNumber)) {
                    return false;
                }

                var tail = s.AsSpan(dot + 1);
                // 支持 DBB/DBW/DBD 前缀
                if (tail.StartsWith("DBB", StringComparison.Ordinal) || tail.StartsWith("DBW", StringComparison.Ordinal) || tail.StartsWith("DBD", StringComparison.Ordinal)) {
                    var numSpan = tail.Slice(3);
                    if (!int.TryParse(numSpan, out startByteAdr)) {
                        return false;
                    }
                    return true;
                }

                return false;
            }
        }
    }
}
