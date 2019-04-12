using System;
using System.IO;
using System.Diagnostics;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using Microsoft.Win32;
using LMSF_Utilities;
using System.Xml;
using System.Text.RegularExpressions;
using SimpleTCP;

namespace LMSF_Scheduler
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        //Fields used to keep track of list of automation steps
        private string inputText = "";
        private bool inputChanged = false;
        private string outputText;
        private string nextStep;
        private string experimentFileName = "";

        private string insertStepText;

        //For parsing and running steps
        private List<string> inputSteps;
        private List<string> outputSteps;

        //lock for waitingForStepCompletion
        private readonly object stepCompletionLock = new object();
        private bool waitingForStepCompletion;

        private int stepNum;

        private readonly object runControlLock = new object();
        private bool isRunning = false;
        private bool isPaused = true;
        private bool abortCalled = false;

        private bool isOneStep = false;

        private readonly object validatingLock = new object();
        private bool isValidating = false;

        private List<int> valFailed;
        //Validation failure is signaled by adding/having one or more entires in the valFailed list
        //    valFailed is intialized to an empty list at the beginning of each run,
        //    if a step fails a validation check, the step number is added to the valFailed list

        //Background worker to run steps
        private Thread runStepsThread;

        //Overlord process (runs Overlord.Main.exe)
        private Process ovProcess;

        //Hamilton process (runs HxRun.exe)
        private Process hamProcess;

        //Timer dialog window
        private TimerDialog stepTimerDialog;

        //Window title, app name, plus file name, plus * to indicate unsaved changes
        private static string appName = "LMSF Scheduler";
        private string displayTitle = appName + " - ";

        //log file
        private string logFilePath;

        public ObservableCollection<string> CommandList { get; set; }
        private string selectedCommand;

        //Fields for XML metadata output
        private string metaDataFilePath;
        private XmlDocument xmlDoc;
        private XmlNode rootNode;
        private XmlNode projectNode;
        private XmlNode experimentNode;
        private XmlNode experimentIdNode;
        string experimentID;
        private XmlNode protocolNode;
        private static string protocolSource = "LMSF Scheduler";
        private bool isCollectingXml;
        private DateTime startDateTime;

        //Dictionaries for storage of user inputs
        private Dictionary<string, string> metaDictionary;
        private Dictionary<string, Concentration> concDictionary;

        //Dictionary for labware on Carousel
        private Dictionary<string, string> carouselInventory;

        //variables for TCP communication
        public List<string> ReaderList { get; set; }
        public ObservableCollection<TextBlock> ReaderBlockList { get; set; }
        private TextBlock selectedReaderBlock;
        Dictionary<string, string> readerIps = new Dictionary<string, string>();
        Dictionary<string, SimpleTcpClient> readerClients = new Dictionary<string, SimpleTcpClient>();

        #region Properties Getters and Setters
        public string InsertStepText
        {
            get { return this.insertStepText; }
            set
            {
                this.insertStepText = value;
                OnPropertyChanged("InsertStepText");
            }
        }

        public TextBlock SelectedReaderBlock
        {
            get { return this.selectedReaderBlock; }
            set
            {
                this.selectedReaderBlock = value;
                OnPropertyChanged("SelectedReaderBlock");
            }
        }

        public string SelectedCommand
        {
            get { return this.selectedCommand; }
            set
            {
                this.selectedCommand = value;
                OnPropertyChanged("SelectedCommand");
            }
        }

        public bool AbortCalled
        {
            get
            {
                lock (runControlLock)
                {
                    return this.abortCalled;
                }
            }
            set
            {
                lock (runControlLock)
                {
                    this.abortCalled = value;
                    if (this.abortCalled)
                    {
                        abortButton.Background = Brushes.Red; //new SolidColorBrush(Colors.Red);
                        playButton.Background = Brushes.Transparent;
                        stepButton.Background = Brushes.Transparent;
                    }
                    else
                    {
                        abortButton.Background = Brushes.White; //new SolidColorBrush(Colors.White);
                    }
                }
            }
        }

        private bool WaitingForStepCompletion
        {
            get
            {
                lock (stepCompletionLock)
                {
                    return this.waitingForStepCompletion;
                }
            }
            set
            {
                lock (stepCompletionLock)
                {
                    this.waitingForStepCompletion = value;
                }
            }
        }

        public bool IsOneStep
        {
            get { return this.isOneStep; }
            set
            {
                this.isOneStep = value;
                UpdateEnabledState();
                OnPropertyChanged("IsOneStep");
            }
        }

        public bool IsPaused
        {
            get
            {
                lock (runControlLock)
                {
                    return this.isPaused;
                }
            }
            set
            {
                lock (runControlLock)
                {
                    this.isPaused = value;
                    UpdateEnabledState();
                }
                OnPropertyChanged("IsPaused");
            }
        }

        public bool IsRunning
        {
            get
            {
                lock (runControlLock)
                {
                    return this.isRunning;
                }
            }
            set
            {
                lock (runControlLock)
                {
                    this.isRunning = value;
                    UpdateEnabledState();
                }
                
                OnPropertyChanged("IsRunning");
            }
        }

        public string ExperimentFileName
        {
            get { return this.experimentFileName; }
            set
            {
                this.experimentFileName = value;
                UpdateTitle();
                OnPropertyChanged("ExperimentFileName");
            }
        }

        public string DisplayTitle
        {
            get { return this.displayTitle; }
            set
            {
                this.displayTitle = value;
                OnPropertyChanged("DisplayTitle");
            }
        }

        public string InputText
        {
            get { return this.inputText; }
            set
            {
                if (this.inputText != value)
                {
                    InputChanged = true;
                }
                this.inputText = value;
                OnPropertyChanged("InputText");
            }
        }

        public bool InputChanged
        {
            get { return this.inputChanged; }
            set
            {
                this.inputChanged = value;
                UpdateTitle();
                OnPropertyChanged("InputChanged");
            }
        }

        public string OutputText
        {
            get { return this.outputText; }
            set
            {
                this.outputText = value;
                OnPropertyChanged("OutputText");
                this.Dispatcher.Invoke(() => { outputTextBox.ScrollToEnd(); });
            }
        }

        public string NextStep
        {
            get { return this.nextStep; }
            set
            {
                this.nextStep = value;
                OnPropertyChanged("NextStep");
            }
        }
        #endregion

        private void UpdateEnabledState()
        {
            //The UpdateEnabledState method is called by property Setters to maintain the appropriate enabled state of the GUI controls
            // Use private fields (instead of properties) here to avoid property update feedback loop (e.g. isPaused instead of IsPaused)
            if (this.isRunning)
            {
                playButton.IsEnabled = isPaused;
                pauseButton.IsEnabled = !isPaused;
                stepButton.IsEnabled = isPaused;
                rewindButton.IsEnabled = isPaused;
                abortButton.IsEnabled = true;

                insertStepButton.IsEnabled = isPaused;
                insertStepTextBox.IsEnabled = isPaused;

                selectComboBox.IsEnabled = false;
                readerComboBox.IsEnabled = false;
                testButton.IsEnabled = false;

                if (isPaused)
                {
                    statusBorder.Background = Brushes.Yellow; //new SolidColorBrush(Colors.Yellow);
                    playButton.Background = Brushes.Yellow;
                    stepButton.Background = Brushes.Yellow;
                    statusTextBlock.Text = "Paused";
                }
                else
                {
                    statusBorder.Background = Brushes.LimeGreen; //new SolidColorBrush(Colors.LimeGreen);
                    playButton.Background = Brushes.Transparent;
                    stepButton.Background = Brushes.Transparent;
                    statusTextBlock.Text = "Running";
                }

                inputTextBox.IsEnabled = true;
                inputTextBox.IsReadOnly = true;
                insertFileButton.IsEnabled = false;
                mainMenu.IsEnabled = false;
            }
            else
            {
                playButton.IsEnabled = true;
                pauseButton.IsEnabled = false;
                stepButton.IsEnabled = true;
                rewindButton.IsEnabled = true;
                abortButton.IsEnabled = true;

                statusBorder.Background = Brushes.Red; //new SolidColorBrush(Colors.Red);
                playButton.Background = Brushes.Transparent;
                stepButton.Background = Brushes.Transparent;
                statusTextBlock.Text = "Stopped";

                inputTextBox.IsEnabled = true;
                inputTextBox.IsReadOnly = false;
                insertFileButton.IsEnabled = true;
                mainMenu.IsEnabled = true;

                selectComboBox.IsEnabled = true;
                readerComboBox.IsEnabled = true;
                testButton.IsEnabled = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();

            //Variables for parsing input arguments
            string[] args = App.commandLineArgs;
            if (args.Length > 0)
            {
                //Open a script file if it is passed as first argument
                //The OpenFile() method handles checking for ".lmsf" ending
                //    and has try... catch in case the filename has other problems
                // it also sets the ExperimentFileName property
                OpenFile(args[0]);
            }

                runStepsThread = new Thread(new ThreadStart(StepsThreadProc));

            DataContext = this;

            CommandList = new ObservableCollection<string>() { "If", "Overlord", "Hamilton", "RemoteHam", "Gen5", "Timer", "WaitFor", "StartPrompt", "NewXML", "AppendXML", "AddXML", "LoadXML", "UserPrompt", "GetUserYesNo", "Set", "Math", "Get", "GetTimeNow", "GetExpID", "GetFile", "CopyRemoteFiles", "ReadScript", "ImportDictionary", "ExportDictionary" }; //SharedParameters.UnitsList;

            ReaderList = new List<string>() { "Neo", "Epoch1", "Epoch2", "Epoch3", "Epoch4", "S-Cell-STAR" };
            ReaderBlockList = new ObservableCollection<TextBlock>();
            foreach (string s in ReaderList)
            {
                TextBlock tb = new TextBlock();
                tb.Text = s;
                ReaderBlockList.Add(tb);

                readerClients[s] = null;
            }
            //Set IPs for each remote server
            readerIps["Neo"] = "localhost";
            readerIps["Epoch1"] = "129.6.167.36";
            readerIps["Epoch2"] = "129.6.167.37";
            readerIps["Epoch3"] = "129.6.167.38";
            readerIps["Epoch4"] = "129.6.167.39";
            readerIps["S-Cell-STAR"] = "129.6.167.35";

            metaDictionary = new Dictionary<string, string>();
            concDictionary = new Dictionary<string, Concentration>();
        }

        public void SecondCommandRun(IList<string> args)
        {
            if (!IsRunning)
            {
                //The agument list here includes "LMSF Scheduler.exe" as args[0]
                if (args.Count > 1)
                {
                    //Open a script file if it is passed as first argument
                    //The OpenFile() method handles checking for ".lmsf" ending
                    //    and has try... catch in case the filename has other problems
                    // it also sets the ExperimentFileName property
                    OpenFile(args[1]);
                }
            }
            this.Show();
            this.WindowState = WindowState.Normal;
            this.Activate();
            this.Topmost = true;
            this.Topmost = false;
            this.Focus();
        }

        private List<string> GetConnectedReadersList()
        {
            var connectedList = new List<string>();

            foreach (string s in ReaderList)
            {
                SimpleTcpClient client = readerClients[s];
                if ((client != null) && (client.IsConnected()))
                {
                    connectedList.Add(s);
                }
            }

            return connectedList;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        //Method to manually test status of remote
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            if ((testTextBox.Text != null) && (testTextBox.Text != ""))
            {
                string reader = testTextBox.Text;
                string msg = "StatusCheck";
                SendTcpMessage(reader, msg);
            }
        }

        private void TestWriteButton_Click(object sender, RoutedEventArgs e)
        {
            string name = "kan";

            //OutputText = "";

            Concentration con = SharedParameters.GetAdditiveConcentration(name, "");

            if (con is null)
            {
                OutputText += "null... \n";
            }
            else
            {
                OutputText += con + "\n";
            }

        }

        private void UpdateTitle()
        {
            DisplayTitle = appName + " - " + ExperimentFileName;
            if (InputChanged)
            {
                DisplayTitle += "*";
            }
        }

        //SaveFirstQuery returns true unless the user chooses 'Cancel'
        //    - either directly in response to the 1st Message Box
        //    - or in in the Select File Save Dialog box-
        private bool SaveFirstQuery()
        {
            string messageBoxText = "Do you want to save changes first?";
            string caption = "Save File?";
            MessageBoxButton button = MessageBoxButton.YesNoCancel;
            MessageBoxImage icon = MessageBoxImage.Warning;

            bool okToGo = false;

            MessageBoxResult result = MessageBox.Show(messageBoxText, caption, button, icon);
            switch (result)
            {
                case MessageBoxResult.Yes:
                    // User pressed Yes button
                    //Save();
                    //okToGo = true;
                    okToGo = Save();
                    break;
                case MessageBoxResult.No:
                    // User pressed No button
                    // do nothing (go ahead without saving)
                    okToGo = true;
                    break;
                case MessageBoxResult.Cancel:
                    // User pressed Cancel button
                    okToGo = false;
                    break;
            }
            return okToGo;
        }

        private void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (!InputChanged || SaveFirstQuery())
            {
                InputText = "";
                ExperimentFileName = "";
            }
            inputTextBox.Focus();
        }

        private void OpenMenuItme_Click(object sender, RoutedEventArgs e)
        {
            if (!InputChanged || SaveFirstQuery())
            {
                Open();
            }
            inputTextBox.Focus();
        }

        private void OpenFile(string file)
        {
            if (!InputChanged || SaveFirstQuery())
            {
                Open(file);
            }
            inputTextBox.Focus();
        }

        private void Open(string file)
        {
            if (file.EndsWith(".lmsf", StringComparison.CurrentCultureIgnoreCase))
            {
                try
                {
                    ExperimentFileName = file;
                    InputText = File.ReadAllText(ExperimentFileName);
                    InputChanged = false;
                }
                catch
                {
                    MessageBox.Show($"Failed to open file, {file}");
                }
            }
            else
            {
                MessageBox.Show($"{file} is not an LMSF script file (*.lmsf)");
            }
        }

        private void Open()
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "LMSF file (*.lmsf)|*.lmsf";
            if (openFileDialog.ShowDialog() == true)
            {
                InputText = File.ReadAllText(openFileDialog.FileName);
                ExperimentFileName = openFileDialog.FileName;
                InputChanged = false;
            }
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            Save();
            inputTextBox.Focus();
        }

        private bool Save()
        {
            bool didSave;
            if (ExperimentFileName != "")
            {
                try
                {
                    File.WriteAllText(ExperimentFileName, InputText);
                    InputChanged = false;
                    didSave = true;
                }
                catch (UnauthorizedAccessException e)
                {
                    MessageBox.Show($"{e.Message} Try saving with a temporary file name, then restart LMSF Scheduler");
                    didSave = SaveAs();
                }
            }
            else
            {
                didSave = SaveAs();
            }

            return didSave;
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
            inputTextBox.Focus();
        }

        private bool SaveAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "LMSF file (*.lmsf)|*.lmsf";
            if (ExperimentFileName != null)
            {
                
                saveFileDialog.FileName = System.IO.Path.GetFileName(ExperimentFileName);
            }
            bool didSave;
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, InputText);
                ExperimentFileName = saveFileDialog.FileName;
                InputChanged = false;
                didSave = true;
            }
            else
            {
                didSave = false;
            }

            return didSave;
        }

        private void InsertFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select File Path to Insert";

            if (openFileDialog.ShowDialog() == true)
            {
                //int caretPos = inputTextBox.SelectionStart;
                string newText = openFileDialog.FileName;

                //InputText = InputText.Insert(caretPos, newText);

                //inputTextBox.SelectionStart = caretPos + newText.Length;
                //inputTextBox.SelectionLength = 0;

                InsertInputText(newText);
            }
            inputTextBox.Focus();
        }

        private void AddOutputText(string txt, bool newLine = true)
        {

            OutputText += txt;

            //Add to log file
            if (!isValidating && (logFilePath != null))
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

        private void InsertInputText(string newText)
        {
            if (!(newText is null))
            {
                int caretPos = inputTextBox.SelectionStart;

                InputText = InputText.Insert(caretPos, newText);

                inputTextBox.SelectionStart = caretPos + newText.Length;
                inputTextBox.SelectionLength = 0;
            }

            inputTextBox.Focus();
        }

        private string[] ReadScriptFile(string scriptLine)
        {
            string[] lines;
            List<string> lineList = new List<string>();
            lineList.Add($"#{scriptLine}");

            int scriptCommandEnd = scriptLine.IndexOf('(');
            string scriptCommand = scriptLine.Substring(0, scriptCommandEnd);

            string scriptArgsStr = scriptLine.Substring(scriptCommandEnd + 1);
            scriptArgsStr = scriptArgsStr.Remove(scriptArgsStr.Length - 1); //remove the closing ")"
            string[] scriptArgs = scriptArgsStr.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            //Then clean up scriptArgs by trimming white space from ends of each string, and removing empty strings.
            scriptArgs = scriptArgs.Select(s => s.Trim()).ToArray();
            scriptArgs = scriptArgs.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            string scriptFilePath = scriptArgs[0];
            //string scriptFilePath = line.Substring(scriptCommandEnd + 1);
            //scriptFilePath = scriptFilePath.Remove(scriptFilePath.Length - 1); //remove the closing ")"
            //scriptFilePath = scriptFilePath.Trim(); //remove leading and trailing white space

            if (scriptCommand == "ReadScript")
            {
                if (File.Exists(scriptFilePath))
                {
                    if (scriptFilePath.EndsWith(".lmsf"))
                    {
                        lines = File.ReadAllLines(scriptFilePath);

                        //parse additional arguments to set values of variables in script
                        if (scriptArgs.Length > 1)
                        {
                            Dictionary<string, string> varDict = new Dictionary<string, string>();
                            bool varArgsOk = true;
                            int i=0;
                            string[] varSet;
                            foreach (string s in scriptArgs)
                            {
                                if (i>0)
                                {
                                    //varArgs.Add(s);

                                    int numberOfEquals = s.Length - s.Replace("=", "").Length;
                                    varSet = s.Split(new string[] { "=" }, StringSplitOptions.RemoveEmptyEntries);
                                    //Then clean up varSet by trimming white space from ends of each string, and removing empty strings.
                                    varSet = varSet.Select(x => x.Trim()).ToArray();
                                    varSet = varSet.Where(x => !string.IsNullOrEmpty(x)).ToArray();
                                    if ( (varSet.Length == 2) && (numberOfEquals == 1) )
                                    {
                                        varDict[varSet[0]] = varSet[1];
                                    }
                                    else
                                    {
                                        varArgsOk = false;
                                        break;
                                    }
                                }
                                i++;
                            }

                            if (varArgsOk)
                            {
                                //replace variables
                                //    by adding Set() commands whenever a "#InsertVariables" line is found in the script file

                                if (lines != null)
                                {
                                    foreach (string s in lines)
                                    {
                                        lineList.Add(s);

                                        if (s.StartsWith("#InsertVariables"))
                                        {
                                            foreach (KeyValuePair<string, string> entry in varDict)
                                            {
                                                lineList.Add($"Set({entry.Key}, {entry.Value})");
                                            }

                                            lineList.Add("#End InsertVariables");
                                        }
                                    }
                                }

                            }
                            else
                            {
                                //throw error
                                throw new ArgumentException($"Syntax error in passing arguments to script file: {scriptLine}");
                            }
                        }
                        else
                        {
                            if (lines != null)
                            {
                                foreach (string s in lines)
                                {
                                    lineList.Add(s);
                                }
                            }
                        }
                    }
                    else
                    {
                        throw new FileFormatException($"Script file must end in \".lmsf\": {scriptFilePath}.");
                    }
                }
                else
                {
                    throw new FileNotFoundException($"Script file not found: {scriptFilePath}.");
                }
            }
            else
            {
                throw new ArgumentException($"{scriptCommand} != ReadScript");
            }
            
            lineList.Add($"#End {scriptLine}");

            //return lines;
            return lineList.ToArray();
        }

        //Breaks the InputText into steps/lines
        private bool InitSteps()
        {
            bool initOK = true;

            //Initialize metadata Dictionaries and outputSteps list
            metaDictionary = new Dictionary<string, string>();
            concDictionary = new Dictionary<string, Concentration>();
            outputSteps = new List<string>();

            //by default, don't collect metadata
            isCollectingXml = false;

            string[] stepsArray = InputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);
            inputSteps = new List<string>(stepsArray);

            string[] scriptSteps;
            bool checkReadScript = true;
            int maxRecursion = 8;
            int recursion = 0;
            string notReadLine = "";
            while (checkReadScript)
            {
                List<string> stepsList = new List<string>();
                checkReadScript = false;

                foreach (string line in inputSteps)
                {
                    if (line.StartsWith("ReadScript"))
                    {
                        if (recursion < maxRecursion)
                        {
                            try
                            {
                                scriptSteps = ReadScriptFile(line);
                                checkReadScript = true;
                                foreach (string scriptLine in scriptSteps)
                                {
                                    stepsList.Add(scriptLine);
                                }
                            }
                            catch (Exception e)
                            {
                                initOK = false;
                                string msg = $"Error reading script file, {line}, {e}";
                                this.Dispatcher.Invoke(() => { SharedParameters.ShowPrompt(msg, "Script File Error"); });
                                return initOK;
                            }
                        }
                        else
                        {
                            notReadLine = line;
                        }
                    }
                    else
                    {
                        stepsList.Add(line);
                    }
                }

                if (recursion >= maxRecursion)
                {
                    string msg = $"Warning: Reached recursion limit for reading script file references. Script not read into protocol: {notReadLine}.\n";
                    msg += "Check for self-referencing script file or change the maxRecursion parameter in the InitSteps() method.";
                    msg += $"\nrecursion: {recursion}; checkReadScript: {checkReadScript}";
                    SharedParameters.ShowPrompt(msg, "Script File Warning");
                }

                if (checkReadScript)
                {
                    inputSteps = stepsList;
                }
                recursion++;
            }

            //remove leading and trailing white space from each line
            //also delete any lines that were just white space (are now empty)
            List<string> newList = new List<string>();
            string listLine;
            foreach (string s in inputSteps)
            {
                listLine = s.Trim();
                if (!string.IsNullOrEmpty(listLine))
                {
                    newList.Add(listLine);
                }
                
            }
            inputSteps = newList;
            

            stepNum = -1;

            if (isValidating)
            {
                OutputText = $"Validating {ExperimentFileName}\n\n";
            }
            else
            {
                //if not running in validation mode, get a new autimatically generated log file path and start wrting to the log file
                initOK = NewLogFile();
                OutputText = $"Running {ExperimentFileName}\n\n";
                File.WriteAllText(logFilePath, OutputText);

                //move setting of the metaDataFilePath to the RunNewXml() method, so that multiple experiments can be run from the same script
                //if the log file got created ok, set the temporary path for the metadata output file
                //if (initOK)
                //{
                //    metaDataFilePath = SharedParameters.LogFileFolderPath + "temp.xml";
                //}
            }

            return initOK;
        }

        private void StepsThreadProc()
        {
            bool okToGo = InitSteps();

            if (okToGo)
            {
                while (IsRunning)
                {
                    while (IsPaused)
                    {
                        if (IsOneStep)
                        {
                            break;
                        }
                        Thread.Sleep(100);
                    }

                    stepNum++;
                    Step();
                    
                }
            }
            else
            {
                string doneText = $"{SharedParameters.GetDateTimeString()}; Protocol failed to start.\n";
                AddOutputText(doneText);

                this.Dispatcher.Invoke(() => { IsRunning = false; });
            }

        }

        private void RunSteps()
        {
            testTextBox.Text = "RunSteps()...";

            runStepsThread = new Thread(new ThreadStart(StepsThreadProc));
            //runStepsThread.SetApartmentState(ApartmentState.STA);

            runStepsThread.Start();

            //Todo: add save log file here
        }

        private void Step()
        {
            if (AbortCalled)
            {
                AddOutputText("Method Aborted.\n");
                this.Dispatcher.Invoke(() => { IsRunning = false; });
            }
            else
            {
                if (stepNum < inputSteps.Count)
                {
                    if (stepNum + 1 < inputSteps.Count)
                    {
                        NextStep = inputSteps[stepNum + 1];
                    }
                    else
                    {
                        NextStep = "";
                    }
                    string oldText = OutputText;
                    string newText = ParseStep(stepNum, inputSteps[stepNum], isValidating, ref valFailed);
                    OutputText = oldText + newText;
                    if (!isValidating)
                    {
                        File.AppendAllText(logFilePath, newText);
                    }

                    //stepNum++;
                }
                else
                {
                    string doneText = $"{SharedParameters.GetDateTimeString()}; Done.\n";
                    AddOutputText(doneText);

                    this.Dispatcher.Invoke(() => { IsRunning = false; });
                }

                this.Dispatcher.Invoke(() => { IsOneStep = false; });
            }

        }

        private string ParseStep(int num, string step, bool localIsValidating, ref List<int> valReport)
        {
            //Note on step validation:
            //    valFailed is intialized to an empty list at the beginning of each run, 
            //        and then passed to this method as valReport
            //    if a step fails a validation check, the step number is added to the valFailed/valReport list
            //Validation of single steps inserted during a pause in the run is handled similarly, 
            //    but with a different valReport target

            outputSteps.Add(step);

            string outString = $"{num}. ";
            outString += $"{SharedParameters.GetDateTimeString()}; ";

            //If step is a comment line, don't do anything except display it.
            if (step.StartsWith("//") || step.StartsWith("#") )
            {
                outString += $"{step}\n\n";
                return outString;
            }

            //Check for opening and closing "(" and ")"
            if (!step.EndsWith(")"))
            {
                valReport.Add(num);
                //exit the method early if the closing ")" is missing
                outString += "Syntax error: missing closing \")\"\n\n";
                return outString;
            }
            if (!step.Contains("("))
            {
                valReport.Add(num);
                //exit the method early if the opening "(" is missing
                outString += "Syntax error: missing opening \"(\"\n\n";
                return outString;
            }

            int scriptCommandEnd = step.IndexOf('(');
            //int scriptLineLength = step.Length;
            string scriptCommand = step.Substring(0, scriptCommandEnd);
            string scriptArgStr = step.Substring(scriptCommandEnd + 1);
            scriptArgStr = scriptArgStr.Remove(scriptArgStr.Length - 1); //remove the closing ")"
            scriptArgStr = scriptArgStr.Trim(); //remove leading and trailing white space

            //If the script command is "If(...)" check that the opening and closing "(" and ")" is there
            //    then replace the first "(" with "," so that things get separated properly in the string.Split step, and delete the closing ")"
            if (scriptCommand == "If")
            {
                if (!scriptArgStr.Contains("("))
                {
                    valReport.Add(num);
                    //exit the method early if the opening "(" is missing
                    outString += "Syntax error: Command inside If() statment missing opening \"(\"\n\n";
                    return outString;
                }
                if (!scriptArgStr.EndsWith(")"))
                {
                    valReport.Add(num);
                    //exit the method early if the opening "(" is missing
                    outString += "Syntax error: If command missing closing \")\"\n\n";
                    return outString;
                }
                scriptArgStr = scriptArgStr.Remove(scriptArgStr.Length - 1); //remove the closing ")"
                scriptCommandEnd = scriptArgStr.IndexOf('(');
                scriptArgStr = scriptArgStr.Remove(scriptCommandEnd, 1);
                scriptArgStr = scriptArgStr.Insert(scriptCommandEnd, ",");
            }

            //string[] scriptArgArr = scriptArgStr.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
            string[] scriptArgArr = scriptArgStr.Split(new[] { "," }, StringSplitOptions.None);

            string[] stepArgs = new string[1 + scriptArgArr.Length];
            stepArgs[0] = scriptCommand;
            scriptArgArr.CopyTo(stepArgs, 1);

            //Then clean up stepArgs by trimming white space from ends of each string, and removing empty strings.
            stepArgs = stepArgs.Select(s => s.Trim()).ToArray();
            //stepArgs = stepArgs.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            //Check for {key} entries in stepArgs and replace with dictionary values, or fail validation
            //Note keys are replaced here.
            bool keysOk = true;
            for (int j = 0; j < stepArgs.Length; j++)
            {
                string arg = stepArgs[j];
                string newArg = arg;

                if (arg.Contains('{'))
                {
                    //First, make sure the number of "{" and "}" occurances are equal
                    List<int> startIndList = arg.AllIndexesOf("{");
                    List<int> endIndList = arg.AllIndexesOf("}");
                    if (startIndList.Count != endIndList.Count)
                    {
                        outString += $"Improper key syntax: {arg}. ";
                        keysOk = false;
                    }
                    else
                    {
                        //Then, make sure each "{" is properly followed by a "}"
                        for (int i = 0; i < startIndList.Count; i++)
                        {
                            int startInd = startIndList[i];
                            int endInd = endIndList[i];
                            if (startInd > endInd)
                            {
                                outString += $"Improper key syntax: {arg}. ";
                                keysOk = false;
                            }
                        }
                    }
                }
                if (keysOk)
                {
                    //If the key syntax is ok, replace the keys with their values from the metaDictionary
                    //    or signal valReport if the key is not in the dictionary
                    while (newArg.Contains('{'))
                    {
                        int startInd = newArg.IndexOf('{');
                        int endInd = newArg.IndexOf('}');

                        string beforeString = newArg.Substring(0, startInd);
                        string keyStr = newArg.Substring(startInd + 1, (endInd - startInd) - 1);
                        string afterString = newArg.Substring(endInd + 1, newArg.Length - endInd - 1);

                        try
                        {
                            //First look in <string> dictionary
                            string newStr = metaDictionary[keyStr];
                            newArg = $"{beforeString}{newStr}{afterString}";
                            //MessageBox.Show(newArg);
                        }
                        catch (KeyNotFoundException)
                        {
                            try
                            {
                                //then, if the key is not found in the <string> dictionary,
                                //    look in the <Concentration> dictionary
                                string newStr = concDictionary[keyStr].ToString();
                                newArg = $"{beforeString}{newStr}{afterString}";
                                //MessageBox.Show(newArg);
                            }
                            catch (KeyNotFoundException)
                            {
                                if (localIsValidating)
                                {
                                    outString += $"Key not in metaDictionary: {keyStr} in {step}. ";
                                    keysOk = false;
                                    break;
                                }
                                else
                                {
                                    //If a key is missing at run time
                                    string msg = $"Key not in metaDictionary: {keyStr} in {step}.\n";
                                    outString += msg;
                                    msg += "Run is paused.\n";
                                    msg += $"Click 'OK' then manually insert a step to Set() a value for {keyStr} then continue run.\n";
                                    msg += "Or click 'Abort' to cancel the run.";
                                    
                                    this.Dispatcher.Invoke(() => {
                                        bool? oKToGo = SharedParameters.ShowPrompt(msg, "Key Not Found!");
                                        if (!(oKToGo == true))
                                        {
                                            AbortCalled = true;
                                        }

                                        IsPaused = true;
                                    });

                                    stepNum = stepNum - 1;
                                    //Then exit ParseSteps() and hope the user can fix it.
                                    keysOk = false;
                                    break;
                                }
                            }
                        }
                    }

                }

                stepArgs[j] = newArg;

            }

            if (!keysOk)
            {
                valReport.Add(num);
                //exit the method early if there are still key syntax errors
                outString += "\n\n";
                return outString;
            }

            bool ifResult = true;
            string ifTestStr = "";
            if (stepArgs[0] == "If")
            {
                //this is an if statement, so the next argment needs to be the logical test
                if (stepArgs.Length > 2)
                {
                    ifTestStr = stepArgs[1].Trim();
                    if (stepArgs[1].Contains("==") || stepArgs[1].Contains("!="))
                    {
                        string[] logicStrings = ifTestStr.Split(new[] { "==", "!=" }, StringSplitOptions.RemoveEmptyEntries);
                        if (logicStrings.Length == 2)
                        {
                            string firstStr = logicStrings[0].Trim();
                            string secondStr = logicStrings[1].Trim();
                            outString += $"If: {ifTestStr}, ";
                            if (ifTestStr.Contains("=="))
                            {
                                ifResult = (firstStr == secondStr);
                            }
                            else
                            {
                                ifResult = (firstStr != secondStr);
                            }
                            outString += $"{ifResult}. ";
                            //always mark ifResult as true when validating, so that the remaining arguments always get validated
                            if (localIsValidating)
                            {
                                ifResult = true;
                            }
                            //then take out the first two arguments and send the rest on to be parsed
                            string[] newStepArgs = new string[stepArgs.Length - 2];
                            for (int i=2; i<stepArgs.Length; i++)
                            {
                                newStepArgs[i-2] = stepArgs[i];
                            }
                            stepArgs = newStepArgs;
                        }
                        else
                        {
                            valReport.Add(num);
                            //exit the method early if the 2nd argument of an If/ command is not a logical comparison
                            outString += "If/ commands need to have a valid logical test as the 2nd argument (e.g. {strain1}==MG1655, or {userChoice}!=No).\n\n";
                            return outString;
                        }
                    }
                    else
                    {
                        if (stepArgs[1].Contains(">=") || stepArgs[1].Contains("<="))
                        {
                            string[] logicStrings = ifTestStr.Split(new[] { ">=", "<=" }, StringSplitOptions.RemoveEmptyEntries);
                            if (logicStrings.Length == 2)
                            {
                                string firstStr = logicStrings[0].Trim();
                                string secondStr = logicStrings[1].Trim();
                                double firstDbl;
                                double secondDbl;
                                if (double.TryParse(firstStr, out firstDbl) && double.TryParse(secondStr, out secondDbl))
                                {
                                    outString += $"If: {ifTestStr}, ";
                                    if (ifTestStr.Contains(">="))
                                    {
                                        ifResult = (firstDbl >= secondDbl);
                                    }
                                    else
                                    {
                                        ifResult = (firstDbl <= secondDbl);
                                    }
                                    outString += $"{ifResult}. ";
                                    //always mark ifResult as true when validating, so that the remaining arguments always get validated
                                    if (localIsValidating)
                                    {
                                        ifResult = true;
                                    }
                                    //then take out the first two arguments and send the rest on to be parsed
                                    string[] newStepArgs = new string[stepArgs.Length - 2];
                                    for (int i = 2; i < stepArgs.Length; i++)
                                    {
                                        newStepArgs[i - 2] = stepArgs[i];
                                    }
                                    stepArgs = newStepArgs;
                                }
                                else
                                {
                                    valReport.Add(num);
                                    outString += $"If/ commands error: {firstStr} or {secondStr} not parsable as number.\n\n";
                                    return outString;
                                }
                                
                            }
                            else
                            {
                                valReport.Add(num);
                                //exit the method early if the 2nd argument of an If/ command is not a valid comparison
                                outString += "If/ commands need to have a valid comparison as the 2nd argument (e.g. {plateNum}>=3, or {tips1000Total}<=96).\n\n";
                                return outString;
                            }
                        }
                        else
                        {
                            if (stepArgs[1].Contains(">") || stepArgs[1].Contains("<"))
                            {
                                string[] logicStrings = ifTestStr.Split(new[] { ">", "<" }, StringSplitOptions.RemoveEmptyEntries);
                                if (logicStrings.Length == 2)
                                {
                                    string firstStr = logicStrings[0].Trim();
                                    string secondStr = logicStrings[1].Trim();
                                    double firstDbl;
                                    double secondDbl;
                                    if (double.TryParse(firstStr, out firstDbl) && double.TryParse(secondStr, out secondDbl))
                                    {
                                        outString += $"If: {ifTestStr}, ";
                                        if (ifTestStr.Contains(">"))
                                        {
                                            ifResult = (firstDbl > secondDbl);
                                        }
                                        else
                                        {
                                            ifResult = (firstDbl < secondDbl);
                                        }
                                        outString += $"{ifResult}. ";
                                        //always mark ifResult as true when validating, so that the remaining arguments always get validated
                                        if (localIsValidating)
                                        {
                                            ifResult = true;
                                        }
                                        //then take out the first two arguments and send the rest on to be parsed
                                        string[] newStepArgs = new string[stepArgs.Length - 2];
                                        for (int i = 2; i < stepArgs.Length; i++)
                                        {
                                            newStepArgs[i - 2] = stepArgs[i];
                                        }
                                        stepArgs = newStepArgs;
                                    }
                                    else
                                    {
                                        valReport.Add(num);
                                        outString += $"If/ commands error: {firstStr} or {secondStr} not parsable as number.\n\n";
                                        return outString;
                                    }

                                }
                                else
                                {
                                    valReport.Add(num);
                                    //exit the method early if the 2nd argument of an If/ command is not a valid comparison
                                    outString += "If/ commands need to have a valid comparison as the 2nd argument (e.g. {plateNum}>3, or {tips1000Total}<96).\n\n";
                                    return outString;
                                }
                            }
                            else
                            {
                                valReport.Add(num);
                                //exit the method early if the 2nd argument of an If/ command is not a logical comparison
                                outString += "If/ commands need to have a logical or comparison test (with \"==\", \"!=\", \">\", \"<\", \">=\", or \"<=\") as the 2nd argument.\n\n";
                                return outString;
                            }
                        }
                    }
                }
                else
                {
                    valReport.Add(num);
                    //exit the method early if there are not enough arguments for an If/ command
                    outString += "If/ commands need to have a logical test followed by a normal command line.\n\n";
                    return outString;
                }
            }

            int numArgs = stepArgs.Length;

            if (numArgs > 0 & ifResult)//only excecute the command when ifResult==true
            {
                string stepType = stepArgs[0];

                switch (stepType)
                {
                    case "Overlord":
                        ParseOverlordStep(ref valReport);
                        break;
                    case "Hamilton":
                        ParseHamiltonStep(ref valReport);
                        break;
                    case "RemoteHam":
                        ParseRemoteHamiltonStep(ref valReport);
                        break;
                    case "Gen5":
                        ParseGen5Step(ref valReport);
                        break;
                    case "WaitFor":
                        ParseWaitForStep(ref valReport);
                        break;
                    case "Timer":
                        ParseTimerStep(ref valReport);
                        break;
                    case "NewXML":
                        ParseNewXml(ref valReport);
                        break;
                    case "AppendXML":
                        ParseAppendXml(ref valReport);
                        break;
                    case "AddXML":
                        ParseAddXml(ref valReport);
                        break;
                    case "SaveXML":
                        ParseSaveXml(ref valReport);
                        break;
                    case "LoadXML":
                        ParseLoadXml(ref valReport);
                        break;
                    case "UserPrompt":
                        ParseUserPrompt(ref valReport);
                        break;
                    case "GetUserYesNo":
                        ParseUserYesNo(ref valReport);
                        break;
                    case "Set":
                        ParseSet(ref valReport);
                        break;
                    case "Math":
                        ParseMath(ref valReport);
                        break;
                    case "Get":
                        ParseGet(ref valReport);
                        break;
                    case "GetExpID":
                        ParseGetExpId(ref valReport);
                        break;
                    case "GetTimeNow":
                        ParseGetTimeNow(ref valReport);
                        break;
                    case "GetFile":
                        ParseGetFile(ref valReport);
                        break;
                    case "StartPrompt":
                        ParseStartPrompt(ref valReport);
                        break;
                    case "CopyRemoteFiles":
                        ParseCopyRemoteFiles(ref valReport);
                        break;
                    case "ImportDictionary":
                        ParseImportDictionary(ref valReport);
                        break;
                    case "ExportDictionary":
                        ParseExportDictionary(ref valReport);
                        break;
                    default:
                        valReport.Add(num);
                        outString += "Step Command not recongnized: ";
                        foreach (string s in stepArgs)
                        {
                            outString += s + ", ";
                        }
                        break;
                }
            }

            outString += "\r\n";
            outString += "\r\n";

            //if there were dictionary keys that were used for the input step, also add the substituted version of the step to the outputList (commented out)
            if (step.Contains("{"))
            {
                string outStep;
                //if (stepArgs[0] == "If")
                if (scriptCommand == "If")
                {
                    outStep = $"If({ifTestStr}, {stepArgs[0]}(";
                    if (stepArgs.Length > 1)
                    {
                        for (int i = 1; i < stepArgs.Length; i++)
                        {
                            outStep += stepArgs[i];
                            if (i < stepArgs.Length - 1)
                            {
                                outStep += ", ";
                            }
                        }
                    }
                    outStep += "))";
                }
                else
                {
                    outStep = $"{stepArgs[0]}(";
                    if (stepArgs.Length > 1)
                    {
                        for (int i = 1; i < stepArgs.Length; i++)
                        {
                            outStep += stepArgs[i];
                            if (i < stepArgs.Length - 1)
                            {
                                outStep += ", ";
                            }
                        }
                    }
                    outStep += ")";
                }

                outputSteps.Add($"//{outStep}");
            }
            outputSteps.Add(""); //add blank line to make output script easier to read


            return outString;

            //Local functions to parse each type of step
            void ParseOverlordStep(ref List<int> val)
            {
                outString += "Running Overlord proccedure: ";
                bool isOvpFileName = false;
                bool varArgsOk = true;
                if (numArgs < 2)
                {
                    outString += "No procedure path given.";
                    val.Add(num);
                }
                else
                {
                    if (stepArgs[1].EndsWith(".ovp"))
                    {
                        isOvpFileName = true;
                    }
                    else
                    {
                        outString += "Not a valid Overlord procedure filename: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                    if (numArgs > 2)
                    {
                        //check passed variables to make sure they have to correct format
                        string varString = stepArgs[2];
                        string[] varArgs = varString.Split(new[] { " " }, StringSplitOptions.None);

                        //Needs to be an even number of varArgs
                        int numVarArgs = varArgs.Length;
                        if (numVarArgs % 2 == 0)
                        {
                            varArgsOk = true;
                            //And even arguments need to start with "[" and end with "]"
                            for (int i = 0; i < varArgs.Length; i += 2)
                            {
                                if (!(varArgs[i].StartsWith("[") && varArgs[i].EndsWith("]")))
                                {
                                    varArgsOk = false;
                                }
                            }
                            //And odd arguments need to either parse as a number or be inclosed with quotes
                            double temp;
                            for (int i = 1; i < varArgs.Length; i += 2)
                            {
                                if (!(varArgs[i].StartsWith("\"") && varArgs[i].EndsWith("\"")))
                                {
                                    if (!Double.TryParse(varArgs[i], out temp))
                                    {
                                        varArgsOk = false;
                                    }
                                    if (!varArgsOk)
                                    {
                                        outString += "Overlord variables must either be a number or a string inclosed in quotes: ";
                                        outString += varArgs[i] + ", ";
                                    }
                                }

                            }
                        }
                        else
                        {
                            varArgsOk = false;
                        }
                        if (!varArgsOk)
                        {
                            outString += "Overlord variable syntax incorrect: ";
                            outString += stepArgs[2];
                            val.Add(num);
                        }

                    }
                }

                if (isOvpFileName & varArgsOk)
                {
                    bool ovpExists = File.Exists(stepArgs[1]);
                    if (ovpExists)
                    {
                        outString += stepArgs[1];
                        if (stepArgs.Length > 2)
                        {
                            outString += ", " + stepArgs[2];
                        }

                        if (!localIsValidating)
                        {
                            //RunOverlord(num, stepArgs[1]);
                            RunOverlord(num, stepArgs);
                        }
                    }
                    else
                    {
                        outString += "Procedure file not found: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                }
            }

            void ParseHamiltonStep(ref List<int> val)
            {
                outString += "Running Hamilton Venus Method: ";
                bool isHslFileName = false;

                //Requires one argument, the Method file path
                if (numArgs < 2)
                {
                    outString += "No method file path given.";
                    val.Add(num);
                }
                else
                {
                    if (stepArgs[1].EndsWith(".hsl"))
                    {
                        isHslFileName = true;
                    }
                    else
                    {
                        outString += "Not a valid Hamilton method (*.hsl) filename: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                }

                if (isHslFileName)
                {
                    bool hslExists = File.Exists(stepArgs[1]);
                    if (hslExists)
                    {
                        outString += stepArgs[1];

                        if (!localIsValidating)
                        {
                            RunHamilton(num, stepArgs);
                        }
                    }
                    else
                    {
                        outString += "Method file not found: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                }
            }

            void ParseRemoteHamiltonStep(ref List<int> val)
            {
                //Gen5 takes 3 arguments
                //First two arguments are Hamilton name and command
                string name;
                string command;
                List<string> remHamCommandList = new List<string> { "RunMethod", "ReadCounters" };
                //3rd argument is the method path
                string methodPath = "";

                outString += "Running RemoteHam Command: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //Requires at least three arguments (plus RemoteHam command itself)
                if (numArgs < 3)
                {
                    outString += "Not enough arguments; RemoteHam command requires at least 2 arguments: Hamilton name and command. ";
                    val.Add(num);
                }
                else
                {
                    name = stepArgs[1];
                    command = stepArgs[2];
                    // Check if 1st argument is valid Hamilton name 
                    if (name.Contains("STAR") && ReaderList.Contains(name))
                    {
                        //Check if reader is connected
                        if (GetConnectedReadersList().Contains(name))
                        {
                            //Check if 2nd argument is valid Hamilton command
                            if (remHamCommandList.Contains(command))
                            {
                                argsOk = true;
                                outString += $"{name}/ {command} ";
                            }
                            else
                            {
                                outString += "Not a valid Hamilton command: ";
                                outString += $"{command}. Valid Hamilton commands are: ";
                                foreach (string s in remHamCommandList)
                                {
                                    outString += $"{s}, ";
                                }
                                val.Add(num);
                            }
                        }
                        else
                        {
                            outString += $"{name} not connected. Make sure Hamilton Remote is running and in \"Remote\" mode on the {name} computer,\n";
                            outString += $"then establish the remote connection to {name} using the drop-down \"Remote Connections\" control at the bottom right of this window.";
                            val.Add(num);
                        }
                    }
                    else
                    {
                        outString += "Not a valid Hamilton instrument name: ";
                        outString += $"{name}. Valid Hamilton instrument names are: ";
                        foreach (string r in ReaderList)
                        {
                            if (r.Contains("STAR"))
                            {
                                outString += $"{r}, ";
                            }
                        }
                        val.Add(num);
                    }

                    //If arguments are ok so far, check additional arguments of RunMethod command
                    if (argsOk && command == "RunMethod")
                    {
                        if (numArgs < 4)
                        {
                            outString += "Not enough arguments; RemoteHam(<STAR>, RunMethod...) command requires at least 3 arguments: Hamilton name, command, and method path. ";
                            val.Add(num);
                            argsOk = false;
                        }
                        else
                        {
                            methodPath = stepArgs[3];
                        }

                        //Check if method path is valid file with .prt extension
                        if (!methodPath.EndsWith(".hsl"))
                        {
                            outString += $"Method path needs to end with .hsl, you enetered: {methodPath} ";
                            val.Add(num);
                            argsOk = false;
                        }
                        //Need to check the existence of the method file from this computer, via the IP address of the Hamilton computer.
                        //    Method folder as seen from STAR computer: @"C:\Program Files (x86)\HAMILTON\Methods"
                        //    Method folder as seen from this computer: @"\\129.6.167.35\Methods"
                        string localMethods = @"C:\Program Files (x86)\Hamilton\Methods";
                        string remoteMethods = @"\\" + readerIps[name] + @"\Methods";
                        if (!methodPath.StartsWith(localMethods))
                        {
                            localMethods = @"C:\Program Files (x86)\HAMILTON\Methods";
                        }
                        string pathFromHere = methodPath.Replace(localMethods, remoteMethods);

                        if (!File.Exists(pathFromHere))
                        {
                            outString += $"Method file/path does not exist: {methodPath}, {pathFromHere} ";
                            val.Add(num);
                            argsOk = false;
                        }

                        //Remote Hamilton method requiers write permission to the Hamilton/LMSF_FrontEnd dirctory on the remote cmoputer
                        string remoteFrontEndpath = @"\\" + readerIps[name] + @"\LMSF_FrontEnd\";
                        if (!SharedParameters.IsDirectoryWritable(remoteFrontEndpath))
                        {
                            outString += $"Write permission to the Hamilton/LMSF_FrontEnd directory is required for the RemoteHam command. This permission was denied for the folder path: {remoteFrontEndpath}.\n";
                            outString += $"Manually check the remote connection to that directory and try again. ";
                            val.Add(num);
                            argsOk = false;
                        }

                        if (argsOk)
                        {
                            outString += $"{methodPath} ";
                        }
                    }
                }

                //If arguments are all ok, then run the step
                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunRemoteHamilton(num, stepArgs);
                    }
                    else
                    {
                        bool isEditCountersMethod = false;
                        if (numArgs > 3 && methodPath.EndsWith("Edit Tip Counters.hsl"))
                        {
                            isEditCountersMethod = true;
                        }
                        //For anything that gets saved by the Run method to the dictionary, put placeholder values into dictionary
                        if (stepArgs[2] == "ReadCounters" || isEditCountersMethod)
                        {
                            metaDictionary["tips1000Status1"] = "0";
                            metaDictionary["tips1000Status2"] = "0";
                            metaDictionary["tips1000Total"] = "0";
                            metaDictionary["tips300Status1"] = "0";
                            metaDictionary["tips300Status2"] = "0";
                            metaDictionary["tips300Total"] = "0";
                            metaDictionary["tips50Status1"] = "0";
                            metaDictionary["tips50Status2"] = "0";
                            metaDictionary["tips50Total"] = "0";
                            metaDictionary["tipsOffsetStatus1"] = "0";
                            metaDictionary["tipsOffsetStatus2"] = "0";
                            metaDictionary["tipsOffsetTotal"] = "0";
                        }
                    }
                }
            }

            void ParseGen5Step(ref List<int> val)
            {
                //Gen5 takes 2 or 5 arguments
                //First two arguments are reader name and reader command
                string name;
                string command;
                List<string> commandList = LMSF_Gen5.Gen5Window.Gen5CommandList; //new List<string> { "CarrierIn", "CarrierOut", "RunExp" };
                //arguments 3, 4 and 5 are the protocol path, experiment Id, and save folder path
                string protocolPath;
                string expIdStr;
                string saveFolderPath;

                outString += "Running Gen5 Command: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //Requires at least two arguments (plus Gen5 command itself)
                if (numArgs < 3)
                {
                    outString += "Not enough arguments; Gen5 command requires at least 2 arguments: reader name, and reader command. ";
                    val.Add(num);
                }
                else
                {
                    name = stepArgs[1];
                    command = stepArgs[2];
                    // Check if 1st argument is valid reader name 
                    if (ReaderList.Contains(name))
                    {
                        //Check if reader is connected
                        if (GetConnectedReadersList().Contains(name))
                        {
                            //Check if 2nd argument is valid reader command
                            if (commandList.Contains(command))
                            {
                                argsOk = true;
                                outString += $"{name}/ {command} ";
                            }
                            else
                            {
                                outString += "Not a valid reader command: ";
                                outString += $"{command}. Valid reader commands are: ";
                                foreach (string s in commandList)
                                {
                                    outString += $"{s}, ";
                                }
                                val.Add(num);
                            }
                        }
                        else
                        {
                            outString += $"{name} not connected. Make sure LMSF_Gen5 is running and in \"Remote\" mode on the {name} computer,\n";
                            outString += $"then establish the remote connection to {name} using the drop-down \"Remote Connections\" control at the bottom right of this window.";
                            val.Add(num);
                        }
                    }
                    else
                    {
                        outString += "Not a valid reader name: ";
                        outString += $"{name}. Valid reader names are: ";
                        foreach (string r in ReaderList)
                        {
                            outString += $"{r}, ";
                        }
                        val.Add(num);
                    }

                    //If arguments are ok so far, check additional arguments of RunExp command
                    if (argsOk && command == "RunExp")
                    {
                        //With RunExp command, there need to be at least arguments (plus Gen5 command itself)
                        if (numArgs < 6)
                        {
                            outString += "Not enough arguments; Gen5/ <reader name>/ RunExp/ command requires three additional: protocol path, experiment Id, and save folder path. ";
                            val.Add(num);
                        }
                        else
                        {
                            protocolPath = stepArgs[3];
                            expIdStr = stepArgs[4];
                            saveFolderPath = stepArgs[5];
                            
                            //Check if protocol path is valid file with .prt extension
                            if (!protocolPath.EndsWith(".prt"))
                            {
                                outString += $"Protocol path needs to end with .prt, you enetered: {protocolPath} ";
                                val.Add(num);
                                argsOk = false;
                            }
                            if (!File.Exists(protocolPath))
                            {
                                outString += $"Protocol file/path does not exist: {protocolPath} ";
                                val.Add(num);
                                argsOk = false;
                            }
                            //check validity of expId
                            //expIdStr needs to be usable in filenames, so make sure it only has just letters, numbers, or "-" or "_")
                            ValidationResult valRes = ValidateExpId(expIdStr);
                            if (!valRes.IsValid)
                            {
                                argsOk = false;
                                //Message for bad Experiment ID argument
                                outString += "Experiment IDs can only contain letters, numbers, or \"-\" or \"_\"";
                                val.Add(num);
                            }
                            //check if save file directory exists
                            if (!Directory.Exists(saveFolderPath))
                            {
                                outString += $"Save folder path does not exist: {saveFolderPath} ";
                                val.Add(num);
                                argsOk = false;
                            }
                            else
                            {
                                //Make sure user has write permission
                                if (!SharedParameters.IsDirectoryWritable(saveFolderPath))
                                {
                                    outString += $"Write permission denied for save folder path: {saveFolderPath} ";
                                    val.Add(num);
                                    argsOk = false;
                                }
                            }
                            if (argsOk)
                            {
                                outString += $"{protocolPath}/ {expIdStr}/ {saveFolderPath} ";
                            }
                        }
                    }
                }

                //If arguments are all ok, then run the step
                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunGen5(num, stepArgs);
                    }
                    else
                    {
                        //For anything that gets saved by the Run method to the dictionary, put placeholder values into dictionary
                    }
                }
            }

            void ParseTimerStep(ref List<int> val)
            {
                outString += "Running Timer: ";
                int waitTime = 0;

                bool argsOk = false;
                if (numArgs < 2)
                {
                    outString += "No time given for the timer.";
                    val.Add(num);
                }
                else
                {
                    if (int.TryParse(stepArgs[1], out waitTime))
                    {
                        argsOk = true;
                    }
                    else
                    {
                        DateTime waitUntil;
                        if (DateTime.TryParse(stepArgs[1], out waitUntil))
                        {
                            //argument is a DateTime string, so wait until the specified time
                            waitTime = (int)Math.Round((waitUntil - DateTime.Now).TotalSeconds);
                            if (waitTime > 0)
                            {
                                argsOk = true;
                            }
                            else
                            {
                                argsOk = false;
                                outString += "Timer date-time parameter is in the past: ";
                                outString += stepArgs[1];
                                val.Add(num);
                            }
                        }
                        else
                        {
                            outString += "Timer parameter is not an integer nor a parsable date-time string: ";
                            outString += stepArgs[1];
                            val.Add(num);
                        }
                    }
                }

                if (argsOk)
                {
                    outString += stepArgs[1];
                    if (!localIsValidating)
                    {
                        RunTimer(num, stepArgs);
                    }
                }
            }

            void ParseWaitForStep(ref List<int> val)
            {
                outString += "WaitFor: ";
                if (numArgs < 2)
                {
                    outString += "Must specify the process to WaitFor (Overlord, Hamilton, Reader, or Timer)";
                    val.Add(num);
                }
                else
                {
                    string processWaitingFor = stepArgs[1];
                    bool writeEndTime = true;
                    int pingInterval = 1000;

                    if (stepArgs.Length > 2)
                    {
                        if (stepArgs[2] == "false" || stepArgs[2] == "False")
                        {
                            writeEndTime = false;
                        }
                    }

                    if (stepArgs.Length > 3)
                    {
                        int tempInt;
                        if (int.TryParse(stepArgs[3], out tempInt))
                        {
                            pingInterval = tempInt;
                            if (pingInterval < 100)
                            {
                                pingInterval = 100;
                            }
                        }
                    }

                    switch (processWaitingFor)
                    {
                        case "Overlord":
                            outString += "Overlord, Done.";
                            if (!localIsValidating)
                            {
                                WaitForOverlord(num);
                            }
                            break;
                        case "Hamilton":
                            outString += "Hamilton, Done.";
                            if (!localIsValidating)
                            {
                                WaitForHamilton(num);
                            }
                            break;
                        case "Timer":
                            outString += "Timer, Done.";
                            if (!localIsValidating)
                            {
                                WaitForTimer(num);
                            }
                            break;
                        default:
                            if (GetConnectedReadersList().Contains(processWaitingFor))
                            {
                                outString += $"{processWaitingFor}, Done.";
                                if (!localIsValidating)
                                {
                                    WaitForRemoteProcess(processWaitingFor, writeEndTime, pingInterval);
                                }
                            }
                            else
                            {
                                outString += "WaitFor process not recognized: ";
                                outString += stepArgs[1];
                                val.Add(num);
                            }
                            break;
                    }
                }
            }

            void ParseNewXml(ref List<int> val)
            {
                //Boolean used to track validity of arguments/parameters
                bool argsOk = true;

                //string for start of output from ParseStep()
                outString += "Creating New XML document: ";

                //Requires an argument for the protocol type:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No protocol type argument given.";
                    val.Add(num);
                }
                else
                {
                    //If the protocol type argument exists, make sure it is ok (needs to be a good xml atribute value, with just letters, numbers, spaces, or "-" or "_")
                    RegexValidationRule valRule = new RegexValidationRule();
                    valRule.RegexText = "^[a-zA-Z0-9-_ ]+$";
                    valRule.ErrorMessage = "Protocol type arguments can only contain letters, numbers, spaces, or \"-\" or \"_\"";
                    ValidationResult valRes = valRule.Validate(stepArgs[1], System.Globalization.CultureInfo.CurrentCulture);
                    if (!valRes.IsValid)
                    {
                        argsOk = false;
                        //Message for bad Protocol argument
                        outString += "Protocol type arguments can only contain letters, numbers, spaces, or \"-\" or \"_\"";
                        val.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        RunNewXml(num, stepArgs);
                    }
                    else
                    {
                        metaDictionary["protocol type"] = stepArgs[1];
                        metaDictionary["projectId"] = "place-holder-projectId";
                        metaDictionary["metaDataFilePath"] = "place-holder-metaDataFilePath";
                        DateTime startDt = DateTime.Now;
                        metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDt, true);
                        metaDictionary["startDate"] = SharedParameters.GetDateString(startDt);
                    }
                }
            }

            void ParseAppendXml(ref List<int> val)
            {
                //Boolean used to track validity of arguments/parameters
                bool argsOk = true;

                //string for start of output from ParseStep()
                outString += "Appending to existing XML document: ";

                //Requires an argument for the protocol type:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No protocol type argument given.";
                    val.Add(num);
                }
                else
                {
                    //If the protocol type argument exists, amke sure it is ok (needs to be a good xml atribute value, with just letters, numbers, spaces, or "-" or "_")
                    RegexValidationRule valRule = new RegexValidationRule();
                    valRule.RegexText = "^[a-zA-Z0-9-_ ]+$";
                    valRule.ErrorMessage = "Experiment step arguments can only contain letters, numbers, spaces, or \"-\" or \"_\"";
                    ValidationResult valRes = valRule.Validate(stepArgs[1], System.Globalization.CultureInfo.CurrentCulture);
                    if (!valRes.IsValid)
                    {
                        argsOk = false;
                        //Message for bad step typ argument
                        outString += "Experiment step arguments can only contain letters, numbers, spaces, or \"-\" or \"_\"";
                        val.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        RunAppendXml(num, stepArgs);
                    }
                    else
                    {
                        //When validating, put placeholder values into dictionary
                        DateTime startDt = DateTime.Now;
                        metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDt, true);
                        metaDictionary["startDate"] = SharedParameters.GetDateString(startDt);

                        metaDictionary["experimentId"] = "place-holder-expId";
                        metaDictionary["projectId"] = "place-holder-projectId";
                        metaDictionary["metaDataFilePath"] = "place-holder-metaDataFilePath";
                        metaDictionary["dataDirectory"] = SharedParameters.WorklistFolderPath;

                    }
                }
            }

            void ParseSaveXml(ref List<int> val)
            {
                //string for start of output from ParseStep()
                outString += "Saving XML document: ";

                // no arguments to check, so go straigt to running it
                if (!localIsValidating)
                {
                    RunSaveXml(num, stepArgs);
                }
            }

            void ParseLoadXml(ref List<int> val)
            {
                //Boolean used to track validity of arguments/parameters
                bool argsOk = true;

                //string for start of output from ParseLoadXml()
                outString += "Loading XML file: ";

                //Requires an argument for the file path:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No file path argument given.";
                    val.Add(num);
                }
                else
                {
                    string filePath = stepArgs[1];
                    //If the file path argument exists, make sure it is a .xml file that it actually exists
                    if (filePath.EndsWith(".xml"))
                    {
                        if (!File.Exists(filePath))
                        {
                            outString += $"XML file not found: {filePath}";
                            val.Add(num);
                            argsOk = false;
                        }
                    }
                    else
                    {
                        outString += $"XML files must have .xml extension): {filePath}";
                        val.Add(num);
                        argsOk = false;
                    }
                }

                if (argsOk)
                {
                    //if (!localIsValidating)
                    //{
                    //    load the xml
                    //}
                    //For this command, it doesn't matter whether or not isValidating
                    string loadReturn = LoadXml(stepArgs[1]);
                    //LoadXml() returns an empty string if sucessful, otherwise returns an error message
                    if (loadReturn != "")
                    {
                        outString += loadReturn;
                        val.Add(num);
                    }
                }
            }

            void ParseUserPrompt(ref List<int> val)
            {
                //UserPrompt takes 2, 3, or 4 arguments
                //First two arguments are title and message
                string titleString;
                string messageString;
                //third argument is string/path to a bitmap file
                string imagePath;
                //4th argument is width to display image
                string imageWidthStr;

                //string for start of output from ParseStep()
                outString += "User Prompt: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //UserPrompt requires at least two arguments:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Missing arguments; UserPrompt requires at least two arguments (title and message).";
                    val.Add(num);
                    argsOk = false;
                }
                //Then check the validity of the arguments (file types, parsable as numbers, etc.)
                else
                {
                    titleString = stepArgs[1];
                    messageString = stepArgs[2];
                    outString += $"{titleString}, ";
                    //TODO: check validity of messageString - {key}
                    if (true)
                    {
                        argsOk = true;
                    }
                    else
                    {
                        //Message to explain what is wrong
                        outString += "Not a valid message string: ";
                        val.Add(num);
                    }
                    outString += messageString;

                    if (argsOk && (numArgs > 3))
                    {
                        //check 3rd argument; needs to be a .bmp or .png
                        //example checking for argumant that is a .ovp file path
                        imagePath = stepArgs[3];
                        if (imagePath.EndsWith(".bmp", StringComparison.CurrentCultureIgnoreCase) || imagePath.EndsWith(".png", StringComparison.CurrentCultureIgnoreCase))
                        {
                            argsOk = File.Exists(imagePath);
                            if (!argsOk)
                            {
                                //Message to explain what is wrong
                                outString += "; Image file not found: ";
                                outString += imagePath;
                                val.Add(num);
                            }
                        }
                        else
                        {
                            //Message for bad filename
                            outString += "; Not a valid image filename: ";
                            outString += imagePath;
                            val.Add(num);
                            argsOk = false;
                        }

                    }

                    if (argsOk && (numArgs > 4))
                    {
                        //check 4th argument; needs to be an int
                        imageWidthStr = stepArgs[4];
                        int tempWidth;
                        if (int.TryParse(imageWidthStr, out tempWidth))
                        {
                            argsOk = true;
                        }
                        else
                        {
                            //Message for non-integer argument
                            outString += "; Image Width parameter must be an integer: ";
                            outString += imageWidthStr;
                            val.Add(num);
                            argsOk = false;
                        }

                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunUserPrompt(num, stepArgs);
                    }
                    else
                    {
                        //Nothing to do here when validating
                    }
                }
            }

            void ParseUserYesNo(ref List<int> val)
            {
                //UserYesNo takes 3 arguments
                //dictionary key, title, and message
                string keyString = "";
                string titleString = "";
                string messageString = "";

                //string for start of output from ParseStep()
                outString += "GetUserYesNo: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //UserPrompt requires at least three arguments:
                if (numArgs < 4)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Missing arguments; GetUserYesNo/ requires at least three arguments (key, title, and message).";
                    val.Add(num);
                    argsOk = false;
                }
                //Then check the validity of the arguments (file types, parsable as numbers, etc.)
                else
                {
                    keyString = stepArgs[1];
                    titleString = stepArgs[2];
                    messageString = stepArgs[3];
                    outString += $"user choice -> {keyString} ";

                    if (true)
                    {
                        argsOk = true;
                    }
                    else
                    {
                        //Message to explain what is wrong
                        outString += "Not a valid message string: ";
                        val.Add(num);
                    }
                    outString += messageString;
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunUserYesNo(num, stepArgs);
                    }
                    else
                    {
                        //When validating, put placeholder value into dictionary
                        metaDictionary[keyString] = "place-holder-yes-no";
                    }
                }
            }

            void ParseAddXml(ref List<int> val)
            {
                //UserPrompt takes 2 or 3 arguments
                //First two arguments are parentNode and newNode names
                string parentNodeStr;
                string newNodeStr;
                //third argument is the inner text
                string innerText;

                //string for start of output from ParseStep()
                outString += "AddXML: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //UserPrompt requires at least two arguments:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Missing arguments; AddXML requires at least two arguments (parentNode and newNodee).";
                    val.Add(num);
                    argsOk = false;
                }
                //need to check that parentNodeStr and newNodeStr are acceptable XML element names
                else
                {
                    parentNodeStr = stepArgs[1];
                    newNodeStr = stepArgs[2];
                    //If the parentNodeStr and newNodeStr exist, make sure they are acceptable XML element names
                    //    with just letters, numbers, or "-" or "_")
                    //    and must start with a letter or underscore
                    //    and not starting with "xml"
                    RegexValidationRule valRule = new RegexValidationRule();
                    valRule.RegexText = @"^[a-zA-Z_][a-zA-Z0-9-_]+$";
                    valRule.ErrorMessage = "XML element names can only contain letters, numbers, or \"-\" or \"_\"; they also must start with a letter or underscore.";
                    ValidationResult valRes1 = valRule.Validate(parentNodeStr, System.Globalization.CultureInfo.CurrentCulture);
                    ValidationResult valRes2 = valRule.Validate(newNodeStr, System.Globalization.CultureInfo.CurrentCulture);
                    if (valRes1.IsValid && valRes2.IsValid)
                    {
                        if (parentNodeStr.StartsWith("xml", StringComparison.CurrentCultureIgnoreCase) || newNodeStr.StartsWith("xml", StringComparison.CurrentCultureIgnoreCase))
                        {
                            argsOk = false;
                            //Message for bad XML element name
                            outString += "XML element names cannot start with \"xml\"";
                            val.Add(num);
                        }
                        else
                        {
                            argsOk = true;
                        }
                    }
                    else
                    {
                        argsOk = false;
                        //Message for bad XML element name
                        outString += "XML element names can only contain letters, numbers, or \"-\" or \"_\"; they also must start with a letter or underscore";
                        val.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunAddXml(num, stepArgs);
                    }
                    else
                    {
                        //When validating, ...
                    }
                }
            }

            void ParseSet(ref List<int> val)
            {
                //Set takes 2 arguments
                //First two arguments are the key and value to be set in the metaDictionary
                //If there are additional arguments, they get concatenated to the 2nd argument (valueString)
                string keyString;
                string valueString;

                //string for start of output from ParseStep()
                outString += "Setting Dictionary entry: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = false;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Set command requries two arguments (key and value).";
                    val.Add(num);
                }
                else
                {
                    //key and value just have to be non-empty strings, which has already been ruled out,
                    //    so no additional validation checks needed
                    keyString = stepArgs[1];
                    if (numArgs > 3)
                    {
                        string[] valueArr = new string[numArgs - 2];
                        for (int i=2; i<numArgs; i++)
                        {
                            valueArr[i - 2] = stepArgs[i];
                        }
                        valueString = string.Join(", ", valueArr);
                        stepArgs[2] = valueString;
                    }
                    else
                    {
                        valueString = stepArgs[2];
                    }
                    
                    argsOk = true;
                    outString += "{" + $"{keyString}" + "}" + $" = {valueString}";
                }

                if (argsOk)
                {
                    //Set steps need to run even when validating
                    RunSet(num, stepArgs);
                }
            }

            void ParseMath(ref List<int> val)
            {
                //Math takes 2 arguments
                //First argument is the key to be set in the metaDictionary
                //Second argument is the expression
                string keyString = "";
                string expressionString;
                double result = 0;

                //string for start of output from ParseStep()
                outString += "Math: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = true;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Math command requires two arguments (key and expression).";
                    val.Add(num);
                    argsOk = false;
                }
                else
                {
                    //key just has to be a non-empty string, which has already been ruled out,
                    //    so no additional validation checks needed for key
                    keyString = stepArgs[1];
                    expressionString = stepArgs[2];

                    //expression needs to contain exactly one occurance of a math operator (+, -, *, /, or %)
                    //    unless there are negative numbers - which are handled below
                    string[] operatorArr = new[] { "+", "-", "*", "/", "%" };
                    string[] expressionArr = expressionString.Split(operatorArr, StringSplitOptions.RemoveEmptyEntries);
                    //Then clean up expressionArr by trimming white space from ends of each string, and removing empty strings.
                    expressionArr = expressionArr.Select(s => s.Trim()).ToArray();
                    expressionArr = expressionArr.Where(x => !string.IsNullOrEmpty(x)).ToArray();

                    if (expressionArr.Length == 2)
                    {
                        string numberOneStr = expressionArr[0];
                        string numberTwoStr = expressionArr[1];
                        //Detect negative numbers based on how many operators and '-' characters are in the expression string
                        int numNegatives = expressionString.Length - expressionString.Replace("-", "").Length;
                        string tempStr = expressionString;
                        foreach (string op in operatorArr)
                        {
                            tempStr = tempStr.Replace(op, "");
                        }
                        int numOperators = expressionString.Length - tempStr.Length;

                        tempStr = expressionString.Replace("+", "");
                        switch (numOperators)
                        {
                            case 1:
                                //if there is just one operator, then the expression is parsed/split as above
                                //so, nothing else needs to be done
                                break;
                            case 2:
                                //If there are two operators, then one of the numbers is negative
                                //Detirmine which and add '-' sign back to it
                                if (expressionString.StartsWith("-"))
                                {
                                    numberOneStr = "-" + numberOneStr;
                                }
                                else
                                {
                                    numberTwoStr = "-" + numberTwoStr;
                                }
                                break;
                            case 3:
                                //If there are three operator characters, then both of the numbers are negative
                                //    or, there are multiple operators in the middle of the expression
                                if (!expressionString.StartsWith("-"))
                                {
                                    outString += $"Error in Math expression. The dumb parser that Dave wrote is confused. Remember, don't use double negatives, and two wrongs don't make a right: {expressionString}.";
                                    val.Add(num);
                                    argsOk = false;
                                }
                                //Then, before parsing, add the '-' sign back on to each
                                numberOneStr = "-" + numberOneStr;
                                numberTwoStr = "-" + numberTwoStr;
                                break;
                            default:
                                //If there are more than three operator characters, then send an error message
                                outString += $"Too many operator charcters in Math expression. The dumb parser that Dave wrote is confused. Remember, don't use double negatives, and two wrongs don't make a right: {expressionString}.";
                                val.Add(num);
                                argsOk = false;
                                break;
                        }

                        if (argsOk)
                        {
                            //then both elements in the expressionArr need to be parsable as numbers
                            double numberOne;
                            double numberTwo;
                            if (double.TryParse(numberOneStr, out numberOne) && double.TryParse(numberTwoStr, out numberTwo))
                            {
                                if (expressionString.Contains("-"))
                                {
                                    result = numberOne - numberTwo;
                                }

                                if (expressionString.Contains("+"))
                                {
                                    result = numberOne + numberTwo;
                                }

                                if (expressionString.Contains("*"))
                                {
                                    result = numberOne * numberTwo;
                                }

                                if (expressionString.Contains("/"))
                                {
                                    result = numberOne / numberTwo;
                                }

                                if (expressionString.Contains("%"))
                                {
                                    result = numberOne % numberTwo;
                                }

                                outString += $"{keyString} = {expressionString} = {result}.";
                            }
                            else
                            {
                                //Message for bad expression, terms not parsable as numbers:
                                outString += $"Expression not parsable as numbers: {expressionString}.";
                                val.Add(num);
                                argsOk = false;
                            }
                        }
                    }
                    else
                    {
                        //Message for bad expression, wrong number of terms:
                        outString += "Math command requires an expression with two numbers separated by a single math operator (+, -, *, /, or %).";
                        val.Add(num);
                        argsOk = false;
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //directly add the result to the dictionary here.
                        metaDictionary[keyString] = $"{result}";
                    }
                    else
                    {
                        //When validating, don't actually replace existing dictionary values;
                        //    this is necesary to avoid running a manually entered Math() step twice.
                        if (!metaDictionary.ContainsKey(keyString))
                        {
                            metaDictionary[keyString] = $"{result}";
                        }
                        
                    }
                }
            }

            void ParseGet(ref List<int> val)
            {
                //Get takes 2 or 3 arguments
                //The first 2 arguments are the metadata type and the key for saving the result in the metaDictionary
                string typeStr = "";
                string keyStr = "";

                //The third argument (optional) is the user prompt
                string promptStr = "";
                //The user prompt can be any string, so there is no validation check on it
                if (numArgs > 3)
                {
                    promptStr = stepArgs[3];
                }

                //string for start of output from ParseStep()
                outString += $"Getting information from user: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = false;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Get command requries two arguments (metadata type and key).";
                    val.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    typeStr = stepArgs[1];
                    keyStr = stepArgs[2];

                    //Make sure the metadata type is a valid type
                    var getList = SharedParameters.GetMetaTypeList();
                    getList.Add("concentration");
                    getList.Add("note");
                    getList.Add("number");
                    getList.Add("integer");
                    if (getList.Contains(typeStr))
                    {
                        argsOk = true;
                        outString += $"{typeStr} -> {keyStr} ";
                    }
                    else
                    {
                        //Message for bad argument:
                        outString += $"\"{typeStr}\" is not a valid Get/ type argument. ";
                        outString += $"Valid Get/ type arguments are: ";
                        foreach (string s in getList)
                        {
                            outString += $"{s}, ";
                        }
                        val.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        RunGet(num, stepArgs);
                    }
                    else
                    {
                        //When validating, put placeholder value into dictionary
                        switch (typeStr)
                        {
                            case "concentration":
                                concDictionary[keyStr] = new Concentration(0, SharedParameters.UnitsList[0]);
                                metaDictionary[$"{keyStr}Conc"] = "0.00";
                                metaDictionary[$"{keyStr}Units"] = SharedParameters.UnitsList[0];
                                break;
                            case "number":
                                metaDictionary[keyStr] = "2.71828182846";
                                break;
                            case "integer":
                                metaDictionary[keyStr] = "42";
                                break;
                            default:
                                metaDictionary[keyStr] = $"place-holder-{typeStr}";
                                break;
                        }
                    }
                }
            }

            void ParseGetExpId(ref List<int> val)
            {
                //GetExpId takes 1 or 2 arguments
                //The first argument is the default experiment ID
                string expIdStr = "";
                //The second (optional) argument is the default data directory
                string dataDirStr = "";

                //string for start of output from ParseStep()
                outString += $"Getting Experiment ID from user: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = true;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "GetExpId command requries at least one argument (default experiment ID).";
                    val.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    expIdStr = stepArgs[1];

                    //expIdStr needs to be usable in filenames, so make sure it only has just letters, numbers, or "-" or "_")
                    ValidationResult valRes = ValidateExpId(expIdStr);
                    if (!valRes.IsValid)
                    {
                        argsOk = false;
                        //Message for bad Experiment ID argument
                        outString += "Experiment IDs can only contain letters, numbers, or \"-\" or \"_\"";
                        val.Add(num);
                    }
                    else
                    {
                        if (numArgs > 2)
                        {
                            //check 2nd argument if there is one
                            dataDirStr = stepArgs[2];
                            //needs to be a valid path to a directory
                            if (!Directory.Exists(dataDirStr))
                            {
                                argsOk = false;
                                //Message for bad dataDirStr argument
                                outString += "Second argument must be a path to a valid directory. ";
                                val.Add(num);
                            }
                        }
                    }

                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        RunGetExpId(num, stepArgs);
                    }
                    else
                    {
                        //When validating, put placeholder value into dictionary
                        metaDictionary["experimentId"] = $"place-holder-experimentId";
                        metaDictionary["metaDataFilePath"] = $"place-holder-metaDataFilePath";
                        metaDictionary["dataDirectory"] = SharedParameters.WorklistFolderPath;
                    }

                }
            }

            void ParseGetFile(ref List<int> val)
            {
                //GetFile takes 2, 3, or 4 arguments
                //The first argument is the file key for saving the file path in the metaDictionary
                string fileKey = "";
                //The second argument is the promt for the get file dialog
                string promptStr = "";
                //Third (optional) argument is a file filter string
                string fileFilter = "";
                //Fourth (optional) argument is the default data directory
                string dataDirStr = "";

                //string for start of output from ParseStep()
                outString += $"Getting File from user: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = true;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 3)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "GetFile command requries at least two arguments (file key, and message).";
                    val.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    //no validity checks for first two arguments, any string is allowed
                    fileKey = stepArgs[1];
                    promptStr = stepArgs[2];
                    if (numArgs > 3)
                    {
                        //check 3rd argument
                        fileFilter = stepArgs[3];
                        //needs to be a valid path file filter string
                        OpenFileDialog dlg = new OpenFileDialog();
                        try
                        {
                            dlg.Filter = fileFilter;
                        }
                        catch (ArgumentException)
                        {
                            argsOk = false;
                            //Message for bad file filter argument
                            outString += "Third argument must be a valid file filter string, see help document. ";
                            val.Add(num);
                        }
                    }

                    if (numArgs > 4)
                    {
                        //check 4th argument
                        dataDirStr = stepArgs[4];
                        //needs to be a valid path to a directory
                        if (!Directory.Exists(dataDirStr))
                        {
                            argsOk = false;
                            //Message for bad dataDirStr argument
                            outString += "Fourth argument must be a path to a valid directory; ";
                            outString += dataDirStr;
                            val.Add(num);
                        }
                    }

                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        RunGetFile(num, stepArgs);
                    }
                    else
                    {
                        //When validating, put placeholder value into dictionary
                        metaDictionary[fileKey] = $"place-holder-file-path";

                    }

                }
            }

            void ParseGetTimeNow(ref List<int> val)
            {
                //ParseGetTimeNow takes one argument
                //The first argument is the file key for saving the time-date string in the metaDictionary
                string timeDateKey = "";

                //string for start of output from ParseStep()
                outString += $"Getting Current date and time: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = true;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "GetTimeNow command requries one argument (date-time key).";
                    val.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    //no validity check for the argument, any string is allowed
                    timeDateKey = stepArgs[1];
                }

                if (argsOk)
                {
                    //for this command, take the same action if validating or actually running the script
                    metaDictionary[timeDateKey] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
                }
            }

            void ParseStartPrompt(ref List<int> val)
            {
                //UserPrompt takes 2 arguments
                //First two arguments are title and listFilePath
                string titleString;
                string listFilePath;

                //string for start of output from ParseStep()
                outString += "Start Dialog: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //UserPrompt requires at least two arguments:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Missing arguments; StartPrompt requires two arguments (title and list file path).";
                    val.Add(num);
                    argsOk = false;
                }
                //Then check the validity of the arguments (file types, parsable as numbers, etc.)
                else
                {
                    titleString = stepArgs[1];
                    listFilePath = stepArgs[2];
                    outString += $"{titleString}, ";

                    if (listFilePath.EndsWith(".txt", StringComparison.CurrentCultureIgnoreCase))
                    {
                        argsOk = File.Exists(listFilePath);
                        if (!argsOk)
                        {
                            //Message to explain what is wrong
                            outString += "; List file not found: ";
                            outString += listFilePath;
                            val.Add(num);
                        }
                        else
                        {
                            outString += listFilePath;
                        }
                    }
                    else
                    {
                        //Message for bad filename
                        outString += "; Not a valid list filename (.txt): ";
                        outString += listFilePath;
                        val.Add(num);
                        argsOk = false;
                    }

                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunStartDialog(num, stepArgs);
                    }
                    else
                    {
                        //Nothing to do when validating
                    }
                }
            }

            void ParseCopyRemoteFiles(ref List<int> val)
            {
                //CopyRemoteFiles takes no arguments
                //so there is nothing to do here except run the method
                bool argsOk = true; //place-holder in case I need to use it later
                
                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        //Run the step
                        RunCopyRemoteFiles();
                    }
                    else
                    {
                    }
                }
            }

            void ParseImportDictionary(ref List<int> val)
            {
                //Boolean used to track validity of arguments/parameters
                bool argsOk = true;

                //string for start of output from ParseImportDictionary()
                outString += "Importing Dictionary: ";

                //Requires an argument for the file path:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No file path argument given.";
                    val.Add(num);
                }
                else
                {
                    string filePath = stepArgs[1];
                    //If the file path argument exists, make sure it is a .txt file that actually exists
                    if (filePath.EndsWith(".txt"))
                    {
                        if (!File.Exists(filePath))
                        {
                            outString += $"Dictionary file not found: {filePath}";
                            val.Add(num);
                            argsOk = false;
                        }
                    }
                    else
                    {
                        outString += $"Dictionary files must be plain text files (*.txt): {filePath}";
                        val.Add(num);
                        argsOk = false;
                    }
                }

                if (argsOk)
                {
                    //if (!localIsValidating)
                    //{
                    //    RunNewXml(num, stepArgs);
                    //}
                    //For this command, it doesn't matter whether or not isValidating
                    ImportDictionary(stepArgs[1]);
                }
            }

            void ParseExportDictionary(ref List<int> val)
            {
                //Boolean used to track validity of arguments/parameters
                bool argsOk = true;

                //string for start of output from ParseImportDictionary()
                outString += "Exporting Dictionary: ";

                //Requires an argument for the file path:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No file path argument given.";
                    val.Add(num);
                }
                else
                {
                    string filePath = stepArgs[1];
                    //If the file path argument exists, make sure it is a .txt file that actually exists
                    if (filePath.EndsWith(".txt"))
                    {
                        string dir = System.IO.Path.GetDirectoryName(filePath);
                        //Make sure program can write to the specified location:
                        if (!SharedParameters.IsDirectoryWritable(dir))
                        {
                            outString += $"Directory for dictionary export is not writable: {dir}";
                            val.Add(num);
                            argsOk = false;
                        }
                    }
                    else
                    {
                        outString += $"Dictionary files must be plain text files (*.txt): {filePath}";
                        val.Add(num);
                        argsOk = false;
                    }
                }

                if (argsOk)
                {
                    if (!localIsValidating)
                    {
                        ExportDictionary(stepArgs[1]);
                    }
                }
            }

            //Don't actually use this local function, it's just here as a template for new ParseXxxxStep functions
            void ParseGenericStep(ref List<int> val)
            {
                //string for start of output from ParseStep()
                outString += "Running Generic Step: ";

                //one or more Booleans used to track validity of arguments/parameters
                bool argsOk = false;

                //If the command requires a certain number of arguments, check that first:
                if (numArgs < 2)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "No command argument given.";
                    val.Add(num);
                }
                //Then check the validity of the arguments (file types, parsable as numbers, etc.)
                else
                {
                    //example checking for argumant that is a .ovp file path
                    if (stepArgs[1].EndsWith(".ovp"))
                    {
                        argsOk = true;
                    }
                    else
                    {
                        //Message to explain what is wrong
                        outString += "Not a valid ovp filename: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                }

                //Other validity checks, for example, checking to see if a file actaully exixts
                if (argsOk)
                {
                    bool ovpExists = File.Exists(stepArgs[1]);
                    if (ovpExists)
                    {
                        outString += stepArgs[1];

                        if (!localIsValidating)
                        {
                            //Put in the code to actaully run the step here, e.g. RunOverlord(num, stepArgs);
                            //  that method has to be written separately
                            //RunGeneric(num, stepArgs);
                        }
                    }
                    else
                    {
                        //Message if the file does not exist
                        outString += "Procedure file not found: ";
                        outString += stepArgs[1];
                        val.Add(num);
                    }
                }
            }
        }

        private ValidationResult ValidateExpId(string expIdStr)
        {
            //expIdStr needs to be usable in filenames, so make sure it only has just letters, numbers, or "-" or "_")
            RegexValidationRule valRule = new RegexValidationRule();
            valRule.RegexText = "^[a-zA-Z0-9-_]+$";
            valRule.ErrorMessage = "Experiment IDs can only contain letters, numbers, or \"-\" or \"_\"";
            ValidationResult valRes = valRule.Validate(expIdStr, System.Globalization.CultureInfo.CurrentCulture);

            return valRes;
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            if (runStepsThread.IsAlive)
            {
                //If the steps are already running, take it out of the paused state
                IsPaused = false;
            }
            else
            {
                //if the step-runner thread is not already running, start it up

                //Run validation check before running actual experiment
                if (Validate())
                {
                    //Change the IsPaused property to false
                    IsPaused = false;

                    Play();
                }
            }

        }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            if (runStepsThread.IsAlive)
            {
                //If the steps are already running, let it go on to the next step
                IsOneStep = true;
            }
            else
            {
                //if the step-runner thread is not already running, start it up the same as with PlayButton_Click
                //    but with IsPaused = true and IsOneStep = true, so that it just runs one step
                if (Validate())
                {
                    IsPaused = true;
                    IsOneStep = true;

                    Play();
                }
            }

        }

        private void Play()
        {
            AbortCalled = false;
            IsRunning = true;
            isValidating = false;
            valFailed = new List<int>();

            //If runStepsThread is not already running, start it from the begining
            if (!runStepsThread.IsAlive)
            {
                RunSteps();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            IsPaused = true;
        }

        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            YesNoDialog.Response response = SharedParameters.ShowYesNoDialog("Are you sure you want to abort?", "OK to Abort?");

            if (response == YesNoDialog.Response.Yes)
            {
                AbortCalled = true;

                //Set IsPaused = false, so that StepsThreadProc() will go ahead to the next step where the Abort action happens
                IsPaused = false;
            }
        }

        private bool Validate()
        {
            //clear GUI
            validationBorder.Background = Brushes.Transparent;
            OutputText = "";
            for (int i=0; i<10; i++)
            {
                Thread.Sleep(10);
            }

            IsPaused = false;
            AbortCalled = false;
            IsRunning = true;
            isValidating = true;
            valFailed = new List<int>();

            bool valReturn = false;

            // For validation, run in the main thread...
            StepsThreadProc();

            //Validation failure is signaled by having one or more entires in the valFailed list 
            if (valFailed.Count > 0)
            {
                AddOutputText("\r\n");
                AddOutputText("Validation failed on the following steps:\r\n");
                foreach (int i in valFailed)
                {
                    AddOutputText($"{i}, ");
                }
                validationTextBlock.Text = "Validation Failed";
                validationBorder.Background = Brushes.Red;// new SolidColorBrush(Colors.Red);
            }
            else
            {
                AddOutputText("\r\n");
                AddOutputText("Validation sucessful.\r\n");
                validationTextBlock.Text = "Validation Sucessful";
                validationBorder.Background = Brushes.LimeGreen;// new SolidColorBrush(Colors.LimeGreen);
                valReturn = true;
            }
            isValidating = false;

            return valReturn;
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            Validate();
        }

        private void RunCopyRemoteFiles()
        {
            string saveDirectory;
            if (metaDictionary.ContainsKey("dataDirectory"))
            {
                saveDirectory = metaDictionary["dataDirectory"];
                //If saveDirectory is in "C:\Shared Files\"
                //    copy files
                if (saveDirectory.StartsWith(@"C:\Shared Files\", StringComparison.CurrentCultureIgnoreCase))
                {
                    var serverList = GetConnectedReadersList();
                    if (serverList.Count > 0)
                    {
                        foreach (string s in serverList)
                        {
                            string ip = readerIps[s];

                            if (ip != "localhost")
                            {
                                string localStart = @"C:\Shared Files";
                                string remoteStart = @"\\" + ip + @"\Shared Files";
                                string pathFromHere = saveDirectory.Replace(localStart, remoteStart);

                                try
                                {
                                    string[] fileArray = Directory.GetFiles(pathFromHere, "*", SearchOption.AllDirectories);
                                    string localCopy;

                                    foreach (string f in fileArray)
                                    {
                                        localCopy = f.Replace(remoteStart, localStart);
                                        try
                                        {
                                            File.Copy(f, localCopy);
                                        }
                                        catch (Exception e)
                                        {
                                            MessageBox.Show($"LMSF Scheduler is not able to the file, {f} on the remote {s} computer (IP adress: {ip}) to the local computer. Manually copy the file and click 'OK' to continue");
                                        }
                                    }
                                }
                                catch (Exception e)
                                {
                                    MessageBox.Show($"LMSF Scheduler is not able to copy files from {saveDirectory} on the remote {s} computer (IP adress: {ip}). Manually copy files and click 'OK' to continue");
                                }
                            }
                        }
                    }
                }
            }
            
        }

        private void AddXmlProtocol(string typeStr, string sourceStr)
        {
            //First check if there is already a protocol of this type attached to the experiment,
            //    and if so, give a warning
            XmlNodeList nodeList = experimentNode.SelectNodes($"descendant::protocol[@type = \"{typeStr}\"]");
            XmlNode existingNode;

            AbortAppendOverwriteDialog.Response dlgResponse = AbortAppendOverwriteDialog.Response.Abort;
            if (nodeList.Count == 0)
            {
                dlgResponse = AbortAppendOverwriteDialog.Response.Append;
            }
            else
            {
                existingNode = nodeList[nodeList.Count - 1];

                string messageStr = $"A {typeStr} protocol is already attached to this experiment. \n\tSelect 'Append' to append the new protocol, \n\t'Overwrite' to replace the existing protocol information, \n\tor 'Abort' to abort.";
                string titleStr = $"Existing {SharedParameters.ToTitleCase(typeStr)} Protocol";
                ;
                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => {
                    dlgResponse = SharedParameters.ShowAbortAppendOverwrite(messageStr, titleStr);
                    if (dlgResponse == AbortAppendOverwriteDialog.Response.Abort)
                    {
                        AbortCalled = true;
                    }
                });

                if (dlgResponse == AbortAppendOverwriteDialog.Response.Overwrite)
                {
                    //delete the existing protocol node
                    experimentNode.RemoveChild(existingNode);
                }
            }

            if (dlgResponse != AbortAppendOverwriteDialog.Response.Abort)
            {
                //turn on metadata collection
                isCollectingXml = true;

                //New protocol node
                protocolNode = xmlDoc.CreateElement("protocol");

                //type attribute for step node
                XmlAttribute stepIdAtt = xmlDoc.CreateAttribute("type");
                stepIdAtt.Value = typeStr;
                protocolNode.Attributes.Append(stepIdAtt);

                //source attribute for step node
                XmlAttribute sourceAtt = xmlDoc.CreateAttribute("source");
                sourceAtt.Value = sourceStr;
                protocolNode.Attributes.Append(sourceAtt);

                startDateTime = DateTime.Now;
                AddDateTimeNodes(startDateTime, protocolNode, "protocol started");

                //add the step node to the experiment node
                experimentNode.AppendChild(protocolNode);
            }

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }

        }

        private void AddXmlMetaDetail(string metaType, string idStr, string key, string notes)
        {
            XmlNode baseNode;
            XmlNode detailNode;
            XmlNode idNode;
            XmlNode notesNode;

            string baseNodeStr = $"{metaType}s";
            string detailNodeStr = metaType;
            string idNodeStr = $"{metaType}Id";
            if (metaType == "plasmid")
            {
                baseNodeStr = "strains";
                detailNodeStr = "strain";
                idNodeStr = "plasmidId";
            }
            if (metaType == "media")
            {
                baseNodeStr = "media";
                detailNodeStr = "medium";
                idNodeStr = "mediaId";
            }
            if (metaType == "antibiotic")
            {
                baseNodeStr = "additives";
                detailNodeStr = "additive";
                idNodeStr = "additiveId";
                key = "antibiotic";
            }
            //Adds metadata to the current protocolNode
            //look for the base node and append to it or create it if it does not exist
            XmlNodeList baseNodeList = protocolNode.SelectNodes($"descendant::{baseNodeStr}");
            if (baseNodeList.Count > 0)
            {
                baseNode = baseNodeList.Item(baseNodeList.Count - 1);
            }
            else
            {
                baseNode = xmlDoc.CreateElement(baseNodeStr);
                protocolNode.AppendChild(baseNode);
            }

            //Plasmid details get attached to the last "strain" detail node if there is one
            if (metaType == "plasmid")
            {
                XmlNodeList nodeList = baseNode.SelectNodes("descendant::strain");
                if (nodeList.Count > 0)
                {
                    detailNode = nodeList.Item(nodeList.Count - 1);
                }
                else
                {
                    detailNode = xmlDoc.CreateElement(detailNodeStr);
                    if (key != "")
                    {
                        XmlAttribute keyAtt = xmlDoc.CreateAttribute("useKey");
                        keyAtt.Value = key;
                        detailNode.Attributes.Append(keyAtt);
                    }
                    baseNode.AppendChild(detailNode);
                }
            }
            else
            {
                //Then create and append the detail node
                //    with attribute useKey if key!=""
                detailNode = xmlDoc.CreateElement(detailNodeStr);
                if ((key != "") && (metaType != "note"))
                {
                    XmlAttribute keyAtt = xmlDoc.CreateAttribute("useKey");
                    keyAtt.Value = key;
                    detailNode.Attributes.Append(keyAtt);
                }
                if (metaType == "note")
                {
                    detailNode.InnerText = idStr;
                }
                baseNode.AppendChild(detailNode);
            }

            if (metaType != "note")
            {
                //Then create and append the ID node
                idNode = xmlDoc.CreateElement(idNodeStr);
                idNode.InnerText = idStr;
                detailNode.AppendChild(idNode);

                //then add notes if notes!=""
                if (notes != "")
                {
                    notesNode = xmlDoc.CreateElement("note");
                    notesNode.InnerText = notes;
                    detailNode.AppendChild(notesNode);
                }
            }

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }
        }

        private void AddXmlConcentration(Concentration conc, string keyStr)
        {
            XmlNode baseNode;
            XmlNode stockNode;

            string baseNodeStr = "additive";
            string stockNodeStr = "concentration";

            //Adds metadata to the current protocolNode
            //look for the base node and append to it or create it if it does not exist
            XmlNodeList baseNodeList = protocolNode.SelectNodes($"descendant::{baseNodeStr}");
            if (baseNodeList.Count > 0)
            {
                baseNode = baseNodeList.Item(baseNodeList.Count - 1);
            }
            else
            {
                baseNode = xmlDoc.CreateElement(baseNodeStr);
                protocolNode.AppendChild(baseNode);
            }

            //Add the concentration node
            stockNode = xmlDoc.CreateElement(stockNodeStr);
            XmlAttribute keyAtt = xmlDoc.CreateAttribute("useKey");
            keyAtt.Value = keyStr;
            stockNode.Attributes.Append(keyAtt);
            baseNode.AppendChild(stockNode);

            //Concentration details get attached to the concentration node
            XmlNode valueNode = xmlDoc.CreateElement("value");
            valueNode.InnerText = $"{conc.ConcValue}";
            stockNode.AppendChild(valueNode);
            XmlNode unitsNode = xmlDoc.CreateElement("units");
            unitsNode.InnerText = conc.Units;
            stockNode.AppendChild(unitsNode);

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }
        }

        private string GetProjectIdentifier()
        {
            string pId = SharedParameters.GetMetaIdentifier("project", "Select the Project Identifier for this experiment:");

            if (pId == "")
            {
                AbortCalled = true;
            }

            return pId;
        }

        private string[] SelectXmlDocForAppend()
        {
            //return string[0] = experimentId
            //return string[1] = XML file path
            //return string[2] = saveDirectory
            string[] getIdStrings;// = SharedParameters.XmlDocForAppend("");
            if (metaDictionary.ContainsKey("dataDirectory"))
            {
                getIdStrings = SharedParameters.XmlDocForAppend(metaDictionary["dataDirectory"]);
            }
            else
            {
                getIdStrings = SharedParameters.XmlDocForAppend("");
            }
            string expIdStr = getIdStrings[0];
            string metaDataFilePath = getIdStrings[1];
            string saveDirectory = getIdStrings[2];

            if (expIdStr == "")
            {
                AbortCalled = true;
            }

            return new string[] { expIdStr, metaDataFilePath, saveDirectory };
        }

        private string SelectFile(string filePrompt, string fileFilter, string initialDir)
        {
            //examples for fileFilter: 
            //    "XML documents (.xml)|*.xml"
            //    "Office Files|*.doc;*.xls;*.ppt"
            //    "Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt"
            string retFile = "";

            retFile = SharedParameters.GetFile(filePrompt, fileFilter, initialDir);

            if (retFile == "")
            {
                AbortCalled = true;
            }

            return retFile;
        }
        
        private void RunGetFile(int num, string[] args)
        {
            string fileKey = args[1];
            string filePrompt = args[2];
            string fileFilter = "";
            string dataDirStr = "";
            if (args.Length>3)
            {
                fileFilter = args[3];
            }
            if (args.Length > 4)
            {
                dataDirStr = args[4];
            }

            string filePath = "";

            //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
            this.Dispatcher.Invoke(() => {
                filePath = SelectFile(filePrompt, fileFilter, dataDirStr);
            });

            if (filePath != "")
            {
                //Add the file path to the metaDictionary
                metaDictionary[fileKey] = filePath;
            }

        }

        private string LoadXml(string filePath)
        {
            string retStr = "";
            try
            {
                xmlDoc = new XmlDocument();
                xmlDoc.Load(filePath);
                metaDataFilePath = filePath;

                //get the experiment node
                XmlNodeList expNodeList = xmlDoc.SelectNodes("descendant::experiment");
                experimentNode = expNodeList.Item(expNodeList.Count - 1);

                //get the protocol node
                XmlNodeList protocolNodeList = xmlDoc.SelectNodes("descendant::protocol");
                protocolNode = protocolNodeList.Item(expNodeList.Count - 1);

                string expIdStr = xmlDoc.SelectSingleNode("descendant::experimentId").InnerText;

                //add experimentId, projectId, to metaDictionary
                metaDictionary["experimentId"] = expIdStr;
                metaDictionary["projectId"] = xmlDoc.SelectSingleNode("descendant::projectId").InnerText;
                metaDictionary["metaDataFilePath"] = metaDataFilePath;

                isCollectingXml = true;
            }
            catch (Exception e)
            {
                retStr = $"Error reading XML file: {e}";
            }

            return retStr;
        }

        private void RunAppendXml(int num, string[] args)
        {
            string protocolTypeStr = args[1];

            string[] argsBack = new string[] { "", "", "" };
            //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
            this.Dispatcher.Invoke(() => {
                argsBack = SelectXmlDocForAppend();
            });

            //auto-detect and/or prompt user to select existing XML file to append to
            metaDataFilePath = argsBack[1];

            if (metaDataFilePath != "")
            {
                //Load existing XML document
                xmlDoc = new XmlDocument();
                xmlDoc.Load(metaDataFilePath);

                //get the experiment node and append to it
                XmlNodeList expNodeList = xmlDoc.SelectNodes("descendant::experiment");
                experimentNode = expNodeList.Item(expNodeList.Count - 1);

                string expIdStr = xmlDoc.SelectSingleNode("descendant::experimentId").InnerText;

                //Add the current experiment protocol to the XML
                AddXmlProtocol(protocolTypeStr, protocolSource);

                //also add the startDateTime to the metaDictionary, as a string formatted for use as part of an experimentId
                metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDateTime, true);
                metaDictionary["startDate"] = SharedParameters.GetDateString(startDateTime);
                //add experimentId, projectId, and dataDirectory to metaDictionary
                metaDictionary["experimentId"] = expIdStr;
                metaDictionary["projectId"] = xmlDoc.SelectSingleNode("descendant::projectId").InnerText;
                metaDictionary["dataDirectory"] = argsBack[2];
                metaDictionary["protocol type"] = protocolTypeStr;
                metaDictionary["metaDataFilePath"] = metaDataFilePath;

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }
            
        }

        private void RunNewXml(int num, string[] args)
        {
            string protocolType = args[1];

            //New XML document
            xmlDoc = new XmlDocument();
            //New file path for saving the XML
            metaDataFilePath = SharedParameters.LogFileFolderPath + "temp.xml";

            //create and configure the root node
            rootNode = xmlDoc.CreateElement("metadata");
            XmlAttribute sourceAtt = xmlDoc.CreateAttribute("source");
            sourceAtt.Value = "NIST LMSF";
            rootNode.Attributes.Append(sourceAtt);
            //add the root node to the document
            xmlDoc.AppendChild(rootNode);

            //New project node
            projectNode = xmlDoc.CreateElement("project");
            ////ID attribute for project node
            //XmlAttribute projectIdAtt = xmlDoc.CreateAttribute("projectId");
            //Project ID node
            XmlNode projectIdNode = xmlDoc.CreateElement("projectId");

            //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
            this.Dispatcher.Invoke(() => { projectIdNode.InnerText = GetProjectIdentifier(); });
            //projectIdAtt.Value = SharedParameters.GetMetaIdentifier("project", "Select the Project Identifier for this experiment:");


            projectNode.AppendChild(projectIdNode);
            //add the project node to the root node
            rootNode.AppendChild(projectNode);

            //add experiment node to the project node
            experimentNode = xmlDoc.CreateElement("experiment");
            //add the experiment ID node to the project node
            projectNode.AppendChild(experimentNode);

            //New experiment ID node
            //    Value/InnerText initially set to "temp_identifier"
            //    then a down-stream command will set it to a standard format, like "2019-01-09_1515_pGTGv1_pGTGv2" "yyyy-MM-dd_HHmm_<identifiers>"
            experimentIdNode = xmlDoc.CreateElement("experimentId");
            experimentID = "temp_identifier";
            experimentIdNode.InnerText = experimentID;
            //add the experiment ID node to the experiment node
            experimentNode.AppendChild(experimentIdNode);

            //Add the current experiment protocol to the XML
            AddXmlProtocol(protocolType, protocolSource);

            //Also add the protocol type, metadataFilePath, and projectID to the metaDictionary
            metaDictionary["protocol type"] = protocolType;
            metaDictionary["projectId"] = projectIdNode.InnerText;
            metaDictionary["metaDataFilePath"] = metaDataFilePath;
            //also add the startDateTime to the metaDictionary, as a string formatted for use as part of an experimentId
            metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDateTime, true);
            metaDictionary["startDate"] = SharedParameters.GetDateString(startDateTime);

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }
        }

        private void RunAddXml(int num, string[] args)
        {
            //Adds metadata to the current protocolNode
            string parentNodeStr = args[1];
            string newNodeStr = args[2];
            string innerText = "";
            if (args.Length > 3)
            {
                innerText = args[3];
            }

            XmlNode parentNode;
            XmlNode newNode;

            //If parentNodeStr is "protocol" then add the new node directly to the protocol node
            if (parentNodeStr == "protocol")
            {
                parentNode = protocolNode;
            }
            else
            {
                //If the parent node is not the protocol node, look for latest descendent node
                //look for the parent node and append to it or create it if it does not exist
                XmlNodeList baseNodeList = protocolNode.SelectNodes($"descendant::{parentNodeStr}");
                if (baseNodeList.Count > 0)
                {
                    parentNode = baseNodeList.Item(baseNodeList.Count - 1);
                }
                else
                {
                    parentNode = xmlDoc.CreateElement(parentNodeStr);
                    protocolNode.AppendChild(parentNode);
                }
            }
            

            //Add the new node
            newNode = xmlDoc.CreateElement(newNodeStr);
            //newNode.InnerText = innerText.Replace(@"\\",@"\");
            newNode.InnerText = innerText;
            parentNode.AppendChild(newNode);

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }
        }

        private void RunSaveXml(int num, string[] args)
        {
            bool isFini = false;
            if (args.Length>1)
            {
                isFini = !(args[1] == "not finished");
            }
            else
            {
                isFini = true;
            }

            if (isFini)
            {
                //Add protocol finishing time to XML output
                DateTime dt = DateTime.Now;
                XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::protocol/dateTime");
                //If for some reason the dateNode is null (no protocol/dateTime Node previously added)
                //  then don't try to append a child node to it, since that will throw an error
                if (dateNodeList != null)
                {
                    XmlNode dateNode = dateNodeList.Item(dateNodeList.Count - 1);
                    XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                    timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                    XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                    statusFiniAtt.Value = "protocol finished";
                    timeFiniNode.Attributes.Append(statusFiniAtt);
                    dateNode.AppendChild(timeFiniNode);
                }
                
            }

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }

            //Also save the outputSteps list as a script file in the experiment folder
            //    append a number to the script output file name so that multiple scripts can be run for a single experiment/folder
            string saveDirectory = System.IO.Path.GetDirectoryName(metaDataFilePath);
            if (Directory.Exists(saveDirectory))
            {
                string fileNameRoot = System.IO.Path.GetFileNameWithoutExtension(metaDataFilePath);
                int expNum = 1;
                string filePath = System.IO.Path.Combine(saveDirectory, $"{fileNameRoot}.output script_{expNum}.lmsf");
                while (File.Exists(filePath))
                {
                    expNum++;
                    filePath = System.IO.Path.Combine(saveDirectory, $"{fileNameRoot}.output script_{expNum}.lmsf");
                }

                try
                {
                    using (StreamWriter outputFile = new StreamWriter(filePath))
                    {
                        foreach (string line in outputSteps)
                        {
                            outputFile.WriteLine(line);
                        }
                    }
                }
                catch (Exception e)
                {
                    AddOutputText($"Exception while writing output steps script: {e}.\n");
                }

            }

            //turn off metadata collection
            //isCollectingXml = false;
        }

        private void RunUserYesNo(int num, string[] args)
        {
            string keyStr = args[1];
            string titleStr = args[2];
            string messageStr = args[3];

            YesNoDialog.Response userResponse = YesNoDialog.Response.No;

            //this has to be delegated becasue it interacts with the GUI by calling up a dialog box
            this.Dispatcher.Invoke(() => {
                userResponse = SharedParameters.ShowYesNoDialog(messageStr, titleStr);
            });

            metaDictionary[keyStr] = userResponse.ToString();
        }

        private void RunUserPrompt(int num, string[] args)
        {
            string messageStr = args[2];
            messageStr = messageStr.Replace(@"\t", "\t");
            messageStr = messageStr.Replace(@"\n", "\n");

            int imageWidth = 400;
            if (args.Length > 4)
            {
                int.TryParse(args[4], out imageWidth);
            }

            string titleStr = args[1];
            bool? oKToGo = false;
            if (args.Length < 4)
            {
                //this has to be delegated becasue it interacts with the GUI by calling up a dialog box
                this.Dispatcher.Invoke(() => {
                    oKToGo = SharedParameters.ShowPrompt(messageStr, titleStr);
                    if (!(oKToGo == true))
                    {
                        AbortCalled = true;
                    }
                });
            }
            else
            {
                string imagePath = args[3];
                this.Dispatcher.Invoke(() => {
                    oKToGo = SharedParameters.ShowPrompt(messageStr, titleStr, imagePath, imageWidth);
                    if (!(oKToGo == true))
                    {
                        AbortCalled = true;
                    }
                });
            }
        }

        private void RunStartDialog(int num, string[] args)
        {
            string listPath = args[2];
            string titleStr = args[1];

            bool? oKToGo = false;

            this.Dispatcher.Invoke(() => {
                oKToGo = SharedParameters.ShowStartDialog(titleStr, listPath);

                if (!(oKToGo == true))
                {
                    AbortCalled = true;
                }
            });

        }

        private string GetMetaIdentifier(string metaType, string prompt)
        {
            string pId = SharedParameters.GetMetaIdentifier(metaType, prompt);

            if (pId == "")
            {
                AbortCalled = true;
            }

            return pId;
        }

        private Concentration GetAdditiveConcentration(string name, string prompt)
        {
            Concentration conc = SharedParameters.GetAdditiveConcentration(name, prompt);

            if (conc is null)
            {
                AbortCalled = true;
            }

            return conc;
        }

        private string GetNotes(string prompt)
        {
            string notes = SharedParameters.GetNotes("Protocol Note", prompt);

            return notes;
        }

        private void RunGet(int num, string[] args)
        {
            string typeStr = args[1];
            string keyStr = args[2];
            string promptStr = "";
            string valueStr = "";
            string notes = "";
            if (args.Length > 3)
            {
                promptStr = args[3];
                if (promptStr=="default") {
                    promptStr = "";
                }
            }
            if (args.Length > 4)
            {
                notes = args[4];
            }

            switch (typeStr)
            {
                case "number":
                    GetNumber();
                    break;
                case "integer":
                    GetInteger();
                    break;
                case "concentration":
                    GetConcentration();
                    break;
                case "note":
                    GetNote();
                    break;
                case "user":
                    GetUser();
                    break;
                default:
                    GetDefault();
                    break;
            }

            void GetNumber()
            {
                //Default prompt for number
                if (promptStr == "")
                {
                    promptStr = $"Enter the number: ";
                }

                //this has to be delegated becasue it interacts with the GUI by calling up a dialog box
                this.Dispatcher.Invoke(() => {
                    valueStr = GetNumberFromUser(promptStr);
                    metaDictionary[keyStr] = valueStr;
                });

            }

            void GetInteger()
            {
                //Default prompt for number
                if (promptStr == "")
                {
                    promptStr = $"Enter the number (integer): ";
                }

                //this has to be delegated becasue it interacts with the GUI by calling up a dialog box
                this.Dispatcher.Invoke(() => {
                    valueStr = GetNumberFromUser(promptStr, true);
                    metaDictionary[keyStr] = valueStr;
                });

            }

            void GetConcentration()
            {
                //Default prompt for concentration
                if (promptStr == "")
                {
                    promptStr = $"Enter the concentration and units for the {keyStr}: ";
                }

                Concentration conc;
                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => {
                    conc = GetAdditiveConcentration(keyStr, promptStr);
                    concDictionary[keyStr] = conc;
                    if (!(conc is null))
                    {
                        metaDictionary[$"{keyStr}Conc"] = conc.ConcValue.ToString();
                        metaDictionary[$"{keyStr}Units"] = conc.Units;
                    }
                    
                    //Then save to XML document if...
                    if (isCollectingXml && !AbortCalled)
                    {
                        AddXmlConcentration(conc, keyStr);
                    }
                });
            }

            void GetNote() {
                //Default prompt for notes
                if (promptStr == "")
                {
                    promptStr = $"Enter any additional {keyStr}s for this protocol: ";
                }

                //this has to be delegated becasue it interacts with the GUI by calling up a dialog box
                this.Dispatcher.Invoke(() => {
                    valueStr = GetNotes(promptStr);
                    metaDictionary[keyStr] = valueStr;
                });

                //Then save to XML document if...
                if (isCollectingXml && !AbortCalled)
                {
                    AddXmlMetaDetail(typeStr, valueStr, keyStr, notes);
                }
            }

            void GetUser()
            {
                //Default prompt for user
                if (promptStr == "")
                {
                    promptStr = $"Select your user ID: ";
                }

                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => { valueStr = GetMetaIdentifier(typeStr, promptStr); metaDictionary[keyStr] = valueStr; });

                //Then save to XML document if...
                if (isCollectingXml && !AbortCalled)
                {
                    AddXmlMetaDetail(typeStr, valueStr, keyStr, notes);
                }
            }

            void GetDefault()
            {
                //Default prompt for everything else
                if (promptStr == "")
                {
                    promptStr = $"Select the {keyStr} for this protocol: ";
                }

                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => { valueStr = GetMetaIdentifier(typeStr, promptStr); metaDictionary[keyStr] = valueStr; });

                //Then save to XML document if...
                if (isCollectingXml && !AbortCalled)
                {
                    AddXmlMetaDetail(typeStr, valueStr, keyStr, notes);
                }
            }

        }

        private string GetNumberFromUser(string prompt, bool isInteger = false)
        {
            string numStr;

            if (isInteger)
            {
                numStr = SharedParameters.GetNumber(prompt, true);
            }
            else
            {
                numStr = SharedParameters.GetNumber(prompt);
            }

            if (numStr == "")
            {
                AbortCalled = true;
            }

            return numStr;
        }

        private void UpdateXmlExpId(string expIdStr)
        {
            XmlNode idNode;
            //Changes the Inner Text of the current experimentId node
            //look for the base node and append to it or create it if it does not exist
            XmlNodeList nodeList = experimentNode.SelectNodes($"descendant::experimentId");
            if (nodeList.Count > 0)
            {
                idNode = nodeList.Item(nodeList.Count - 1);
            }
            else
            {
                idNode = xmlDoc.CreateElement("experimentId");
                experimentNode.AppendChild(idNode);
            }
            
            idNode.InnerText = expIdStr;

            //Save the XML document
            try
            {
                xmlDoc.Save(metaDataFilePath);
            }
            catch (Exception e)
            {
                MessageBox.Show($"Error saving XML document: {e}");
            }
        }

        private string[] GetExpId(string dataDirStr, string expIdStr, string projID = "")
        {
            //return string[0] = experimentId
            //return string[1] = XML file path
            //return string[2] = saveDirectory
            string[] getIdStrings = SharedParameters.GetExperimentId(dataDirStr, expIdStr, projID);
            expIdStr = getIdStrings[0];
            metaDataFilePath = getIdStrings[1];
            string saveDirectory = getIdStrings[2];

            if (expIdStr == "")
            {
                AbortCalled = true;
            }
            else
            {
                //If saveDirectory is in "C:\Shared Files\"
                //    create matching saveDirectory on all connected server computers
                if (saveDirectory.StartsWith(@"C:\Shared Files\", StringComparison.CurrentCultureIgnoreCase))
                {
                    var serverList = GetConnectedReadersList();
                    if (serverList.Count > 0)
                    {
                        foreach (string s in serverList)
                        {
                            string ip = readerIps[s];

                            string localStart = @"C:\Shared Files";
                            string remoteStart = @"\\" + ip + @"\Shared Files";
                            string pathFromHere = saveDirectory.Replace(localStart, remoteStart);

                            try
                            {
                                Directory.CreateDirectory(pathFromHere);
                            }
                            catch (Exception e)
                            {
                                MessageBox.Show($"LMSF Scheduler failed to create the directory, {saveDirectory}, on the remote {s} computer (IP adress: {ip}). Manually create that directory and click 'OK' to continue");
                            }
                        }
                    }
                }
                
            }

            return new string[] { expIdStr, metaDataFilePath, saveDirectory };
        }

        private void RunGetExpId(int num, string[] args)
        {
            //The first argument is the default experiment ID
            string expIdStr = args[1];
            //The second (optional) argument is the default data directory
            string dataDirStr = "";
            if (args.Length>2)
            {
                dataDirStr = args[2];
            }

            string[] argsBack = new string[] { "", "", "" };
            //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
            this.Dispatcher.Invoke(() => {
                //argsBack = GetExpId(dataDirStr, expIdStr);
                if (metaDictionary.ContainsKey("projectId"))
                {
                    argsBack = GetExpId(dataDirStr, expIdStr, metaDictionary["projectId"]);
                }
                else
                {
                    argsBack = GetExpId(dataDirStr, expIdStr);
                }
            });
            
            //Then save to XML document if...
            if (isCollectingXml && !AbortCalled)
            {
                UpdateXmlExpId(expIdStr);
            }

            //And add the experiment ID to the metaDictionary
            metaDictionary["experimentId"] = argsBack[0];
            metaDictionary["metaDataFilePath"] = argsBack[1];
            metaDictionary["dataDirectory"] = argsBack[2];

        }

        private void RunSet(int num, string[] args)
        {
            string keyStr = args[1];
            string valueStr = args[2];

            metaDictionary[keyStr] = valueStr;
        }

        private void RunGen5(int num, string[] args)
        {
            //First two arguments are reader name and reader command
            string readerName = args[1];
            string command = args[2];
            //arguments 3, 4 and 5 are the protocol path, experiment Id, and save folder path
            string protocolPath="";
            string expIdStr="";
            string saveFolderPath="";
            string msg;
            if (command != "RunExp")
            {
                msg = command;
            }
            else
            {
                protocolPath = args[3];
                expIdStr = args[4];
                saveFolderPath = args[5];
                msg = $"{command}/{protocolPath}/{expIdStr}/{saveFolderPath}";
            }

            string replyStatus = SendTcpMessage(readerName, msg);
            //program got stuck here, so I edited the Server code so this loop is not needed
            //while (replyStatus == "Idle")
            //{
            //    replyStatus = SendTcpMessage(readerName, "StatusCheck");
            //    Thread.Sleep(100);
            //}
            AddOutputText($"... {readerName} status: {replyStatus}.\n");

            //Send info to metadata if collecting
            if (command == "RunExp" && isCollectingXml && !AbortCalled)
            {
                //Add <Gen5Experiment> node to metadata
                XmlNode gen5Node = xmlDoc.CreateElement("Gen5Experiment");
                //add the Gen5Experiment node to the step node
                protocolNode.AppendChild(gen5Node);

                //Protocol file
                XmlNode protocolFileNode = xmlDoc.CreateElement("protocolFile");
                protocolFileNode.InnerText = protocolPath;
                gen5Node.AppendChild(protocolFileNode);

                //Data file
                XmlNode dataFileNode = xmlDoc.CreateElement("dataFile");
                dataFileNode.InnerText = LMSF_Gen5_Reader.Gen5Reader.GetExperimentFilePath(saveFolderPath, expIdStr, readerName);
                gen5Node.AppendChild(dataFileNode);

                //Export file
                XmlNode exportFileNode = xmlDoc.CreateElement("exportFile");
                exportFileNode.InnerText = LMSF_Gen5_Reader.Gen5Reader.GetExperimentFilePath(saveFolderPath, expIdStr, readerName, true);
                gen5Node.AppendChild(exportFileNode);

                //Date and time
                AddDateTimeNodes(DateTime.Now, gen5Node, "experiment started");

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }
        }

        private void WaitForRemoteProcess(string remoteName, bool writeEndTime = true, int pingInterval = 1000)
        {
            WaitingForStepCompletion = true;

            bool isReader = (remoteName.Contains("Epoch") || remoteName.Contains("Neo"));
            bool isStar = remoteName.Contains("STAR");
            string outText;

            if (isReader)
            {
                outText = "... waiting for Gen5 to finish.\n";
            }
            else {
                if (isStar)
                {
                    outText = "... waiting for remote Hamilton to finish.\n";
                }
                else
                {
                    outText = "... waiting for unknown remote proecess.\n";
                }
            }
            AddOutputText(outText);

            BackgroundWorker remoteMonitorWorker = new BackgroundWorker();
            remoteMonitorWorker.WorkerReportsProgress = false;
            remoteMonitorWorker.DoWork += RemoteProcessMonitor_DoWork;
            remoteMonitorWorker.RunWorkerCompleted += RemoteProcessMonitor_RunWorkerCompleted;

            List<object> arguments = new List<object>();
            arguments.Add(remoteName);
            arguments.Add(pingInterval);
            remoteMonitorWorker.RunWorkerAsync(arguments);

            while (WaitingForStepCompletion)
            {
                System.Threading.Thread.Sleep(100);
            }

            //Send info to metadata if collecting
            if (writeEndTime && isCollectingXml && !AbortCalled)
            {
                DateTime dt = DateTime.Now;

                if (isReader)
                {
                    XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::Gen5Experiment/dateTime");
                    XmlNode dateNode = dateNodeList.Item(dateNodeList.Count - 1);
                    //If for some reason the dateNode is null (no Gen5Experiment/dateTime Node previously added)
                    //  then don't try to append a child node to it, since that will throw an error
                    if (dateNode != null)
                    {
                        XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                        timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                        XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                        statusFiniAtt.Value = "experiment finished";
                        timeFiniNode.Attributes.Append(statusFiniAtt);
                        dateNode.AppendChild(timeFiniNode);
                    }
                }
                if (isStar)
                {
                    XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::hamiltonMethod/dateTime");
                    XmlNode dateNode = dateNodeList.Item(dateNodeList.Count - 1);
                    //If for some reason the dateNode is null (no hamiltonMethod/dateTime Node previously added)
                    //  then don't try to append a child node to it, since that will throw an error
                    if (dateNode != null)
                    {
                        XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                        timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                        XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                        statusFiniAtt.Value = "method finished";
                        timeFiniNode.Attributes.Append(statusFiniAtt);
                        dateNode.AppendChild(timeFiniNode);
                    }

                }

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }

        }

        void RemoteProcessMonitor_DoWork(object sender, DoWorkEventArgs e)
        {
            List<object> argsList = e.Argument as List<object>;
            string remoteServer = (string)argsList[0];
            int pingInterval = 1000;
            if (argsList.Count>1)
            {
                try
                {
                    pingInterval = (int)argsList[1];
                }
                catch
                {
                    //don't need to do anything here - just use the default value, 1000
                }
            }

            string remoteStatus = GetRemoteServerStatus(remoteServer);
            while (remoteStatus == $"{SharedParameters.ServerStatusStates.Busy}")
            {
                Thread.Sleep(pingInterval);
                remoteStatus = GetRemoteServerStatus(remoteServer);
            }

            //Check for errors and respond accordingly
            if (remoteStatus.StartsWith("Error")) {
                AddOutputText($"Remote Error on {remoteServer}.");
                this.Dispatcher.Invoke(() => {
                    IsPaused = true;
                    MessageBox.Show($"Error during remote method on {remoteServer}! Check error messages, correct errors, and continue; or abort experiment", "Error!");
                });
            }
        }

        void RemoteProcessMonitor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            WaitingForStepCompletion = false;
        }

        private void RunOverlord(int num, string[] args)
        {
            //args[0] is "Overlord"
            //args[1] is file path
            string file = args[1];
            //second argument (if any) is the variables to pass

            WaitingForStepCompletion = true;

            if ( !(ovProcess is null) )
            {
                if (!ovProcess.HasExited)
                {
                    AddOutputText("... waiting for last Overlord Process to exit.\n");
                    while (!ovProcess.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            //This part starts the Overlord process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            //startInfo.FileName = @"C:\Users\djross\source\repos\NIST LMSF\Overlord Simulator\bin\Release\Overlord.Main.exe";
            startInfo.FileName = @"C:\Program Files (x86)\PAA\Overlord3\Overlord.Main.exe";

            if (args.Length > 2)
            {
                //if there are variables to pass
                startInfo.Arguments = "\"" + file + "\"" + " -r -c -v " + args[2];
            }
            else
            {
                //if no variables to pass
                startInfo.Arguments = "\"" + file + "\"" + " -r -c";
            }
            
            ovProcess = Process.Start(startInfo);

            //Send info to metadata if collecting
            if (isCollectingXml && !AbortCalled)
            {
                //Add <overlordProcudure> node to metadata
                XmlNode ovpNode = xmlDoc.CreateElement("overlordProcedure");
                //add the overlordProcudure node to the step node
                protocolNode.AppendChild(ovpNode);

                //Procedure file
                XmlNode ovpFileNode = xmlDoc.CreateElement("procedureFile");
                ovpFileNode.InnerText = file;
                ovpNode.AppendChild(ovpFileNode);

                //Date and time
                AddDateTimeNodes(DateTime.Now, ovpNode, "procedure started");

                if (args.Length > 2)
                {
                    //Add variables/parameters that got set to XML document
                    startInfo.Arguments = "\"" + file + "\"" + " -r -c -v " + args[2];
                    XmlNode paramsNode = xmlDoc.CreateElement("parameters");
                    ovpNode.AppendChild(paramsNode);

                    string varString = args[2];
                    string[] varArgs = varString.Split(new[] { " " }, StringSplitOptions.None);
                    for (int i = 0; i < varArgs.Length; i += 2)
                    {
                        string param = varArgs[i].Trim(new char[] { '[', ']' } );
                        XmlNode pNode = xmlDoc.CreateElement(param);
                        pNode.InnerText = varArgs[i+1];
                        paramsNode.AppendChild(pNode);
                    }
                }

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }
        }

        private void ExportDictionary(string filePath)
        {
            string dir = System.IO.Path.GetDirectoryName(filePath);
            string file = System.IO.Path.GetFileName(filePath);
            ExportDictionary(dir, file);
        }

        private void ExportDictionary(string dir, string fileName)
        {
            //First, create directory if it does not exits
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            //Then, delete the schema.ini file (generated by Hamilton) if it is there
            string iniFile = System.IO.Path.Combine(dir, "schema.ini");
            if (File.Exists(iniFile))
            {
                File.Delete(iniFile);
            }

            string outPath = System.IO.Path.Combine(dir, fileName);

            //string header = String.Join(",", metaDictionary.Keys);
            //string values = String.Join(",", metaDictionary.Values);

            try
            {
                using (StreamWriter outputFile = new StreamWriter(outPath))
                {
                    //outputFile.WriteLine(header);
                    //outputFile.WriteLine(values);
                    foreach (string key in metaDictionary.Keys)
                    {
                        outputFile.WriteLine($"{key},{metaDictionary[key]}");
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show(e.Message);
                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => { AbortCalled = true; });
            }

        }

        private void ImportDictionary(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            string[] split;
            foreach (string entry in lines)
            {
                split = entry.Split(new[] { "," }, StringSplitOptions.RemoveEmptyEntries);
                if (split.Length == 2)
                {
                    metaDictionary[split[0]] = split[1];
                }
            }
        }

        private void ExportCarouselInventory(string dir, string fileName)
        {
            //First, overwite any item in the carouselInventory for which there is a matching key in the metaDictionary
            foreach (string key in carouselInventory.Keys)
            {
                if (metaDictionary.ContainsKey(key))
                {
                    carouselInventory[key] = metaDictionary[key];
                }
            }

            //First, create directory if it does not exits
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            string outPath = System.IO.Path.Combine(dir, fileName);

            //string header = String.Join(",", metaDictionary.Keys);
            //string values = String.Join(",", metaDictionary.Values);

            try
            {
                using (StreamWriter outputFile = new StreamWriter(outPath))
                {
                    //outputFile.WriteLine(header);
                    //outputFile.WriteLine(values);
                    foreach (string key in carouselInventory.Keys)
                    {
                        outputFile.WriteLine($"{key},{carouselInventory[key]}");
                    }
                }
            }
            catch (UnauthorizedAccessException e)
            {
                MessageBox.Show(e.Message);
                //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
                this.Dispatcher.Invoke(() => { AbortCalled = true; });
            }

        }

        private void ImportCarouselInventory(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);

            int splitIndex;
            string keyStr;
            string valueStr;

            //string[] split;
            foreach (string entry in lines)
            {
                splitIndex = entry.IndexOf(',');
                if (splitIndex > 0)
                {
                    keyStr = entry.Substring(0, splitIndex);
                    valueStr = entry.Substring(splitIndex + 1);
                    carouselInventory[keyStr] = valueStr;
                }
            }
        }

        private void RunRemoteHamilton(int num, string[] args)
        {
            //First two arguments are Hamilton name and command
            string name = args[1];
            string command = args[2];
            //3rd argument is the method path
            string methodPath = "";

            string msg;
            if (command != "RunMethod")
            {
                msg = command;
                methodPath = @"C:\Program Files (x86)\HAMILTON\Methods\Common\Tip Handling\With 96-Head\Check Tip Counters.hsl";
            }
            else
            {
                methodPath = args[3];
                msg = $"{command}/{methodPath}";
            }
            
            //Export the metaDicitonary to the Hamilton/LMSF_FrontEnd dirctory on the remote cmoputer
            string remoteFrontEndpath = @"\\" + readerIps[name] + @"\LMSF_FrontEnd\";
            int numTries = 0;
            bool exportSucess = false;
            while (!exportSucess && numTries<10)
            {
                numTries++;
                try
                {
                    ExportDictionary(remoteFrontEndpath, "parameters.csv");
                    exportSucess = true;
                }
                catch
                {
                    Thread.Sleep(3000);
                }
            }

            if (exportSucess)
            {
                //send message to remote Hamilton
                string replyStatus = SendTcpMessage(name, msg);
                //The program might get stuck in this while loop, so I re-wrote the server code so that this loop is not needed
                //while (replyStatus == "Idle")
                //{
                //    replyStatus = SendTcpMessage(name, "StatusCheck");
                //    Thread.Sleep(200);
                //}
                AddOutputText($"... {name} status: {replyStatus}.\n");
            }
            else
            {
                AddOutputText($"Attempt to write dictionary to Hamilton/LMSF_FrontEnd dirctory failed 10 times. Aborting automation protocol.\n");
                AbortCalled = true;
            }
            
            
            //Send info to metadata if collecting
            if (isCollectingXml && !AbortCalled)
            {
                //Add <hamiltonMethod> node to metadata
                XmlNode hamNode = xmlDoc.CreateElement("hamiltonMethod");
                //add the hamiltonMethod node to the protocol node
                protocolNode.AppendChild(hamNode);

                //Method file
                XmlNode hamFileNode = xmlDoc.CreateElement("methodFile");
                hamFileNode.InnerText = methodPath;
                hamNode.AppendChild(hamFileNode);

                //Date and time
                AddDateTimeNodes(DateTime.Now, hamNode, "method started");

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }

            //If command = "ReadCounters" or methodPath ends with "Edit Tip Counters.hsl", wait until it's done, then read tip counter status into metaDictionary
            if (command == "ReadCounters" || methodPath.EndsWith("Edit Tip Counters.hsl")) {
                //Wait for ReadCounters method to finish
                WaitingForStepCompletion = true;
                BackgroundWorker remoteMonitorWorker = new BackgroundWorker();
                remoteMonitorWorker.WorkerReportsProgress = false;
                remoteMonitorWorker.DoWork += RemoteProcessMonitor_DoWork;
                remoteMonitorWorker.RunWorkerCompleted += RemoteProcessMonitor_RunWorkerCompleted;

                List<object> arguments = new List<object>();
                arguments.Add(name);
                remoteMonitorWorker.RunWorkerAsync(arguments);

                while (WaitingForStepCompletion)
                {
                    System.Threading.Thread.Sleep(100);
                }

                //Read tip counter status
                string tipStatusPath = @"\\" + readerIps[name] + @"\Shared Files\LMSF Scheduler\Hamilton\tip_status.txt";
                numTries = 0;
                bool tipReadSuccess = false;
                while (!tipReadSuccess && numTries < 10)
                {
                    numTries++;
                    try
                    {
                        ImportDictionary(tipStatusPath);
                        tipReadSuccess = true;
                    }
                    catch
                    {
                        Thread.Sleep(3000);
                    }
                }
                if (!tipReadSuccess)
                {
                    AddOutputText($"Attempt to read tip counter status on remote Hamilton computer failed 10 times. Aborting automation protocol.\n");
                    AbortCalled = true;
                }
            }
        }

        private void RunHamilton(int num, string[] args)
        {
            //args[0] is "Hamilton"
            //args[1] is file path
            string file = args[1];

            WaitingForStepCompletion = true;

            if ( !(hamProcess is null) )
            {
                if (!hamProcess.HasExited)
                {
                    AddOutputText("... waiting for last Hamilton Process to exit.\n");
                    while (!hamProcess.HasExited)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            //Export the metaDicitonary to the Hamilton/LMSF_FrontEnd dirctory
            ExportDictionary(SharedParameters.HamiltonFolderPath, "parameters.csv");

            //This part starts the Hamiltin HxRun.exe process
            ProcessStartInfo startInfo = new ProcessStartInfo();
            startInfo.FileName = @"C:\Program Files (x86)\HAMILTON\Bin\HxRun.exe";

            //the -t modifier causes HxRun.exe to run the method and then terminate itself after
            startInfo.Arguments = "\"" + file + "\"" + " -t";

            hamProcess = Process.Start(startInfo);

            //Send info to metadata if collecting
            if (isCollectingXml && !AbortCalled)
            {
                //Add <hamiltonMethod> node to metadata
                XmlNode hamNode = xmlDoc.CreateElement("hamiltonMethod");
                //add the hamiltonMethod node to the protocol node
                protocolNode.AppendChild(hamNode);

                //Method file
                XmlNode hamFileNode = xmlDoc.CreateElement("methodFile");
                hamFileNode.InnerText = file;
                hamNode.AppendChild(hamFileNode);

                //Date and time
                AddDateTimeNodes(DateTime.Now, hamNode, "method started");

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }
        }

        private void AddDateTimeNodes(DateTime dt, XmlNode parentNode, string statusStr)
        {
            //DateTime dt = DateTime.Now;
            XmlNode dateTimeNode = xmlDoc.CreateElement("dateTime");
            parentNode.AppendChild(dateTimeNode);

            XmlNode yearNode = xmlDoc.CreateElement("year");
            yearNode.InnerText = dt.ToString("yyyy");
            dateTimeNode.AppendChild(yearNode);

            XmlNode monthNode = xmlDoc.CreateElement("month");
            monthNode.InnerText = dt.ToString("MM");
            dateTimeNode.AppendChild(monthNode);

            XmlNode dayNode = xmlDoc.CreateElement("day");
            dayNode.InnerText = dt.ToString("dd");
            dateTimeNode.AppendChild(dayNode);

            XmlNode timeNode = xmlDoc.CreateElement("time");
            timeNode.InnerText = dt.ToString("HH:mm:ss");
            XmlAttribute statusAtt = xmlDoc.CreateAttribute("status");
            statusAtt.Value = statusStr;
            timeNode.Attributes.Append(statusAtt);
            dateTimeNode.AppendChild(timeNode);
        }

        private void WaitForOverlord(int num)
        {
            WaitingForStepCompletion = true;

            AddOutputText("... waiting for Overlord to finish and exit.\n");

            BackgroundWorker ovMonitorWorker = new BackgroundWorker();
            ovMonitorWorker.WorkerReportsProgress = false;
            ovMonitorWorker.DoWork += OutsideProcessMonitor_DoWork;
            ovMonitorWorker.RunWorkerCompleted += OutsideProcessMonitor_RunWorkerCompleted;

            List<object> arguments = new List<object>();
            arguments.Add(num);
            arguments.Add(ovProcess);
            ovMonitorWorker.RunWorkerAsync(arguments);

            while (WaitingForStepCompletion)
            {
                System.Threading.Thread.Sleep(100);
            }

            //Send info to metadata if collecting
            if (isCollectingXml && !AbortCalled)
            {
                DateTime dt = DateTime.Now;

                //XmlNode testNode = xmlDoc.SelectSingleNode("descendant::overlordProcedure/dateTime");
                XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::overlordProcedure/dateTime");
                XmlNode dateNode = dateNodeList.Item(dateNodeList.Count-1);
                //If for some reason the dateNode is null (no overlordProcedure/dateTime Node previously added)
                //  then don't try to append a child node to it, since that will throw an error
                if (dateNode != null)
                {
                    XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                    timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                    XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                    statusFiniAtt.Value = "procedure finished";
                    timeFiniNode.Attributes.Append(statusFiniAtt);
                    dateNode.AppendChild(timeFiniNode);
                }

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
            }

        }
        
        private void WaitForHamilton(int num)
        {
            WaitingForStepCompletion = true;

            AddOutputText("... waiting for Hamilton Runtime Engine to finish and exit.\n");

            BackgroundWorker hamMonitorWorker = new BackgroundWorker();
            hamMonitorWorker.WorkerReportsProgress = false;
            hamMonitorWorker.DoWork += OutsideProcessMonitor_DoWork;
            hamMonitorWorker.RunWorkerCompleted += OutsideProcessMonitor_RunWorkerCompleted;

            List<object> arguments = new List<object>();
            arguments.Add(num);
            arguments.Add(hamProcess);
            hamMonitorWorker.RunWorkerAsync(arguments);

            while (WaitingForStepCompletion)
            {
                System.Threading.Thread.Sleep(100);
            }

            //Send finish info to metadata if collecting
            if (isCollectingXml && !AbortCalled)
            {
                DateTime dt = DateTime.Now;

                XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::hamiltonMethod/dateTime");
                XmlNode dateNode = dateNodeList.Item(dateNodeList.Count-1);
                //If for some reason the dateNode is null (no hamiltonMethod/dateTime Node previously added)
                //  then don't try to append a child node to it, since that will throw an error
                if (dateNode != null)
                {
                    XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                    timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                    XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                    statusFiniAtt.Value = "method finished";
                    timeFiniNode.Attributes.Append(statusFiniAtt);
                    dateNode.AppendChild(timeFiniNode);
                }

                //Save the XML document
                try
                {
                    xmlDoc.Save(metaDataFilePath);
                }
                catch (Exception e)
                {
                    MessageBox.Show($"Error saving XML document: {e}");
                }
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
            WaitingForStepCompletion = false;
        }

        private void RunTimer(int num, string[] args)
        {
            WaitingForStepCompletion = true;

            int waitTime;
            DateTime waitUntil;

            if (int.TryParse(args[1], out waitTime))
            {
                //argument is an integer, so wait that long, in seconds
            }
            else
            {
                //argument is a DateTime string, so wait until the specified time
                if (DateTime.TryParse(args[1], out waitUntil))
                {
                    //argument is a DateTime string, so wait until the specified time
                    waitTime = (int)Math.Round((waitUntil - DateTime.Now).TotalSeconds);
                    if (waitTime<0)
                    {
                        waitTime = 0;
                    }
                }

                //TODO: add that code here
            }

            if (!(stepTimerDialog is null))
            {
                if (!stepTimerDialog.IsClosed)
                {
                    AddOutputText("... waiting for last Timer to finish.\n");
                    while (!stepTimerDialog.IsClosed)
                    {
                        Thread.Sleep(100);
                    }
                }
            }

            //This part starts the Timer, which needs to be handled via a Dispatcher 
            //    becasue the runStepsThread cannot directly handle GUI components
            this.Dispatcher.Invoke(() => {
                stepTimerDialog = new TimerDialog("LMSF Timer", waitTime);
                stepTimerDialog.Owner = this;
                stepTimerDialog.Show();
            });

        }

        private void WaitForTimer(int num)
        {
            WaitingForStepCompletion = true;

            AddOutputText("... waiting for Timer to finish.\n");

            BackgroundWorker timerMonitorWorker = new BackgroundWorker();
            timerMonitorWorker.WorkerReportsProgress = false;
            timerMonitorWorker.DoWork += TimerMonitor_DoWork;
            timerMonitorWorker.RunWorkerCompleted += TimerMonitor_RunWorkerCompleted;

            timerMonitorWorker.RunWorkerAsync();

            while (WaitingForStepCompletion)
            {
                System.Threading.Thread.Sleep(100);
            }

        }

        void TimerMonitor_DoWork(object sender, DoWorkEventArgs e)
        {
            while (!stepTimerDialog.IsClosed)
            {
                Thread.Sleep(100);
            }
        }

        void TimerMonitor_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            WaitingForStepCompletion = false;
        }

        private void Window_Closing(object sender, CancelEventArgs e)
        {
            //Make sure that any edits in the inputTextBox are updated to the InputTest property
            inputTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            if (InputChanged && !SaveFirstQuery())
            {
                //Input trext has changed, and the user selected 'Cancel'
                //    so do not close
                e.Cancel = true;
            }
            else
            {
                // go ahead and close-
            }
            inputTextBox.Focus();
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            this.Close();
            inputTextBox.Focus();
        }

        private string NewLogFileName()
        {
            return $"{DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")}-lmsf.trc";
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

        private void SelectComboBox_DropDownClosed(object sender, EventArgs e)
        {
            //Don't add text if SelectedCommand is null
            if (SelectedCommand != null)
            {
                InsertInputText($"{SelectedCommand}(");

                //if it is a NewXML or AppendXML command, also add in the SaveXML command automatically
                if (SelectedCommand == "NewXML" || SelectedCommand == "AppendXML")
                {
                    int caretPos = inputTextBox.SelectionStart;

                    InsertInputText("<protocol type>)\n\nSaveXML()");

                    //move caret to middle line between NewXML and SaveXML
                    inputTextBox.SelectionStart = caretPos + 17;
                    inputTextBox.SelectionLength = 0;
                }
                else
                {
                    InsertInputText(")");
                    //otherwise, move caret to between parentheses
                    int caretPos = inputTextBox.SelectionStart;
                    inputTextBox.SelectionStart = caretPos - 1;
                    inputTextBox.SelectionLength = 0;
                }
            }

        }

        private void RemoteComboBox_DropDownClosed(object sender, EventArgs e)
        {
            if (SelectedReaderBlock != null)
            {
                string reader = SelectedReaderBlock.Text;
                string title = $"Remote Connection to {reader}";

                bool readerConnected = GetConnectedReadersList().Contains(reader);

                string messageText = "";
                if (readerConnected)
                {
                    messageText = $"{reader} is currently connected. Do you want to retain that remote connection or disconnect?\n";
                    messageText += "Select 'Yes' to retain the connection, or 'No' to disconnect.";
                }
                else
                {
                    messageText = $"Do you want to make a remote connection to {reader}?\n";
                    messageText += "Select 'Yes' to establish a connection, or 'No' to cancel.";
                }

                YesNoDialog.Response userResp = SharedParameters.ShowYesNoDialog(messageText, title);

                ConfigureRemoteServer(reader, userResp);

                if (userResp == YesNoDialog.Response.Yes)
                {
                    testTextBox.Text = reader;
                }
            }
        }

        private void ConfigureRemoteServer(string reader, YesNoDialog.Response connect)
        {
            string remoteExe = "Remote Program";
            if (reader.Contains("Epoch") || reader.Contains("Neo"))
            {
                remoteExe = "LMSF_Gen5";
            }
            else
            {
                if (reader.Contains("STAR"))
                {
                    remoteExe = "Hamilton Remote";
                }
            }

            if (connect == YesNoDialog.Response.Yes)
            {
                SelectedReaderBlock.Background = Brushes.Transparent;
                if (readerClients[reader] == null)
                {
                    SimpleTcpClient client = new SimpleTcpClient();
                    client.Delimiter = 0x13;
                    try
                    {
                        client.Connect(readerIps[reader], 42222);
                        SelectedReaderBlock.Background = Brushes.LimeGreen;
                        readerClients[reader] = client;
                    }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        MessageBox.Show($"Exception: {e}. {reader} is not accepting the connection. Make sure {remoteExe} is running and in \"Remote\" mode on the {reader} computer. Then try again.");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Exception: {e}");
                    }
                }
                else
                {
                    SimpleTcpClient client = readerClients[reader];
                    client.Delimiter = 0x13;
                    try
                    {
                        client.Connect(readerIps[reader], 42222);
                        SelectedReaderBlock.Background = Brushes.LimeGreen;
                    }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        MessageBox.Show($"{reader} is not accepting the connection. Make sure {remoteExe} is running and in \"Remote\" mode on the {reader} computer. Then try again.");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Exception: {e}");
                    }
                }
            }
            else
            {
                SelectedReaderBlock.Background = Brushes.Transparent;
                if (readerClients[reader] == null)
                {
                    //do nothing
                }
                else
                {
                    SimpleTcpClient client = readerClients[reader];
                    client.Disconnect(); // this sets client equal to null after closing the underlying TcpClient
                }
            }
        }

        private string GetRemoteServerStatus(string server)
        {
            return SendTcpMessage(server, "StatusCheck");
        }

        //This method sends message and handles wrapping the message with identifier and hash, and re-sending if message is not received
        private string SendTcpMessage(string reader, string msg)
        {
            string remoteExe = "Remote Program";
            if (reader.Contains("Epoch") || reader.Contains("Neo"))
            {
                remoteExe = "LMSF_Gen5";
            }
            else
            {
                if (reader.Contains("STAR"))
                {
                    remoteExe = "Hamilton Remote";
                }
            }

            string replyStatus = "";

            SimpleTcpClient client = readerClients[reader];
            while ((client == null) || !client.IsConnected())
            {
                string warningText = $"{reader} is not connected. Make sure {remoteExe} is running and in \"Remote\" mode on the {reader} computer.";
                warningText += $"\nThen click 'OK' to try again, or 'Cancel' to abort.";

                MessageBoxResult result = MessageBox.Show(warningText, $"{reader} Not Connected", MessageBoxButton.OKCancel);
                if (result == MessageBoxResult.Cancel)
                {
                    AbortCalled = true;
                    return replyStatus;
                }
            }

            //MessageBox.Show($"client connected: {client.IsConnected()}");

            string wrappedMessage = Message.WrapTcpMessage(msg);
            //For testing:
            //wrappedMessage += "1";
            //Note WriteLineAndGetReply() returns null if server takes longer than timeout to send reply
            Message replyMsg = null;
            AddOutputText($"sending message to {reader}, {wrappedMessage} ... ");
            //TODO: add maxRetries
            int numTries = 0;
            while (replyMsg == null && !AbortCalled)
            {
                if ((numTries % 5) == 4)
                {
                    try
                    {
                        client.Connect(readerIps[reader], 42222);
                    }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        MessageBox.Show($"{reader} is not accepting the connection. Make sure {remoteExe} is running and in \"Remote\" mode on the {reader} computer. Then click 'OK'.");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Exception: {e}");
                    }
                }

                numTries++;
                AddOutputText($"try {numTries}, ");
                //replyMsg = client.WriteLineAndGetReply(wrappedMessage, TimeSpan.FromSeconds(3));
                try
                {
                    replyMsg = client.WriteLineAndGetReply(wrappedMessage, TimeSpan.FromSeconds(3));
                }
                catch (Exception e)
                {
                    AddOutputText($"\n*****************************\nException caught in SendTcpMessage(), client.WriteLineAndGetReply(): {e}\n*****************************\n");
                    replyMsg = null;
                    Thread.Sleep(250);
                }
                if (replyMsg != null)
                {
                    string[] messageParts = new string[] { "msg", "msg", "msg" };
                    string[] replyParts = new string[] { "rpl", "fail", "rpl" };
                    try
                    {
                        messageParts = Message.UnwrapTcpMessage(wrappedMessage);
                        AddOutputText($"messageParts: {messageParts[0]},{messageParts[1]},{messageParts[2]}, ", false);
                    }
                    catch (ArgumentException e)
                    {
                        AddOutputText($"\n*****************************\nException caught in SendTcpMessage(), UnwrapTcpMessage(wrappedMessage): {e}\n*****************************\n");
                        Thread.Sleep(250);
                    }
                    try
                    {
                        replyParts = Message.UnwrapTcpMessage(replyMsg.MessageString);
                        AddOutputText($"replyParts: {replyParts[0]},{replyParts[1]},{replyParts[2]}, ", false);
                    }
                    catch (ArgumentException e)
                    {
                        AddOutputText($"\n*****************************\nException caught in SendTcpMessage(), UnwrapTcpMessage(replyMsg.MessageString): {e}\n*****************************\n");
                        Thread.Sleep(250);
                    }
                    
                    replyStatus = replyParts[1];
                    bool msgRecieved = false;
                    if (replyStatus != "fail")
                    {
                        msgRecieved = (messageParts[0] == replyParts[0]) && (messageParts[2] == replyParts[2]);
                    }

                    if (!msgRecieved)
                    {
                        //if the message did not get received properly, set replyMSg = null to try again.
                        replyMsg = null;

                        SharedParameters.SleepWithUiUpdates(1000);
                        //MessageBox.Show("message not received properly, going to send again-");
                    }
                    else
                    {
                        AddOutputText($"reply received... {replyStatus} ");
                    }
                }
            }
            AddOutputText($"... done.\n");

            return replyStatus;
        }

        private void InsertStepButton_Click(object sender, RoutedEventArgs e)
        {
            //Make sure that any edits in the insertStepTextBox are updated to the InsertStepText property
            insertStepTextBox.GetBindingExpression(TextBox.TextProperty).UpdateSource();

            //Then make sure it is not an empty or null string
            if (!string.IsNullOrWhiteSpace(InsertStepText))
            {
                //Validate, and execute step that is manually typed into insertStepTextBox (property: InsertStepText); during paused run.
                List<int> valCheck = new List<int>();
                string parseOut = ParseStep(1, InsertStepText, true, ref valCheck);
                if (valCheck.Count == 0)
                {
                    //Insert manually inserted step to inputSteps
                    if (inputSteps.Count <= stepNum + 1)
                    {
                        inputSteps.Add(InsertStepText);
                    }
                    else
                    {
                        inputSteps.Insert(stepNum + 1, InsertStepText);
                    }

                    //Take out Dialog if the step validates - just run it
                    //dialog to let user know it got inserted, and allow for protocol Cancel
                    //string msg = $"Manually entered step validated and inserted: {InsertStepText}\n";
                    //msg += "Click 'OK' to execute step, or 'Abort' to abort experiment.";
                    bool? oKToGo = true;// SharedParameters.ShowPrompt(msg, "Step Inserted");
                    if (!(oKToGo == true))
                    {
                        AbortCalled = true;
                    }
                    else
                    {
                        AddOutputText($"User inserted step during run: {InsertStepText}\n");
                        IsOneStep = true;
                    }
                }
                else
                {
                    bool? oKToGo = SharedParameters.ShowPrompt($"Manually entered step failed to validate: {parseOut}.\nEdit step and retry.", "Step Validation Failed");
                    if (!(oKToGo == true))
                    {
                        AbortCalled = true;
                    }
                }
            }
            
        }
    }

}
