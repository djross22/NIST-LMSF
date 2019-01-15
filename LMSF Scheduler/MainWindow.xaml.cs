using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Text;
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
        private string inputText;
        private string outputText;

        #region Properties Getters and Setters
        public string InputText
        {
            get { return this.inputText; }
            set
            {
                this.inputText = value;
                OnPropertyChanged("InputText");
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

        public MainWindow()
        {

            InitializeComponent();
            DataContext = this;
        }


        //temporary method for debugging/testing
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            testTextBox.Text = "button pushed";
            string message = InputText + ", " + OutputText;
            MessageBoxResult result = MessageBox.Show(message);
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
    }
}
