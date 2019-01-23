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
    public partial class UserPromptImageDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        private string promptText;
        private ImageSource picture;

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

        public ImageSource Picture
        {
            get { return this.picture; }
            set
            {
                this.picture = value;
                OnPropertyChanged("Picture");
            }
        }
        #endregion

        public UserPromptImageDialog()
        {
            InitializeComponent();
            DataContext = this;
        }

        public UserPromptImageDialog(string title, string prompt, ImageSource pic)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            PromptText = prompt;
            Picture = pic;
        }

        public UserPromptImageDialog(string title, string prompt, string bitmapFilePath)
        {
            InitializeComponent();
            DataContext = this;

            Title = title;
            PromptText = prompt;

            BitmapImage bi = new BitmapImage();
            // BitmapImage.UriSource must be in a BeginInit/EndInit block.
            bi.BeginInit();
            bi.UriSource = new Uri(bitmapFilePath, UriKind.Absolute);
            bi.EndInit();
            // Set the image source.
            Picture = bi;
            
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
    }
}
