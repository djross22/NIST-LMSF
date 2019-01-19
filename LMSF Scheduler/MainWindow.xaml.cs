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
        private bool isPaused = true;
        private bool isValidating = false;
        private List<int> valFailed;

        //Background worker to run steps
        private BackgroundWorker runStepsWorker = new BackgroundWorker();

        //Window title, app name, plus file name, plus * to indicate unsaved changes
        private static string appName = "LMSF Scheduler";
        private string displayTitle = appName + " - ";

        #region Properties Getters and Setters
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
                this.inputText = value;
                InputChanged = true;
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
                abortButton.IsEnabled = false;

                inputTextBox.IsEnabled = true;
                insertFileButton.IsEnabled = true;
                mainMenu.IsEnabled = true;
            }
        }

        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;
        }

        //temporary method for debugging/testing
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            testTextBox.Text = DisplayTitle;
            string message = InputText + ", " + OutputText;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void TestWriteButton_Click(object sender, RoutedEventArgs e)
        {
            string addon = " test add text";
            InputText += addon;
            addon = " test add out text";
            OutputText += addon;
        }

        private void UpdateTitle()
        {
            DisplayTitle = appName + " - " + ExperimentFileName;
            if (InputChanged)
            {
                DisplayTitle += "*";
            }
        }

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
                    Save();
                    okToGo = true;
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

        private void Save()
        {
            if (ExperimentFileName != "")
            {
                File.WriteAllText(ExperimentFileName, InputText);
                InputChanged = false;
            }
            else
            {
                SaveAs();
            }
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
            inputTextBox.Focus();
        }

        private void SaveAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, InputText);
                ExperimentFileName = saveFileDialog.FileName;
                InputChanged = false;
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {
            inputTextBox.Focus();
        }

        private void InsertFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select File Path to Insert";

            if (openFileDialog.ShowDialog() == true)
            {
                int caretPos = inputTextBox.SelectionStart;
                string newText = openFileDialog.FileName;

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

            OutputText = "";
        }

        private void StepRunner_DoWork(object sender, DoWorkEventArgs e)
        {
            InitSteps();
            
            while (IsRunning)
            {
                if (IsPaused)
                {
                    Thread.Sleep(100);
                }
                else
                {
                    Step();
                }
                
            }

        }

        private void StepRunner_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            
        }

        private void StepRunner_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            IsPaused = true;
            IsRunning = false;
        }

        private void RunSteps()
        {
            testTextBox.Text = "RunSteps()...";

            //Set up the BackgroundWorker
            runStepsWorker = new BackgroundWorker();
            runStepsWorker.WorkerReportsProgress = false;
            //runStepsWorker.WorkerReportsProgress = true;
            runStepsWorker.DoWork += StepRunner_DoWork;
            //runStepsWorker.ProgressChanged += StepRunner_ProgressChanged;
            runStepsWorker.RunWorkerCompleted += StepRunner_RunWorkerCompleted;

            //Start the BackgroundWorker
            runStepsWorker.RunWorkerAsync();
        }

        private void Step()
        {
            if (stepNum < totalSteps)
            {
                OutputText += ParseStep(stepNum, inputSteps[stepNum]);
                stepNum++;
            }
            else
            {
                IsRunning = false;
            }
        }

        private string ParseStep(int num, string step)
        {
            string outString = $"{num}. ";
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
                        outString += "Running Overlord proccedure: ";
                        bool isOvpFileName = false;
                        if (numArgs < 2)
                        {
                            outString += "No procedure path give.";
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
                        }

                        if (isOvpFileName)
                        {
                            bool ovpExists = File.Exists(stepArgs[1]);
                            if (ovpExists)
                            {
                                outString += stepArgs[1];

                                if (!isValidating)
                                {
                                    RunOverlord(num, stepArgs[1]);
                                }
                            }
                            else
                            {
                                outString += "Procedure file not found: ";
                                outString += stepArgs[1];
                                valFailed.Add(num);
                            }
                        }
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
        }

        private void PlayButton_Click(object sender, RoutedEventArgs e)
        {
            //Change the IsPaused property to false
            IsPaused = false;
            IsRunning = true;
            isValidating = false;
            valFailed = new List<int>();

            Play();

            inputTextBox.Focus();
        }

        private void StepButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO: This is not right
            //Change the IsPaused property to true
            IsPaused = true;
            IsRunning = true;

            Play();
        }

        private void Play()
        {
            if (!runStepsWorker.IsBusy)
            {
                //If runStepsWorker is not already running, start it from the begining 
                RunSteps();
            }
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO: ???
            IsPaused = true;
        }

        private void RewindButton_Click(object sender, RoutedEventArgs e)
        {

        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {
            IsRunning = false;
            IsPaused = true;
        }

        private void ValidateButton_Click(object sender, RoutedEventArgs e)
        {
            IsPaused = false;
            IsRunning = true;
            isValidating = true;
            valFailed = new List<int>();

            // For validation, run in the main thread...
            StepRunner_DoWork(this, new DoWorkEventArgs(this));
            //### Copy code from StepRunner_RunWorkerCompleted
            IsPaused = true;
            IsRunning = false;
            //###

            if (valFailed.Count>0)
            {
                OutputText += "\r\n";
                OutputText += "Validation failed on the following steps:\r\n";
                foreach (int i in valFailed)
                {
                    OutputText += $"{i}, ";
                }
            }
            else
            {
                OutputText += "\r\n";
                OutputText += "Validation sucessful.\r\n";
            }
            isValidating = false;

            inputTextBox.Focus();
        }

        private void RunOverlord(int num, string file)
        {
            WaitingForStepCompletion = true;
            stepsRunning[num] = true;

            ProcessStartInfo startInfo = new ProcessStartInfo();
            //startInfo.FileName = @"C:\Users\djross\source\repos\NIST LMSF\Overlord Simulator\bin\Release\Overlord.Main.exe";
            startInfo.FileName = @"C:\Program Files (x86)\PAA\Overlord3\Overlord.Main.exe";
            startInfo.Arguments = "\"" + file + "\"" + " -r -c";
            Process ovProcess = Process.Start(startInfo);

            //TODO: replace "if (true)" with "if (waitUntilFinished)
            if (true)
            {
                BackgroundWorker ovWorker = new BackgroundWorker();
                ovWorker.WorkerReportsProgress = false;
                ovWorker.DoWork += OutsideProcessMonitor_DoWork;

                List<object> arguments = new List<object>();
                arguments.Add(num);
                arguments.Add(ovProcess);
                ovWorker.RunWorkerAsync(arguments);
            }
            else
            {
                WaitingForStepCompletion = false;
            }

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

            WaitingForStepCompletion = false;
        }

    }
}
