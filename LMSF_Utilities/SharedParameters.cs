using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;

namespace LMSF_Utilities
{
    public static class SharedParameters
    {
        //Delimeter used in output files
        public static char Delimeter => ',';

        //Folder for LMSF Scheduler log files
        public static string LogFileFolderPath => "C:\\Shared Files\\LMSF Scheduler\\LogFiles\\";

        //Folders for shared data and metadata
        public static string WorklistFolderPath => "C:\\Shared Files\\Data\\";
        public static string MetadataFolderPath => "C:\\Shared Files\\MetaData Schema\\";
        public static string OverlordFolderPath => "C:\\Shared Files\\Overlord-Venus\\";

        //Media parameters
        public static string MediaFolderPath => MetadataFolderPath + "MediaList\\";
        public static string MediaFilePath => MediaFolderPath + "MediaList.csv";

        //Strain parameters
        public static string StrainFolderPath => MetadataFolderPath + "StrainList\\";
        public static string StrainFilePath => StrainFolderPath + "StrainList.csv";

        //Plasmid parameters
        public static string PlasmidFolderPath => MetadataFolderPath + "PlasmidList\\";
        public static string PlasmidFilePath => PlasmidFolderPath + "PlasmidList.csv";

        //Additive parameters
        public static string AdditiveFolderPath => MetadataFolderPath + "AdditiveList\\";
        public static string AdditiveFilePath => AdditiveFolderPath + "AdditiveList.csv";

        //Antibiotic parameters
        public static string AntibioticFolderPath => MetadataFolderPath + "AntibioticList\\";
        public static string AntibioticFilePath => AntibioticFolderPath + "AntibioticList.csv";

        //Project parameters
        public static string ProjectFolderPath => MetadataFolderPath + "ProjectList\\";
        public static string ProjectFilePath => ProjectFolderPath + "ProjectList.csv";

        //Units
        public static ObservableCollection<string> UnitsList => new ObservableCollection<string>() { "mmol/L", "umol/L", "mg/mL", "ug/mL", "ug/L", "%" };


        //Utility methods

        //Check to see if type exists
        public static bool IsValidMetaType(string metaType)
        {
            return (GetFilePath(metaType) != "");
        }
        
        private static ObservableCollection<MetaItem> GetMetaList(string metaType)
        {
            string filePath = GetFilePath(metaType);
            if (filePath is null)
            {
                return null;
            }
            string line;
            int counter = 0;
            ObservableCollection<MetaItem> outList = new ObservableCollection<MetaItem>();

            //System.IO.StreamReader file = new System.IO.StreamReader(filePath, System.Text.Encoding.UTF8);
            System.IO.StreamReader file = new System.IO.StreamReader(filePath);
            string[] lineStrings;
            string newID;
            int newNum;
            string newName;
            while ((line = file.ReadLine()) != null)
            {
                if (counter>0)
                {
                    lineStrings = line.Split(Delimeter);
                    newID = lineStrings[0].Trim('"');
                    newName = lineStrings[2].Trim('"');
                    if (Int32.TryParse(lineStrings[1], out newNum))
                    {
                        outList.Add(new MetaItem(newID, newNum, newName));
                    }
                    else
                    {
                        outList.Add(new MetaItem(newID, 0, newName));
                    }
                }
                counter++;
            }
            file.Close();

            return outList;
        }

        private static string GetFilePath(string metaType)
        {
            string filePath = "";

            switch (metaType)
            {
                case "media":
                    filePath = MediaFilePath;
                    break;
                case "strain":
                    filePath = StrainFilePath;
                    break;
                case "plasmid":
                    filePath = PlasmidFilePath;
                    break;
                case "additive":
                    filePath = AdditiveFilePath;
                    break;
                case "antibiotic":
                    filePath = AntibioticFilePath;
                    break;
                case "project":
                    filePath = ProjectFilePath;
                    break;
                default:
                    return null;
            }

            return filePath;
        }

