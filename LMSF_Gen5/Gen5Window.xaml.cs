using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using LMSF_Utilities;
using LMSF_Gen5_Reader;
using Gen5;

namespace LMSF_Gen5
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class Gen5Window : Window, INotifyPropertyChanged, IReaderTextOut
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string experimentId;
        private string expFolderPath;
        private string protocolPath;
        private string textOut;

        private Gen5Reader gen5Reader;

        public Gen5Window()
        {
            InitializeComponent();
            DataContext = this;

            gen5Reader = new Gen5Reader(this);

            TextOut = gen5Reader.StartGen5();
            TextOut += gen5Reader.SetClientWindow(this);
            TextOut += gen5Reader.ConfigureUSBReader();
        }

        #region Properties Getters and Setters
        public string TextOut
        {
            get { return this.textOut; }
            set
            {
                this.textOut = value;
                OnPropertyChanged("TextOut");
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

        public string ExpFolderPath
        {
            get { return this.expFolderPath; }
            set
            {
                this.expFolderPath = value;
                OnPropertyChanged("ExpFolderPath");
            }
        }

        public string ProtocolPath
        {
            get { return this.protocolPath; }
            set
            {
                this.protocolPath = value;
                OnPropertyChanged("ProtocolPath");
            }
        }
        #endregion

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void SelectProtocolButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            //dlg.DefaultExt = ".prt"; // Default file extension
            dlg.Filter = "Gen5 Protocol (.prt)|*.prt"; // Filter files by extension
            //dlg.InitialDirectory = initialDirectory;
            dlg.Title = "Select Gen5 Protocol:";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                ProtocolPath = dlg.FileName;
            }
        }

        private void SelectExpFolderButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.SetClientWindow(this);
            ExpFolderPath = gen5Reader.BrowseForFolder();
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            if (gen5Reader.IsGen5Active())
            {
                TextOut += gen5Reader.TerminateGen5();
            }
        }

        private void NewExpButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.NewExperiment(ProtocolPath);

            gen5Reader.ExperimentID = ExperimentId;
            gen5Reader.ExperimentFolderPath = ExpFolderPath;
            TextOut += gen5Reader.ExpSaveAs();

            TextOut += gen5Reader.PlatesGetPlate();
        }

        private void RunExpButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.PlateStartRead();

            TextOut += gen5Reader.WaitForFinishThenExportAndClose();
        }

        private void OpenButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.CarrierOut();
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.CarrierIn();
        }

        //Button Click event handlers to be deleted after initial testing
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut = gen5Reader.StartGen5();
            TextOut += gen5Reader.SetClientWindow(this);
            TextOut += gen5Reader.ConfigureUSBReader();
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.PlateFileExport();
        }

        private void TemperatureButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.GetCurrentTemperature();
        }

        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadNotStarted;
            TextOut += gen5Reader.PlateReadStatus(ref status);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.ExpSave();
        }
    }
}
