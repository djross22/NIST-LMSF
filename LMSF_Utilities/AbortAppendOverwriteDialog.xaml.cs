using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.ComponentModel;
using System.Collections.ObjectModel;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;
using System.Text.RegularExpressions;

namespace LMSF_Utilities
{
    /// <summary>
    /// Interaction logic for SelectMetaIdentDialog.xaml
    /// </summary>
    /// 
    public partial class AbortAppendOverwriteDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string promptText;

        //Enumberation for response options (which button got pushed)
        public enum Response { Abort, Append, Overwrite };
        public Response UserResponse { get; set; }

        #region Properties Getters and Setters
        public string PromptText
        {
            get { return this.promptText; }
            set
            {
                this.promptText = value;
                OnPropertyChanged("PromptText");
            }
        }
        #endregion

        public AbortAppendOverwriteDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public AbortAppendOverwriteDialog(string title, string prompt)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            PromptText = prompt;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void OverwriteButton_Click(object sender, RoutedEventArgs e)
        {
            UserResponse = Response.Overwrite;
            this.DialogResult = true;
        }

        private void AppendButton_Click(object sender, RoutedEventArgs e)
        {
            UserResponse = Response.Append;
            this.DialogResult = true;
        }

        private void Button_Click(object sender, RoutedEventArgs e)
        {
            UserResponse = Response.Abort;
            this.DialogResult = true;
        }
    }
}
