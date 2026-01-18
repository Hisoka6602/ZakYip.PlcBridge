using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;

namespace ZakYip.PlcBridge.Core.Models {
    /// <summary>
    /// PLC IO 点定义
    /// </summary>
    public readonly record struct PlcIoPoint {
        /// <summary>点位编号（例如 IO 点号）</summary>
        public required int Point { get; init; }

        /// <summary>点位类型</summary>
        public required PlcIoPointType Type { get; init; }

        /// <summary>点位说明（可为空）</summary>
        public string? Description { get; init; }
    }
}
