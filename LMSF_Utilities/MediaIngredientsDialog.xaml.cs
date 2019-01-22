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
    public partial class MediaIngredientsDialog : Window, INotifyPropertyChanged
    {
        //Property change notification event required for INotifyPropertyChanged
        public event PropertyChangedEventHandler PropertyChanged;

        //Note that ItemList handles notifications on its own (included with ObservableCollection class)
        //    But the other fields have Property wrappers with set methods that handle the notification.
        //    This is necessary to get data bindings to work properly with the GUI
        public ObservableCollection<string> UnitsList { get; set; }
        public ObservableCollection<MediaIngredient> IngredientsList { get; set; }
        private string promptText;
        private string ingredientName;
        //in this dialog, the concentration is a string, making sure it is a valid number is handled by the NumberValidationRule
        private string concentration;
        private string selectedUnits;

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

        public string Concentration
        {
            get { return this.concentration; }
            set
            {
                this.concentration = value;
                OnPropertyChanged("Concentration");
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

        public MediaIngredientsDialog()
        {
            InitializeComponent();
            DataContext = this;

            IngredientsList = new ObservableCollection<MediaIngredient>();
        }

        protected void OnPropertyChanged(string name)
        {
            if (PropertyChanged != null)
            {
                PropertyChanged(this, new PropertyChangedEventArgs(name));
            }
        }

        private void DoneButton_Click(object sender, RoutedEventArgs e)
        {
            if (SharedParameters.IsValid(this) && (MessageBox.Show("Click 'OK' to save new media definition, or 'Cancel' to enter more ingredients.", "Save Media Definition?", MessageBoxButton.OKCancel) == MessageBoxResult.OK) )
            {
                this.DialogResult = true;
            }
            else
            {
                return;
            }
        }

        private void CancelButton_Click(object sender, RoutedEventArgs e)
        {
            //TODO: decide what to do if user Cancels, probably promprt for Ok,and move on without saving
            if (MessageBox.Show("Click 'OK' to continue without saving new media definition, or 'Cancel' to enter more ingredients.", "Cancel Save Media Definiiton?", MessageBoxButton.OKCancel) == MessageBoxResult.OK)
            {
                this.DialogResult = false;
            }
        }

        private void NextButton_Click(object sender, RoutedEventArgs e)
        {
            //MessageBox.Show(IngredientName);
            if (SharedParameters.IsValid(this) && !(IngredientName is null) && !(Concentration is null) )
            {
                IngredientsList.Add(new MediaIngredient(IngredientName, double.Parse(Concentration), SelectedUnits));
            }
            else
            {
                return;
            }
        }
    }
}
