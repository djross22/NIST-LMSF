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
        private string experimentFileName = "";

        //For parsing and running steps
        private string[] inputSteps;
        private bool[] stepsRunning;
        private bool waitingForStepCompletion;
        private int stepNum;
        private int totalSteps;
        private bool isRunning = false;
        private bool abortCalled = false;
        private bool isPaused = true;
        private bool isOneStep = false;
        private bool isValidating = false;
        private bool isValUserInput;
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

        //variables for TCP communication
        public List<string> ReaderList { get; set; }
        public ObservableCollection<TextBlock> ReaderBlockList { get; set; }
        private TextBlock selectedReaderBlock;
        Dictionary<string, string> readerIps = new Dictionary<string, string>();
        Dictionary<string, SimpleTcpClient> readerClients = new Dictionary<string, SimpleTcpClient>();

        #region Properties Getters and Setters
        public bool IsValUserInput
        {
            get { return this.isValUserInput; }
            set
            {
                this.isValUserInput = value;
                OnPropertyChanged("IsValUserInput");
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
            get { return this.abortCalled; }
            set
            {
                this.abortCalled = value;
                UpdateEnabledState();
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
                //OnPropertyChanged("AbortCalled");
            }
        }

        private bool WaitingForStepCompletion
        {
            get
            {
                return this.waitingForStepCompletion;
            }
            set
            {
                this.waitingForStepCompletion = value;
                //OnPropertyChanged("WaitingForStepCompletion");
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
            get { return this.isPaused; }
            set
            {
                this.isPaused = value;
                UpdateEnabledState();
                OnPropertyChanged("IsPaused");
            }
        }

        public bool IsRunning
        {
            get { return this.isRunning; }
            set
            {
                this.isRunning = value;
                UpdateEnabledState();
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

                inputTextBox.IsEnabled = false;
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
                insertFileButton.IsEnabled = true;
                mainMenu.IsEnabled = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            runStepsThread = new Thread(new ThreadStart(StepsThreadProc));

            DataContext = this;

            CommandList = new ObservableCollection<string>() { "Overlord", "Hamilton", "Timer", "WaitFor", "StartPrompt", "NewXML", "AppendXML", "AddXML", "UserPrompt", "GetUserYesNo", "Set", "Get", "GetExpID", "GetFile" }; //SharedParameters.UnitsList;

            ReaderList = new List<string>() { "Neo", "Epoch1", "Epoch2", "Epoch3", "Epoch4" };
            ReaderBlockList = new ObservableCollection<TextBlock>();
            foreach (string s in ReaderList)
            {
                TextBlock tb = new TextBlock();
                tb.Text = s;
                ReaderBlockList.Add(tb);

                readerClients[s] = null;
            }
            readerIps["Neo"] = "localhost";
            //Set IPs for Epoch readers

            metaDictionary = new Dictionary<string, string>();
            concDictionary = new Dictionary<string, Concentration>();
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        //temporary method for debugging/testing
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            var client = new SimpleTcpClient();
            client.Delimiter = 0x13;
            client.Connect("localhost", 42222);
            client.WriteLine("test sending line");
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

        //Breaks the InputText into steps/lines
        private bool InitSteps()
        {
            bool initOK = true;

            //Initialize metadata Dictionaries
            metaDictionary = new Dictionary<string, string>();
            concDictionary = new Dictionary<string, Concentration>();

            //by default, don't collect metadata
            isCollectingXml = false;

            inputSteps = InputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            //remove leading and trailing white space from each line
            inputSteps = inputSteps.Select(s => s.Trim()).ToArray();
            //then delete any lines that were just white space (are now empty)
            inputSteps = inputSteps.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            //then delete any lines that start with "//" - comment lines
            inputSteps = inputSteps.Where(s => !s.StartsWith("//")).ToArray();

            stepNum = 0;
            totalSteps = inputSteps.Length;

            //init running state for each step
            stepsRunning = Enumerable.Repeat(false, totalSteps).ToArray();

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

                //if the log file got created ok, set the temporary path for the metadata output file
                if (initOK)
                {
                    metaDataFilePath = SharedParameters.LogFileFolderPath + "temp.xml";
                }
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

                    Step();
                }
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
                OutputText += "Method Aborted.\n";
                //Add to log file
                if (!isValidating)
                {
                    File.AppendAllText(logFilePath, "Method Aborted.\n");
                }
                this.Dispatcher.Invoke(() => { IsRunning = false; });
            }
            else
            {
                if (stepNum < totalSteps)
                {
                    string oldText = OutputText;
                    string newText = ParseStep(stepNum, inputSteps[stepNum]);
                    OutputText = oldText + newText;
                    if (!isValidating)
                    {
                        File.AppendAllText(logFilePath, newText);
                    }

                    stepNum++;
                }
                else
                {
                    string doneText = $"{SharedParameters.GetDateTimeString()}; Done.\n";
                    OutputText += doneText;
                    if (!isValidating)
                    {
                        File.AppendAllText(logFilePath, doneText);
                    }

                    this.Dispatcher.Invoke(() => { IsRunning = false; });
                }

                this.Dispatcher.Invoke(() => { IsOneStep = false; });
            }

        }

        private string ParseStep(int num, string step)
        {
            //Note on step validation:
            //    valFailed is intialized to an empty list at the beginning of each run,
            //    if a step fails a validation check, the step number is added to the valFailed list

            string outString = $"{num}. ";
            outString += $"{SharedParameters.GetDateTimeString()}; ";
            string[] stepArgs = step.Split(new[] { "/" }, StringSplitOptions.RemoveEmptyEntries);

            stepArgs = stepArgs.Select(s => s.Trim()).ToArray();
            stepArgs = stepArgs.Where(x => !string.IsNullOrEmpty(x)).ToArray();

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
                    //    or signal valFailed if the key is not in the dictionary
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
                                outString += $"Key not in metaDictionary: {keyStr} in {arg}. ";
                                keysOk = false;
                                break;
                            }
                        }
                    }

                }

                stepArgs[j] = newArg;

            }

            if (!keysOk)
            {
                valFailed.Add(num);
                //exit the method early if there are still key syntax errors
                outString += "\n\n";
                return outString;
            }

            bool ifResult = true;
            if (stepArgs[0] == "If")
            {
                //this is an if statement, so the next argment needs to be the logical test
                if (stepArgs.Length > 2)
                {
                    if (stepArgs[1].Contains("==") || stepArgs[1].Contains("!="))
                    {
                        //string stringToSplit = stepArgs[1].Replace(" ", "");
                        string stringToSplit = stepArgs[1].Trim();
                        string[] logicStrings = stringToSplit.Split(new[] { "==", "!=" }, StringSplitOptions.RemoveEmptyEntries);
                        if (logicStrings.Length == 2)
                        {
                            string firstStr = logicStrings[0].Trim();
                            string secondStr = logicStrings[1].Trim();
                            outString += $"If: {stringToSplit}, ";
                            if (stringToSplit.Contains("=="))
                            {
                                ifResult = (firstStr == secondStr);
                            }
                            else
                            {
                                ifResult = (firstStr != secondStr);
                            }
                            outString += $"{ifResult}. ";
                            //always mark ifResult as true when validating, so that the remaining arguments always get validated
                            if (isValidating)
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
                            valFailed.Add(num);
                            //exit the method early if the 2nd argument of an If/ command is not a logical comparison
                            outString += "If/ commands need to have a valid logical test as the 2nd argument (e.g. {strain1}==MG1655, or {userChoice}!=No).\n\n";
                            return outString;
                        }
                    }
                    else
                    {
                        valFailed.Add(num);
                        //exit the method early if the 2nd argument of an If/ command is not a logical comparison
                        outString += "If/ commands need to have a logical test (with \"==\" or \"!=\") as the 2nd argument.\n\n";
                        return outString;
                    }
                }
                else
                {
                    valFailed.Add(num);
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
                        ParseOverlordStep();
                        break;
                    case "Hamilton":
                        ParseHamiltonStep();
                        break;
                    case "WaitFor":
                        ParseWaitForStep();
                        break;
                    case "Timer":
                        ParseTimerStep();
                        break;
                    case "NewXML":
                        ParseNewXml();
                        break;
                    case "AppendXML":
                        ParseAppendXml();
                        break;
                    case "AddXML":
                        ParseAddXml();
                        break;
                    case "SaveXML":
                        ParseSaveXml();
                        break;
                    case "UserPrompt":
                        ParseUserPrompt();
                        break;
                    case "GetUserYesNo":
                        ParseUserYesNo();
                        break;
                    case "Set":
                        ParseSet();
                        break;
                    case "Get":
                        ParseGet();
                        break;
                    case "GetExpID":
                        ParseGetExpId();
                        break;
                    case "GetFile":
                        ParseGetFile();
                        break;
                    case "StartPrompt":
                        ParseStartPrompt();
                        break;
                    default:
                        valFailed.Add(num);
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

            return outString;

            //Local functions to parse each type of step
            void ParseOverlordStep()
            {
                outString += "Running Overlord proccedure: ";
                bool isOvpFileName = false;
                bool varArgsOk = true;
                if (numArgs < 2)
                {
                    outString += "No procedure path given.";
                    valFailed.Add(num);
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
                        valFailed.Add(num);
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
                            valFailed.Add(num);
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

                        if (!isValidating)
                        {
                            //RunOverlord(num, stepArgs[1]);
                            RunOverlord(num, stepArgs);
                        }
                    }
                    else
                    {
                        outString += "Procedure file not found: ";
                        outString += stepArgs[1];
                        valFailed.Add(num);
                    }
                }
            }

            void ParseHamiltonStep()
            {
                outString += "Running Hamilton Venus Method: ";
                bool isHslFileName = false;

                //Requires one argument, the Method file path
                if (numArgs < 2)
                {
                    outString += "No method file path given.";
                    valFailed.Add(num);
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
                        valFailed.Add(num);
                    }
                }

                if (isHslFileName)
                {
                    bool hslExists = File.Exists(stepArgs[1]);
                    if (hslExists)
                    {
                        outString += stepArgs[1];

                        if (!isValidating)
                        {
                            RunHamilton(num, stepArgs);
                        }
                    }
                    else
                    {
                        outString += "Method file not found: ";
                        outString += stepArgs[1];
                        valFailed.Add(num);
                    }
                }
            }

            void ParseTimerStep()
            {
                outString += "Running Timer: ";
                int waitTime = 0;

                bool argsOk = false;
                if (numArgs < 2)
                {
                    outString += "No time given for the timer.";
                    valFailed.Add(num);
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
                                valFailed.Add(num);
                            }
                        }
                        else
                        {
                            outString += "Timer parameter is not an integer nor a parsable date-time string: ";
                            outString += stepArgs[1];
                            valFailed.Add(num);
                        }
                    }
                }

                if (argsOk)
                {
                    outString += stepArgs[1];
                    if (!isValidating)
                    {
                        RunTimer(num, stepArgs);
                    }
                }
            }

            void ParseWaitForStep()
            {
                outString += "WaitFor: ";
                if (numArgs < 2)
                {
                    outString += "Must specify the process to WaitFor (Overlord or Timer)";
                    valFailed.Add(num);
                }
                else
                {
                    switch (stepArgs[1])
                    {
                        case "Overlord":
                            outString += "Overlord, Done.";
                            if (!isValidating)
                            {
                                WaitForOverlord(num);
                            }
                            break;
                        case "Hamilton":
                            outString += "Hamilton, Done.";
                            if (!isValidating)
                            {
                                WaitForHamilton(num);
                            }
                            break;
                        case "Timer":
                            outString += "Timer, Done.";
                            if (!isValidating)
                            {
                                WaitForTimer(num);
                            }
                            break;
                        default:
                            outString += "WaitFor process not recognized: ";
                            outString += stepArgs[1];
                            valFailed.Add(num);
                            break;
                    }
                }
            }

            void ParseNewXml()
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
                    valFailed.Add(num);
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
                        valFailed.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        RunNewXml(num, stepArgs);
                    }
                    else
                    {
                        metaDictionary["protocol type"] = stepArgs[1];
                        metaDictionary["projectId"] = "place-holder-projectId";
                        DateTime startDt = DateTime.Now;
                        metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDt, true);
                        metaDictionary["startDate"] = SharedParameters.GetDateString(startDt);
                    }
                }
            }

            void ParseAppendXml()
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
                    valFailed.Add(num);
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
                        valFailed.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        RunAppendXml(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder values into dictionary
                        if (IsValUserInput)
                        {
                            RunAppendXml(num, stepArgs);
                        }
                        else
                        {
                            DateTime startDt = DateTime.Now;
                            metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDt, true);
                            metaDictionary["startDate"] = SharedParameters.GetDateString(startDt);

                            metaDictionary["experimentId"] = "place-holder-expId";
                            metaDictionary["projectId"] = "place-holder-projectId";
                            metaDictionary["dataDirectory"] = SharedParameters.WorklistFolderPath;
                        }

                    }
                }
            }

            void ParseSaveXml()
            {
                //string for start of output from ParseStep()
                outString += "Saving XML document: ";

                // no arguments to check, so go straigt to running it
                if (!isValidating)
                {
                    RunSaveXml(num, stepArgs);
                }
            }

            void ParseUserPrompt()
            {
                //UserPrompt takes 2 or 3 arguments
                //First two arguments are title and message
                string titleString;
                string messageString;
                //third argument is string/path to a bitmap file
                string imagePath;

                //string for start of output from ParseStep()
                outString += "User Prompt: ";

                //Booleans to track validity of arguments
                bool argsOk = false;

                //UserPrompt requires at least two arguments:
                if (numArgs < 3)
                {
                    //Message for missing argument or not enough arguments:
                    outString += "Missing arguments; UserPrompt requires at least two arguments (title and message).";
                    valFailed.Add(num);
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
                        valFailed.Add(num);
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
                                valFailed.Add(num);
                            }
                        }
                        else
                        {
                            //Message for bad filename
                            outString += "; Not a valid image filename: ";
                            outString += imagePath;
                            valFailed.Add(num);
                            argsOk = false;
                        }

                    }
                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        //Run the step
                        RunUserPrompt(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunUserPrompt(num, stepArgs);
                        }
                        else
                        {

                        }
                    }
                }
            }

            void ParseUserYesNo()
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
                    valFailed.Add(num);
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
                        valFailed.Add(num);
                    }
                    outString += messageString;
                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        //Run the step
                        RunUserYesNo(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunUserYesNo(num, stepArgs);
                        }
                        else
                        {
                            metaDictionary[keyString] = "place-holder-yes-no";
                        }
                    }
                }
            }

            void ParseAddXml()
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
                    valFailed.Add(num);
                    argsOk = false;
                }
                //All arguments can be any string, so no additional validation checks
                else
                {
                    argsOk = true;
                }

                if (argsOk)
                {
                    if (!isValidating)
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

            void ParseSet()
            {
                //Set takes 2 arguments
                //First two arguments are the key and value to be set in the metaDictionary
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
                    valFailed.Add(num);
                }
                //Then check the validity of the arguments (file types, parsable as numbers, etc.)
                else
                {
                    //key and value just have to be non-empty strings, which has already been ruled out,
                    //    so no additional validation checks needed
                    keyString = stepArgs[1];
                    valueString = stepArgs[2];
                    argsOk = true;
                    outString += $"{valueString} -> {keyString} ";
                }

                if (argsOk)
                {
                    //Set steps need to run even when validating
                    RunSet(num, stepArgs);
                }
            }

            void ParseGet()
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
                    valFailed.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    typeStr = stepArgs[1];
                    keyStr = stepArgs[2];

                    //Make sure the metadata type is a valid type
                    if (SharedParameters.IsValidMetaType(typeStr) || typeStr == "concentration" || typeStr == "note" || typeStr == "number")
                    {
                        argsOk = true;
                        outString += $"{typeStr} -> {keyStr} ";
                    }
                    else
                    {
                        //Message for bad argument:
                        outString += $"Not a valid metadata type: {typeStr}. ";
                        valFailed.Add(num);
                    }
                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        RunGet(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunGet(num, stepArgs);
                        }
                        else
                        {
                            if (typeStr == "concentration")
                            {
                                concDictionary[keyStr] = new Concentration(0, SharedParameters.UnitsList[0]);
                                metaDictionary[$"{keyStr}Conc"] = "0.00";
                                metaDictionary[$"{keyStr}Units"] = SharedParameters.UnitsList[0];
                            }
                            else
                            {
                                metaDictionary[keyStr] = $"place-holder-{typeStr}";
                            }

                        }

                    }

                }
            }

            void ParseGetExpId()
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
                    valFailed.Add(num);
                }
                //Then check the validity of the arguments
                else
                {
                    expIdStr = stepArgs[1];

                    //expIdStr needs to be usable in filenames, so make sure it only has just letters, numbers, or "-" or "_")
                    RegexValidationRule valRule = new RegexValidationRule();
                    valRule.RegexText = "^[a-zA-Z0-9-_]+$";
                    valRule.ErrorMessage = "Experiment IDs can only contain letters, numbers, or \"-\" or \"_\"";
                    ValidationResult valRes = valRule.Validate(expIdStr, System.Globalization.CultureInfo.CurrentCulture);
                    if (!valRes.IsValid)
                    {
                        argsOk = false;
                        //Message for bad Experiment ID argument
                        outString += "Experiment IDs can only contain letters, numbers, or \"-\" or \"_\"";
                        valFailed.Add(num);
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
                                valFailed.Add(num);
                            }
                        }
                    }

                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        RunGetExpId(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunGetExpId(num, stepArgs);
                        }
                        else
                        {
                            metaDictionary["experimentId"] = $"place-holder-experimentId";
                            metaDictionary["dataDirectory"] = SharedParameters.WorklistFolderPath;
                        }

                    }

                }
            }

            void ParseGetFile()
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
                    valFailed.Add(num);
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
                            valFailed.Add(num);
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
                            valFailed.Add(num);
                        }
                    }

                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        RunGetFile(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunGetFile(num, stepArgs);
                        }
                        else
                        {
                            metaDictionary[fileKey] = $"place-holder-file-path";
                        }

                    }

                }
            }

            void ParseStartPrompt()
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
                    valFailed.Add(num);
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
                            valFailed.Add(num);
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
                        valFailed.Add(num);
                        argsOk = false;
                    }

                }

                if (argsOk)
                {
                    if (!isValidating)
                    {
                        //Run the step
                        RunStartDialog(num, stepArgs);
                    }
                    else
                    {
                        //When validating, get actual user input for testing if IsValUserInput is true,
                        // otherwise put placeholder value into dictionary
                        if (IsValUserInput)
                        {
                            RunStartDialog(num, stepArgs);
                        }
                        else
                        {

                        }
                    }
                }
            }

            //Don't actually use this local function, it's just here as a template for new ParseXxxxStep functions
            void ParseGenericStep()
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
                    valFailed.Add(num);
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
                        valFailed.Add(num);
                    }
                }

                //Other validity checks, for example, checking to see if a file actaully exixts
                if (argsOk)
                {
                    bool ovpExists = File.Exists(stepArgs[1]);
                    if (ovpExists)
                    {
                        outString += stepArgs[1];

                        if (!isValidating)
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
                        valFailed.Add(num);
                    }
                }
            }
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

                //Run validation check before running actual experiment, but without user input
                IsValUserInput = false;
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
                //    and without user input
                IsValUserInput = false;
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

            AbortCalled = true;

            //Set IsPaused = true, so that StepsThreadProc() will go ahead to the next step where the Abort action happens
            IsPaused = false;
        }

        private bool Validate()
        {
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
                OutputText += "\r\n";
                OutputText += "Validation failed on the following steps:\r\n";
                foreach (int i in valFailed)
                {
                    OutputText += $"{i}, ";
                }
                validationTextBlock.Text = "Validation Failed";
                validationBorder.Background = Brushes.Red;// new SolidColorBrush(Colors.Red);
            }
            else
            {
                OutputText += "\r\n";
                OutputText += "Validation sucessful.\r\n";
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
            XmlNode unitsNode = xmlDoc.CreateElement("value");
            unitsNode.InnerText = conc.Units;
            stockNode.AppendChild(unitsNode);

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
            string[] getIdStrings = SharedParameters.XmlDocForAppend("");
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
            }
            
        }

        private void RunNewXml(int num, string[] args)
        {
            string protocolType = args[1];

            //New XML document
            xmlDoc = new XmlDocument();
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

            //Also add the protocol type and projectID to the metaDictionary
            metaDictionary["protocol type"] = protocolType;
            metaDictionary["projectId"] = projectIdNode.InnerText;
            //also add the startDateTime to the metaDictionary, as a string formatted for use as part of an experimentId
            metaDictionary["startDateTime"] = SharedParameters.GetDateTimeString(startDateTime, true);
            metaDictionary["startDate"] = SharedParameters.GetDateString(startDateTime);
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
                XmlNode dateNode = dateNodeList.Item(dateNodeList.Count - 1);
                XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                statusFiniAtt.Value = "protocol finished";
                timeFiniNode.Attributes.Append(statusFiniAtt);
                dateNode.AppendChild(timeFiniNode);
            }
            
            //Save the XML document
            xmlDoc.Save(metaDataFilePath);

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
            //
            //string messageStr;// = Regex.Unescape(args[2]);
            //try
            //{
            //    messageStr = Regex.Unescape(args[2]);
            //}
            //catch (ArgumentException e)
            //{
            //    MessageBox.Show("Warning: Unrecognized escape characters.");
            //    messageStr = args[2];
            //}
            string messageStr = args[2];
            messageStr = messageStr.Replace(@"\t", "\t");
            messageStr = messageStr.Replace(@"\n", "\n");

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
                    oKToGo = SharedParameters.ShowPrompt(messageStr, titleStr, imagePath);
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
                if (isCollectingXml)
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
                if (isCollectingXml)
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
                if (isCollectingXml)
                {
                    AddXmlMetaDetail(typeStr, valueStr, keyStr, notes);
                }
            }

        }

        private string GetNumberFromUser(string prompt)
        {
            string numStr = SharedParameters.GetNumber(prompt);

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

        }

        private string[] GetExpId(string dataDirStr, string expIdStr)
        {
            //return string[0] = experimentId
            //return string[1] = XML file path
            //return string[2] = saveDirectory
            string[] getIdStrings = SharedParameters.GetExperimentId(dataDirStr, expIdStr);
            expIdStr = getIdStrings[0];
            metaDataFilePath = getIdStrings[1];
            string saveDirectory = getIdStrings[2];

            if (expIdStr == "")
            {
                AbortCalled = true;
            }

            return new string[] { expIdStr, metaDataFilePath, saveDirectory };
        }
        //If a projectID is given, use it in the default filePath, via the overloaded SharedParameters.GetExperimentId()
        private string[] GetExpId(string dataDirStr, string expIdStr, string projID)
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
            metaDictionary["dataDirectory"] = argsBack[2];

        }

        private void RunSet(int num, string[] args)
        {
            string keyStr = args[1];
            string valueStr = args[2];

            metaDictionary[keyStr] = valueStr;
        }

        private void RunOverlord(int num, string[] args)
        {
            //args[0] is "Overlord"
            //args[1] is file path
            string file = args[1];
            //second argument (if any) is the variables to pass

            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            if ( !(ovProcess is null) )
            {
                if (!ovProcess.HasExited)
                {
                    OutputText += "... waiting for last Overlord Process to exit.";
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
            if (isCollectingXml)
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

            }
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

        private void RunHamilton(int num, string[] args)
        {
            //args[0] is "Hamilton"
            //args[1] is file path
            string file = args[1];

            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            if ( !(hamProcess is null) )
            {
                if (!hamProcess.HasExited)
                {
                    OutputText += "... waiting for last Hamilton Process to exit.";
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
            if (isCollectingXml)
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
            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            OutputText += "... waiting for Overlord to finish and exit.";

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
            if (isCollectingXml)
            {
                DateTime dt = DateTime.Now;

                //XmlNode testNode = xmlDoc.SelectSingleNode("descendant::overlordProcedure/dateTime");
                XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::overlordProcedure/dateTime");
                XmlNode dateNode = dateNodeList.Item(dateNodeList.Count-1);
                XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                statusFiniAtt.Value = "procedure finished";
                timeFiniNode.Attributes.Append(statusFiniAtt);
                dateNode.AppendChild(timeFiniNode);
            }

        }
        
        private void WaitForHamilton(int num)
        {
            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            OutputText += "... waiting for Hamilton Runtime Engine to finish and exit.";

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
            if (isCollectingXml)
            {
                DateTime dt = DateTime.Now;

                XmlNodeList dateNodeList = xmlDoc.SelectNodes("descendant::hamiltonMethod/dateTime");
                XmlNode dateNode = dateNodeList.Item(dateNodeList.Count-1);
                XmlNode timeFiniNode = xmlDoc.CreateElement("time");
                timeFiniNode.InnerText = dt.ToString("HH:mm:ss");
                XmlAttribute statusFiniAtt = xmlDoc.CreateAttribute("status");
                statusFiniAtt.Value = "method finished";
                timeFiniNode.Attributes.Append(statusFiniAtt);
                dateNode.AppendChild(timeFiniNode);
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
            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

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
                    OutputText += "... waiting for last Timer to finish.";
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
            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            OutputText += "... waiting for Timer to finish.";

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
            return $"{DateTime.Now.ToString("yyyy-MM-dd HH.mm.ss")}.trc";
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
                InsertInputText($"{SelectedCommand}/ ");

                //if it is a NewXML or AppendXML command, also add in the SaveXML command automatically
                if (SelectedCommand == "NewXML" || SelectedCommand == "AppendXML")
                {
                    int caretPos = inputTextBox.SelectionStart;

                    InsertInputText(" <protocol type>\n\nSaveXML/ ");

                    //move caret to middle line between NewXML and SaveXML
                    inputTextBox.SelectionStart = caretPos + 1;
                    inputTextBox.SelectionLength = 0;
                }
            }

        }

        private string WrapTcpMessage(string msg)
        {
            return SimpleTCP.Message.WrapTcpMessage(msg);
        }

        private void ReaderComboBox_DropDownClosed(object sender, EventArgs e)
        {
            string reader = SelectedReaderBlock.Text;
            string title = $"Remote Connection to {reader}";
            string messageText = $"Do you want to make the remote connection to {reader}?\nSelect 'Yes' to establish or continue a connection, or 'No' to close an existing connection.";
            YesNoDialog.Response userResp = SharedParameters.ShowYesNoDialog(messageText, title);

            ConfigureReader(reader, userResp);
        }

        private void ConfigureReader(string reader, YesNoDialog.Response connect)
        {
            if (connect == YesNoDialog.Response.Yes)
            {
                SelectedReaderBlock.Background = Brushes.LimeGreen;
                if (readerClients[reader] == null)
                {
                    SimpleTcpClient client = new SimpleTcpClient();
                    client.Delimiter = 0x13;
                    try
                    {
                        client.Connect(readerIps[reader], 42222);
                    }
                    catch (System.Net.Sockets.SocketException e)
                    {
                        MessageBox.Show($"{reader} is not accepting the connection. Make sure LMSF_Gen5 is running and in \"Remote\" mode on the {reader} computer. Then try again.");
                    }
                    catch (Exception e)
                    {
                        MessageBox.Show($"Exception: {e}");
                    }
                    readerClients[reader] = client;
                }
                else
                {
                    SimpleTcpClient client = readerClients[reader];
                    client.Delimiter = 0x13;
                    client.Connect(readerIps[reader], 42222);
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
    }

}
