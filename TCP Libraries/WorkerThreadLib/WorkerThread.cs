using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace IL.WorkerThreadLib
{
    public class WorkerThread
    {
        #region Internal Exception Class

        public class StopThreadException : Exception
        { 
        }

        #endregion // Internal Exception Class

        #region Vars

        private Thread thread = null;
        private AutoResetEvent ev = new AutoResetEvent(false);
        private WorkerThreadControl wtc = new WorkerThreadControl();

        public delegate void ExceptionHandlerDelegate(Exception e);
        private ExceptionHandlerDelegate dlgtExceptionHandler = null;

        public delegate void AfterLoopDelegate();
        
        #endregion // Vars

        #region Constructor

        public WorkerThread(ExceptionHandlerDelegate dlgtExceptionHandler = null)
        {
            this.dlgtExceptionHandler = dlgtExceptionHandler;
        }

        #endregion // Constructor

        #region Start

        public bool Start(ThreadStart dlgtThreadStart, AfterLoopDelegate dlgtAfterLoop = null, 
                            int delayInMs = Timeout.Infinite, bool synchStart = false)
        {
            bool br = false;
            if (dlgtThreadStart != null && thread == null || !thread.IsAlive)
            {
                AutoResetEvent evSynch = null;

                if (synchStart)
                    evSynch = new AutoResetEvent(false);

                thread = new Thread(new ThreadStart(() =>
                    {
                        if (evSynch != null)
                            evSynch.Set();

                        while (true)
                        {
                            if (delayInMs == Timeout.Infinite || delayInMs > 0)
                                ev.WaitOne(delayInMs);

                            if (wtc.ShouldProceed)
                            {
                                try
                                {
                                    dlgtThreadStart();
                                }
                                catch (StopThreadException)
                                {
                                    wtc.SetToStop();
                                }
                                catch (Exception e)
                                {
                                    ExceptionHandler(e);
                                }
                            }
                            else
                                break;
                        }

                        if (dlgtAfterLoop != null)
                        {
                            try
                            {
                                dlgtAfterLoop();
                            }
                            catch (Exception e)
                            {
                                ExceptionHandler(e);
                            }
                        }
                    }));

                thread.Start();
                
                if (evSynch != null)
                    evSynch.WaitOne();

                br = true;
            }

            return br;
        }

        private void ExceptionHandler(Exception e)
        {
            if (dlgtExceptionHandler != null)
            {
                try
                {
                    dlgtExceptionHandler(e);
                }
                catch { }
            }
        }

        #endregion // Start

        #region Stop, Close

        public static void Stop(ref WorkerThread workerThread)
        {
            if (workerThread != null)
            {
                workerThread.Stop();
                workerThread = null;
            }
        }

        public static void CloseThread(int timeoutInMs, ref Thread thread)
        {
            try
            {
                if (thread != null)
                    if (!thread.Join(timeoutInMs))
                        thread.Abort();
            }
            catch
            { 
            }
            finally
            {
                thread = null;
            }
        }

        private void Stop()
        {
            wtc.SetToStop();
            ev.Set();
            CloseThread(3000, ref thread);
        }

        public void SetEvent()
        {
            ev.Set();
        }

        public void SetToStop()
        {
            wtc.SetToStop();
        }

        #endregion // Stop, Close

        #region Is

        public bool IsThreadActive
        {
            get
            {
                return thread != null &&
                            (thread.ThreadState == ThreadState.Background ||
                             thread.ThreadState == ThreadState.Running ||
                             thread.ThreadState == ThreadState.WaitSleepJoin);
            }
        }

        #endregion // Is
    }
}
