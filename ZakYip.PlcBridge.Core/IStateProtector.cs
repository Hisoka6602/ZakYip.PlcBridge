using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Core {

    /// <summary>
    /// 状态保护器（加密/解密 + 完整性保护）
    /// </summary>
    public interface IStateProtector {

        byte[] Protect(ReadOnlySpan<byte> plain);

        byte[] Unprotect(ReadOnlySpan<byte> protectedData);
    }
}
