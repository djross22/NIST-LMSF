using System;
using System.IO;
using System.Collections.Generic;
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

namespace Overlord_Simulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //Control Booleans set by input arguments
        static bool closeAutomatically = false;
        static bool runImmediately = false;
        static bool runMinimized = false;
        static bool passVariables = false;

        //Controls whether or not the progress bar Backgroundprocess runs
        private static bool _isRunning = true;

        public MainWindow()
        {
            InitializeComponent();

            //Simulation time i seconds
            float simTime = 10;
            
            //Variables for parsing input arguments
            string[] args = App.commandLineArgs;

            //Dictionary of Overlord variables to be set/displayed
            Dictionary<string, string> ovpVarDictionay = new Dictionary<string, string>();
            Resources["OvpVarDict"] = ovpVarDictionay;

            if (App.commandLineArgs.Length > 0)
            {
                // write the ovp file name to the ovp path text box
                ovpPathBox.Text = args[0];

                try
                {
                    // Open the ovp file using a stream reader. 
                    using (StreamReader sr = new StreamReader(args[0]))
                    {
                        // Read the stream to a string, and write  
                        // the string to the ovp xml display text box 
                        String line = sr.ReadToEnd();
                        ovpXmlDisplayBox.AppendText(line.ToString());
                        ovpXmlDisplayBox.AppendText("\n");
                        
                    }
                }
                catch (Exception e)
                {
                    ovpXmlDisplayBox.AppendText("The Overlord procedure file, ");
                    ovpXmlDisplayBox.AppendText(args[0]);
                    ovpXmlDisplayBox.AppendText(", could not be read:");
                    ovpXmlDisplayBox.AppendText("\n");
                    ovpXmlDisplayBox.AppendText(e.Message);
                    ovpXmlDisplayBox.Foreground = Brushes.Red;
                }

                // argument parsing for -r, -c, -m, -v
                foreach (string s in args)
                {
                    switch (s)
                    {
                        case "-r":
                            // -r = run the procedure immediately after Overlord Simulator is open
                            runImmediately = true;
                            break;
                        case "-c":
                            // -c = automatically close Overlord Simulator once the procedure has completed running. Must be used with -r
                            closeAutomatically = true;
                            break;
                        case "-m":
                            // -m = run Overlord Simulator minimized
                            runMinimized = true;
                            break;
                        case "-v":
                            // -v = pass variables into Overlord. Each variable/value must be supplied as a pair, separated by a space
                            passVariables = true;
                            break;
                    }
                }

                // read in variables if passVariables
                if (passVariables)
                {
                    bool isVarKey = false;
                    string key = "";
                    string val = "";
                    int keyLength = 0;
                    foreach (string s in args)
                    {
                        if ( s.StartsWith("[") & s.EndsWith("]") )
                        {
                            isVarKey = true;
                            keyLength = s.Length;
                            key = s.Substring(1, keyLength - 2);
                        }
                        else
                        {
                            if (isVarKey) {
                                val = s;
                                ovpVarDictionay.Add(key, val);
                            }
                            isVarKey = false;
                        }
                    }
                }

                if (runImmediately)
                {
                    _isRunning = true;
                    pauseButton.Content = "Pause";
                }
                else
                {
                    _isRunning = false;
                    pauseButton.Content = "Start";
                }

                if (runMinimized)
                {
                    WindowState = WindowState.Minimized;
                }
            
                //Setup and run Background worker for progress bar
                // Code (mostly) from: https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
                simTimeProgressBar.Value = 0;
                BackgroundWorker worker = new BackgroundWorker();
                worker.WorkerReportsProgress = true;
                worker.DoWork += Worker_DoWork;
                worker.ProgressChanged += Worker_ProgressChanged;
                worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
                worker.RunWorkerAsync(simTime);
                //End code from https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
            }
            else
            {
                ovpXmlDisplayBox.AppendText("No Overlord procedure file given as command line argument");
                pauseButton.IsEnabled = false;
                stopButton.IsEnabled = false;
            }

        }

        // More code (mostly) from: https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int sleepTime = Convert.ToInt32((float)e.Argument * 10);
            int simProgress = 0;

            while (simProgress < 100)
            {
                (sender as BackgroundWorker).ReportProgress(simProgress);
                System.Threading.Thread.Sleep(sleepTime);
                if (_isRunning)
                {
                    simProgress++;
                }
                
            }
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            simTimeProgressBar.Value = e.ProgressPercentage;
        }

        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            //close App if -c option
            if (closeAutomatically)
            {
                System.Windows.Application.Current.Shutdown();
            }

            //otherwise disable pauseButton if the sim pregress bar reaches 100%
            pauseButton.IsEnabled = false;
        }

        private void PauseButton_Click(object sender, RoutedEventArgs e)
        {
            if (_isRunning)
            {
                _isRunning = false;
                pauseButton.Content = "Resume";
            }
            else
            {
                _isRunning = true;
                pauseButton.Content = "Pause";
            }
        }

        private void StopButton_Click(object sender, RoutedEventArgs e)
        {
            System.Windows.Application.Current.Shutdown();
        }
        //End code from https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/

    }
}
