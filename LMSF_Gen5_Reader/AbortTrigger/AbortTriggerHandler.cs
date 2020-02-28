using System.Collections.Generic;
using System.Linq;

namespace LMSF_Gen5_Reader.AbortTrigger
{
    /// <summary>
    ///     Handles the management of the abort triggers for a given experiment run.
    ///     This includes getting and setting the UI elements to allow the user to see/set abort thresholds,
    ///     and poll all the triggers to determine if a run should be aborted.
    /// </summary>
    public sealed class AbortTriggerHandler
    {
        public string LastErrorMessage { get; private set; } = string.Empty;

        private List<AbortTriggerProfile> abortTriggerProfiles { get; } = new List<AbortTriggerProfile>();
        private AbortTriggerUI abortTriggerUI { get; }

        /// <summary>
        ///     Initializes a new instance of the <see cref="AbortTriggerHandler"/> class.
        /// </summary>
        /// <param name="abortTriggerUI">
        ///     The UI elements that are relevant to getting and setting abort trigger thresholds.
        /// </param>
        public AbortTriggerHandler(AbortTriggerUI abortTriggerUI)
        {
            this.abortTriggerUI = abortTriggerUI;
        }

        /// <summary>
        ///     Handles setting the UI elements to the values held in a given abort trigger profile.
        /// </summary>
        /// <param name="dataSetName">The data set name used to lookup which abort trigger is relevant.</param>
        public void HandleLoad(string dataSetName)
        {
            var profile = GetMatchingAbortTriggerProfile(dataSetName);
            abortTriggerUI.AverageCheckBox.IsChecked = profile.EnabledAverageTrigger;
            abortTriggerUI.AverageTextBox.Text = profile.ValueAverageTrigger.ToString("F99").TrimEnd('0');
            abortTriggerUI.MaximumCheckBox.IsChecked = profile.EnabledMaximumTrigger;
            abortTriggerUI.MaximumTextBox.Text = profile.ValueMaximumTrigger.ToString("F99").TrimEnd('0');
        }

        /// <summary>
        ///     Handles setting the abort trigger values from the values entered in the UI.
        /// </summary>
        /// <param name="dataSetName">The data set name used to lookup which abort trigger is relevant.</param>
        public void HandleSave(string dataSetName)
        {
            var profile = GetMatchingAbortTriggerProfile(dataSetName);
            profile.EnabledAverageTrigger = abortTriggerUI.AverageCheckBox.IsChecked ?? false;
            profile.EnabledMaximumTrigger = abortTriggerUI.MaximumCheckBox.IsChecked ?? false;
            profile.ValueAverageTrigger = ParseDoubleFromText(abortTriggerUI.AverageTextBox.Text);
            profile.ValueMaximumTrigger = ParseDoubleFromText(abortTriggerUI.MaximumTextBox.Text);
        }

        /// <summary>
        ///     returns Abort Trigger Profile for currently selected dataset or null.
        /// </summary>
        /// <param name="dataSetName">The data set name used to lookup which abort trigger is relevant.</param>
        public AbortTriggerProfile getAbortTriggerProfileByDataset(string dataSetName)
        {
            return GetMatchingAbortTriggerProfile(dataSetName);
                
        }

        /// <summary>
        ///     Determines if a run should be aborted by querying the abort triggers with the given
        ///     <see cref="RealTimeData.RealTimeData"/>.
        /// </summary>
        /// <param name="realTimeData">The real time data currently being read by the instrument.</param>
        /// <returns>True if an abort trigger threshold was crossed, false otherwise.</returns>
        public bool ShouldRunAbort(RealTimeData.RealTimeData realTimeData)
        {
            foreach (var abortTriggerProfile in abortTriggerProfiles)
            {
                if (abortTriggerProfile.IsTriggered(realTimeData))
                {
                    LastErrorMessage = abortTriggerProfile.LastErrorMessage;
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        ///     Finds the matching <see cref="AbortTriggerProfile"/> from a given data set name.
        ///     If no abort trigger profile is found, a new empty one is created with the given data
        ///     set name.
        /// </summary>
        /// <param name="dataSetName">The data set name used to identify a particular set of data.</param>
        /// <returns>The matching abort trigger profile.</returns>
        private AbortTriggerProfile GetMatchingAbortTriggerProfile(string dataSetName)
        {
            var selectedProfile = abortTriggerProfiles.FirstOrDefault(x => x.DataSetName.Equals(dataSetName));

            if (selectedProfile == null)
            {
                selectedProfile = new AbortTriggerProfile(dataSetName);
                abortTriggerProfiles.Add(selectedProfile);
            }            

            return selectedProfile;
        }

        public void addPersistedAbortProfile(AbortTriggerProfile persistedProfile)
        {
            if(persistedProfile == null)
            {
                return;
            }
            //confirm the profile isn't already in the list
            var selectedProfile = abortTriggerProfiles.FirstOrDefault(x => x.DataSetName.Equals(persistedProfile.DataSetName));
            if(selectedProfile != null)
            {
                throw new System.ArgumentException("ERROR Profile for " + persistedProfile.DataSetName + "already exists./ n");
            }
            abortTriggerProfiles.Add(persistedProfile);
        }


        /// <summary>
        ///     Parses the text from a TextBox UI element intended to hold type <see cref="double"/> numbers.
        /// </summary>
        /// <param name="text">The text from the TextBox UI element to parse.</param>
        /// <returns>The parsed double of the text, or 0 if the value couldn't be parsed.</returns>
        private double ParseDoubleFromText(string text)
        {
            var success = double.TryParse(text, out var result);

            if (success)
            {
                return result;
            }

            return 0d;
        }
    }
}