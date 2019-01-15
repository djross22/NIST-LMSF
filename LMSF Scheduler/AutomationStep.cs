using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;
using System.Windows.Media.Imaging;

namespace LMSF_Scheduler
{
    public class AutomationStep : INotifyPropertyChanged, ICloneable
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string stepType;
        private string stepShortDetail;
        private bool isWaitUntil;
        private bool waitCheckBoxEnabled;
        private BitmapImage stepIcon;

        //Icons for different step types
        //private static BitmapImage overlordIcon = new BitmapImage();
        private static readonly BitmapImage overlordIcon = (BitmapImage)App.Current.Resources["Overlord BMP"];
        private static readonly BitmapImage waitIcon = (BitmapImage)App.Current.Resources["Wait BMP"];
        private static readonly BitmapImage dialogIcon = (BitmapImage)App.Current.Resources["Dialog BMP"];


        #region Properties Getters and Setters
        public string StepType
        {
            get { return this.stepType; }
            set
            {
                this.stepType = value;
                OnPropertyChanged("StepType");
            }
        }

        public string StepShortDetail
        {
            get { return this.stepShortDetail; }
            set
            {
                this.stepShortDetail = value;
                OnPropertyChanged("StepShortDetail");
            }
        }

        public bool IsWaitUntil
        {
            get { return this.isWaitUntil; }
            set
            {
                this.isWaitUntil = value;
                OnPropertyChanged("IsWaitUntil");
            }
        }

        public bool WaitCheckBoxEnabled
        {
            get { return this.waitCheckBoxEnabled; }
            set
            {
                this.waitCheckBoxEnabled = value;
                OnPropertyChanged("WaitCheckBoxEnabled");
            }
        }

        public BitmapImage StepIcon
        {
            get { return this.stepIcon; }
            set
            {
                this.stepIcon = value;
                OnPropertyChanged("StepIcon");
            }
        }
        #endregion

        public AutomationStep(string stepType, string stepShortDetail, bool isWaitUntil)
        {
            this.stepType = stepType;
            this.stepShortDetail = stepShortDetail;
            this.isWaitUntil = isWaitUntil;
            InitStep();
        }

        public AutomationStep(string stepType)
        {
            this.stepType = stepType;
            this.stepShortDetail = "short detail test";
            this.isWaitUntil = true;
            InitStep();
        }

        void InitStep()
        {
            switch (stepType)
            {
                case "Run Overlord procedure":
                    stepIcon = overlordIcon;
                    waitCheckBoxEnabled = true;
                    break;
                case "Wait step":
                    stepIcon = waitIcon;
                    waitCheckBoxEnabled = true;
                    break;
                case "Get metadata from user":
                    stepIcon = dialogIcon;
                    waitCheckBoxEnabled = false;
                    isWaitUntil = true;
                    break;
                default:
                    break;
            }
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        //TODO: revisit this once more details are filled in.
        public object Clone()
        {
            return new AutomationStep(stepType, stepShortDetail, isWaitUntil);
        }
    }
}
