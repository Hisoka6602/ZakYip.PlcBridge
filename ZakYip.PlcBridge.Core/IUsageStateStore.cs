using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Models.Security;

namespace ZakYip.PlcBridge.Core {

    /// <summary>
    /// 使用状态存储（不抛异常，失败返回 null/false）
    /// </summary>
    public interface IUsageStateStore {

        ValueTask<UsageState?> TryLoadAsync(CancellationToken cancellationToken = default);

        ValueTask<bool> TrySaveAsync(UsageState state, CancellationToken cancellationToken = default);
    }
}
