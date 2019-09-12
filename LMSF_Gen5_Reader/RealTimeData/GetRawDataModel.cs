namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     This model holds the managed data and types from the unmanaged <see cref="GetRawDataVariantModel"/>.
    /// </summary>
    internal sealed class GetRawDataModel
    {
        public string DataSetName { get; set; } = string.Empty;
        public long[] Rows { get; set; }
        public long[] Columns { get; set; }
        public long[] KineticIndexes { get; set; }
        public long[] WavelengthIndexes { get; set; }
        public long[] HorizontalIndexes { get; set; }
        public long[] VerticalIndexes { get; set; }
        public double[] Values { get; set; }
        public long[] PrimaryStatuses { get; set; }
        public long[] SecondaryStatuses { get; set; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="GetRawDataModel" /> class, populating itself from the
        ///     data of a given instance of <see cref="GetRawDataVariantModel"/>.
        /// </summary>
        /// <param name="rawDataVariant">
        ///     The unmanaged data/types populated from the Gen5 Reader COM object's GetRawData() method.
        /// </param>
        public GetRawDataModel(GetRawDataVariantModel rawDataVariant)
        {
            DataSetName = rawDataVariant.DataSetName;

            var arraySize = ((double[])rawDataVariant.Values).Length;

            Rows = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                Rows[i] = ((int[])rawDataVariant.Rows)[i];
            }

            Columns = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                Columns[i] = ((int[])rawDataVariant.Columns)[i];
            }

            KineticIndexes = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                KineticIndexes[i] = ((int[])rawDataVariant.KineticIndexes)[i];
            }

            WavelengthIndexes = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                WavelengthIndexes[i] = ((int[])rawDataVariant.WavelengthIndexes)[i];
            }

            HorizontalIndexes = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                HorizontalIndexes[i] = ((int[])rawDataVariant.HorizontalIndexes)[i];
            }

            VerticalIndexes = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                VerticalIndexes[i] = ((int[])rawDataVariant.VerticalIndexes)[i];
            }

            Values = new double[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                Values[i] = ((double[])rawDataVariant.Values)[i];
            }

            PrimaryStatuses = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                PrimaryStatuses[i] = ((int[])rawDataVariant.PrimaryStatuses)[i];
            }

            SecondaryStatuses = new long[arraySize];
            for (var i = 0; i < arraySize; i++)
            {
                SecondaryStatuses[i] = ((int[])rawDataVariant.SecondaryStatuses)[i];
            }
        }
    }
}