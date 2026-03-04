using System;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Collections.Generic;

namespace ZakYip.PlcBridge.Client.Attributes {

    [AttributeUsage(AttributeTargets.Field, Inherited = false, AllowMultiple = false)]
    public sealed class WinIconAttribute : Attribute {
        public string Icon { get; }

        public WinIconAttribute(string icon) {
            Icon = icon;
        }
    }
}
