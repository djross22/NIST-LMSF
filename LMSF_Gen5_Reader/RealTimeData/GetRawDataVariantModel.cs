namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     This data model holds the unmanaged data returned from the Gen5 Reader COM object's
    ///     GetRawData() method.
    /// </summary>
    internal sealed class GetRawDataVariantModel
    {
        public string DataSetName;
        public object Rows;
        public object Columns;
        public object KineticIndexes;
        public object WavelengthIndexes;
        public object HorizontalIndexes;
        public object VerticalIndexes;
        public object Values;
        public object PrimaryStatuses;
        public object SecondaryStatuses;
    }
}