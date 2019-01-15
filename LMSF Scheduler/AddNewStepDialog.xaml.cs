using System;
using System.Collections.Generic;
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
using System.Windows.Shapes;

namespace LMSF_Scheduler
{
    /// <summary>
    /// Interaction logic for AddNewStepWindow.xaml
    /// </summary>
    public partial class AddNewStepWindow : Window
    {
        MainWindow mainWindow;

        public AddNewStepWindow(MainWindow mainWin)
        {
            mainWindow = mainWin;
            InitializeComponent();
        }

        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            //Set the newStepType equal to the string content of the selected radio button
            List<RadioButton> buttonList = new List<RadioButton>(new RadioButton[] { ovpRadioButton, waitRadioButton, getMetaRadioButton });
            foreach (RadioButton b in buttonList)
            {
                if (b.IsChecked == true)
                {
                    mainWindow.newStepType = (string)b.Content;
                }
            }
            
            // Dialog box accepted
            this.DialogResult = true;
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            // Dialog box canceled
            this.DialogResult = false;
        }
    }
}
