using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Diagnostics;
using IL.WorkerThreadLib;

namespace IL.TcpCommunicationLib
{
    [Serializable]
    public delegate void TcpChannelEventHandler<TEventArgs>(TcpChannel sender, TEventArgs e) where TEventArgs : EventArgs;

    public class TcpChannel
    {
        #region Consts

        protected const int POLL_TIMEOUT = 1000000; // in mcs,  = 1 sec
        protected const int DEFAULT_RECEIVE_TIMEOUT_IN_SEC = 15;
        protected const int DEFAULT_SEND_TIMEOUT_IN_SEC = 5;
        protected const int DEFAULT_RECEIVE_BUFFER_SIZE = 1024 * 128;
        protected const int DEFAULT_SEND_BUFFER_SIZE = 1024 * 128;
        protected const int DEFAULT_EXTEND_RECON_DELAY_AFTER = 1;
        protected const string DEFAULT_LOCAL_HOST_NAME = "localhost";
       
        #endregion // Consts

        #region Vars
    
        protected DateTime lastReceiveTime;
        protected AutoResetEvent evForReconnect = new AutoResetEvent(false);
        protected Socket socket = null;
        protected ReconnectDelegate dlgtReconnect;
        protected TimeSpan channelReceiveTimeout;
        protected object sync = new object();

        private WorkerThread readingThread = null;
        private WorkerThread processingThread;
        private ConcurrentQueue<byte[]> cqueBytes = new ConcurrentQueue<byte[]>();
        private static IPAddress localIP;      
        private static int closingAllChannelsCount = 0;
        private int finishingCount = 0;
        private int failureToSend = 0;
        private long pendingBytesNumber = 0;
        private int socketReceiveTimeoutInSec;
        private int socketSendTimeoutInSec;
        private int socketReceiveBufferSize;
        private int socketSendBufferSize;

        #endregion // Vars

        #region Properties

        public string Id { private set; get; }
        public IPEndPoint RemoteEP { protected set; get; }
        public int State { set; get; }
        public byte[] UnparsedBytes { set; private get; }

        #endregion // Properties

        #region Delegate

        protected delegate bool ReconnectDelegate(/*TcpChannel tcpChannel*/);

        #endregion // Delegate

        #region Events

        internal event TcpChannelEventHandler<TcpChannelReceivedEventArgs> onReceived;
       
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onAsyncSendException;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onAsyncSendFailureToSend;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onAsyncSendSendingTimeoutExceeded;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onAsyncSendTooFewBytesSent;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onInfo;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onNoBytesToSend;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onNoDataRead;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onProcessingThreadException;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onReadingThreadException;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onSocketNullOrNotConnected;
        public event TcpChannelEventHandler<TcpChannelNotifyEventArgs> onSyncSendException;
       
        #endregion // Events

        #region Constructors

        // Client Ctor
        protected TcpChannel(TcpChannelEventHandler<TcpChannelReceivedEventArgs> onReceived,
                             string id, int socketReceiveTimeoutInSec, int socketSendTimeoutInSec,
                             int socketReceiveBufferSize, int socketSendBufferSize)
            : this(socketReceiveTimeoutInSec, socketSendTimeoutInSec, socketReceiveBufferSize, socketSendBufferSize)
        {
            Id = id;

            if (onReceived == null)
                throw new Exception("onReceived is null.");

            this.onReceived += onReceived;
        }

        // Server Ctor
        protected TcpChannel(Socket socket, TcpChannelEventHandler<TcpChannelNotifyEventArgs> onNotify,
                             int socketReceiveTimeoutInSec, int socketSendTimeoutInSec,
                             int socketReceiveBufferSize, int socketSendBufferSize)
            : this(socketReceiveTimeoutInSec, socketSendTimeoutInSec, socketReceiveBufferSize, socketSendBufferSize)
        {
            this.socket = socket;

            SetSocketParams();

            RemoteEP = this.socket.RemoteEndPoint as IPEndPoint;
            Id = RemoteEP.ToString();
        }

