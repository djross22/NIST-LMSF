using System.Collections.Generic;

namespace LMSF_Gen5_Reader.RealTimeData
{
    /// <summary>
    ///     This data model holds all the data from a given data set, organized by the data set name.
    /// </summary>
    public sealed class RawDataSetModel
    {
        private const int PlateRows = 8;
        private const int PlateColumns = 12;

        public string DataSetName { get; set; } = string.Empty;

        public string ParameterName { get; set; } = string.Empty;

        public List<RawDataModel>[,] RawDataPlate { get; } = new List<RawDataModel>[PlateRows, PlateColumns];

        /// <summary>
        ///     Initializes a new instance of the <see cref="RawDataSetModel" /> class.
        ///     Resets the data collections in the plate array to empty lists.
        /// </summary>
        public RawDataSetModel()
        {
            for (var row = 0; row < PlateRows; row++)
            {
                for (var column = 0; column < PlateColumns; column++)
                {
                    RawDataPlate[row, column] = new List<RawDataModel>();
                }
            }
        }
    }
}