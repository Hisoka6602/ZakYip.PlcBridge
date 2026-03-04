using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Attributes {

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class WinLottiesPathAttribute : Attribute {
        public string ResourcePathName { get; }

        public WinLottiesPathAttribute(string resourcePathName) {
            ResourcePathName = resourcePathName;
        }
    }
}
