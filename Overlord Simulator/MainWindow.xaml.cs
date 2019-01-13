using System;
using System.IO;
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
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Overlord_Simulator
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();

            string[] args = App.commandLineArgs;

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
                

                foreach (string s in args)
                {

                }
            }

            
        }
    }
}