        private TcpChannel(int socketReceiveTimeoutInSec, int socketSendTimeoutInSec,
                           int socketReceiveBufferSize, int socketSendBufferSize)
        {
            this.socketReceiveTimeoutInSec = socketReceiveTimeoutInSec;
            this.socketSendTimeoutInSec = socketSendTimeoutInSec;
            this.socketReceiveBufferSize = socketReceiveBufferSize;
            this.socketSendBufferSize = socketSendBufferSize;

            lastReceiveTime = DateTime.Now;

            channelReceiveTimeout = new TimeSpan(0, 0, socketReceiveTimeoutInSec > 0 ? socketReceiveTimeoutInSec : DEFAULT_RECEIVE_TIMEOUT_IN_SEC);
        }

        protected void SetSocketParams()
        {
            if (socket != null)
            {
                socket.NoDelay = true;
                socket.ReceiveTimeout = socketReceiveTimeoutInSec * 1000;
                socket.SendTimeout = socketSendTimeoutInSec * 1000;
                socket.ReceiveBufferSize = socketReceiveBufferSize;
                socket.SendBufferSize = socketSendBufferSize;
            }
        }

        #endregion // Constructors

        #region Receive

        internal void Receive()
        {
            ResetFinishing();
            
            socket.ReceiveTimeout = 1000; // milliseconds

            lastReceiveTime = DateTime.Now;

            if (processingThread == null)
            {
                processingThread = new WorkerThread(e => 
                    {
                        if (onProcessingThreadException != null)
                            onProcessingThreadException(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "Receive", null, e));
                    });
                processingThread.Start(() =>
                    {
                        List<byte> lstBts = BtsQueueToList();
                        if (lstBts != null && lstBts.Count > 0)
                        {
                            SetLastReceiveTime();
                            onReceived(this, new TcpChannelReceivedEventArgs(lstBts.ToArray()));
                        }
                    });
            }

            readingThread = new WorkerThread(e =>
                {
                    if (onReadingThreadException != null)
                        onReadingThreadException(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "Receive", null, e));
                });
            readingThread.Start(
                // ThreadStart Delegate
                () =>
                {
                    if (IsFinishing || !IsWithinReceiveTimeout || !IsSocketConnected)
                        throw new WorkerThread.StopThreadException();

                    if (socket.Poll(POLL_TIMEOUT, SelectMode.SelectRead))
                    {
                        if (socket.Available > 0)
                        {
                            // The socket is supposed to be connected and data are available.
                            byte[] buf = new byte[socket.Available];

                            int received = 0;
                            if ((received = socket.Receive(buf, 0, buf.Length, SocketFlags.None)) > 0 &&
                                onReceived != null && processingThread != null)
                            {
                                // Processing in dedicated thread
                                if (processingThread.IsThreadActive)
                                    cqueBytes.Enqueue(buf);

                                processingThread.SetEvent();
                            }
                        }
                        else
                            if (onNoDataRead != null)
                                onNoDataRead(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Debug, "Reading Thread, Start"));
                    }
                },

