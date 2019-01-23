using System;
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
using System.Windows.Shapes;

namespace LMSF_Utilities
{
    /// <summary>
    /// Interaction logic for TimerDialog.xaml
    /// </summary>
    public partial class TimerDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        //Used to detect whether or not the TimerDialog has closed
        public bool IsClosed { get; private set; } = false;

        //Controls whether or not the progress bar Backgroundprocess runs
        private static bool _isRunning = true;

        //Wait time in seconds
        int waitTime = 10;

        private string timeLeftString;
        public string TimeLeftString
        {
            get { return this.timeLeftString; }
            set
            {
                this.timeLeftString = value;
                OnPropertyChanged("TimeLeftString");
            }
        }

        public TimerDialog(string title, int time)
        {
            InitializeComponent();
            DataContext = this;

            _isRunning = true;

            this.Title = title;
            this.waitTime = time;

            //Setup and run Background worker for progress bar
            // Code (mostly) from: https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
            simTimeProgressBar.Value = 0;
            BackgroundWorker worker = new BackgroundWorker();
            worker.WorkerReportsProgress = true;
            worker.DoWork += Worker_DoWork;
            worker.ProgressChanged += Worker_ProgressChanged;
            worker.RunWorkerCompleted += Worker_RunWorkerCompleted;
            worker.RunWorkerAsync(waitTime);
            //End code from https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        // More code (mostly) from: https://www.wpf-tutorial.com/misc/multi-threading-with-the-backgroundworker/
        void Worker_DoWork(object sender, DoWorkEventArgs e)
        {
            int timeInSeconds = (int)e.Argument;
            double doubleTime = (double)timeInSeconds;

            int sleepTime = 100;// timeInSeconds * 10;
            int simProgress = 0;

            DateTime endTime = DateTime.Now;
            endTime = endTime.AddSeconds(timeInSeconds);
            TimeSpan timeLeft = endTime - DateTime.Now;
            double doubleLeft = timeLeft.TotalSeconds;

            while (simProgress < 100)
            {
                (sender as BackgroundWorker).ReportProgress(simProgress);
                System.Threading.Thread.Sleep(sleepTime);
                if (_isRunning)
                {
                    timeLeft = endTime - DateTime.Now;
                    doubleLeft = timeLeft.TotalSeconds;

                    simProgress = 100 - (int)Math.Round(100*doubleLeft/doubleTime);
                    if (simProgress>100)
                    {
                        simProgress = 100;
                    }
                }
                else
                {
                    endTime = DateTime.Now + timeLeft;
                }
                this.Dispatcher.Invoke(() => { TimeLeftString = ToTimeLeft(timeLeft); });
            }
        }

        private string ToTimeLeft(TimeSpan ts)
        {
            int d = ts.Days;
            int h = ts.Hours;
            string timeString = ts.ToString(@"mm\:ss");
            timeString = $"Time remaining: {h + 24 * d}:" + timeString;
            if (!_isRunning)
            {
                timeString += " (Paused)";
            }
            return timeString;
        }

        void Worker_ProgressChanged(object sender, ProgressChangedEventArgs e)
        {
            simTimeProgressBar.Value = e.ProgressPercentage;
        }

        void Worker_RunWorkerCompleted(object sender, RunWorkerCompletedEventArgs e)
        {
            this.Close();
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
            this.Close();
        }

        private void Window_Closed(object sender, EventArgs e)
        {
            IsClosed = true;
        }
    }
}