        private static void SortMetaList(ObservableCollection<MetaItem> listToSort)
        {
            bool switched = true;
            int keyLoopLength = listToSort.Count - 1;
            int value1;
            int value2;

            while (switched)
            {
                switched = false;
                for (int i=0; i < keyLoopLength; i++)
                {
                    value1 = listToSort.ElementAt(i).TimesUsed;
                    value2 = listToSort.ElementAt(i+1).TimesUsed;
                    if (value2>value1)
                    {
                        switched = true;
                        listToSort.Move(i + 1, i);
                    }
                }
            }
        }

        //In SortAndSaveMetaList, the selectedIndex has its TimesUsed property incremented to account for it just being used again
        //    if selectedIndex < 0, the method does not increment, but just sorts and saves.
        private static void SortAndSaveMetaList(ObservableCollection<MetaItem> listToSort, string metaType, int selectedIndex)
        {
            string filePath = GetFilePath(metaType);
            if (filePath is null)
            {
                return;
            }

            if (selectedIndex >= 0)
            {
                listToSort.ElementAt(selectedIndex).TimesUsed += 1;
            }
            SortMetaList(listToSort);

            //get the header line with a read
            //System.IO.StreamReader readFile = new System.IO.StreamReader(filePath, System.Text.Encoding.UTF8);
            System.IO.StreamReader readFile = new System.IO.StreamReader(filePath);
            string headerLine = readFile.ReadLine();
            readFile.Close();

            //then re-Save the list...
            FileStream fs = new FileStream(filePath, FileMode.Create);
            //using (System.IO.StreamWriter file = new System.IO.StreamWriter(fs, System.Text.Encoding.UTF8))
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fs))
            {
                file.WriteLine(headerLine);
                foreach (MetaItem item in listToSort)
                {
                    file.WriteLine(item.SaveString());
                }
            }

        }

        //If the proposed new Identifier is valid, this method returns an empty string.
        private static string CheckNewIdentifier(string metaType, string newID)
        {
            string notValidReason = "";
            ObservableCollection<MetaItem> metaList = GetMetaList(metaType);

            foreach (MetaItem m in metaList)
            {
                if (newID == m.ShortID)
                {
                    notValidReason = "That identifier has already been used.";
                }
            }

            return notValidReason;
        }

        //GetExperimentId() gets both the experiment id and the XML save file path
        //    return string[0] = experimentId
        //    return string[1] = XML file path
        //    return string[2] = saveDirectory
        public static string[] GetExperimentId(string initialDir, string defaultId)
        {
            string experimentId = "";
            string xmlFilePath = "";
            string saveDirectory = "";

            if (initialDir=="")
            {
                initialDir = $"{WorklistFolderPath}{defaultId}\\";
            }

            if (!Directory.Exists(initialDir)) {
                Directory.CreateDirectory(initialDir);
            }

            GetExperimentIdDialog dlg = new GetExperimentIdDialog(initialDir, defaultId);

            // Open the dialog box modally and return concentration if dialog returns true (OK)
            if (dlg.ShowDialog() == true)
            {
                experimentId = dlg.ExperimentId;
                xmlFilePath = dlg.SaveFilePath;
                saveDirectory = Directory.GetParent(xmlFilePath).FullName;
            }

            return new string[] { experimentId, xmlFilePath, saveDirectory };
        }

        public static string GetMetaIdentifier(string metaType, string selectPrompt)
        {
            string metaID="";
            ObservableCollection<MetaItem> metaList = GetMetaList(metaType);

            string createNewText = "Create New " + ToTitleCase(metaType) + " Identifier";
            string selectTitle = "Select " + ToTitleCase(metaType);
            string defaultPrompt = "Select the " + metaType + " used for the method:";

            if ( (selectPrompt is null) || (selectPrompt == "") )
            {
                selectPrompt = defaultPrompt;
            }

            int metaIndex = metaList.Count;
            while (metaIndex > metaList.Count - 1)
            {
                metaList = GetMetaList(metaType);
                ObservableCollection<MetaItem> listPlusNew = new ObservableCollection<MetaItem>(metaList);
                listPlusNew.Add(new MetaItem(createNewText, 0, ""));

                // Instantiate the dialog box to select the Meta Identifier
                SelectMetaIdentDialog dlg = new SelectMetaIdentDialog();
                // Configure the dialog box
                dlg.ItemList = listPlusNew;
                dlg.SelectedIndex = -1;
                dlg.Title = selectTitle;
                dlg.PromptText = selectPrompt;
                // Open the dialog box modally set metaID = "" if it does not return true
                if (dlg.ShowDialog() != true)
                {
                    metaID = "";
                    metaIndex = -1;
                    //break here to exit from the while loop, leaving metaID = ""
                    //    calling code can handle blank identifer as appropriate
                    break;
                }
                else
                {
                    metaIndex = dlg.SelectedIndex;
                    metaID = dlg.SelectedItem.ShortID;
                    if (metaIndex == metaList.Count)
                    {
                        CreateNewMetaIdentifier(metaType);
                        //if (metaType == "media")
                        //{
                        //    //This CreateNew method is a bit different from the others
                        //    //CreateNewMediaIdentifier();
                        //}
                        //else
                        //{
                        //    CreateNewMetaIdentifier(metaType);
                        //}
                        
                    }
                }
            }

            //Re-sort the MetaList to keep most used identifiers at the top
            SortAndSaveMetaList(metaList, metaType, metaIndex);

            return metaID;
        }

        private static void CreateNewMetaIdentifier(string metaType)
        {
            string newIdent = "";
            string newLongName = "";
            string newNotes = "";
            string notValidReason = "Something unexpected happened in CreateNewMetaIdentifier().";
            ObservableCollection<MetaItem> metaList = GetMetaList(metaType);

            string titleText = "New " + ToTitleCase(metaType) + " Identifier";
            string promptText = "Enter new " + metaType + " short identifier:";
            string longPrompt = "Enter long name for new " + metaType + ":";

            while (notValidReason != "")
            {
                // Instantiate the dialog box
                NewMetaIdentDialog dlg = new NewMetaIdentDialog();
                // Configure the dialog box
                dlg.ItemList = metaList;
                dlg.Title = titleText;
                dlg.PromptText = promptText;
                // Open the dialog box modally and leave newIdent="" if it does not return true
                if (dlg.ShowDialog() == true)
                {
                    newIdent = dlg.NewIdent;
                    notValidReason = CheckNewIdentifier(metaType, newIdent);
                    if (notValidReason != "")
                    {
                        string messageTest = newIdent + "\n  is not a valid identifier.\n  " + notValidReason + "\n\n    Try again.";
                        MessageBox.Show(messageTest, "Identifier Not Valid", MessageBoxButton.OK, MessageBoxImage.Error);
                    }
                }
                else
                {
                    break;
                }
            }
            if (notValidReason=="")
            {
                //Custom dialog for long name
                // Instantiate the dialog box
                NewLongNameDialog dlg = new NewLongNameDialog();
                // Configure the dialog box
                dlg.Title = "Long Name";
                dlg.PromptText = longPrompt;
                if (dlg.ShowDialog() == true)
                {
                    newLongName = dlg.NewLongName;
                }
                else
                {
                    newLongName = "Long Name Place-Holder";
                }

                string parentMessage = "";
                switch (metaType)
                {
                    case "strain":
                        parentMessage = "To create a new strain definition, you will need to specify the parent strain, or 'none'.\n\nThe parent strain is the strain that the new strain was derived from (via knock -out or knock-in, etc.).";
                        break;
                    case "plasmid":
                        parentMessage = "To create a new plasmid definition, you will need to specify the parent plasmid, or 'none'.\n\nThe parent plasmid is the plasmid that the new plasmid was derived from.";
                        break;
                    case "media":
                        parentMessage = "To create a new media definition, you will need a list of the ingredients.\nFor each ingredient, you will also need the concentration and units.";
                        break;
                    default:
                        break;
                }

                if (parentMessage != "")
                {
                    MessageBox.Show(parentMessage, titleText, MessageBoxButton.OK, MessageBoxImage.Information);

                    if (metaType=="media")
                    {
                        GetAndSaveMediaIngredients(newIdent);
                    }
                    else
                    {
                        ObservableCollection<MetaItem> listPlusNone = new ObservableCollection<MetaItem>(metaList);
                        listPlusNone.Add(new MetaItem("none", 0, ""));

                        string selectTitle = "Select Parent " + ToTitleCase(metaType) + " For " + ToTitleCase(metaType) + " " + newIdent;
                        string selectPrompt = "Select the parent " + metaType + " for new " + metaType + ": " + newIdent;

                        // Instantiate the dialog box
                        SelectMetaIdentDialog dlg2 = new SelectMetaIdentDialog();
                        // Configure the dialog box
                        dlg2.ItemList = listPlusNone;
                        dlg2.SelectedIndex = -1;
                        dlg2.Title = selectTitle;
                        dlg2.PromptText = selectPrompt;
                        // Open the dialog box modally and set newIdent = "" if it does not returns true
                        if (dlg2.ShowDialog() != true)
                        {
                            newIdent = "";
                        }
                        else
                        {
                            //int parentIndex = dlg2.SelectedIndex;
                            string parentID = dlg2.SelectedItem.ShortID;

                            //Get notes for new strain/plasmid and save to new strain/plasmid file
                            string notesPrompt = "Enter Notes for New " + ToTitleCase(metaType) + ": ";
                            titleText = "New " + ToTitleCase(metaType) + " Notes";

                            string newDefinitionFilePath;
                            switch (metaType)
                            {
                                case "strain":
                                    newDefinitionFilePath = StrainFolderPath;
                                    break;
                                case "plsmid":
                                    newDefinitionFilePath = PlasmidFolderPath;
                                    break;
                                default:
                                    newDefinitionFilePath = MetadataFolderPath;
                                    break;
                            }
                            newDefinitionFilePath += newIdent + "-" + metaType + ".txt";
                            string newDefinitionString = ToTitleCase(metaType) + " identifier: \t" + newIdent;

                            notesPrompt += newIdent;

                            // Instantiate the dialog box
                            NotesDialog notesDlg = new NotesDialog();
                            // Configure the dialog box
                            notesDlg.Title = titleText;
                            notesDlg.PromptText = notesPrompt;
                            // Open the dialog box modally and abort if it does not returns true
                            if (notesDlg.ShowDialog() != true)
                            {
                                newNotes = "";
                            }
                            else
                            {
                                newNotes = notesDlg.Notes;
                            }

                            newDefinitionString += "\n\n";
                            newDefinitionString += "Parent identifier: \t" + parentID + "\n\n";
                            newDefinitionString += "Notes:\n" + newNotes + "\n";

                            //Write out newDefinitionString to text file (newDefinitionFilePath)
                            System.IO.File.WriteAllText(newDefinitionFilePath, newDefinitionString);
                        }
                    }
                    
                }
            }
            else
            {
                newIdent = "";
            }

            if (newIdent != "")
            {
                ObservableCollection<MetaItem> listPlusNew = new ObservableCollection<MetaItem>(metaList);
                listPlusNew.Add(new MetaItem(newIdent, 0, newLongName));

                SortAndSaveMetaList(listPlusNew, metaType, -1);
            }
        }

        private static void GetAndSaveMediaIngredients(string newIdent)
        {
            //Set up Empty list of ingredients
            ObservableCollection<MediaIngredient> ingredientsList = new ObservableCollection<MediaIngredient>();

            string newDefinitionFilePath = MediaFolderPath + newIdent + "-" + "media" + ".txt";

            //Custom dialog to enter media ingredients
            // Instantiate the dialog box
            MediaIngredientsDialog dlg = new MediaIngredientsDialog();
            // Configure the dialog box
            dlg.Title = "Define New Media Composition: " + newIdent;
            dlg.UnitsList = SharedParameters.UnitsList;
            dlg.SelectedUnits = dlg.UnitsList.First();

            if (dlg.ShowDialog() == true)
            {
                //Save media definition (list of ingredients)
                ingredientsList = dlg.IngredientsList;

                using (StreamWriter outputFile = new StreamWriter(newDefinitionFilePath, false))
                {
                    outputFile.WriteLine(MediaIngredient.HeaderString());
                    foreach (MediaIngredient line in ingredientsList)
                    {
                        outputFile.WriteLine(line.SaveString());
                    }
                }
            }
            else
            {
                //Don't do anything
            }
            
        }

        private static string ToTitleCase(string inString)
        {
            return char.ToUpper(inString[0]) + inString.Substring(1);
        }

        //IsValid() method copied from: https://docs.microsoft.com/en-us/dotnet/framework/wpf/app-development/dialog-boxes-overview
        // Validate all dependency objects in a window
        public static bool IsValid(DependencyObject node)
        {
            // Check if dependency object was passed
            if (node != null)
            {
                // Check if dependency object is valid.
                // NOTE: Validation.GetHasError works for controls that have validation rules attached 
                bool isValid = !Validation.GetHasError(node);
                if (!isValid)
                {
                    // If the dependency object is invalid, and it can receive the focus,
                    // set the focus
                    if (node is IInputElement) Keyboard.Focus((IInputElement)node);
                    return false;
                }
            }

            // If this dependency object is valid, check all child dependency objects
            foreach (object subnode in LogicalTreeHelper.GetChildren(node))
            {
                if (subnode is DependencyObject)
                {
                    // If a child dependency object is invalid, return false immediately,
                    // otherwise keep checking
                    if (IsValid((DependencyObject)subnode) == false) return false;
                }
            }

            // All dependency objects are valid
            return true;
        }

        public static Concentration GetAdditiveConcentration(string name, string prompt)
        {
            ConcentrationDialog dlg;

            string title = ToTitleCase(name) + " Concentration?";
            //Dialog for selecting conentration
            // Instantiate the dialog box; configuration done in constructor
            if (prompt == "")
            {
                dlg = new ConcentrationDialog(name);
            }
            else
            {
                dlg = new ConcentrationDialog(name, prompt, title);
            }
            

            // Open the dialog box modally and return concentration if dialog returns true (OK)
            if (dlg.ShowDialog() == true)
            {
                return new Concentration(dlg.ConcDouble, dlg.SelectedUnits);
            }
            else
            {
                return null;
            }

                
        }

        public static bool? ShowPrompt(string messageText, string title)
        {
            UserPromptDialog dlg = new UserPromptDialog(title, messageText);

            return dlg.ShowDialog();
        }

        public static bool? ShowPrompt(string messageText, string title, string bitmapFilePath)
        {
            UserPromptImageDialog dlg = new UserPromptImageDialog(title, messageText, bitmapFilePath);

            return dlg.ShowDialog();
        }

        //For consistent formatting of DateTime strings:
        public static string GetDateTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }

        //from: https://stackoverflow.com/questions/2641326/finding-all-positions-of-substring-in-a-larger-string-in-c-sharp
        public static List<int> AllIndexesOf(this string str, string value)
        {
            if (String.IsNullOrEmpty(value))
                throw new ArgumentException("the string to find may not be empty", "value");
            List<int> indexes = new List<int>();
            for (int index = 0; ; index += value.Length)
            {
                index = str.IndexOf(value, index);
                if (index == -1)
                    return indexes;
                indexes.Add(index);
            }
        }

    }
}
