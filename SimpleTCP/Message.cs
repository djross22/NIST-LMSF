using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace SimpleTCP
{
    public class Message
    {
        private TcpClient _tcpClient;
        private System.Text.Encoding _encoder = null;
        private byte _writeLineDelimiter;
        private bool _autoTrim = false;
        internal Message(byte[] data, TcpClient tcpClient, System.Text.Encoding stringEncoder, byte lineDelimiter)
        {
            Data = data;
            _tcpClient = tcpClient;
            _encoder = stringEncoder;
            _writeLineDelimiter = lineDelimiter;
        }

        internal Message(byte[] data, TcpClient tcpClient, System.Text.Encoding stringEncoder, byte lineDelimiter, bool autoTrim)
        {
            Data = data;
            _tcpClient = tcpClient;
            _encoder = stringEncoder;
            _writeLineDelimiter = lineDelimiter;
            _autoTrim = autoTrim;
        }

        public byte[] Data { get; private set; }
        public string MessageString
        {
            get
            {
                if (_autoTrim)
                {
                    return _encoder.GetString(Data).Trim();
                }

                return _encoder.GetString(Data);
            }
        }

        public void Reply(byte[] data)
        {
            _tcpClient.GetStream().Write(data, 0, data.Length);
        }

        public void Reply(string data)
        {
            if (string.IsNullOrEmpty(data)) { return; }
            Reply(_encoder.GetBytes(data));
        }

        public void ReplyLine(string data)
        {
            if (string.IsNullOrEmpty(data)) { return; }
            if (data.LastOrDefault() != _writeLineDelimiter)
            {
                Reply(data + _encoder.GetString(new byte[] { _writeLineDelimiter }));
            } else
            {
                Reply(data);
            }
        }

        public TcpClient TcpClient {  get { return _tcpClient; } }

        //DJR addition, 2019-02017
        public static bool CheckMessageHash(string wrappedMessage)
        {
            string[] messageParts = UnwrapTcpMessage(wrappedMessage);

            string msg = messageParts[1];
            string hash = messageParts[2];

            return $"{msg.GetHashCode()}" == hash;
        }

        public static string[] UnwrapTcpMessage(string wrappedMessage)
        {
            string[] messageParts = wrappedMessage.Split(new[] { ',' }, StringSplitOptions.None);

            if (messageParts.Length != 3)
            {
                if (messageParts.Length > 3)
                {
                    throw new ArgumentException($"Too many commas in wrapped message. Commas are reserved as separators between message ID, message text, and message hash.", "wrappedMessage");
                    //return false;
                }
                else
                {
                    throw new ArgumentException($"Missing components of wrapped message. Wrapped message should be: \"<message ID>,<message text>,<message hash>\".", "wrappedMessage");
                    //return false;
                }
            }

            return messageParts;
        }

        public static string WrapTcpMessage(string msg)
        {
            return $"{GetUniqueMsgId()},{msg},{msg.GetHashCode()}";
        }

        private static string GetUniqueMsgId()
        {
            DateTime now = DateTime.Now;
            return now.ToString("yyyyMMddHHmmssfff");
        }
        //end DJR addition
    }
}
