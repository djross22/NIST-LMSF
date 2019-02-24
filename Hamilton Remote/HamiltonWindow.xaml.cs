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
        private bool isMethodQueuedOrRunning;
        private bool isVenusBusy;

        private Brush startingButtonBackground;

        //Hamilton process (runs HxRun.exe)
        private Process hamProcess;

        //log file
        private string logFilePath;

        //variables for TCP communication
        private string serverName;
        private SimpleTcpServer server;
        private int tcpPort;
        private readonly object messageHandlingLock = new object();
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

        public bool IsMethodQueuedOrRunning
        {
            get { return this.isMethodQueuedOrRunning; }
            private set
            {
                this.isMethodQueuedOrRunning = value;
                OnPropertyChanged("IsMethodQueuedOrRunning");
            }
        }

        public bool IsVenusBusy
        {
            get { return this.isVenusBusy; }
            private set
            {
                this.isVenusBusy = value;
                OnPropertyChanged("IsVenusBusy");
                if (isVenusBusy)
                {
                    statusBorder.Background = Brushes.LimeGreen;
                    statusTextBlock.Text = "Hamilton Busy";
                }
                else
                {
                    statusBorder.Background = Brushes.Red;
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
                selectMethodButton.IsEnabled = !IsMethodQueuedOrRunning;
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
            IsVenusBusy = true;
            string file = MethodPath;
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
                    AddOutputText("... waiting for last Hamilton Method to finish and exit.");
                    while (!hamProcess.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            //This part starts the Hamiltin HxRun.exe process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = @"C:\Program Files (x86)\HAMILTON\Bin\HxRun.exe";

            //the -t modifier causes HxRun.exe to run the method and then terminate itself after
            startInfo.Arguments = "\"" + file + "\"" + " -t";

            hamProcess = Process.Start(startInfo);
            AddOutputText("Hamilton Method started...");

            WaitForHamilton();

            AddOutputText("Hamilton Method finished.");
        }

        private void WaitForHamilton()
        {
            AddOutputText("    ... waiting for Hamilton Runtime Engine to finish and exit.");

            BackgroundWorker hamMonitorWorker = new BackgroundWorker();
            hamMonitorWorker.WorkerReportsProgress = false;
            hamMonitorWorker.DoWork += OutsideProcessMonitor_DoWork;
            hamMonitorWorker.RunWorkerCompleted += OutsideProcessMonitor_RunWorkerCompleted;

            List<object> arguments = new List<object>();
            int num = 1;//this is just here to keep the form of the call the the OutsideProcessMonitor methods consistent
            arguments.Add(num);
            arguments.Add(hamProcess);
            hamMonitorWorker.RunWorkerAsync(arguments);

            while (IsMethodQueuedOrRunning)
            {
                Thread.Sleep(100);
            }
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
            this.Dispatcher.Invoke(() => {
                //Property change calls UpdateControlEnabledStatus(), which sets relevant controls enabled
                IsMethodQueuedOrRunning = false;
            });
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

            lock (messageHandlingLock)
            {
                goodMsg = Message.CheckMessageHash(msg.MessageString);
                if (goodMsg && !messageQueue.Contains(msg.MessageString) && !oldMessageQueue.Contains(msg.MessageString))
                {
                    messageQueue.Enqueue(msg.MessageString);
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

            string[] msgParts = Message.UnwrapTcpMessage(msg.MessageString);

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
                if (IsMethodQueuedOrRunning || IsVenusBusy)
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

            // "RunMethod", "StatusCheck"
            switch (command)
            {
                case "StatusCheck":
                    //Don't need to do anything here because the server status is automatically sent back
                    break;
                default:
                    if (command.StartsWith("RunMethod"))
                    {
                        //command = $"RunMethod/{methodPath};
                        //From LMSF_Scheduler: msg = $"{command}/{methodPath}";
                        string[] runExpParts = command.Split('/');
                        string methodFilePath = runExpParts[1];
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
    }
}