                // AfterLoop Delegate
                () =>
                {
                    WorkerThread.Stop(ref processingThread);

                    if (IsSocketConnected)
                        CloseSocket();

                    socket = null;
                    UnparsedBytes = null;

                    bool isConnected = false;
                    if (!IsFinishing && dlgtReconnect != null)
                        isConnected = dlgtReconnect();  // reconnection after certain delay

                    if (!isConnected)
                        Close();
                }, 0);
        }
       
        #endregion // Receive

        #region Sync. Send

        public int Send(string toSend)
        {
            int sent = 0;
            if (!string.IsNullOrEmpty(toSend))
                sent = Send(Encoding.ASCII.GetBytes(toSend));
                
            return sent;
        }

        public int Send(byte[] bts)
        {
            if (socket == null || !socket.Connected)
            {
                if (onSocketNullOrNotConnected != null)
                   onSocketNullOrNotConnected(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "Send"));

                return -1;
            }

            if (bts == null || bts.Length <= 0)
            {
                if (onNoBytesToSend != null)
                    onNoBytesToSend(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "Send"));

                return -1;
            }

            int sent = -1;
            try
            {
                sent = socket.Send(bts, 0, bts.Length, SocketFlags.None);
            }
            catch (Exception e)
            {
                if (onSyncSendException != null)
                    onSyncSendException(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "socket.Send, Sync.", null, e));
                
                sent = -1;
            }

            return sent;
        }

        #endregion // Sync. Send

        #region Async. Send

        public bool AsyncSend(string toSend)
        {
            bool br = false;
            if (!string.IsNullOrEmpty(toSend))
                br = AsyncSend(Encoding.ASCII.GetBytes(toSend));

            return br;
        }
   
        public bool AsyncSend(byte[] bts)
        {
            if (Interlocked.CompareExchange(ref failureToSend, 0, 0) != 0)
            {
                if (onAsyncSendFailureToSend != null)
                    onAsyncSendFailureToSend(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend"));
                
                return false;
            }

            if (socket == null || !socket.Connected)
            {
                if (onSocketNullOrNotConnected != null)
                    onSocketNullOrNotConnected(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend"));
                
                return false;
            }

            if (bts == null || bts.Length <= 0)
            {
                if (onNoBytesToSend != null)
                    onNoBytesToSend(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend"));
                
                return false;
            }

            bool br = false;
            if (Interlocked.CompareExchange(ref failureToSend, 0, 0) == 0)
            {
                try
                {
                    Stopwatch stopWatch = new Stopwatch();
                    stopWatch.Start();
                    br = socket.BeginSend(bts, 0, bts.Length, SocketFlags.None, new AsyncCallback(ar =>
                            {
                                // Async. Callback
                                Stopwatch stopWatchParam = ar.AsyncState as Stopwatch;

                                int sent = -1;
                                AddPendingBytes(bts.Length);
                                Exception ex = null;
                                try
                                {
                                    sent = socket.EndSend(ar);
                                    AddPendingBytes(-sent);
                                }
                                catch (Exception e)
                                {
                                    ex = e;
                                }

                                stopWatchParam.Stop();
                                double totalMs = stopWatchParam.Elapsed.TotalMilliseconds;

                                if (onInfo != null)
                                    onInfo(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Debug, "AsyncSend, Socket.BeginSend Callback",
                                               string.Format("TcpChannel \"{0}\": {1} bytes sent asynchronously in {2} ms. Pending bytes number is {3}.", Id, sent, totalMs, PendingBytesNumber)));

                                if (ex != null || sent == 0 || sent < bts.Length || totalMs > socket.SendTimeout)
                                {
                                    int result = Interlocked.Increment(ref failureToSend);

                                    Dictionary<string, object> dctData = new Dictionary<string, object>()
                                        {
                                            {"ActualSendTimeInMs", totalMs},
                                            {"SendTimeoutInMs", socket.SendTimeout},
                                            {"ToBeSentBytes", bts.Length},
                                            {"SentBytes", sent},
                                            {"PendingBytesNumber", PendingBytesNumber},
                                        };

                                    TcpChannelNotifyEventArgs e = new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend, Socket.BeginSend Callback", dctData, ex);

                                    if ((sent == 0 || sent < bts.Length) && onAsyncSendTooFewBytesSent != null)
                                        onAsyncSendTooFewBytesSent(this, e);

                                    if (totalMs > socket.SendTimeout && onAsyncSendSendingTimeoutExceeded != null)
                                        onAsyncSendSendingTimeoutExceeded(this, e);

                                    if (ex != null && onAsyncSendException != null)
                                        onAsyncSendException(this, e);
                                }
                            }), stopWatch) != null;
                }
                catch (Exception e)
                {
                    if (onAsyncSendException != null)
                        onAsyncSendException(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend, Socket.BeginSend Callback", string.Format("ERROR in TcpChannel \"{0}\": Failed to send {1} bytes. ", Id, bts.Length), e));
                    
                    br = false;
                };
            }
            else
                if (socket == null)
                {
                    if (onAsyncSendFailureToSend != null)
                        onAsyncSendFailureToSend(this, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "AsyncSend, Socket.BeginSend Callback", string.Format("ERROR: TcpChannel \"{0}\": Failed to send {1} bytes due to invalid socket.", Id, bts.Length)));
                    
                    br = false;
                }

            return br;
        }

        internal void AddPendingBytes(long bytesNum)
        {
            Interlocked.Add(ref pendingBytesNumber, bytesNum);
        }

        public long PendingBytesNumber
        {
            get { return Interlocked.Read(ref pendingBytesNumber); }
        }

        #endregion // Async. Send

        #region Closing

        public static void CloseAllChannels()
        {
            Interlocked.Increment(ref closingAllChannelsCount);
        }

        public void Close()
        {
            evForReconnect.Set();
            FinishChannel();
            WorkerThread.Stop(ref processingThread);
            WorkerThread.Stop(ref readingThread);
        }

        private void FinishChannel()
        {
            Interlocked.Increment(ref finishingCount);
        }

        protected bool IsFinishing
        {
            get 
            { 
                return Interlocked.CompareExchange(ref finishingCount, 0, 0) > 0 || 
                       Interlocked.CompareExchange(ref closingAllChannelsCount, 0, 0) > 0; 
            }         
        }

        private void ResetFinishing()
        {
            Interlocked.Exchange(ref finishingCount, 0);
            Interlocked.Exchange(ref closingAllChannelsCount, 0);
        }

        private void CloseSocket()
        {
            if (socket != null)
            { 
                socket.Shutdown(SocketShutdown.Both);
                socket.Close();
                socket = null;
            }
        }

        #endregion // Closing

        #region IP Address Handling

        public static string LocalHost
        {
            set
            {
                lock (typeof(TcpChannel))
                {
                    IPAddress.TryParse(value, out localIP);
                }
            }
        }

        static public IPAddress LocalIP
        {
            get
            {
                return localIP != null
                        ? localIP
                        : Dns.GetHostAddresses(Dns.GetHostName()).Where(item => item.AddressFamily == AddressFamily.InterNetwork && !item.IsIPv6LinkLocal).ToArray()[0];
            }
        }

        static public IPAddress GetIPAddress(string hostAddressOrName)
        {
            IPAddress ipAddr = null;
            if (!string.IsNullOrEmpty(hostAddressOrName) && hostAddressOrName.ToLower() != DEFAULT_LOCAL_HOST_NAME)
            {
                IPAddress ipAddrTemp;
                if (IPAddress.TryParse(hostAddressOrName, out ipAddrTemp))
                    ipAddr = ipAddrTemp;

                if (ipAddr == null)
                    ipAddr = Dns.GetHostEntry(hostAddressOrName).AddressList.Where(addr => addr.AddressFamily == AddressFamily.InterNetwork).ToArray()[0];
            }
            else
                ipAddr = LocalIP;

            return ipAddr;
        }

        #endregion // IP Address Handling

        #region Is

        internal bool IsSocketConnected
        {
            get { return socket != null && socket.Connected; }
        }

        protected virtual bool IsWithinReceiveTimeout
        {
            get
            {
                lock (sync)
                {
                    return DateTime.Now - lastReceiveTime < channelReceiveTimeout;
                }
            }
        }

        #endregion // Is

        #region Helpers

        private List<byte> BtsQueueToList()
        {
            List<byte> lstByte = new List<byte>();

            if (UnparsedBytes != null && UnparsedBytes.Length > 0)
            {
                lstByte.AddRange(UnparsedBytes);
                UnparsedBytes = null;
            }
       
            byte[] bts;
            while (cqueBytes.TryDequeue(out bts))
                lstByte.AddRange(bts);

            return lstByte.Count > 0 ? lstByte : null;
        }

        private void SetLastReceiveTime()
        {
            lock (sync)
            {
                lastReceiveTime = DateTime.Now;
            }
        }

        public DateTime LastReceiveTime
        {
            get
            {
                lock (sync)
                {
                    return lastReceiveTime;
                }
            }
        }

        #endregion // Helpers 
    }
}
