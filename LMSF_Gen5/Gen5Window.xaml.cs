using System;
using System.IO;
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
using SimpleTCP;

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
        private bool isReaderBusy;
        private bool isExperimentQueuedOrRunning;
        private bool isRemoteControlled;
        private bool isConnected;
        private BackgroundWorker readerMonitorWorker;

        private Gen5Reader gen5Reader;
        //private string readerName;

        private Brush startingButtonBackground;

        //variables for TCP communication
        //private string computerName;
        private string readerName;
        private SimpleTcpServer server;
        private int tcpPort;
        private bool isHandlingMessage;
        private readonly object messageHandlingLock = new object();
        private Queue<Message> messageQueue = new Queue<Message>();
        public enum ReaderStatus { Idle, Busy };
        private ReaderStatus status;

        public Gen5Window()
        {
            InitializeComponent();
            DataContext = this;

            //Get the starting/default button background brush so I can re-set it later
            startingButtonBackground = remoteButton.Background;

            //ComputerName = Environment.MachineName;
            SetReaderNameAndPort();

            try
            {
                gen5Reader = new Gen5Reader(this);

                TextOut = gen5Reader.StartGen5();
                TextOut += gen5Reader.SetClientWindow(this);
                TextOut += gen5Reader.ConfigureUSBReader();
                ReaderName = gen5Reader.ReaderName;
            }
            catch (Exception exc)
            {
                TextOut += $"Error at initilization of Gen5, {exc}./n";
            }
        }

        #region Properties Getters and Setters
        public string ReaderName
        {
            get { return this.readerName; }
            private set
            {
                this.readerName = value;
                OnPropertyChanged("ReaderName");
            }
        }

        public bool IsRemoteControlled
        {
            get { return this.isRemoteControlled; }
            private set
            {
                this.isRemoteControlled = value;
                OnPropertyChanged("IsRemoteControlled");
                if (isRemoteControlled)
                {
                    remoteButton.Background = Brushes.LimeGreen;
                    remoteButton.Content = "Switch to Local";
                }
                else
                {
                    remoteButton.Background = startingButtonBackground;
                    remoteButton.Content = "Switch to Remote";
                }
            }
        }

        public bool IsConnected
        {
            get { return this.isConnected; }
            private set
            {
                this.isConnected = value;
                OnPropertyChanged("IsConnected");
                if (isConnected)
                {
                    remoteBorder.Background = Brushes.LimeGreen;
                    remoteTextBlock.Text = "Connected";
                    remoteTextBlock.Foreground = Brushes.Black;
                }
                else
                {
                    remoteBorder.Background = Brushes.Transparent;
                    remoteTextBlock.Text = "Not Connected";
                    remoteTextBlock.Foreground = Brushes.White;
                }
            }
        }

        public bool IsExperimentQueuedOrRunning
        {
            get { return this.isExperimentQueuedOrRunning; }
            private set
            {
                this.isExperimentQueuedOrRunning = value;
                OnPropertyChanged("IsExperimentQueuedOrRunning");
            }
        }

        public bool IsReaderBusy
        {
            get { return this.isReaderBusy; }
            private set
            {
                this.isReaderBusy = value;
                OnPropertyChanged("IsReaderBusy");
                if (isReaderBusy)
                {
                    statusBorder.Background = Brushes.LimeGreen;
                    statusTextBlock.Text = "Reader Busy";
                    status = ReaderStatus.Busy;
                }
                else
                {
                    statusBorder.Background = Brushes.Red;
                    statusTextBlock.Text = "Reader Idle";
                    status = ReaderStatus.Idle;
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
                tempOutTextBox.ScrollToEnd();
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
            UpdateControlEnabledStatus();
        }

        private void UpdateControlEnabledStatus()
        {
            //This sets the IsEnabled property for all the controls in the window
            SetEnableAllControl(!IsRemoteControlled);
            //These two controls should always be enabled:
            remoteButton.IsEnabled = true;
            tempOutTextBox.IsEnabled = true;

            //When in local control mode, set the enabled properties according to whether or not an experiment has beed queued or is running
            if (!IsRemoteControlled)
            {
                newExpButton.IsEnabled = !IsExperimentQueuedOrRunning;
                experimentIdTextBox.IsEnabled = !IsExperimentQueuedOrRunning;
                selectExpFolderButton.IsEnabled = !IsExperimentQueuedOrRunning;
                selectProtocolButton.IsEnabled = !IsExperimentQueuedOrRunning;
            }
        }

        private void SetEnableAllControl(bool isEnabled)
        {
            foreach (Button b in FindVisualChildren<Button>(this))
            {
                b.IsEnabled = isEnabled;
            }

            foreach (TextBox b in FindVisualChildren<TextBox>(this))
            {
                b.IsEnabled = isEnabled;
            }
        }

        public static IEnumerable<T> FindVisualChildren<T>(DependencyObject depObj) where T : DependencyObject
        {
            if (depObj != null)
            {
                for (int i = 0; i < VisualTreeHelper.GetChildrenCount(depObj); i++)
                {
                    DependencyObject child = VisualTreeHelper.GetChild(depObj, i);
                    if (child != null && child is T)
                    {
                        yield return (T)child;
                    }

                    foreach (T childOfChild in FindVisualChildren<T>(child))
                    {
                        yield return childOfChild;
                    }
                }
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
            try
            {
                TextOut += gen5Reader.SetClientWindow(this);
                ExpFolderPath = gen5Reader.BrowseForFolder();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at SelectExpFolderButton_Click, {exc}./n";
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (gen5Reader.IsGen5Active())
                {
                    TextOut += gen5Reader.TerminateGen5();
                }
            }
            catch (Exception exc)
            {
                TextOut += $"Error at termination of Gen5, {exc}./n";
            }
        }

        private void NewExpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = gen5Reader.GetExperimentFilePath(ExpFolderPath, ExperimentId);
                if (File.Exists(filePath))
                {
                    MessageBoxResult res = MessageBox.Show("That Gen5 epxeriment file already exists. Ok to overwrite?", "Overwrite File", MessageBoxButton.YesNo);
                    if (res == MessageBoxResult.No)
                    {
                        return;
                    }
                }
            }
            catch (Exception exc)
            {
                TextOut += $"Error at NewExpButton_Click, {exc}./n";
                return;
            }

            NewExp();
        }

        private void NewExp()
        {
            //Property change calls UpdateControlEnabledStatus(), which sets button and other controls disabled
            IsExperimentQueuedOrRunning = true;

            try
            {
                TextOut += gen5Reader.NewExperiment(ProtocolPath);

                gen5Reader.ExperimentID = ExperimentId;
                gen5Reader.ExperimentFolderPath = ExpFolderPath;
                TextOut += gen5Reader.ExpSaveAs();

                TextOut += gen5Reader.PlatesGetPlate();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at NewExp, {exc}./n";
            }
        }

        private void RunExpButton_Click(object sender, RoutedEventArgs e)
        {
            RunExp();
        }

        private void RunExp()
        {
            IsReaderBusy = true;
            string startText = "";
            try
            {
                startText = gen5Reader.PlateStartRead();
                TextOut += gen5Reader.PlateStartRead();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at RunExp, {exc}./n";
            }

            if (startText.Contains("StartRead Successful"))
            {
                TextOut += WaitForFinishThenExportAndClose();
            }
            else
            {
                IsReaderBusy = false;
            }
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
            try
            {
                bool liveData = gen5Reader.Gen5App.DataExportEnabled;
            }
            catch (Exception exc)
            {
                TextOut += $"Error at ReaderMonitor_DoWork, {exc}./n";
            }

            while (status == Gen5ReadStatus.eReadInProgress)
            {
                Thread.Sleep(100);

                try
                {
                    gen5Reader.PlateReadStatus(ref status); //Note: the PlateReadStatus sets the IsReading Property according to state of reader.
                }
                catch (Exception exc)
                {
                    TextOut += $"Error at ReaderMonitor_DoWork.PlateReadStatus, {exc}./n";
                }

                //TODO: Handle live data stream

                this.Dispatcher.Invoke(() => {
                    if (status == Gen5ReadStatus.eReadInProgress)
                    {
                        IsReaderBusy = true;
                    }
                    else
                    {
                        IsReaderBusy = false;
                    }
                });
            }

        }

        void ReaderMonitor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadInProgress;
            //TODO: Handle outcomes other than "eReadCompleted" or "eReadAborted"
            try
            {
                gen5Reader.PlateFileExport();
                gen5Reader.ExpSave();
                gen5Reader.ExpClose();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at ReaderMonitor_RunWorkerCompleted, {exc}./n";
            }

            this.Dispatcher.Invoke(() => {
                //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
                IsExperimentQueuedOrRunning = false;
            });

            TextOut += "            ... Done.\n\n";
        }

        private void CarrierInButton_Click(object sender, RoutedEventArgs e)
        {
            CarrierIn();
        }

        private void CarrierIn()
        {
            IsReaderBusy = true;
            try
            {
                TextOut += gen5Reader.CarrierIn();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at CarrierIn, {exc}./n";
            }
            IsReaderBusy = false;
        }

        private void CarrierOutButton_Click(object sender, RoutedEventArgs e)
        {
            CarrierOut();
        }

        private void CarrierOut()
        {
            IsReaderBusy = true;
            try
            {
                TextOut += gen5Reader.CarrierOut();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at CarrierOut, {exc}./n";
            }
            IsReaderBusy = false;
        }

        private void CloseExpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                TextOut += gen5Reader.ExpClose();
            }
            catch (Exception exc)
            {
                TextOut += $"Error at CloseExpButton_Click, {exc}./n";
            }

            //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
            IsExperimentQueuedOrRunning = false;
        }

        private void TemperatureButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.GetCurrentTemperature();
        }

        private void SetReaderNameAndPort()
        {
            tcpPort = 42222;
            //readerName = "Neo";

            //switch (ComputerName)
            //{
            //    case ("Main"):
            //        readerName = "Neo";
            //        break;
            //}
        }

        private void StartTcpServer()
        {
            //Turn on TCP server
            server = new SimpleTcpServer().Start(tcpPort);
            server.Delimiter = 0x13;
            server.ClientConnected += Server_ClientConnected;
            server.ClientDisconnected += Server_ClientDisconnected;
            server.DelimiterDataReceived += MessageReceived;

            //Create and start message handling thread
        }

        private void MessageReceived(object sender, Message msg)
        {
            bool goodMsg = false;

            lock (messageHandlingLock)
            {
                goodMsg = Message.CheckMessageHash(msg.MessageString);
                if (goodMsg && !messageQueue.Contains(msg))
                {
                    messageQueue.Enqueue(msg);
                }
            }

            string[] msgParts = Message.UnwrapTcpMessage(msg.MessageString);
            if (goodMsg)
            {
                this.Dispatcher.Invoke(() =>
                {
                    TextOut += $"message received {messageQueue.Count}: {msg.MessageString}";
                });
                //send back status if good message
                string replyStr = $"{msgParts[0]},{status},{msgParts[2]}";
                msg.ReplyLine(replyStr);
                TextOut += $"; reply sent, {status}.\n";
            }
            else
            {
                //send back "fail" if bad message
                string replyStr = $"{msgParts[0]},fail,{msgParts[2]}";
                msg.ReplyLine(replyStr);
                TextOut += $"; reply sent, fail.\n";
            }
        }

        private void Server_ClientDisconnected(object sender, System.Net.Sockets.TcpClient client)
        {
            this.Dispatcher.Invoke(() =>
            {
                IsConnected = false;
                TextOut += $"Client Disconnected\n";
            });
        }

        private void Server_ClientConnected(object sender, System.Net.Sockets.TcpClient client)
        {
            this.Dispatcher.Invoke(() =>
            {
                IsConnected = true;
                TextOut += $"Client Connected\n";
            });
        }

        private void RemoteButton_Click(object sender, RoutedEventArgs e)
        {
            IsRemoteControlled = !IsRemoteControlled;

            if (IsRemoteControlled)
            {
                StartTcpServer();
            }
            else
            {
                if (server != null)
                {
                    server.Stop();
                }
            }
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

        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadNotStarted;
            TextOut += gen5Reader.PlateReadStatus(ref status);
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            TextOut += gen5Reader.ExpSave();
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //===============================================================
    }
}
