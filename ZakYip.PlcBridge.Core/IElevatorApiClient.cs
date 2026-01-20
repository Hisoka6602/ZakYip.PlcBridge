using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Events;
using ZakYip.PlcBridge.Core.Models.Elevator;

namespace ZakYip.PlcBridge.Core {

    /// <summary>
    /// 电梯 API 访问客户端（契约层，不绑定具体协议）
    /// </summary>
    public interface IElevatorApiClient : IAsyncDisposable {

        /// <summary>
        /// 异常事件（用于隔离异常，不影响上层调用链）
        /// </summary>
        event EventHandler<ElevatorApiFaultedEventArgs>? Faulted;

        /// <summary>
        /// 呼叫电梯
        /// </summary>
        ValueTask<ElevatorApiResult> CallElevatorAsync(
            ElevatorCallRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 入库执行完成上报
        /// </summary>
        ValueTask<ElevatorApiResult> ReportInfeedDoneAsync(
            ElevatorInfeedDoneRequest request,
            CancellationToken cancellationToken = default);

        /// <summary>
        /// 电梯任务查询
        /// </summary>
        ValueTask<ElevatorTaskQueryResult> QueryTaskAsync(
            ElevatorTaskQueryRequest request,
            CancellationToken cancellationToken = default);
    }
}
