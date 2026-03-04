using System;
using Prism.Mvvm;
using System.Linq;
using System.Text;
using System.Windows.Data;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Enums;

namespace ZakYip.PlcBridge.Client.Models {

    /// <summary>
    /// 生产进度。
    /// </summary>
    public sealed class ProductionProgressModel : BindableBase {
        private StepProgressStatus _elevatorCallStatus = StepProgressStatus.Waiting;
        private StepProgressStatus _elevatorArriveStatus = StepProgressStatus.NotStarted;
        private StepProgressStatus _feedingCompleteStatus = StepProgressStatus.NotStarted;
        private DateTimeOffset _lastUpdatedAt = DateTimeOffset.Now;

        /// <summary>
        /// 呼叫电梯状态。
        /// </summary>
        public StepProgressStatus ElevatorCallStatus {
            get => _elevatorCallStatus;
            set => SetProperty(ref _elevatorCallStatus, value);
        }

        /// <summary>
        /// 电梯到位状态。
        /// </summary>
        public StepProgressStatus ElevatorArriveStatus {
            get => _elevatorArriveStatus;
            set => SetProperty(ref _elevatorArriveStatus, value);
        }

        /// <summary>
        /// 进料完成状态。
        /// </summary>
        public StepProgressStatus FeedingCompleteStatus {
            get => _feedingCompleteStatus;
            set => SetProperty(ref _feedingCompleteStatus, value);
        }

        /// <summary>
        /// 最近一次状态更新时间（UTC）。
        /// </summary>
        public DateTimeOffset LastUpdatedAt {
            get => _lastUpdatedAt;
            set => SetProperty(ref _lastUpdatedAt, value);
        }

        /// <summary>
        /// 复位进度（用于下一次流程）。
        /// 规则：呼叫电梯状态固定为等待中；其余步骤回到未开始。
        /// </summary>
        public void Reset() {
            ElevatorCallStatus = StepProgressStatus.Waiting;
            ElevatorArriveStatus = StepProgressStatus.NotStarted;
            FeedingCompleteStatus = StepProgressStatus.NotStarted;
            Touch();
        }

        /// <summary>
        /// 标记：已呼叫电梯。
        /// </summary>
        public void MarkElevatorCalled() {
            ElevatorCallStatus = StepProgressStatus.Completed;

            // 若后续步骤已进入等待/完成，前置完成是必然条件，此处不反向修改后续步骤
            Touch();
        }

        /// <summary>
        /// 进入：等待电梯到位。
        /// 规则：进入该步骤时，呼叫电梯必然已完成。
        /// </summary>
        public void EnterWaitingElevatorArrive() {
            EnsureElevatorCallCompleted();
            ElevatorArriveStatus = StepProgressStatus.Waiting;
            Touch();
        }

        /// <summary>
        /// 标记：电梯已到位。
        /// </summary>
        public void MarkElevatorArrived() {
            EnsureElevatorCallCompleted();
            ElevatorArriveStatus = StepProgressStatus.Completed;
            Touch();
        }

        /// <summary>
        /// 进入：等待进料完成。
        /// 规则：进入该步骤时，呼叫电梯与电梯到位必然已完成。
        /// </summary>
        public void EnterWaitingFeedingComplete() {
            EnsureElevatorArriveCompleted();
            FeedingCompleteStatus = StepProgressStatus.Waiting;
            Touch();
        }

        /// <summary>
        /// 标记：进料已完成。
        /// </summary>
        public void MarkFeedingCompleted() {
            EnsureElevatorArriveCompleted();
            FeedingCompleteStatus = StepProgressStatus.Completed;
            Touch();
        }

        private void EnsureElevatorCallCompleted() {
            // 业务规则：一旦进入后续等待，前置步骤必须视为已完成（自动收敛，避免状态不一致）
            if (ElevatorCallStatus != StepProgressStatus.Completed) {
                ElevatorCallStatus = StepProgressStatus.Completed;
            }
        }

        private void EnsureElevatorArriveCompleted() {
            EnsureElevatorCallCompleted();

            if (ElevatorArriveStatus != StepProgressStatus.Completed) {
                // 进入“等待进料完成”时，电梯到位必然已完成（自动收敛）
                ElevatorArriveStatus = StepProgressStatus.Completed;
            }
        }

        private void Touch() => LastUpdatedAt = DateTimeOffset.Now;
    }
}
