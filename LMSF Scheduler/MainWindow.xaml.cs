using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
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
    public partial class MainWindow : Window
    {
        public ObservableCollection<AutomationStep> ListOfSteps { get; set; }
        public AutomationStep selectedStep { get; set; }

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

            selectedStep = ListOfSteps.First();
        }

        private void AddStepButton_Click(object sender, RoutedEventArgs e)
        {
            ListOfSteps.Add(new AutomationStep("type add button"));
        }
    }
}
