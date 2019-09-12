using System.Collections.Generic;

namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     Holds all the data to be collected as read from the microplate reader in real time for the application.
    /// </summary>
    public sealed class RealTimeData
    {
        public List<RawDataSetModel> DataSets { get; } = new List<RawDataSetModel>();
    }
}