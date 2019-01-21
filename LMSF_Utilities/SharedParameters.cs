using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;

namespace LMSF_Utilities
{
    public static class SharedParameters
    {
        //Delimeter used in output files
        public static char Delimeter => ',';

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

        //Units
        public static ObservableCollection<string> UnitsList => new ObservableCollection<string>() { "mmol/L", "umol/L", "mg/mL", "ug/mL", "ug/L", "%" };

        public static ObservableCollection<MetaItem> GetMetaList(string metaType)
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
                default:
                    return null;
            }

            return filePath;
        }

        public static void SortMetaList(ObservableCollection<MetaItem> listToSort)
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

        public static void SortAndSaveMetaList(ObservableCollection<MetaItem> listToSort, string metaType, int selectedIndex)
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

        public static string GetMetaIdentifier(string metaType, string selectPrompt)
        {
            string metaID="";
            ObservableCollection<MetaItem> metaList = GetMetaList(metaType);

            string createNewText = "Create New " + char.ToUpper(metaType[0]) + metaType.Substring(1) + " Identifier";
            string selectTitle = "Select " + char.ToUpper(metaType[0]) + metaType.Substring(1);
            string defaultPrompt = "Select the " + metaType + " used for the method:";

            if ( (selectPrompt is null) || (selectPrompt == "") )
            {
                selectPrompt = defaultPrompt;
            }

            int metaIndex = metaList.Count;
            while (metaIndex > metaList.Count - 1)
            {
                ObservableCollection<MetaItem> listPlusNew = new ObservableCollection<MetaItem>(metaList);
                listPlusNew.Add(new MetaItem(createNewText, 0, ""));

                // Instantiate the dialog box
                SelectMetaIdentDialog dlg = new SelectMetaIdentDialog();
                // Configure the dialog box
                dlg.ItemList = listPlusNew;
                dlg.Title = selectTitle;
                dlg.PromptText = selectPrompt;
                // Open the dialog box modally and abort if it does not returns true
                if (dlg.ShowDialog() != true)
                {
                    //TODO: abort the experiment in the Scheduler
                }
                metaIndex = dlg.SelectedIndex;
                metaID = dlg.SelectedItem.ShortID;
                if (metaIndex>metaList.Count)
                {
                    if (metaType=="media")
                    {
                        //This CreateNew method is a bit different from the others
                        //CreateNewMediaIdentifier();
                    }
                    else
                    {
                        //CreateNewMetaIdentifier(metaType);
                    }
                    metaList = GetMetaList(metaType);
                }
            }

            return metaID;
        }
    }
}
