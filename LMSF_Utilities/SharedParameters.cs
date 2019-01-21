using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

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
            string filePath = "";
            string line;
            int counter = 0;
            ObservableCollection<MetaItem> outList = new ObservableCollection<MetaItem>();

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

            System.IO.StreamReader file = new System.IO.StreamReader(filePath, System.Text.Encoding.UTF8);
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

    }
}
