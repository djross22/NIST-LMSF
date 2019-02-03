using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace LMSF_Utilities
{
    /// <summary>
    /// Interaction logic for SelectMetaIdentDialog.xaml
    /// </summary>
    /// 
    public partial class AppendExperimentIdDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string promptText;
        private string experimentId;
        private string saveFilePath;
        private string initialDirectory;
        private string fileName;
        

        #region Properties Getters and Setters
        public string PromptText
        {
            get { return this.promptText; }
            set
            {
                this.promptText = value;
                OnPropertyChanged("PromptText");
            }
        }

        public string ExperimentId
        {
            get { return this.experimentId; }
            set
            {
                this.experimentId = value;
                OnPropertyChanged("ExperimentId");
            }
        }
        
        public string SaveFilePath
        {
            get { return this.saveFilePath; }
            set
            {
                this.saveFilePath = value;
                OnPropertyChanged("SaveFilePath");
            }
        }
        #endregion

        public AppendExperimentIdDialog(string initialDir, string defaultId)
        {
            InitializeComponent();
            DataContext = this;

            ExperimentId = defaultId;
            initialDirectory = initialDir;
            fileName = $"{defaultId}.xml";
            SaveFilePath = initialDirectory + fileName;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedParameters.IsValid(this))
            {
                this.DialogResult = true;
            }
            else
            {
                return;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void SelectFileButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();
            dlg.FileName = fileName; // Default file name
            dlg.DefaultExt = ".xml"; // Default file extension
            dlg.Filter = "XML documents (.xml)|*.xml"; // Filter files by extension
            dlg.InitialDirectory = initialDirectory;
            dlg.Title = "Select XML File for Saving Metadata (Other protocol data will be saved in the same folder):";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                SaveFilePath = dlg.FileName;
            }
        }
    }
}
