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
        //  Note that ListOfSteps handles notifications on its own (included with ObservableCollection class)
        //    But the other fields have Property wrappers with set methods that handle the notification.
        //    This is necessary to get data bindings to work properly with the GUI
        public ObservableCollection<AutomationStep> ListOfSteps { get; set; }
        private AutomationStep selectedStep;
        private int selectedIndex;

        #region Properties Getters and Setters
        public int SelectedIndex
        {
            get { return this.selectedIndex; }
            set
            {
                this.selectedIndex = value;
                OnPropertyChanged("SelectedIndex");
            }
        }

        public AutomationStep SelectedStep
        {
            get { return this.selectedStep; }
            set
            {
                this.selectedStep = value;
                OnPropertyChanged("SelectedStep");
            }
        }
        #endregion

        public MainWindow()
        {
            ListOfSteps = new ObservableCollection<AutomationStep>();
            //Add temporary list items for testing
            TempStepInit();

            InitializeComponent();
            DataContext = this;
        }

        //temporary method for debugging/testing
        void TempStepInit()
        {
            ListOfSteps.Add(new AutomationStep("type 1"));
            ListOfSteps.Add(new AutomationStep("type 2"));
            ListOfSteps.Add(new AutomationStep("type 3"));

            SelectedStep = ListOfSteps.First();

            SelectedIndex = 1;
        }

        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            // Instantiate the dialog box
            AddNewStepWindow dlg = new AddNewStepWindow();

            // Configure the dialog box
            dlg.Owner = this;

            // Open the dialog box modally 
            bool? dialogResult = dlg.ShowDialog();

            if (dialogResult==true)
            {
                ListOfSteps.Insert(SelectedIndex + 1, new AutomationStep($"type {SelectedIndex + 1} added"));
            }
            
        }

        //temporary method for debugging/testing
        private void TestButton_Click(object sender, RoutedEventArgs e)
        {
            // Configure message box
            string message = $"SelectedIndex = {SelectedIndex}";
            // Show message box
            MessageBoxResult result = MessageBox.Show(message);
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void DuplicateStepButton_Click(object sender, RoutedEventArgs e)
        {
            ListOfSteps.Insert(SelectedIndex + 1, (AutomationStep)SelectedStep.Clone());
        }

        private void DeleteStepButton_Click(object sender, RoutedEventArgs e)
        {
            ListOfSteps.RemoveAt(SelectedIndex);
        }

        private void MoveUpButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedIndex>0)
            {
                ListOfSteps.Move(SelectedIndex, SelectedIndex - 1);

            }
        }

        private void MoveDownButton_Click(object sender, RoutedEventArgs e)
        {
            if (SelectedIndex < (ListOfSteps.Count-1) )
            {
                ListOfSteps.Move(SelectedIndex, SelectedIndex + 1);

            }
        }
    }
}
