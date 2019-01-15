using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.ComponentModel;

namespace LMSF_Scheduler
{
    public class AutomationStep : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler PropertyChanged;

        private string stepType;
        private string stepShortDetail;
        private bool isWaitUntil;
        private bool waitCheckBoxEnabled;


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
        #endregion

        public AutomationStep(string stepType, string stepShortDetail, bool isWaitUntil, bool waitCheckBoxEnabled)
        {
            this.stepType = stepType;
            this.stepShortDetail = stepShortDetail;
            this.isWaitUntil = isWaitUntil;
            this.waitCheckBoxEnabled = waitCheckBoxEnabled;
        }



        public AutomationStep(string stepType)
        {
            this.stepType = stepType;
            this.stepShortDetail = "short detail test";
            this.isWaitUntil = true;
            this.waitCheckBoxEnabled = false;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

    }
}
