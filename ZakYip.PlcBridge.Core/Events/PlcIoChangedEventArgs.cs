using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;
using ZakYip.PlcBridge.Core.Enums;
using ZakYip.PlcBridge.Core.Models;

namespace ZakYip.PlcBridge.Core.Events {
    /// <summary>
    /// PLC IO 变动事件载荷
    /// </summary>
    public readonly record struct PlcIoChangedEventArgs {
        /// <summary>IO 点</summary>
        public required PlcIoPoint IoPoint { get; init; }

        /// <summary>旧电平</summary>
        public required PlcIoSignalState OldState { get; init; }

        /// <summary>新电平</summary>
        public required PlcIoSignalState NewState { get; init; }

        /// <summary>发生时间</summary>
        public required DateTimeOffset OccurredAt { get; init; }
    }
}
