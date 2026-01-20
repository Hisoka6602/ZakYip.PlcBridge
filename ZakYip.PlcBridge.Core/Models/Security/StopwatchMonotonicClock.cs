using System;
using System.Linq;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core.Models.Security {

    /// <summary>
    /// 单调时钟（Stopwatch）
    /// </summary>
    public sealed class StopwatchMonotonicClock {

        public static long GetTimestamp() => Stopwatch.GetTimestamp();

        public static long ToMilliseconds(long deltaTicks) {
            if (deltaTicks <= 0) return 0;
            return deltaTicks * 1000L / Stopwatch.Frequency;
        }
    }
}
