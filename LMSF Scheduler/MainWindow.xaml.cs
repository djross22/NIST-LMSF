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
        private List<int> valFailed;

        //Background worker to run steps
        private Thread runStepsThread;

        //Overlord process (runs Overlord.Main.exe)
        private Process ovProcess;

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
        private static string stepSource = "LMSF Scheduler";
        private bool isCollectingXml;

        #region Properties Getters and Setters
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

            CommandList = new ObservableCollection<string>() { "Overlord", "Timer", "WaitFor", "NewXML" }; //SharedParameters.UnitsList;
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
            string metaType = "project";

            //OutputText = "";

            string metaID = SharedParameters.GetMetaIdentifier(metaType, "Select the Project Identifier for this experiment:");

            OutputText += metaID + "\n";
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
            openFileDialog.Filter = "Text file (*.txt)|*.txt";
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
                File.WriteAllText(ExperimentFileName, InputText);
                InputChanged = false;
                didSave = true;
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
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
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
                this.Dispatcher.Invoke(() => { IsRunning = false;  });
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
            string outString = $"{num}. ";
            outString += $"{SharedParameters.GetDateTimeString()}; ";
            string[] stepArgs = step.Split(new[] { ",", ";" }, StringSplitOptions.RemoveEmptyEntries);

            stepArgs = stepArgs.Select(s => s.Trim()).ToArray();
            stepArgs = stepArgs.Where(x => !string.IsNullOrEmpty(x)).ToArray();

            int numArgs = stepArgs.Length;

            if (numArgs > 0)
            {
                string stepType = stepArgs[0];

                switch (stepType)
                {
                    case "Overlord":
                        ParseOverlordStep();
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
                    case "SaveXML":
                        ParseSaveXml();
                        break;
                    default:
                        valFailed.Add(num);
                        outString += "Step type not recongnized: ";
                        foreach (string s in stepArgs)
                        {
                            outString += s + ", ";
                        }
                        break;
                }

                outString += "\r\n";
                outString += "\r\n";
            }
            
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
                    if (numArgs>2)
                    {
                        //check passed variables to make sure they have to correct format
                        string varString = stepArgs[2];
                        string[] varArgs = varString.Split(new[] { " " }, StringSplitOptions.None);

                        //Needs to be an even number of varArgs
                        int numVarArgs = varArgs.Length;
                        if (numVarArgs%2 == 0)
                        {
                            varArgsOk = true;
                            //And even arguments need to start with "[" and end with "]"
                            for (int i = 0; i < varArgs.Length; i += 2)
                            {
                                if (!( varArgs[i].StartsWith("[") && varArgs[i].EndsWith("]") ) )
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
                        if (stepArgs.Length>2)
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

            void ParseTimerStep()
            {
                outString += "Running Timer: ";
                int waitTime = 0;
                bool isInteger = false;
                if (numArgs < 2)
                {
                    outString += "No time given for the timer.";
                    valFailed.Add(num);
                }
                else
                {
                    if (int.TryParse(stepArgs[1], out waitTime))
                    {
                        isInteger = true;
                    }
                    else
                    {
                        outString += "Timer time parameter is not an integer: ";
                        outString += stepArgs[1];
                        valFailed.Add(num);
                    }
                }

                if (isInteger)
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

                //Requires an argument for the step type:
                if (numArgs < 2)
                {
                    argsOk = false;
                    //Message for missing argument or not enough arguments:
                    outString += "No step type argument given.";
                    valFailed.Add(num);
                }
                else
                {
                    //If the step type argument exists, amke sure it is ok (needs to be a good xml atribute value, with just letters, numbers, spaces, or "-" or "_")
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
                        RunNewXml(num, stepArgs);
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
                    RunSaveXml();
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

                //Run validation check before ruanning actual experiment
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

        private void AddXmlStep(string typeStr, string sourceStr)
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

            AddDateTimeNodes(protocolNode, "protocol started");

            //add the step node to the experiment node
            experimentNode.AppendChild(protocolNode);
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

        private void RunNewXml(int num, string[] args)
        {
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
            //ID attribute for project node
            XmlAttribute projectIdAtt = xmlDoc.CreateAttribute("projectId");

            //this has to be delegated becasue it interacts with the GUI by callin up a dialog box
            this.Dispatcher.Invoke(() => { projectIdAtt.Value = GetProjectIdentifier(); });
            //projectIdAtt.Value = SharedParameters.GetMetaIdentifier("project", "Select the Project Identifier for this experiment:");


            projectNode.Attributes.Append(projectIdAtt);
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

            //Add the current experiment step to the XML
            AddXmlStep(args[1], stepSource);
        }

        private void RunSaveXml()
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

            //Save the XML document
            xmlDoc.Save(metaDataFilePath);

            //turn off metadata collection
            isCollectingXml = false;
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
                AddDateTimeNodes(ovpNode, "procedure started");

            }
        }

        private void AddDateTimeNodes(XmlNode parentNode, string statusStr)
        {
            DateTime dt = DateTime.Now;
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

            if (int.TryParse(args[1], out waitTime))
            {
                //argument is an integer, so wait that long, in seconds
            }
            else
            {
                //argument is a DateTime string, so wait until the specified time
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
            InsertInputText($"{SelectedCommand}, ");

            //if it is a NewXML or AppendXML command, also add in the SaveXML command automatically
            if (SelectedCommand == "NewXML" || SelectedCommand == "AppendXML")
            {
                int caretPos = inputTextBox.SelectionStart;

                InsertInputText(" <step type>\n\nSaveXML, ");

                //move caret to middle line between NewXML and SaveXML
                inputTextBox.SelectionStart = caretPos + 1;
                inputTextBox.SelectionLength = 0;
            }
        }

    }

}
