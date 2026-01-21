using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Utilities {

    public static class ElevatorRuntimeState {
        private static string? _erpGuid;

        /// <summary>
        /// 当前电梯任务的 ErpGuid（进程内共享）。
        /// </summary>
        public static string? ErpGuid {
            get => Volatile.Read(ref _erpGuid);
            set => Volatile.Write(ref _erpGuid, value);
        }

        /// <summary>
        /// 清空当前 ErpGuid。
        /// </summary>
        public static void ClearErpGuid() => Volatile.Write(ref _erpGuid, null);
    }
}
