using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Utilities {

    public static class ElevatorRuntimeState {
        private static string? _erpGuid;
        private static string? _latestProgressTopic;
        private static string? _latestProgressPayloadJson;

        /// <summary>
        /// 当前电梯任务的 ErpGuid（进程内共享）。
        /// </summary>
        public static string? ErpGuid {
            get => Volatile.Read(ref _erpGuid);
            set => Volatile.Write(ref _erpGuid, value);
        }

        /// <summary>
        /// 最近一次进度通知主题。
        /// </summary>
        public static string? LatestProgressTopic {
            get => Volatile.Read(ref _latestProgressTopic);
            set => Volatile.Write(ref _latestProgressTopic, value);
        }

        /// <summary>
        /// 最近一次进度通知载荷（JSON）。
        /// </summary>
        public static string? LatestProgressPayloadJson {
            get => Volatile.Read(ref _latestProgressPayloadJson);
            set => Volatile.Write(ref _latestProgressPayloadJson, value);
        }

        /// <summary>
        /// 更新当前进度快照。
        /// </summary>
        public static void UpdateProgress(string topic, string payloadJson) {
            Volatile.Write(ref _latestProgressTopic, topic);
            Volatile.Write(ref _latestProgressPayloadJson, payloadJson);
        }

        /// <summary>
        /// 清空当前 ErpGuid。
        /// </summary>
        public static void ClearErpGuid() => Volatile.Write(ref _erpGuid, null);
    }
}
