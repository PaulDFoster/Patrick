using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using com.microsoft.ZWaveBridge.SwitchBinary.Switch;

namespace Billy
{
    public class Context
    {
        public SwitchConsumer switchConsumer;
        public bool lightStatus = false;
        public bool lightsAvailable = false;
        public bool IsIoTCore = false;
    }
}
