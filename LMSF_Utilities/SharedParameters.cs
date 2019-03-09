using System;
using System.IO;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Text.RegularExpressions;
using System.Xml;

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
        public static string HamiltonFolderPath => "C:\\Program Files (x86)\\HAMILTON\\LMSF_FrontEnd\\";

        //XML file for saving metaLists
        public static string MetaIdFilePath => MetadataFolderPath + "MetaIdLists.xml";

        //User parameters
        public static string UserFolderPath => MetadataFolderPath + "UserList\\";
        public static string UserFilePath => UserFolderPath + "UserList.csv";

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

        // enumeration for reporting server status
        public enum ServerStatusStates { Idle, Busy };


        //Utility methods

        //Check to see if type exists
        public static bool IsValidMetaType(string metaType)
        {
            return GetMetaTypeList().Contains(metaType);
        }

        public static List<string> GetMetaTypeList()
        {
            List<string> typeList = new List<string>();

            typeList.Add("user");
            typeList.Add("media");
            typeList.Add("strain");
            typeList.Add("plasmid");
            typeList.Add("additive");
            typeList.Add("antibiotic");
            typeList.Add("project");

            return typeList;
        }

        private static void StartNewMetaList(string metaType, string filePath)
        {
            string headerLine = $"{metaType}_identifier,times_used,long_name";

            //create directroy if necessary
            string dir = Path.GetDirectoryName(filePath);
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            //then save the file with just the header line
            FileStream fs = new FileStream(filePath, FileMode.Create);
            using (System.IO.StreamWriter file = new System.IO.StreamWriter(fs))
            {
                file.WriteLine(headerLine);
            }
        }

        private static ObservableCollection<MetaItem> GetMetaList(string metaType)
        {
            ObservableCollection<MetaItem> outList = new ObservableCollection<MetaItem>();

            //Open the metaList.xml document (MetaIdFilePath), if it exists
            XmlNode rootNode;
            XmlDocument xmlDoc = new XmlDocument(); ;
            if (File.Exists(MetaIdFilePath))
            {
                //Load existing XML document if it exists
                xmlDoc.Load(MetaIdFilePath);
                rootNode = xmlDoc.SelectSingleNode("metadata");

                XmlNodeList detailNodeList;

                string detailNodeStr = metaType;
                string key = "";
                if (metaType == "media")
                {
                    detailNodeStr = "medium";
                }
                if (metaType == "antibiotic")
                {
                    detailNodeStr = "additive";
                    key = "antibiotic";

                    detailNodeList = rootNode.SelectNodes($"descendant::{detailNodeStr}[@useKey = \"{key}\"]");
                }
                else
                {
                    detailNodeList = rootNode.SelectNodes($"descendant::{detailNodeStr}");
                }

                foreach (XmlNode node in detailNodeList)
                {
                    outList.Add(new MetaItem(node, metaType));
                }
            }

            SortMetaList(outList);

            return outList;
        }

        private static string GetMetaFilePath(string metaType)
        {
            string filePath = "";

            switch (metaType)
            {
                case "user":
                    filePath = UserFilePath;
                    break;
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

        //In SaveMetaList, the selectedIndex has its TimesUsed property incremented to account for it just being used again
        //    if selectedIndex < 0, the method does not increment, but just sorts and saves.
        private static void SaveMetaList(ObservableCollection<MetaItem> listToSort, string metaType, int selectedIndex)
        {
            if (selectedIndex >= 0)
            {
                listToSort.ElementAt(selectedIndex).TimesUsed += 1;
            }
            
            //Save or append the list to the metaList.xml document (MetaIdFilePath)
            XmlNode rootNode;
            XmlDocument xmlDoc = new XmlDocument(); ;
            if (File.Exists(MetaIdFilePath))
            {
                //Load existing XML document if it exists
                xmlDoc.Load(MetaIdFilePath);
                rootNode = xmlDoc.SelectSingleNode("metadata");
            }
            else
            {
                //create and configure the root node
                rootNode = xmlDoc.CreateElement("metadata");
                XmlAttribute sourceAtt = xmlDoc.CreateAttribute("source");
                sourceAtt.Value = "NIST LMSF";
                rootNode.Attributes.Append(sourceAtt);
                //add the root node to the document
                xmlDoc.AppendChild(rootNode);
            }

            XmlNode baseNode;
            //XmlNode detailNode;
            XmlElement detailNode;
            XmlNode idNode;
            XmlNode longNameNode;
            XmlNode timesUsedNode;

            string baseNodeStr = $"{metaType}s";
            string detailNodeStr = metaType;
            string idNodeStr = $"{metaType}Id";
            string longNameNodeStr = $"{metaType}LongName";
            string timesUsedNodeStr = $"timesUsed";
            string key = "";
            if (metaType == "media")
            {
                baseNodeStr = "media";
                detailNodeStr = "medium";
                idNodeStr = "mediaId";
                longNameNodeStr = "mediaLongName";
            }
            if (metaType == "antibiotic")
            {
                baseNodeStr = "additives";
                detailNodeStr = "additive";
                idNodeStr = "additiveId";
                longNameNodeStr = "additiveLongName";
                key = "antibiotic";
            }

            //Adds metadata to the rootNode
            //look for the base node and append to it or create it if it does not exist
            XmlNodeList baseNodeList = rootNode.SelectNodes($"descendant::{baseNodeStr}");
            if (baseNodeList.Count > 0)
            {
                baseNode = baseNodeList.Item(baseNodeList.Count - 1);
            }
            else
            {
                baseNode = xmlDoc.CreateElement(baseNodeStr);
                rootNode.AppendChild(baseNode);
            }

            //Then, for each item in the list, create if it is not already there;
            //   otherwise append the detail node to the baseNode
            string iDString;
            string longName;
            string timesUsed;
            foreach (MetaItem item in listToSort)
            {
                iDString = item.ShortID;
                longName = item.Name;
                timesUsed = item.TimesUsed.ToString();

                XmlNodeList detailNodeList = baseNode.SelectNodes($"descendant::{detailNodeStr}[{idNodeStr}='{iDString}']");
                if (detailNodeList.Count > 0)
                {
                    detailNode = (XmlElement)detailNodeList.Item(detailNodeList.Count - 1);

                    //if the detailNode already exists, just update the timesUsedNode
                    timesUsedNode = detailNode.SelectSingleNode(timesUsedNodeStr);
                    timesUsedNode.InnerText = timesUsed;
                }
                else
                {
                    detailNode = xmlDoc.CreateElement(detailNodeStr);
                    baseNode.AppendChild(detailNode);

                    //Then add idNode, longNameNode, and timesUsedNode
                    idNode = xmlDoc.CreateElement(idNodeStr);
                    idNode.InnerText = iDString;
                    detailNode.AppendChild(idNode);
                    longNameNode = xmlDoc.CreateElement(longNameNodeStr);
                    longNameNode.InnerText = longName;
                    detailNode.AppendChild(longNameNode);
                    timesUsedNode = xmlDoc.CreateElement(timesUsedNodeStr);
                    timesUsedNode.InnerText = timesUsed;
                    detailNode.AppendChild(timesUsedNode);
                }
                //  Set attribute useKey if key!=""
                if (key != "")
                {
                    detailNode.SetAttribute("useKey", key);
                }
            }

            //Finally, save teh XML document
            xmlDoc.Save(MetaIdFilePath);
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

        //XmlDocForAppend() gets both the experiment id and the XML save file path
        //    return string[0] = experimentId
        //    return string[1] = XML file path
        //    return string[2] = saveDirectory
        public static string[] XmlDocForAppend(string initialDir)
        {
            string experimentId = "";
            string xmlFilePath = "";
            string saveDirectory = "";

            if (initialDir == "" || !Directory.Exists(initialDir))
            {
                initialDir = WorklistFolderPath;
            }

            string[] fileList = Directory.GetFiles(initialDir, "*.xml", SearchOption.AllDirectories);
            if (fileList.Length == 0)
            {
                //if there are no .xml files to append to, return empty strings, which will result in an abort
                return new string[] { experimentId, xmlFilePath, saveDirectory };
            }
            DateTime latestTime = new DateTime(1, 1, 1);
            string latestFile = fileList[0];
            foreach (string file in fileList)
            {
                if (File.GetLastWriteTime(file) > latestTime)
                {
                    latestFile = file;
                    latestTime = File.GetLastWriteTime(file);
                }
            }

            string defaultId = Directory.GetParent(latestFile).Name;
            initialDir = Directory.GetParent(latestFile).FullName;
            
            AppendExperimentIdDialog dlg = new AppendExperimentIdDialog(initialDir, defaultId);

            // Open the dialog box modally and get file/directroy info if 'OK'
            if (dlg.ShowDialog() == true)
            {
                experimentId = dlg.ExperimentId;
                xmlFilePath = dlg.SaveFilePath;
                saveDirectory = Directory.GetParent(xmlFilePath).FullName;
            }

            //saveDirectory = saveDirectory.Replace(@"\", @"\\");

            return new string[] { experimentId, xmlFilePath, saveDirectory };
        }

        //GetExperimentId() gets both the experiment id and the XML save file path
        //    return string[0] = experimentId
        //    return string[1] = XML file path
        //    return string[2] = saveDirectory
        public static string[] GetExperimentId(string initialDir, string defaultId, string projId = "")
        {
            string experimentId = "";
            string xmlFilePath = "";
            string saveDirectory = "";
            bool dirCreated = false;

            if (initialDir == "")
            {
                if (projId == "")
                {
                    initialDir = $"{WorklistFolderPath}{defaultId}\\";
                }
                else
                {
                    //If a projectID is given, use it in the default filePath,
                    initialDir = $"{WorklistFolderPath}{projId}\\{defaultId}\\";
                }
            }

            if (!Directory.Exists(initialDir))
            {
                Directory.CreateDirectory(initialDir);
                dirCreated = true;
            }

            GetExperimentIdDialog dlg = new GetExperimentIdDialog(initialDir, defaultId);

            // Open the dialog box modally and return concentration if dialog returns true (OK)
            if (dlg.ShowDialog() == true)
            {
                experimentId = dlg.ExperimentId;
                xmlFilePath = dlg.SaveFilePath;
                saveDirectory = Directory.GetParent(xmlFilePath).FullName;

                if (dirCreated && (Path.GetFullPath(saveDirectory).TrimEnd('\\') != Path.GetFullPath(initialDir).TrimEnd('\\')))
                {
                    if (Directory.Exists(initialDir))
                    {
                        try
                        {
                            Directory.Delete(initialDir);
                        }
                        catch (IOException e)
                        {
                            //do nothing
                        }
                    }
                }
            }
            else
            {
                if (dirCreated)
                {
                    if (Directory.Exists(initialDir))
                    {
                        try
                        {
                            Directory.Delete(initialDir);
                        }
                        catch (IOException e)
                        {
                            //do nothing
                        }
                    }
                }
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
                listPlusNew.Add(new MetaItem(createNewText, 0, "", metaType));

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
            SaveMetaList(metaList, metaType, metaIndex);

            return metaID;
        }

        public static string FindMostRecentDirectory(string fileFilter, string initialDir = "")
        {
            //First auto-find an initial directory, if one is not given
            if (initialDir == "" || !Directory.Exists(initialDir))
            {
                initialDir = WorklistFolderPath;
            }
            //looking for directory with most recently modified files that match the fileFilter
            List<string> patternList = new List<string>();
            if (fileFilter != "" && fileFilter.Contains("|"))
            {
                string[] splitFilter = fileFilter.Split(new char[] { '|', ';' });

                foreach (string s in splitFilter)
                {
                    if (s.StartsWith("*."))
                    {
                        patternList.Add(s);
                    }
                }
            }
            else
            {
                patternList.Add("*.*");
            }
            List<string> fileList = new List<string>();
            foreach (string pattern in patternList)
            {
                string[] files = Directory.GetFiles(initialDir, pattern, SearchOption.AllDirectories);
                foreach (string f in files)
                {
                    fileList.Add(f);
                }
            }

            if (fileList.Count == 0)
            {
                initialDir = WorklistFolderPath;
            }
            else
            {
                DateTime latestTime = new DateTime(1, 1, 1);
                string latestFile = fileList[0];
                foreach (string file in fileList)
                {
                    if (File.GetLastWriteTime(file) > latestTime)
                    {
                        latestFile = file;
                        latestTime = File.GetLastWriteTime(file);
                    }
                }
                initialDir = Directory.GetParent(latestFile).FullName;
            }

            return initialDir;
        }

        public static string GetFile(string filePrompt, string fileFilter, string initialDir = "")
        {
            //examples for fileFilter: 
            //    "XML documents (.xml)|*.xml"
            //    "Office Files|*.doc;*.xls;*.ppt"
            //    "Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt"
            string retFile = "";

            //First auto-find an initial directory, if one is not given
            if (initialDir == "" || !Directory.Exists(initialDir))
            {
                initialDir = WorklistFolderPath;
            }
            
            // Instantiate the dialog box
            ChooseFileDialog dlg = new ChooseFileDialog(initialDir, fileFilter);
            // Configure the dialog box
            dlg.PromptText = filePrompt;

            // Open the dialog box modally and abort if it does not returns true
            if (dlg.ShowDialog() == true)
            {
                retFile = dlg.ChooseFilePath;
            }
            else
            {
                retFile = "";
            }

            //retFile = retFile.Replace(@"\", @"\\");

            return retFile;
        }

        public static string GetNotes(string titleText, string notesPrompt)
        {
            string newNotes = "";

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

            return newNotes;
        }

        private static void CreateNewMetaIdentifier(string metaType)
        {
            string newIdent = "";
            string newLongName = "";
            string newNotes = "";
            string notValidReason = "Something unexpected happened in CreateNewMetaIdentifier().";
            string parentMessage = "";
            string parentID = "";
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
                        listPlusNone.Add(new MetaItem("none", 0, "", metaType));

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
                            parentID = dlg2.SelectedItem.ShortID;

                            //Get notes for new strain/plasmid and save to new strain/plasmid file
                            string notesPrompt = "Enter Notes for New " + ToTitleCase(metaType) + ": ";
                            titleText = "New " + ToTitleCase(metaType) + " Notes";

                            string newDefinitionFilePath;
                            switch (metaType)
                            {
                                case "strain":
                                    newDefinitionFilePath = StrainFolderPath;
                                    break;
                                case "plasmid":
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
                listPlusNew.Add(new MetaItem(newIdent, 0, newLongName, metaType));

                SaveMetaList(listPlusNew, metaType, -1);

                //Add parentId and notes to XML doc here (if there is any)
                if ((parentMessage != "") && (metaType != "media"))
                {
                    //Append the new nodes to the metaList.xml document (MetaIdFilePath)
                    XmlNode rootNode;
                    XmlDocument xmlDoc = new XmlDocument(); ;
                    if (File.Exists(MetaIdFilePath))
                    {
                        //Load existing XML document if it exists
                        xmlDoc.Load(MetaIdFilePath);
                        rootNode = xmlDoc.SelectSingleNode("metadata");
                    }
                    else
                    {
                        //create and configure the root node
                        rootNode = xmlDoc.CreateElement("metadata");
                        XmlAttribute sourceAtt = xmlDoc.CreateAttribute("source");
                        sourceAtt.Value = "NIST LMSF";
                        rootNode.Attributes.Append(sourceAtt);
                        //add the root node to the document
                        xmlDoc.AppendChild(rootNode);
                    }

                    XmlNode baseNode;
                    //XmlNode detailNode;
                    XmlNode detailNode;
                    XmlNode idNode;
                    string baseNodeStr = $"{metaType}s";
                    string detailNodeStr = metaType;
                    string idNodeStr = $"{metaType}Id";

                    //look for the base node and append to it or create it if it does not exist
                    XmlNodeList baseNodeList = rootNode.SelectNodes($"descendant::{baseNodeStr}");
                    if (baseNodeList.Count > 0)
                    {
                        baseNode = baseNodeList.Item(baseNodeList.Count - 1);
                    }
                    else
                    {
                        baseNode = xmlDoc.CreateElement(baseNodeStr);
                        rootNode.AppendChild(baseNode);
                    }

                    //Then find the detail node with the correct idNode
                    XmlNodeList detailNodeList = baseNode.SelectNodes($"descendant::{detailNodeStr}[{idNodeStr}='{newIdent}']");
                    if (detailNodeList.Count > 0)
                    {
                        detailNode = detailNodeList.Item(detailNodeList.Count - 1);

                        //if the detailNode exists, append the parentId and notes to the detailNode
                        XmlNode parentNode = xmlDoc.CreateElement("parentId");
                        parentNode.InnerText = parentID;
                        detailNode.AppendChild(parentNode);
                        if (newNotes !="")
                        {
                            XmlNode noteNode = xmlDoc.CreateElement("note");
                            noteNode.InnerText = newNotes;
                            detailNode.AppendChild(noteNode);
                        }
                    }
                    else
                    {
                        //if there is no matching idNode, then something is wrong...
                    }
                    //Save the XML doc
                    xmlDoc.Save(MetaIdFilePath);
                }
                
            }
        }

        public static string GetNumber(string prompt, bool isInteger = false)
        {
            string numStr = "";
            NumericInputDialog dlg;
            if (isInteger)
            {
                dlg = new NumericInputDialog(true);
            }
            else
            {
                dlg = new NumericInputDialog();
            }
            // Configure the dialog box
            //dlg.Title = "Long Name";
            dlg.PromptText = prompt;
            if (dlg.ShowDialog() == true)
            {
                numStr = dlg.NumString;
            }

            return numStr;
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

        public static string ToTitleCase(string inString)
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

        public static YesNoDialog.Response ShowYesNoDialog(string messageText, string title)
        {
            YesNoDialog dlg = new YesNoDialog(title, messageText);
            
            bool? dlgRetunr = dlg.ShowDialog();
            if (dlgRetunr == true)
            {
                return dlg.UserResponse;
            }
            else
            {
                return YesNoDialog.Response.No;
            }
        }

        public static AbortAppendOverwriteDialog.Response ShowAbortAppendOverwrite(string messageText, string title)
        {
            AbortAppendOverwriteDialog dlg = new AbortAppendOverwriteDialog(title, messageText);

            //int numLines = messageText.Split('\n').Length;

            //double height = (numLines + 1) * 21 + 175;

            //dlg.Height = height;

            bool? dlgRetunr = dlg.ShowDialog();
            if (dlgRetunr == true)
            {
                return dlg.UserResponse;
            }
            else
            {
                return AbortAppendOverwriteDialog.Response.Abort;
            }
        }

        public static bool? ShowPrompt(string messageText, string title)
        {
            UserPromptDialog dlg = new UserPromptDialog(title, messageText);

            //int numLines = messageText.Split('\n').Length;

            //double height = (numLines + 1) * 21 + 175;

            //dlg.Height = height;

            return dlg.ShowDialog();
        }

        public static bool? ShowPrompt(string messageText, string title, string bitmapFilePath)
        {
            UserPromptImageDialog dlg = new UserPromptImageDialog(title, messageText, bitmapFilePath);

            //int numLines = messageText.Split('\n').Length;

            //double height = (numLines + 1) * 21 + 625;

            //dlg.Height = height;

            return dlg.ShowDialog();
        }

        public static bool? ShowStartDialog(string title, string listFilePath)
        {
            ProtocolStartDialog dlg = new ProtocolStartDialog(title, listFilePath);

            return dlg.ShowDialog();
        }

        //For consistent formatting of DateTime strings:
        public static string GetDateTimeString()
        {
            return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
        }
        public static string GetDateTimeString(bool isForExpId)
        {
            if (isForExpId)
            {
                return DateTime.Now.ToString("yyyy-MM-dd-HHmm");
            }
            else
            {
                return DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }
        public static string GetDateTimeString(DateTime dt, bool isForExpId)
        {
            if (isForExpId)
            {
                return dt.ToString("yyyy-MM-dd-HHmm");
            }
            else
            {
                return dt.ToString("yyyy-MM-dd HH:mm:ss");
            }
        }

        public static string GetDateString(DateTime dt)
        {
            return dt.ToString("yyyy-MM-dd");
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

        public static async void SleepWithUiUpdates(int milliSecs)
        {
            bool isDone = false;
            await Task.Run(() => RunSleep());

            while (!isDone)
            {
                Thread.Sleep(20);
            }

            void RunSleep()
            {
                Thread.Sleep(milliSecs);
                isDone = true;
            }
        }

        //From: https://stackoverflow.com/questions/1410127/c-sharp-test-if-user-has-write-access-to-a-folder
        public static bool IsDirectoryWritable(string dirPath, bool throwIfFails = false)
        {
            try
            {
                using (FileStream fs = File.Create(
                    Path.Combine(
                        dirPath,
                        Path.GetRandomFileName()
                    ),
                    1,
                    FileOptions.DeleteOnClose)
                )
                { }
                return true;
            }
            catch
            {
                if (throwIfFails)
                    throw;
                else
                    return false;
            }
        }
    }
}
