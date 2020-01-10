using System;
using System.Diagnostics;
using System.IO;
using System.Collections.Generic;
using System.Threading;
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
using SimpleTCP;

namespace Hamilton_Remote
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class HamiltonWindow : Window, INotifyPropertyChanged, IReportsRemoteStatus
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string methodPath;
        private string textOut;
        private bool isRemoteControlled;
        private bool isConnected;

        private readonly object venusBusyLock = new object();
        private bool isVenusBusy;

        private Brush startingButtonBackground;

        //Hamilton process (runs HxRun.exe)
        private Process hamProcess;

        //log file
        private string logFilePath;

        //Error handlimg
        List<string> errorList = new List<string>();

        //variables for TCP communication
        private string serverName;
        private SimpleTcpServer server;
        private int tcpPort;
        private readonly object serverStatusLock = new object();
        private Queue<string> messageQueue = new Queue<string>();
        private Queue<string> oldMessageQueue = new Queue<string>();
        //public enum ServerStatusStates { Idle, Busy };
        public SharedParameters.ServerStatusStates ServerStatus { get; private set; }

        public HamiltonWindow()
        {
            InitializeComponent();
            DataContext = this;

            //Get the starting/default button background brush so I can re-set it later
            startingButtonBackground = remoteButton.Background;

            //ComputerName = Environment.MachineName;
            SetTcpPort();

            SetHamiltonName();

            NewLogFile();
        }


        #region Properties Getters and Setters
        public string ServerName
        {
            get { return this.serverName; }
            private set
            {
                this.serverName = value;
                OnPropertyChanged("ServerName");
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

        public bool IsVenusBusy
        {
            get
            {
                lock (venusBusyLock)
                {
                    return this.isVenusBusy;
                }
            }
            private set
            {
                lock (venusBusyLock)
                {
                    this.isVenusBusy = value;
                }
                
                OnPropertyChanged("IsVenusBusy");
                if (isVenusBusy)
                {
                    statusBorder.Background = Brushes.Yellow;
                    statusTextBlock.Text = "Hamilton Busy";
                }
                else
                {
                    statusBorder.Background = Brushes.LimeGreen;
                    statusTextBlock.Text = "Hamilton Idle";
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

        public string MethodPath
        {
            get { return this.methodPath; }
            set
            {
                this.methodPath = value;
                OnPropertyChanged("MethodPath");
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
                selectMethodButton.IsEnabled = !IsVenusBusy;
                runMethodButton.IsEnabled = !IsVenusBusy;
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

        private void SetHamiltonName()
        {
            ServerName = "Test";

            string computerName = Environment.MachineName;
            switch (computerName)
            {
                case ("HAMILTO-S4KKFGQ"):
                    ServerName = "S-Cell-STAR";
                    break;
            }
        }

        private void SelectMethodButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure open file dialog box
            Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();
            //dlg.DefaultExt = ".hsl"; // Default file extension
            dlg.Filter = "Venus Method (.hsl)|*.hsl"; // Filter files by extension
            //dlg.InitialDirectory = initialDirectory;
            dlg.Title = "Select Venus Method:";

            // Show open file dialog box
            Nullable<bool> result = dlg.ShowDialog();

            // Process open file dialog box results
            if (result == true)
            {
                // Open document
                MethodPath = dlg.FileName;
            }
        }

        private void AddOutputText(string txt, bool newLine = true)
        {
            if (newLine)
            {
                OutputText += "\n";
            }
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
            return $"{DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")}-hamilton.trc";
        }

        private void RunMethodButton_Click(object sender, RoutedEventArgs e)
        {
            RunHamilton();
        }

        private void RunHamilton()
        {
            string file = MethodPath;

            if (file != null)
            {
                IsVenusBusy = true;

                if (!file.EndsWith(".hsl"))
                {
                    AddOutputText("Method File Path does not end with \".hsl\"\n");
                    IsVenusBusy = false;
                    return;
                }

                if (!File.Exists(file))
                {
                    AddOutputText($"Method File, {file}, not found.\n");
                    IsVenusBusy = false;
                    return;
                }

                if (!(hamProcess is null))
                {
                    if (!hamProcess.HasExited)
                    {
                        AddOutputText("... waiting for last Hamilton Method to finish and exit. ");
                        while (!hamProcess.HasExited)
                        {
                            Thread.Sleep(100);
                        }
                    }
                }

                //Clear the error list
                errorList = new List<string>();

                //This part starts the Hamilton HxRun.exe process
                ProcessStartInfo startInfo = new ProcessStartInfo();
                startInfo.FileName = @"C:\Program Files (x86)\HAMILTON\Bin\HxRun.exe";

                //the -t modifier causes HxRun.exe to run the method and then terminate itself after
                startInfo.Arguments = "\"" + file + "\"" + " -t";

                hamProcess = Process.Start(startInfo);
                AddOutputText("Hamilton Method started...");

                WaitForHamilton();
            }
        }

        private void WaitForHamilton()
        {
            AddOutputText("    ... waiting for Hamilton Runtime Engine to finish and exit. ");

            BackgroundWorker hamMonitorWorker = new BackgroundWorker();
            hamMonitorWorker.WorkerReportsProgress = false;
            hamMonitorWorker.DoWork += OutsideProcessMonitor_DoWork;
            hamMonitorWorker.RunWorkerCompleted += OutsideProcessMonitor_RunWorkerCompleted;

            List<object> arguments = new List<object>();
            int num = 1;//this is just here to keep the form of the call the the OutsideProcessMonitor methods consistent
            arguments.Add(num);
            arguments.Add(hamProcess);
            hamMonitorWorker.RunWorkerAsync(arguments);
        }

        void OutsideProcessMonitor_DoWork(object sender, DoWorkEventArgs e)
        {
            List<object> argsList = e.Argument as List<object>;
            int num = (int)argsList[0];
            Process outside_Process = argsList[1] as Process;

            while (!outside_Process.HasExited)
            {
                Thread.Sleep(100);
            }

        }

        void OutsideProcessMonitor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            AddOutputText("Hamilton Method finished.");

            //Check for errors
            //    first find log file 
            string logDirectory = @"C:\Program Files (x86)\HAMILTON\LogFiles";
            string logStart = System.IO.Path.GetFileNameWithoutExtension(MethodPath);
            string[] traceFileArr = Directory.GetFiles(logDirectory, $"{logStart}*.trc", SearchOption.TopDirectoryOnly);

            if (traceFileArr.Length == 0)
            {
                //if there are no matching trace files, something is probably wrong
                AddOutputText("No log file found. Something is wrong.");
                return;
            }
            DateTime latestTime = new DateTime(1, 1, 1);
            string latestFile = traceFileArr[0];
            foreach (string file in traceFileArr)
            {
                if (File.GetLastWriteTime(file) > latestTime)
                {
                    latestFile = file;
                    latestTime = File.GetLastWriteTime(file);
                }
            }
            string logFilePath = latestFile;

            //    create list of lines that have error messages from log file
            string[] lines = File.ReadAllLines(logFilePath);
            
            foreach (string s in lines)
            {
                if (!string.IsNullOrWhiteSpace(s) && s.IndexOf("error", StringComparison.OrdinalIgnoreCase) > -1)
                {
                    if (IsActualError(s))
                    {
                        errorList.Add(s);
                        AddOutputText($"    Error in Hamilton Method: {s}");
                    }
                }
            }

            Thread.Sleep(100);


            //Setting IsVenusBusy to false should always be the last step in this RunWorkerCompleted method
            this.Dispatcher.Invoke(() => {
                //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
                IsVenusBusy = false;
            });
        }

        private bool IsActualError(string line)
        {
            bool isActual = true;

            //This is the list of strings that contain the word "error" but don't indicate an actual error
            List<string> notActualStrings = new List<string>();
            notActualStrings.Add("Start error handling in walkaway mode (no dialog).");
            notActualStrings.Add("error;  ");
            notActualStrings.Add("Error manually recovered by user");
            notActualStrings.Add("Error automatically recovered depending of custom error");
            notActualStrings.Add("_Method::EASYPICKII::APPLICATION::GetApplicationStartError");
            notActualStrings.Add("o_intApplicationStartError=0");
            notActualStrings.Add("Error Code=\"er00\"");
            notActualStrings.Add("Main - error;");
            notActualStrings.Add("The error description is: Step canceled.");
            notActualStrings.Add("complete with error;");
            notActualStrings.Add("User-defined error handling will be used.");
            notActualStrings.Add("o_intErrorCode=0");
            notActualStrings.Add("Trace - error;");
            notActualStrings.Add("o_strErrorCode = 'TEC_0'");
            notActualStrings.Add("SmartStepCustomErrorHandling");
            notActualStrings.Add("Running ThermoShakeErrorCheck()");

            foreach (String s in notActualStrings)
            {
                if (line.Contains(s))
                {
                    isActual = false;
                }
            }
            
            return isActual;
        }

        private void SetTcpPort()
        {
            tcpPort = 42222;
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

            SharedParameters.ServerStatusStates statusForReply;

            lock (serverStatusLock)
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
                        ServerStatus = SharedParameters.ServerStatusStates.Busy;
                    }

                    msgQueued = true;
                }

                statusForReply = ServerStatus;
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
                if (statusForReply != SharedParameters.ServerStatusStates.Error)
                {
                    replyStr = $"{msgParts[0]},{statusForReply},{msgParts[2]}";
                }
                else
                {
                    replyStr = $"{msgParts[0]},{statusForReply}{errorList.Count},{msgParts[2]}";
                }
            }
            else
            {
                //send back "fail" if bad message
                replyStr = $"{msgParts[0]},fail,{msgParts[2]}";
            }
            
            try
            {
                msg.ReplyLine(replyStr);
            }
            catch (Exception ex)
            {
                string errMsg = $"Error in ReplyLine call from Hamilton Remote: {ex}\n";
                AddOutputText(errMsg);
            }

            textOutAdd = $"reply sent: {replyStr}\n";
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
                string okCancelPrompt = $"Are you sure you want to disconnect {ServerName} from remote?\nClick 'OK' to continue or 'Cancel' to cancel";
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
                    AddOutputText("Remote control started.\n");
                }
                else
                {
                    if (server != null)
                    {
                        server.Stop();
                    }
                    AddOutputText("Remote control ended.\n");
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
                if (IsVenusBusy)
                {
                    lock (serverStatusLock)
                    {
                        ServerStatus = SharedParameters.ServerStatusStates.Busy;
                    }
                }
                else
                {
                    if (messageQueue.Count == 0)
                    {
                        lock (serverStatusLock)
                        {
                            if (errorList.Count == 0)
                            {
                                ServerStatus = SharedParameters.ServerStatusStates.Idle;
                            }
                            else
                            {
                                ServerStatus = SharedParameters.ServerStatusStates.Error;
                            }
                        }

                    }
                    else
                    {
                        lock (serverStatusLock)
                        {
                            ServerStatus = SharedParameters.ServerStatusStates.Busy;
                        }
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

            string methodFilePath;

            // "RunMethod", "StatusCheck"
            switch (command)
            {
                case "StatusCheck":
                    //Don't need to do anything here because the server status is automatically sent back
                    break;
                case "ReadCounters":
                    methodFilePath = @"C:\Program Files (x86)\HAMILTON\Methods\Common\Tip Handling\With 96-Head\Check Tip Counters.hsl";
                    this.Dispatcher.Invoke(() => {
                        MethodPath = methodFilePath;
                        RunHamilton();
                    });
                    break;
                default:
                    if (command.StartsWith("RunMethod"))
                    {
                        //command = $"RunMethod/{methodPath};
                        //From LMSF_Scheduler: msg = $"{command}/{methodPath}";
                        string[] runExpParts = command.Split('/');
                        methodFilePath = runExpParts[1];
                        this.Dispatcher.Invoke(() => {
                            MethodPath = methodFilePath;
                            RunHamilton();
                        });
                    }
                    else
                    {
                        MessageBox.Show($"Unsupported remote server command, {command}", "Unsupported Command Error");
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

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            var messageBoxResult = MessageBox.Show("Are you sure you want to exit LMSF-Hamilton?\nClick 'Yes' to abort or 'No' to continue.", "Abort Read?", MessageBoxButton.YesNo);
            if (messageBoxResult == MessageBoxResult.Yes)
            {
                AddOutputText("Closing LMSF-Hamilton.");
            }
            else
            {
                // If user doesn't want to close, cancel closure
                e.Cancel = true;
            }
        }
    }
}
