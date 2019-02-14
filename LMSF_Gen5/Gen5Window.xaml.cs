using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
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
        private bool isReadRunning;
        private BackgroundWorker readerMonitorWorker;

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
        public bool IsReadRunning
        {
            get { return this.isReadRunning; }
            set
            {
                this.isReadRunning = value;
                OnPropertyChanged("IsReadRunning");
                if (isReadRunning)
                {
                    statusBorder.Background = Brushes.LimeGreen;
                    statusTextBlock.Text = "Read In Progress";
                }
                else
                {
                    statusBorder.Background = Brushes.Red;
                    statusTextBlock.Text = "Reader Idle";
                }
            }
        }

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
            NewExp();
        }

        private void NewExp()
        {
            //Set button and other controls disabled
            newExpButton.IsEnabled = false;
            experimentIdTextBox.IsEnabled = false;
            selectExpFolderButton.IsEnabled = false;
            selectProtocolButton.IsEnabled = false;

            TextOut += gen5Reader.NewExperiment(ProtocolPath);

            gen5Reader.ExperimentID = ExperimentId;
            gen5Reader.ExperimentFolderPath = ExpFolderPath;
            TextOut += gen5Reader.ExpSaveAs();

            TextOut += gen5Reader.PlatesGetPlate();
        }

        private void RunExpButton_Click(object sender, RoutedEventArgs e)
        {
            RunExp();
        }

        private void RunExp()
        {
            TextOut += gen5Reader.PlateStartRead();

            TextOut += WaitForFinishThenExportAndClose();
        }

        public string WaitForFinishThenExportAndClose()
        {
            string retStr = "Running WaitForFinishThenExportAndClose\n";

            if (!(readerMonitorWorker is null))
            {
                if (readerMonitorWorker.IsBusy)
                {
                    retStr += "Read in progress, abort read or wait until end of read before starting a new read.\n";
                    return retStr;
                }
            }

            readerMonitorWorker = new BackgroundWorker();
            readerMonitorWorker.WorkerReportsProgress = false;
            readerMonitorWorker.DoWork += ReaderMonitor_DoWork;
            readerMonitorWorker.RunWorkerCompleted += ReaderMonitor_RunWorkerCompleted;

            readerMonitorWorker.RunWorkerAsync();

            retStr += "    ... Read in Progress...\n";

            return retStr;
        }

        void ReaderMonitor_DoWork(object sender, DoWorkEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadInProgress;
            bool liveData = gen5Reader.Gen5App.DataExportEnabled;

            while (status == Gen5ReadStatus.eReadInProgress)
            {
                Thread.Sleep(100);
                gen5Reader.PlateReadStatus(ref status); //Note: the PlateReadStatus sets the IsReading Property according to state of reader.

                //TODO: Handle live data stream

                this.Dispatcher.Invoke(() => {
                    if (status == Gen5ReadStatus.eReadInProgress)
                    {
                        IsReadRunning = true;
                    }
                    else
                    {
                        IsReadRunning = false;
                    }
                });
            }

        }

        void ReaderMonitor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadInProgress;
            //TODO: Handle outcomes other than "eReadCompleted" or "eReadAborted"
            gen5Reader.PlateFileExport();
            gen5Reader.ExpSave();
            gen5Reader.ExpClose();

            this.Dispatcher.Invoke(() => {
                //Set relevant controls enabled
                newExpButton.IsEnabled = true;
                experimentIdTextBox.IsEnabled = true;
                selectExpFolderButton.IsEnabled = true;
                selectProtocolButton.IsEnabled = true;
            });

            TextOut += "            ... Done.\n\n";
        }

        private void CarrierInButton_Click(object sender, RoutedEventArgs e)
        {
            CarrierIn();
        }

        private void CarrierIn()
        {
            TextOut += gen5Reader.CarrierIn();
        }

        private void CarrierOutButton_Click(object sender, RoutedEventArgs e)
        {
            CarrierOut();
        }

        private void CarrierOut()
        {
            TextOut += gen5Reader.CarrierOut();
        }

        private void CloseExpButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.ExpClose();

            //Set relevant controls enabled
            newExpButton.IsEnabled = true;
            experimentIdTextBox.IsEnabled = true;
            selectExpFolderButton.IsEnabled = true;
            selectProtocolButton.IsEnabled = true;
        }

        //===============================================================
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
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
