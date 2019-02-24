using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace LMSF_Utilities
{
    public interface IReportsRemoteStatus
    {
        SharedParameters.ServerStatusStates ServerStatus { get; }
    }
}
