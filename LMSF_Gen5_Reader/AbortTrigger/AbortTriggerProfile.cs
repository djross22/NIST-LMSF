using LMSF_Gen5_Reader.RealTimeData;
using System;
using System.Collections.Generic;
using System.Linq;

namespace LMSF_Gen5_Reader.AbortTrigger
{
    /// <summary>
    ///     Holds the abort triggers for a data set, using the data set name as the unique identifier.
    ///     This class can be polled with real time data to determine if the abort thresholds have
    ///     been crossed.
    /// </summary>
    public sealed class AbortTriggerProfile
    {
        public string DataSetName { get; } = string.Empty;
        public bool EnabledAverageTrigger { get; set; } = false;
        public bool EnabledMaximumTrigger { get; set; } = false;
        public string LastErrorMessage { get; private set; } = string.Empty;
        public double ValueAverageTrigger { get; set; } = 0d;
        public double ValueMaximumTrigger { get; set; } = 0d;

        /// <summary>
        ///     Initializes a new instance of the <see cref="AbortTriggerProfile"/> class.
        ///     Uses the data set name as the unique identifier for this abort trigger profile.
        /// </summary>
        /// <param name="dataSetName"></param>
        public AbortTriggerProfile(string dataSetName)
        {
            DataSetName = dataSetName;
        }

        /// <summary>
        ///     Inatliizes a new instance of the <see cref="AbortTriggerProfile"/> class as a clone of the origional.
        /// </summary>
        /// <param name="profile"></param>
        private AbortTriggerProfile(AbortTriggerProfile profile)
        {
            DataSetName = profile.DataSetName;
            EnabledAverageTrigger = profile.EnabledAverageTrigger;
            EnabledMaximumTrigger = profile.EnabledMaximumTrigger;
            ValueAverageTrigger = profile.ValueAverageTrigger;
            ValueMaximumTrigger = profile.ValueMaximumTrigger;
        }

        /// <summary>
        ///     Returns a clone of this instance.
        /// </summary>
        /// <returns></returns>
        public AbortTriggerProfile getClone()
        {
            return new AbortTriggerProfile(this);
        }

        /// <summary>
        ///     Determines if this class' average or maximum abort trigger thresholds have been passed
        ///     from a given <see cref="RealTimeData.RealTimeData"/> class.
        ///     If triggered, the property LastErrorMessage will contain the specific reason.
        /// </summary>
        /// <param name="realTimeData">The real time data currently being read by the instrument.</param>
        /// <returns>True if either the average or maximum abort thresholds are crossed, false otherwise.</returns>
        public bool IsTriggered(RealTimeData.RealTimeData realTimeData)
        {
            if (IsAverageTriggered(realTimeData) || IsMaximumTriggered(realTimeData))
            {
                return true;
            }

            return false;
        }

        /// <summary>
        ///     Retrieves the data set specific to this abort trigger from a
        ///     <see cref="RealTimeData.RealTimeData"/> class.
        /// </summary>
        /// <param name="realTimeData">The real time data currently being read by the instrument.</param>
        /// <returns>
        ///     The matching data set that this classes abort triggers are relevant to, or null if none found.
        /// </returns>
        private RawDataSetModel GetMatchingDataSet(RealTimeData.RealTimeData realTimeData)
            => realTimeData.DataSets.FirstOrDefault(x => x.ParameterName.Equals(DataSetName));

