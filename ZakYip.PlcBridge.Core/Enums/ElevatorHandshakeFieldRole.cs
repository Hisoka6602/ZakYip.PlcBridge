using System;
using System.Linq;
using System.Text;
using System.ComponentModel;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Enums {

    /// <summary>
    /// 电梯对接字段业务作用枚举
    /// </summary>
    public enum ElevatorHandshakeFieldRole {

        /// <summary>
        /// 物料编号
        /// </summary>
        [Description("物料编号")]
        ItemCode = 1,

        /// <summary>
        /// 批次
        /// </summary>
        [Description("批次")]
        BatchNo = 2,

        /// <summary>
        /// 箱子数量
        /// </summary>
        [Description("箱子数量")]
        BoxQuantity = 3,

        /// <summary>
        /// 呼叫电梯层数（电梯排数/目标层）
        /// </summary>
        [Description("呼叫电梯层数")]
        CallElevatorLayer = 4,

        /// <summary>
        /// 呼叫电梯使用层数（使用电梯第几层：1/2/...）
        /// </summary>
        [Description("呼叫电梯使用层数")]
        CallElevatorUseLayer = 5,

        /// <summary>
        /// 呼叫电梯信号
        /// </summary>
        [Description("呼叫电梯信号")]
        CallElevatorSignal = 6,

        /// <summary>
        /// 进料完成信号
        /// </summary>
        [Description("进料完成信号")]
        InfeedDoneSignal = 7,

        /// <summary>
        /// 电梯到位信号
        /// </summary>
        [Description("电梯到位信号")]
        ElevatorArrivedSignal = 8,

        /// <summary>
        /// 唯一 Guid
        /// </summary>
        [Description("唯一Guid")]
        UniqueGuid = 9,

        /// <summary>
        /// 是否成功（PLC 侧可能用 Bool 或 String 表达）
        /// </summary>
        [Description("是否成功")]
        IsSuccess = 10,
    }
}
