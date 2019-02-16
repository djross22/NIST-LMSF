using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace IL.TcpCommunicationLib
{
    public class TcpChannelReceivedEventArgs : EventArgs
    {
        public byte[] BtsReceived { private set; get; }
                
        public TcpChannelReceivedEventArgs(byte[] btsReceived)
        {
            BtsReceived = btsReceived;
        }

        public bool AreBytesAvailable
        {
            get { return BtsReceived != null && BtsReceived.Length > 0; }
        }

        public string AsciiStringReceived
        {
            get { return AreBytesAvailable ? Encoding.ASCII.GetString(BtsReceived) : null; }
        }

        public string UnicodeStringReceived
        {
            get { return AreBytesAvailable ? Encoding.Unicode.GetString(BtsReceived) : null; }
        }
    }

    public class TcpChannelNotifyEventArgs : EventArgs
    {
        public enum NotificationLevel
        {
            Debug,
            Info,
            Warn,
            Error,
            Fatal,
        }

        public NotificationLevel Level { private set; get; }
        public string MethodName { private set; get; }
        public object Data { private set; get; }
        public Exception Ex { private set; get; }

        public TcpChannelNotifyEventArgs(NotificationLevel notifLevel, string methodName, object data = null, Exception e = null)
        {
            Level = notifLevel;
            MethodName = methodName;
            Data = data;
            Ex = e;
        }
    }
}