        /// <summary>
        ///     Determines if the average threshold has been passed.
        ///     Per each full read of all 96-wells, calculates the average and gets the difference from the
        ///     first average reading and the last average reading. If this calculated average is over the
        ///     user entered average OD threshold, this abort trigger is activated.
        /// </summary>
        /// <param name="realTimeData">The real time data currently being read by the instrument.</param>
        /// <returns>True if the average threshold from the user has been exceeded, false otherwise.</returns>
        private bool IsAverageTriggered(RealTimeData.RealTimeData realTimeData)
        {
            var matchingDataSet = GetMatchingDataSet(realTimeData);

            if (!EnabledAverageTrigger || matchingDataSet == null)
            {
                return false;
            }

            var fullyPopulatedDataSetTotalValues = new List<double>();

            // Read each index until we run out of indexes or a half-populated (in progress) index is found
            for (var readPosition = 0; readPosition < int.MaxValue; readPosition++)
            {
                var dataSetTotalValue = 0d;
                var isDataSetFullyPopulated = true;

                for (var row = 0; row < RawDataSetModel.PlateRows; row++)
                {
                    for (var column = 0; column < RawDataSetModel.PlateColumns; column++)
                    {
                        if (matchingDataSet.RawDataPlate[row, column].Count > readPosition)
                        {
                            dataSetTotalValue += matchingDataSet.RawDataPlate[row, column][readPosition].Value;
                        }
                        else
                        {
                            isDataSetFullyPopulated = false;
                        }
                    }
                }

                if (isDataSetFullyPopulated)
                {
                    var dataSetAverage = dataSetTotalValue / (RawDataSetModel.PlateRows * RawDataSetModel.PlateColumns);
                    fullyPopulatedDataSetTotalValues.Add(dataSetAverage);
                }
                else
                {
                    break;
                }
            }

            // Check if threshold was reached; return true if triggered
            if (fullyPopulatedDataSetTotalValues.Count >= 2)
            {
                var firstAverage = fullyPopulatedDataSetTotalValues.First();
                var lastAverage = fullyPopulatedDataSetTotalValues.Last();

                if (Math.Abs(lastAverage - firstAverage) > ValueAverageTrigger)
                {
                    LastErrorMessage = $"ABORT TRIGGERED\n"
                        + $"Data set: [{DataSetName}]\n"
                        + $"Entered average threshold: [{ValueAverageTrigger.ToString("F99").TrimEnd('0')}]\n"
                        + $"First OD average: [{firstAverage.ToString("F99").TrimEnd('0')}]\n"
                        + $"Last OD average: [{lastAverage.ToString("F99").TrimEnd('0')}]\n"
                        + $"Calculated difference: [{Math.Abs(lastAverage - firstAverage).ToString("F99").TrimEnd('0')}]\n";
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Determines if the maximum threshold has been passed.
        ///     Per each well, calculates the difference from the first reading and the last reading. If this
        ///     difference is over the user entered maximum OD threshold, this abort trigger is activated.
        /// </summary>
        /// <param name="realTimeData">The real time data currently being read by the instrument.</param>
        /// <returns>True if the maximum threshold from the user has been exceeded, false otherwise.</returns>
        private bool IsMaximumTriggered(RealTimeData.RealTimeData realTimeData)
        {
            var matchingDataSet = GetMatchingDataSet(realTimeData);

            if (!EnabledMaximumTrigger || matchingDataSet == null)
            {
                return false;
            }

            for (var row = 0; row < RawDataSetModel.PlateRows; row++)
            {
                for (var column = 0; column < RawDataSetModel.PlateColumns; column++)
                {
                    // Make sure there are 2 or more readings
                    if (matchingDataSet.RawDataPlate[row, column].Count >= 2)
                    {
                        // Get first and last
                        var firstValue = matchingDataSet.RawDataPlate[row, column].First().Value;
                        var lastValue = matchingDataSet.RawDataPlate[row, column].Last().Value;

                        // Check if the maximum threshold was reached
                        if (Math.Abs(lastValue - firstValue) > ValueMaximumTrigger)
                        {
                            LastErrorMessage = $"ABORT TRIGGERED\nData set: [{DataSetName}]\n"
                                + $"Entered maximum threshold: [{ValueMaximumTrigger.ToString("F99").TrimEnd('0')}]\n"
                                + $"First OD: [{firstValue.ToString("F99").TrimEnd('0')}]\n"
                                + $"Last OD: [{lastValue.ToString("F99").TrimEnd('0')}]\n"
                                + $"Calculated difference: [{Math.Abs(lastValue - firstValue).ToString("F99").TrimEnd('0')}]\n";
                            return true;
                        }
                    }
                }
            }

            return false;
        }
    }
}