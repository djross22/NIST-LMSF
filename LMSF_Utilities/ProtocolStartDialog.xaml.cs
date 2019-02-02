using System;
using System.IO;
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
    public partial class ProtocolStartDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string protocolTitle;
        private string stuffList;

        #region Properties Getters and Setters
        public string ProtocolTitle
        {
            get { return this.protocolTitle; }
            set
            {
                this.protocolTitle = value;
                OnPropertyChanged("ProtocolTitle");
            }
        }
        public string StuffList
        {
            get { return this.stuffList; }
            set
            {
                this.stuffList = value;
                OnPropertyChanged("StuffList");
            }
        }
        #endregion

        public ProtocolStartDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public ProtocolStartDialog(string title, string stuffFile)
        {
            InitializeComponent();
            DataContext = this;

            ProtocolTitle = title;

            try
            {   // Open the text file using a stream reader.
                using (StreamReader sr = new StreamReader(stuffFile))
                {
                    // Read the stream to a string, and write the string to the console.
                    StuffList = sr.ReadToEnd();
                }
            }
            catch (IOException e)
            {
                StuffList = "";
            }

            int numLines = StuffList.Split('\n').Length;

            Height = numLines * 19 + 175 + 90;

        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = true;
        }

        private void AbortButton_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
