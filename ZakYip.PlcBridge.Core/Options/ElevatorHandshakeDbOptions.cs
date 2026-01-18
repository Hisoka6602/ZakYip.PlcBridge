using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Options {
    /// <summary>
    /// 电梯信号对接（上位机对接）DB 监控配置（对应 DB27 结构）
    /// </summary>
    public sealed record class ElevatorHandshakeDbOptions {
        /// <summary>
        /// DB 编号（例如：27）
        /// </summary>
        public required int DbNumber { get; init; }

        /// <summary>
        /// 字段点位定义
        /// </summary>
        public required IReadOnlyList<ElevatorHandshakeDbFieldOptions> Fields { get; init; }
    }
}
