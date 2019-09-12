using Gen5;
using System.Linq;

namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     Handles the collecting and organizing data from the microplate reader.
    ///     This includes receiving the data from the Gen5 Reader COM method GetRawData(), and populating
    ///     that data into a given <see cref="RealTimeData"/> model provided by the user of this class.
    /// </summary>
    public sealed class RealTimeDataHandler
    {
        private bool hasNewData = false;
        private readonly Plate plate;

        /// <summary>
        ///     Return codes that are returned from the Gen5 Reader COM method GetRawData().
        /// </summary>
        private enum GetRawDataStatusCodes
        {
            NoneAvailable = 0,
            FoundWithMore = 1,
            FoundWithNoMore = 2
        }

        /// <summary>
        ///     Initializes a new instance of the <see cref="RealTimeDataHandler" /> class.
        /// </summary>
        /// <param name="plate">
        ///     The <see cref="Plate" /> interface that implements the Gen5 Reader COM method GetRawData().
        /// </param>
        public RealTimeDataHandler(Plate plate)
        {
            this.plate = plate;
        }

        /// <summary>
        ///     Determines if there is new data returned from PollForData() method.
        ///     This is useful to only update the UI when there is new data to render.
        /// </summary>
        /// <returns>True if there is new data to render on the UI, false otherwise.</returns>
        public bool HasNewData()
        {
            if (hasNewData)
            {
                hasNewData = false;
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Uses the <see cref="Plate" /> interface to call the Gen5 Reader COM method GetRawData().
        ///     If the return code of this method indicates data was received, the data is added to the given
        ///     real time data model.
        /// </summary>
        /// <param name="realTimeData">
        ///     The real time data model to populate data into if the Gen5 Reader has new data.
        /// </param>
        public void PollForData(RealTimeData realTimeData)
        {
            var rawDataVariant = new GetRawDataVariantModel();

            var getRawDataStatusCode = plate.GetRawData(
                ref rawDataVariant.DataSetName,
                ref rawDataVariant.Rows,
                ref rawDataVariant.Columns,
                ref rawDataVariant.KineticIndexes,
                ref rawDataVariant.WavelengthIndexes,
                ref rawDataVariant.HorizontalIndexes,
                ref rawDataVariant.VerticalIndexes,
                ref rawDataVariant.Values,
                ref rawDataVariant.PrimaryStatuses,
                ref rawDataVariant.SecondaryStatuses);

            if ((GetRawDataStatusCodes)getRawDataStatusCode != GetRawDataStatusCodes.NoneAvailable)
            {
                hasNewData = true;
                var rawData = new GetRawDataModel(rawDataVariant);
                AddRawDataToDataSet(rawData, realTimeData);
            }
        }

        /// <summary>
        ///     Coordinates adding new raw data from the Gen5 Reader into the existing real time data model.
        /// </summary>
        /// <param name="rawData">The raw data derived from the Gen5 Reader.</param>
        /// <param name="realTimeData">The real time data model containing all the current data sets.</param>
        private void AddRawDataToDataSet(GetRawDataModel rawData, RealTimeData realTimeData)
        {
            var dataSet = GetMatchingDataSet(rawData, realTimeData);
            PopulateDataIntoDataSet(rawData, dataSet);
        }

        /// <summary>
        ///     Determines if the real time data model already contains a matching data set to the raw data
        ///     received from the Gen5 Reader. If no match is found, a new data set to contain this raw data
        ///     is created and populated into the real time data model.
        /// </summary>
        /// <param name="rawData">The raw data derived from the Gen5 Reader.</param>
        /// <param name="realTimeData">The real time data model containing all the current data sets.</param>
        /// <returns>
        ///     The raw data set that matches names with the raw data, or a new empty data set populated with
        ///     the raw data's data set name. The new empty data set is added to the real time data model's data
        ///     sets.
        /// </returns>
        private RawDataSetModel GetMatchingDataSet(GetRawDataModel rawData, RealTimeData realTimeData)
        {
            var dataSet = realTimeData.DataSets.FirstOrDefault(x => x.DataSetName == rawData.DataSetName);

            if (dataSet == null)
            {
                var newDataSet = new RawDataSetModel
                {
                    DataSetName = rawData.DataSetName,
                    ParameterName = $"Data Set: {rawData.DataSetName}"
                };

                dataSet = newDataSet;
                realTimeData.DataSets.Add(dataSet);
            }

            return dataSet;
        }

        /// <summary>
        ///     Populates raw data into a data set.
        /// </summary>
        /// <param name="rawData">The raw data derived from the Gen5 Reader.</param>
        /// <param name="dataSet">The data set that matches names with the raw data.</param>
        private void PopulateDataIntoDataSet(GetRawDataModel rawData, RawDataSetModel dataSet)
        {
            for (var i = 0; i < rawData.Values.Length; i++)
            {
                var row = rawData.Rows[i];
                var column = rawData.Columns[i];

                var data = new RawDataModel
                {
                    KineticIndex = rawData.KineticIndexes[i],
                    HorizontalIndex = rawData.HorizontalIndexes[i],
                    PrimaryStatus = rawData.PrimaryStatuses[i],
                    SecondaryStatus = rawData.SecondaryStatuses[i],
                    Value = rawData.Values[i],
                    VerticalIndex = rawData.VerticalIndexes[i],
                    WavelengthIndex = rawData.WavelengthIndexes[i]
                };

                dataSet.RawDataPlate[row, column].Add(data);
            }
        }
    }
}