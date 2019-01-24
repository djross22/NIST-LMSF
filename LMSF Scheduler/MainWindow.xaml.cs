﻿using System;
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
                    abortButton.Background = new SolidColorBrush(Colors.Red);
                }
                else
                {
                    abortButton.Background = new SolidColorBrush(Colors.White);
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
                    statusBorder.Background = new SolidColorBrush(Colors.Yellow);
                    statusTextBlock.Text = "Paused";
                }
                else
                {
                    statusBorder.Background = new SolidColorBrush(Colors.LimeGreen);
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

                statusBorder.Background = new SolidColorBrush(Colors.Red);
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

            CommandList = new ObservableCollection<string>() { "Overlord", "Timer", "WaitFor" }; //SharedParameters.UnitsList;
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
            string metaType = "media";

            //OutputText = "";

            string metaID = SharedParameters.GetMetaIdentifier(metaType, "");

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
        private void InitSteps()
        {
            inputSteps = InputText.Split(new[] { "\r\n", "\r", "\n" }, StringSplitOptions.RemoveEmptyEntries);

            //remove leading and trailing white space from each line
            inputSteps = inputSteps.Select(s => s.Trim()).ToArray();
            //then delete any lines that were just white space (are now empty)
            inputSteps = inputSteps.Where(x => !string.IsNullOrEmpty(x)).ToArray();

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
                NewLogFile();
                OutputText = $"Running {ExperimentFileName}\n\n";
                File.WriteAllText(logFilePath, OutputText);
            }
            
        }

        private void StepsThreadProc()
        {
            InitSteps();

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
                    string doneText = $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}; Done.\n";
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
            outString += $"{DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss")}; ";
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
                        RunTimer(num, waitTime);
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
                validationBorder.Background = new SolidColorBrush(Colors.Red);
            }
            else
            {
                OutputText += "\r\n";
                OutputText += "Validation sucessful.\r\n";
                validationTextBlock.Text = "Validation Sucessful";
                validationBorder.Background = new SolidColorBrush(Colors.LimeGreen);
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

        private void RunTimer(int num, int waitTime)
        {
            //TODO: re-evaluate the need for these variables-
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

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
            return $"{DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss")}.trc";
        }

        private void NewLogFile()
        {
            logFilePath = SharedParameters.LogFileFolderPath + NewLogFileName();
        }

        private void SelectComboBox_DropDownClosed(object sender, EventArgs e)
        {
            InsertInputText($"{SelectedCommand}, ");
        }
    }

}
