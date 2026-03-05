using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Utilities {

    public static class ElevatorRuntimeState {
        private static string? _erpGuid;
        private static ProgressSnapshot? _latestProgress;

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
            get => Volatile.Read(ref _latestProgress)?.Topic;
        }

        /// <summary>
        /// 最近一次进度通知载荷（JSON）。
        /// </summary>
        public static string? LatestProgressPayloadJson {
            get => Volatile.Read(ref _latestProgress)?.PayloadJson;
        }

        /// <summary>
        /// 最近一次进度通知快照。
        /// </summary>
        public static (string Topic, string PayloadJson)? LatestProgressSnapshot {
            get {
                var snapshot = Volatile.Read(ref _latestProgress);
                return snapshot is null ? null : (snapshot.Topic, snapshot.PayloadJson);
            }
        }

        /// <summary>
        /// 更新当前进度快照。
        /// </summary>
        public static void UpdateProgress(string topic, string payloadJson) {
            Volatile.Write(ref _latestProgress, new ProgressSnapshot(topic, payloadJson));
        }

        /// <summary>
        /// 清空当前 ErpGuid。
        /// </summary>
        public static void ClearErpGuid() => Volatile.Write(ref _erpGuid, null);

        private sealed record class ProgressSnapshot(string Topic, string PayloadJson);
    }
}
