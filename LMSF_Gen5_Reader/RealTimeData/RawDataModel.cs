namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     This data model holds one read of data for a given row/column from the reader.
    /// </summary>
    public sealed class RawDataModel
    {
        public long KineticIndex { get; set; }
        public long WavelengthIndex { get; set; }
        public long HorizontalIndex { get; set; }
        public long VerticalIndex { get; set; }
        public double Value { get; set; }
        public long PrimaryStatus { get; set; }
        public long SecondaryStatus { get; set; }
    }
}