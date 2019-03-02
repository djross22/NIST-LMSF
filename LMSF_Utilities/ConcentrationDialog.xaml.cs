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
    public partial class ConcentrationDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        //Note that ItemList handles notifications on its own (included with ObservableCollection class)
        //    But the other fields have Property wrappers with set methods that handle the notification.
        //    This is necessary to get data bindings to work properly with the GUI
        public ObservableCollection<string> UnitsList { get; set; }
        private string promptText;
        private string ingredientName;
        //in this dialog, the concentration is a string, making sure it is a valid number is handled by the NumberValidationRule
        private string concString;
        private string selectedUnits;

        public double ConcDouble { get; set; }

        #region Properties Getters and Setters
        public string SelectedUnits
        {
            get { return this.selectedUnits; }
            set
            {
                this.selectedUnits = value;
                OnPropertyChanged("SelectedUnits");
            }
        }

        public string ConcString
        {
            get { return this.concString; }
            set
            {
                this.concString = value;
                OnPropertyChanged("ConcString");
            }
        }

        public string PromptText
        {
            get { return this.promptText; }
            set
            {
                this.promptText = value;
                OnPropertyChanged("PromptText");
            }
        }

        public string IngredientName
        {
            get { return this.ingredientName; }
            set
            {
                this.ingredientName = value;
                OnPropertyChanged("IngredientName");
            }
        }
        #endregion

        public ConcentrationDialog()
        {
            InitializeComponent();
            DataContext = this;

            IngredientName = "additive";
            PromptText = "Enter concentration and units for: " + IngredientName;
            Title = "Concentration?";

            UnitsList = SharedParameters.UnitsList;
        }

        public ConcentrationDialog(string name)
        {
            InitializeComponent();
            DataContext = this;

            IngredientName = name;
            PromptText = "Enter concentration and units for: " + IngredientName;
            Title = "Concentration?";

            UnitsList = SharedParameters.UnitsList;
        }

        public ConcentrationDialog(string name, string prompt, string title)
        {
            InitializeComponent();
            DataContext = this;

            IngredientName = name;
            PromptText = prompt;
            Title = title;

            UnitsList = SharedParameters.UnitsList;
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            this.DialogResult = false;
        }
        
        private void OkButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedParameters.IsValid(this) && !(ConcString is null))
            {
                if ((SelectedUnits != null) && (SelectedUnits != ""))
                {
                    double doubVal;
                    if (double.TryParse(ConcString, out doubVal))
                    {
                        ConcDouble = doubVal;
                        this.DialogResult = true;
                    }
                    else
                    {
                        return;
                    }
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }
    }
}
