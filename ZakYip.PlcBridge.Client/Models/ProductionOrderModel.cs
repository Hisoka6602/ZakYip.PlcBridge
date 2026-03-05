using System;
using Prism.Mvvm;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Client.Enums;
using System.Text.Json.Serialization;
using System.Diagnostics.CodeAnalysis;

namespace ZakYip.PlcBridge.Client.Models {

    /// <summary>
    /// 生产订单数据（生产工单维度）。
    /// </summary>
    public class ProductionOrderModel : BindableBase {
        private string _workOrderNo = string.Empty;
        private string _itemCode = string.Empty;
        private string _batchNo = string.Empty;
        private int _plannedBoxCount;
        private OperationResultStatus _pushStatus;

        /// <summary>
        /// 工单号。
        /// </summary>
        [JsonPropertyName("workOrderNo")]
        public required string WorkOrderNo {
            get => _workOrderNo;
            set => SetProperty(ref _workOrderNo, value);
        }

        /// <summary>
        /// 物料编号。
        /// </summary>
        [JsonPropertyName("itemCode")]
        public required string ItemCode {
            get => _itemCode;
            set => SetProperty(ref _itemCode, value);
        }

        /// <summary>
        /// 批次。
        /// </summary>
        [JsonPropertyName("batchNo")]
        public required string BatchNo {
            get => _batchNo;
            set => SetProperty(ref _batchNo, value);
        }

        /// <summary>
        /// 计划箱数。
        /// </summary>
        [JsonPropertyName("PlanQty")]
        public required int PlannedBoxCount {
            get => _plannedBoxCount;
            set => SetProperty(ref _plannedBoxCount, value);
        }

        /// <summary>
        /// 推送状态。
        /// </summary>
        public OperationResultStatus PushStatus {
            get => _pushStatus;
            set => SetProperty(ref _pushStatus, value);
        }
    }
}
