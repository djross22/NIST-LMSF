using System;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Linq;
using System.IO;
using System.IO.Compression;
using System.Text;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;
using System.Diagnostics;
using System.Configuration;
using IL.WorkerThreadLib;

namespace IL.TcpCommunicationLib
{
    public class TcpServer : TcpChannel
    {
        #region Var
       
        private static ConcurrentDictionary<TcpListener, WorkerThread> cdctThread = new ConcurrentDictionary<TcpListener, WorkerThread>();

        #endregion // Var

        #region Delegate

        protected delegate void AcceptEndDelegate(TcpServer tcpServer);
        protected delegate void ExceptionDelegate(Exception e);

        #endregion // Delegate

        #region Event

        private static TcpChannelEventHandler<TcpChannelNotifyEventArgs> onServerNotifies = null;

        #endregion // Event

        #region Constructor

        internal TcpServer(Socket socket,  TcpChannelEventHandler<TcpChannelNotifyEventArgs> onNotify,
                           int socketReceiveTimeoutInSec, int socketSendTimeoutInSec, int socketReceiveBufferSize, int socketSendBufferSize) :
            base(socket, onNotify, socketReceiveTimeoutInSec, socketSendTimeoutInSec, socketReceiveBufferSize, socketSendBufferSize)
        {
        }

        #endregion // Constructors

        #region Accept Socket

        private static void AcceptBegin(TcpListener tcpListener, AutoResetEvent evAccept, AcceptEndDelegate dlgtAcceptEnd, ExceptionDelegate dlgtException,
                                        int socketReceiveTimeoutInSec, int socketSendTimeoutInSec, int socketReceiveBufferSize, int socketSendBufferSize)
        {
            tcpListener.BeginAcceptSocket(new AsyncCallback(ar =>
                {
                    evAccept.Set();

                    lock (typeof(TcpChannel))
                    {
                        try
                        {
                            dlgtAcceptEnd(new TcpServer((ar.AsyncState as TcpListener).EndAcceptSocket(ar), onServerNotifies,
                                          socketReceiveTimeoutInSec, socketSendTimeoutInSec, socketReceiveBufferSize, socketSendBufferSize));
                        }
                        catch (Exception e)
                        {
                            dlgtException(e);
                        }
                    }
                }), tcpListener);
        }

        public static void StartAcceptSubscribersOnPort(int acceptPort, 
                                                        TcpChannelEventHandler<TcpChannelReceivedEventArgs> onReceived,
                                                        TcpChannelEventHandler<EventArgs> onInitConnectionToServer = null,
                                                        TcpChannelEventHandler<TcpChannelNotifyEventArgs> onServerNotifies = null,
                                                        int socketReceiveTimeoutInSec = DEFAULT_RECEIVE_TIMEOUT_IN_SEC,
                                                        int socketSendTimeoutInSec = DEFAULT_SEND_TIMEOUT_IN_SEC,
                                                        int socketReceiveBufferSize = DEFAULT_RECEIVE_BUFFER_SIZE,
                                                        int socketSendBufferSize = DEFAULT_SEND_BUFFER_SIZE)
        {
            if (onReceived == null)
                throw new Exception("onReceived is null.");

            TcpServer.onServerNotifies += onServerNotifies;
            string processName = Process.GetCurrentProcess().ProcessName;
            
            AutoResetEvent evAccept = new AutoResetEvent(false);
            TcpListener tcpListener = null;
            try
            {
                tcpListener = StartListenOnPort(acceptPort);
            }
            catch (Exception e)
            {
                if (TcpServer.onServerNotifies != null)
                    TcpServer.onServerNotifies(null, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "StartAcceptSubscribersOnPort", null, e));
            }

            if (tcpListener != null)
            {
                WorkerThread wtAccept = new WorkerThread(e => 
                    {
                        if (TcpServer.onServerNotifies != null)
                            TcpServer.onServerNotifies(null, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "StartAcceptSubscribersOnPort, Accept Worker Thread", null, e));
                    });
                        
                cdctThread[tcpListener] = wtAccept;
                    
                wtAccept.Start(() =>
                    {
                        AcceptBegin(tcpListener, evAccept,
                                    tcpServer =>
                                    {
                                        if (tcpServer.IsSocketConnected)
                                        {
                                            tcpServer.onReceived += onReceived;
                                            tcpServer.Receive();

                                            if (onInitConnectionToServer != null)
                                                onInitConnectionToServer(tcpServer, null);
                                        }
                                    },
                                    ex => 
                                    {
                                        if (TcpServer.onServerNotifies != null)
                                            TcpServer.onServerNotifies(null, new TcpChannelNotifyEventArgs(TcpChannelNotifyEventArgs.NotificationLevel.Error, "StartAcceptSubscribersOnPort.AcceptBegin, AcceptEnd", null, ex));
                                    },
                                    socketReceiveTimeoutInSec, socketSendTimeoutInSec, socketReceiveBufferSize, socketSendBufferSize);

                        evAccept.WaitOne();

                        wtAccept.SetEvent();
                    });

                wtAccept.SetEvent();
            }
        }

        public static TcpListener StartListenOnPort(int port)
        {
            TcpListener tcpListener = new TcpListener(new IPEndPoint(TcpChannel.LocalIP, port));
            tcpListener.Start();
            return tcpListener;
        }

        public static void StopAcceptSubscribersOnPort(int acceptPort)
        {
            IEnumerable<TcpListener> colLr = cdctThread.Keys.Where(lr => (lr.Server.LocalEndPoint as IPEndPoint).Port == acceptPort);
            if (colLr != null && colLr.Count() == 1)
            {
                TcpListener tcpListener = colLr.First();
                StopOne(tcpListener);

                WorkerThread wt;
                cdctThread.TryRemove(tcpListener, out wt);
            }
        }

        public static void StopAllAcceptSubscribers()
        {
            foreach (TcpListener tcpListener in cdctThread.Keys)
                StopOne(tcpListener);

            cdctThread.Clear();
        }

        private static void StopOne(TcpListener tcpListener)
        {
            if (tcpListener == null)
                return;

            WorkerThread wt = cdctThread[tcpListener];

            if (tcpListener != null)
            {
                tcpListener.Stop();
                tcpListener = null;
            }

            wt.SetToStop();
            wt.SetEvent();
        }

        #endregion // Accept Socket
    }
}
