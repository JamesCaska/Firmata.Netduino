using System;
using Microsoft.SPOT;
using VirtualBreadboard.Remoting.NETMF.Firmata;

namespace VirtualBreadboard.Remoting.NETMF.Firmata
{
    public abstract class RemotePin 
    {
        int _previousValue;

        internal FirmataHost Host { get; set; }
        public abstract int PinMode { set; get; }
        public abstract int Value { get; set; }

        public RemotePin()
        {
            
        }

        public void NotifyChanged()
        {
            if (DigitalReportEnabled)
            {
                Host.DigitalWriteReport(PinIndex,Value);
            }
          
        }
 
        internal bool AnalogReportEnabled { get; set; }
        internal bool DigitalReportEnabled { get; set; }
        internal int PinIndex { get; set; }
        internal bool SampleChanged {
            get
            {
                int v = Value;
                bool ret = _previousValue != v;
                _previousValue = v;
                return ret;
            }
        }
    }
}
