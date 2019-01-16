﻿using System;
using System.IO;
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
using Microsoft.Win32;

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
        private string experimentFileName ="";

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

        private void NewMenuItem_Click(object sender, RoutedEventArgs e)
        {
            testTextBox.Text = "New...";
        }

        private void OpenMenuItme_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Text file (*.txt)|*.txt";
            if (openFileDialog.ShowDialog() == true)
            {
                InputText = File.ReadAllText(openFileDialog.FileName);
            }
                
        }

        private void SaveMenuItem_Click(object sender, RoutedEventArgs e)
        {
            if (experimentFileName!="")
            {
                File.WriteAllText(experimentFileName, InputText);
            }
            else
            {
                SaveAs();
            }
        }

        private void SaveAsMenuItem_Click(object sender, RoutedEventArgs e)
        {
            SaveAs();
        }

        private void SaveAs()
        {
            SaveFileDialog saveFileDialog = new SaveFileDialog();
            saveFileDialog.Filter = "Text file (*.txt)|*.txt";
            if (saveFileDialog.ShowDialog() == true)
            {
                File.WriteAllText(saveFileDialog.FileName, InputText);
                experimentFileName = saveFileDialog.FileName;
            }
        }

        private void ExitMenuItem_Click(object sender, RoutedEventArgs e)
        {

        }

        private void InsertFileButton_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Title = "Select File Path to Insert";
            if (openFileDialog.ShowDialog() == true)
            {
                InputText += openFileDialog.FileName;
            }
        }

        private void StartButton_Click(object sender, RoutedEventArgs e)
        {
            OutputText = InputText;
        }
    }
}
