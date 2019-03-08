﻿using System;
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
    public partial class Gen5Window : Window, INotifyPropertyChanged, IReaderTextOut, IReportsRemoteStatus
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string experimentId;
        private string expFolderPath;
        private string protocolPath;
        private string textOut;

        private readonly object readerBusyLock = new object();
        private bool isReaderBusy;

        private readonly object experimentQueuedLock = new object();
        private bool isExperimentQueuedOrRunning;

        private bool isRemoteControlled;
        private bool isConnected;
        private BackgroundWorker readerMonitorWorker;

        private Gen5Reader gen5Reader;

        private Brush startingButtonBackground;

        //log file
        private string logFilePath;

        //variables for TCP communication
        //private string computerName;
        private string readerName;
        private SimpleTcpServer server;
        private int tcpPort;
        private readonly object messageHandlingLock = new object();
        private Queue<string> messageQueue = new Queue<string>();
        private Queue<string> oldMessageQueue = new Queue<string>();
        //public enum ReaderStatusStates { Idle, Busy };
        public SharedParameters.ServerStatusStates ServerStatus { get; private set; }
        public static List<string> Gen5CommandList = new List<string> { "CarrierIn", "CarrierOut", "RunExp" };

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

                OutputText = gen5Reader.StartGen5();
                AddOutputText(gen5Reader.SetClientWindow(this));
                AddOutputText(gen5Reader.ConfigureUSBReader());
                ReaderName = gen5Reader.ReaderName;
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at initilization of Gen5, {exc}./n");
            }
            NewLogFile();
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
            get
            {
                lock (experimentQueuedLock)
                {
                    return this.isExperimentQueuedOrRunning;
                }
            }
            private set
            {
                lock (experimentQueuedLock)
                {
                    this.isExperimentQueuedOrRunning = value;
                }
                OnPropertyChanged("IsExperimentQueuedOrRunning");
            }
        }

        public bool IsReaderBusy
        {
            get
            {
                lock (readerBusyLock)
                {
                    return this.isReaderBusy;
                }
            }
            private set
            {
                lock (readerBusyLock)
                {
                    this.isReaderBusy = value;
                }
                OnPropertyChanged("IsReaderBusy");
                if (isReaderBusy)
                {
                    statusBorder.Background = Brushes.Yellow;
                    statusTextBlock.Text = "Reader Busy";
                }
                else
                {
                    statusBorder.Background = Brushes.LimeGreen;
                    statusTextBlock.Text = "Reader Idle";
                }
            }
        }

        public string OutputText
        {
            get { return this.textOut; }
            set
            {
                this.textOut = value;
                OnPropertyChanged("OutputText");
                outputTextBox.ScrollToEnd();
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
            outputTextBox.IsEnabled = true;

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

        private void AddOutputText(string txt, bool newLine = true)
        {
            OutputText += txt;
            //Add to log file
            if (IsRemoteControlled && (logFilePath != null))
            {
                if (newLine)
                {
                    string timeStr = DateTime.Now.ToString("yyyy-MM-dd.HH:mm:ss.fff");
                    File.AppendAllText(logFilePath, $"\n{timeStr},\t {txt}");
                }
                else
                {
                    File.AppendAllText(logFilePath, txt);
                }
            }
        }

        private bool NewLogFile()
        {
            bool okToGo = true;
            logFilePath = SharedParameters.LogFileFolderPath + NewLogFileName();
            if (!Directory.Exists(SharedParameters.LogFileFolderPath))
            {
                try
                {
                    Directory.CreateDirectory(SharedParameters.LogFileFolderPath);
                }
                catch (UnauthorizedAccessException e)
                {
                    string dialogText = @"Failed to create log file directory. Try manually creating the directory: 'C:\Shared Files\LMSF Scheduler\LogFiles\', then press 'OK' to continue, or 'Cancel' to abort the run.";
                    MessageBoxResult result = MessageBox.Show(dialogText, "Log File Directory Error", MessageBoxButton.OKCancel);
                    if (result == MessageBoxResult.Cancel)
                    {
                        okToGo = false;
                    }

                }
            }

            return okToGo;
        }

        private string NewLogFileName()
        {
            return $"{DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")}-gen5.trc";
        }

        private void SelectExpFolderButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddOutputText(gen5Reader.SetClientWindow(this));
                ExpFolderPath = gen5Reader.BrowseForFolder();
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at SelectExpFolderButton_Click, {exc}./n");
            }
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            try
            {
                if (gen5Reader.IsGen5Active())
                {
                    AddOutputText(gen5Reader.TerminateGen5());
                }
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at termination of Gen5, {exc}./n");
            }
        }

        private void NewExpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                string filePath = Gen5Reader.GetExperimentFilePath(ExpFolderPath, ExperimentId, gen5Reader);
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
                AddOutputText($"Error at NewExpButton_Click, {exc}./n");
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
                AddOutputText(gen5Reader.NewExperiment(ProtocolPath));

                gen5Reader.ExperimentID = ExperimentId;
                gen5Reader.ExperimentFolderPath = ExpFolderPath;
                AddOutputText(gen5Reader.ExpSaveAs());

                AddOutputText(gen5Reader.PlatesGetPlate());
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at NewExp, {exc}./n");
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
                AddOutputText(startText);
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at RunExp, {exc}./n");
            }

            if (startText.Contains("StartRead Successful"))
            {
                AddOutputText(WaitForFinishThenExportAndClose());
            }
            else
            {
                IsReaderBusy = false;
            }
        }

        public string WaitForFinishThenExportAndClose()
        {
            string retStr = "Running WaitForFinishThenExportAndClose; ";

            if (!(readerMonitorWorker is null))
            {
                if (readerMonitorWorker.IsBusy)
                {
                    retStr += "Read in progress, abort read or wait until end of read before starting a new read. ";
                    return retStr;
                }
            }

            readerMonitorWorker = new BackgroundWorker();
            readerMonitorWorker.WorkerReportsProgress = false;
            readerMonitorWorker.DoWork += ReaderMonitor_DoWork;
            readerMonitorWorker.RunWorkerCompleted += ReaderMonitor_RunWorkerCompleted;

            readerMonitorWorker.RunWorkerAsync();

            retStr += "    ... Read in Progress... ";

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
                AddOutputText($"Error at ReaderMonitor_DoWork, {exc}./n");
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
                    AddOutputText($"Error at ReaderMonitor_DoWork.PlateReadStatus, {exc}./n");
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
                AddOutputText($"Error at ReaderMonitor_RunWorkerCompleted, {exc}./n");
            }

            this.Dispatcher.Invoke(() => {
                //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
                IsExperimentQueuedOrRunning = false;
            });

            AddOutputText("... Done.\n\n");
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
                AddOutputText(gen5Reader.CarrierIn());
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at CarrierIn, {exc}./n");
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
                AddOutputText(gen5Reader.CarrierOut());
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at CarrierOut, {exc}./n");
            }
            IsReaderBusy = false;
        }

        private void CloseExpButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                AddOutputText(gen5Reader.ExpClose());
            }
            catch (Exception exc)
            {
                AddOutputText($"Error at CloseExpButton_Click, {exc}./n");
            }

            //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
            IsExperimentQueuedOrRunning = false;
        }

        private void AbortReadButton_Click(object sender, RoutedEventArgs e)
        {
            if (IsReaderBusy)
            {
                var messageBoxResult =  MessageBox.Show("Are you sure you want to abort the current read?\nClick 'Yes' to abort or 'No' to continue the current read.", "Abort Read?", MessageBoxButton.YesNo);
                if (messageBoxResult == MessageBoxResult.Yes)
                {
                    try
                    {
                        AddOutputText(gen5Reader.PlateAbortRead());
                    }
                    catch (Exception exc)
                    {
                        AddOutputText($"Error at AbortReadButton_Click, {exc}./n");
                    }
                    //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
                    IsExperimentQueuedOrRunning = false;
                }
            }
        }

        private void TemperatureButton_Click(object sender, RoutedEventArgs e)
        {
            AddOutputText(gen5Reader.GetCurrentTemperature());
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
            bool msgQueued = false;

            string[] msgParts = Message.UnwrapTcpMessage(msg.MessageString);

            lock (messageHandlingLock)
            {
                goodMsg = Message.CheckMessageHash(msg.MessageString);
                if (goodMsg && !messageQueue.Contains(msg.MessageString) && !oldMessageQueue.Contains(msg.MessageString))
                {
                    if (msgParts[1] == "StatusCheck")
                    {
                        oldMessageQueue.Enqueue(msg.MessageString);
                    }
                    else
                    {
                        messageQueue.Enqueue(msg.MessageString);
                    }
                    
                    msgQueued = true;
                }
            }

            string textOutAdd;
            if (msgQueued)
            {
                textOutAdd = $"message received and queued {messageQueue.Count}: {msg.MessageString}; ";
            }
            else
            {
                textOutAdd = $"duplicate message received (but not queued) {messageQueue.Count}: {msg.MessageString}; ";
            }
            this.Dispatcher.Invoke(() =>
            {
                AddOutputText(textOutAdd);
            });

            //Reply
            string replyStr;
            if (goodMsg)
            {
                //send back status if good message
                replyStr = $"{msgParts[0]},{ServerStatus},{msgParts[2]}";
                textOutAdd = $"reply sent, {ServerStatus}.\n";
            }
            else
            {
                //send back "fail" if bad message
                replyStr = $"{msgParts[0]},fail,{msgParts[2]}";
                textOutAdd = $"reply sent, fail.\n";
            }
            msg.ReplyLine(replyStr);
            this.Dispatcher.Invoke(() =>
            {
                AddOutputText(textOutAdd);
            });
        }

        private void Server_ClientDisconnected(object sender, System.Net.Sockets.TcpClient client)
        {
            this.Dispatcher.Invoke(() =>
            {
                IsConnected = false;
                AddOutputText($"Client Disconnected\n");
            });
        }

        private void Server_ClientConnected(object sender, System.Net.Sockets.TcpClient client)
        {
            this.Dispatcher.Invoke(() =>
            {
                IsConnected = true;
                AddOutputText($"Client Connected\n");
            });
        }

        private void RemoteButton_Click(object sender, RoutedEventArgs e)
        {
            MessageBoxResult res = MessageBoxResult.OK;
            if (IsRemoteControlled)
            {
                string okCancelPrompt = $"Are you sure you want to disconnect {ReaderName} from remote?\nClick 'OK' to continue or 'Cancel' to cancel";
                res = MessageBox.Show(okCancelPrompt, "Disconnect Remote?", MessageBoxButton.OKCancel);
            }

            if (res == MessageBoxResult.OK)
            {
                IsRemoteControlled = !IsRemoteControlled;

                if (IsRemoteControlled)
                {
                    messageQueue.Clear();
                    oldMessageQueue.Clear();
                    StartTcpServer();
                    StartRemoteControl();
                }
                else
                {
                    if (server != null)
                    {
                        server.Stop();
                    }
                }
            }
        }

        private void StartRemoteControl()
        {
            BackgroundWorker remoteControlWorker = new BackgroundWorker();
            remoteControlWorker.WorkerReportsProgress = false;
            remoteControlWorker.DoWork += RemoteControl_DoWork;
            remoteControlWorker.RunWorkerCompleted += RemoteControl_RunWorkerCompleted;

            remoteControlWorker.RunWorkerAsync();
        }

        void RemoteControl_DoWork(object sender, DoWorkEventArgs e)
        {
            while (IsRemoteControlled)
            {
                if (IsExperimentQueuedOrRunning || IsReaderBusy)
                {
                    ServerStatus = SharedParameters.ServerStatusStates.Busy;
                }
                else
                {
                    if (messageQueue.Count == 0)
                    {
                        ServerStatus = SharedParameters.ServerStatusStates.Idle;
                    }
                    else
                    {
                        ServerStatus = SharedParameters.ServerStatusStates.Busy;
                        string nextMsg = messageQueue.Dequeue();
                        oldMessageQueue.Enqueue(nextMsg);
                        ParseAndRunCommand(nextMsg);
                    }
                }
                
                Thread.Sleep(100);
            }

        }

        void ParseAndRunCommand(string msg)
        {
            string[] messageParts = Message.UnwrapTcpMessage(msg);
            string command = messageParts[1];

            //{ "CarrierIn", "CarrierOut", "RunExp" }, "StatusCheck"
            switch (command)
            {
                case "CarrierIn":
                    this.Dispatcher.Invoke(() => {
                        CarrierIn();
                    });
                    break;
                case "CarrierOut":
                    this.Dispatcher.Invoke(() => {
                        CarrierOut();
                    });
                    break;
                case "StatusCheck":
                    //Don't need to do anything here because the reader status is automatically sent back
                    break;
                default:
                    if (command.StartsWith("RunExp"))
                    {
                        //command = $"RunExp/{protocolPath}/{expIdStr}/{saveFolderPath}";
                        string[] runExpParts = command.Split('/');
                        string protocolPath = runExpParts[1];
                        string expIdStr = runExpParts[2];
                        string saveFolder = runExpParts[3];
                        this.Dispatcher.Invoke(() => {
                            ProtocolPath = protocolPath;
                            ExperimentId = expIdStr;
                            ExpFolderPath = saveFolder;
                            NewExp();
                            CarrierIn();
                            RunExp();
                        });
                    }
                    else
                    {
                        MessageBox.Show($"Unsupported remote reader command, {command}", "Unsupported Command Error");
                        //throw new System.ArgumentException($"Unsupported remote reader command, {command}", "command");
                    }
                    break;
            }
        }

        void RemoteControl_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Dispatcher.Invoke(() => {
                UpdateControlEnabledStatus();
            });
        }
        
        private void InstrCntrlButton_Click(object sender, RoutedEventArgs e)
        {
            AddOutputText(gen5Reader.RunReaderControlCommand());
        }

        //===============================================================
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //Button Click event handlers to be deleted after initial testing
        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            OutputText = gen5Reader.StartGen5();
            AddOutputText(gen5Reader.SetClientWindow(this));
            AddOutputText(gen5Reader.ConfigureUSBReader());
        }

        private void ExportButton_Click(object sender, RoutedEventArgs e)
        {
            AddOutputText(gen5Reader.PlateFileExport());
        }

        private void StatusButton_Click(object sender, RoutedEventArgs e)
        {
            Gen5ReadStatus status = Gen5ReadStatus.eReadNotStarted;
            AddOutputText(gen5Reader.PlateReadStatus(ref status));
        }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            AddOutputText(gen5Reader.ExpSave());
        }
        //+++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++++
        //===============================================================
    }
}
