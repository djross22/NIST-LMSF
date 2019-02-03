using System;
using System.IO;
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
    public partial class ChooseFileDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string promptText;
        private string chooseFilePath;
        private string initialDirectory;
        private string fileName;
        public string Filter { get; set; } //example: "XML documents (.xml)|*.xml"


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
        
        public string ChooseFilePath
        {
            get { return this.chooseFilePath; }
            set
            {
                this.chooseFilePath = value;
                OnPropertyChanged("ChooseFilePath");
            }
        }
        #endregion

        public ChooseFileDialog(string initialDir, string filter)
        {
            InitializeComponent();
            DataContext = this;

            initialDirectory = initialDir;
            Filter = filter;
            ChooseFilePath = "";// System.IO.Path.Combine(initialDirectory, fileName);
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
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            dlg.FileName = fileName; // Default file name
            dlg.DefaultExt = ".xml"; // Default file extension
            if (Filter != "")
            {
                dlg.Filter = Filter; // Filter files by extension
            }

            initialDirectory = initialDirectory.Replace(@"\\", @"\");

            dlg.InitialDirectory = initialDirectory;

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Choose document
                ChooseFilePath = dlg.FileName;
            }
        }
    }
}
