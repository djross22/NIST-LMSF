using System.Windows.Controls;

namespace LMSF_Gen5_Reader.AbortTrigger
{
    /// <summary>
    ///     Holds the needed UI elements for the user to see and set values related to abort triggers.
    /// </summary>
    public sealed class AbortTriggerUI
    {
        public CheckBox AverageCheckBox { get; set; }
        public TextBox AverageTextBox { get; set; }
        public CheckBox MaximumCheckBox { get; set; }
        public TextBox MaximumTextBox { get; set; }
    }
}