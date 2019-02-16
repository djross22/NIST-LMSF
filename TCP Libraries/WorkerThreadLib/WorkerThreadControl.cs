using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;

namespace IL.WorkerThreadLib
{
    public class WorkerThreadControl
    {
        private int proceed = 0;

        public bool ShouldProceed
        {
            get { return Interlocked.CompareExchange(ref proceed, 0, 0) == 0; }
        }

        public void SetToProceed()
        {
            Interlocked.Exchange(ref proceed, 0);
        }

        public void SetToStop()
        {
            Interlocked.Exchange(ref proceed, -1);
        }
    }
}
