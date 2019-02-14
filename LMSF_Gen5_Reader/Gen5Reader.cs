using System;
using System.Windows;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Gen5;
using System.Threading;
using System.ComponentModel;

namespace LMSF_Gen5_Reader
{
    public class Gen5Reader
    {
        public Gen5.Application Gen5App { get; private set; }
        private Gen5.Experiment experiment;
        private Gen5.Plates plates;
        private Gen5.Plate plate;
        private Gen5.PlateReadMonitor plateReadMonitor;

        public string ExperimentID { get; set; }
        public string ProtocolPath { get; set; }
        public string ExperimentPath { get; set; }
        public string ExportFilePath { get; set; }
        public string ExperimentFolderPath { get; set; }
        public IReaderTextOut gen5Window { get; set; }

        public Gen5Reader(IReaderTextOut win)
        {
            gen5Window = win;
        }

        public bool IsReading { get; private set; }

        //===========================================================================================
        // Start Gen5
        //===========================================================================================
        public string StartGen5()
        {
            string retStr = "Running StartGen5\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before starting Gen5.\n";
                return retStr;
            }

            try
            {
                Gen5App = new Gen5.Application();
                retStr += "Gen5App created Successfully\n";
                IsReading = false;
            }
            catch (COMException exception)
            {
                retStr += $"StartGen5 Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // Terminate Gen5
        //===========================================================================================
        public string TerminateGen5()
        {
            string retStr = "Running TerminateGen5\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before terminating Gen5.\n";
                return retStr;
            }

            plateReadMonitor = null;
            plates = null;
            plate = null;
            experiment = null;
            Gen5App = null;

            //perform garbage collection
            GC.Collect();

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // Check if Gen5 App is active or null
        //===========================================================================================
        public bool IsGen5Active()
        {
            return !(Gen5App is null);
        }

        //===========================================================================================
        // NewExperiment
        //===========================================================================================
        public string NewExperiment()
        {
            string retStr = "Running NewExperiment\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before creating new experiment.\n";
                return retStr;
            }

            if (Gen5App == null)
            {

                retStr += "Gen5App is null\n";
                return retStr;
            }

            try
            {
                experiment = (Gen5.Experiment)Gen5App.NewExperiment(ProtocolPath);
                plates = (Gen5.Plates)experiment.Plates;
                retStr += "NewExperiment successful\n";
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                retStr += $"NewExperiment Failed, {exception}.\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // NewExperiment
        //===========================================================================================
        public string NewExperiment(string protocolPath)
        {
            string retStr = "Running NewExperiment\n";
            ProtocolPath = protocolPath;

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before creating new experiment.\n";
                return retStr;
            }

            if (Gen5App == null)
            {

                retStr += "Gen5App is null\n";
                return retStr;
            }

            try
            {
                experiment = (Gen5.Experiment)Gen5App.NewExperiment(ProtocolPath);
                plates = (Gen5.Plates)experiment.Plates;
                retStr += "NewExperiment successful, with plates\n";
            }
            catch (System.Runtime.InteropServices.COMException exception)
            {
                retStr += $"NewExperiment Failed, {exception}.\n";
            }
            catch (Exception exception)
            {
                retStr += $"NewExperiment Failed, {exception}.\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // ConfigureUSBReader
        //===========================================================================================
        public string ConfigureUSBReader()
        {
            string retStr = "Running ConfigureUSBReader\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before configuring reader.\n";
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                Gen5App.ConfigureUSBReader(-1, "");
                retStr += "ConfigureUSBReader Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"ConfigureUSBReader Failed, {exception}.\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // SetClientWindow
        //===========================================================================================
        public string SetClientWindow(Window win)
        {
            string retStr = "Running SetClientWindow\n";

            if (IsReading)
            {
                //do nothing
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                Gen5App.SetClientWindow(new WindowInteropHelper(win).Handle.ToInt32());
                retStr += "SetClientWindow Successful\n";
            }
            catch (System.FormatException)
            {
                retStr += "handle value not defined\n";
            }
            catch (COMException exception)
            {
                retStr += $"SetClientWindow Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // BrowseForFolder
        //===========================================================================================
        public string BrowseForFolder()
        {
            string retStr = "";

            if (IsReading)
            {
                //do nothing
                return retStr;
            }

            if (Gen5App == null)
            {
                return retStr;
            }

            try
            {
                retStr = Gen5App.BrowseForFolder("");
            }
            catch (COMException exception)
            {
                return retStr;
            }

            return retStr;
        }

        //===========================================================================================
        // CarrierIn
        //===========================================================================================
        public string CarrierIn()
        {
            string retStr = "Running CarrierIn\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before moving carrier in or out.\n";
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            int numTries = 0;
            while (numTries <= 3)
            {
                numTries++;
                try
                {
                    Gen5App.CarrierIn();
                    retStr += "CarrierIn Successful\n";
                    break;
                }
                catch (COMException exception)
                {
                    retStr += $"CarrierIn Failed on try number {numTries}, {exception}.\n";
                    Thread.Sleep(500);
                }
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // CarrierOut
        //===========================================================================================
        public string CarrierOut()
        {
            string retStr = "Running CarrierOut\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before moving carrier in or out.\n";
                return retStr;
            }
            
            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                Gen5App.CarrierOut();
                retStr += "CarrierOut Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"CarrierOut Failed, {exception}.";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // TestReaderCommunication
        //===========================================================================================
        public string TestReaderCommunication()
        {
            string retStr = "Running TestReaderCommunication\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before testing communication.\n";
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            long iError = Gen5App.TestReaderCommunication();


            if (iError == (int)BTIStatusCodes.BTI_OK)
            {
                retStr += "TestReaderCommunication Successful.\n";
            }
            else
            {
                string errorDescription = "No descriptions";
                switch ((BTIStatusCodes)iError)
                {
                    case BTIStatusCodes.BTI_AUTM_APP_READER_OPERATION_READER_NOT_CONFIGURED:
                        errorDescription = "Reader not configured";
                        break;

                    case BTIStatusCodes.BTI_BAUD_RATE_INVALID:
                        errorDescription = "Invalid baud rate";
                        break;

                    case BTIStatusCodes.BTI_DATA_BITS_INVALID:
                        errorDescription = "Invalid data bits";
                        break;

                    case BTIStatusCodes.BTI_STOP_BITS_INVALID:
                        errorDescription = "Invalid stop bits";
                        break;

                    case BTIStatusCodes.BTI_PARITY_INVALID:
                        errorDescription = "Invalid parity";
                        break;

                    case BTIStatusCodes.BTI_SERIAL_PORT_ERROR:
                        errorDescription = "Serial port setup failed";
                        break;

                    case BTIStatusCodes.BTI_PURGE_COMM_FAILURE:
                        errorDescription = "Purge com failed";
                        break;

                    case BTIStatusCodes.BTI_PORT_NOT_OPEN:
                        errorDescription = "port not open";
                        break;

                    case BTIStatusCodes.BTI_PORT_HANDLE_ERROR:
                        errorDescription = "Port handle error";
                        break;

                    case BTIStatusCodes.BTI_SERIAL_STREAM_ERROR:
                        errorDescription = "data received not format expected";
                        break;

                    case BTIStatusCodes.BTI_SEND_CMD_HEADER_ERROR:
                        errorDescription = "Send cmd header error";
                        break;

                    case BTIStatusCodes.BTI_COM_PORT_ALREADY_IN_USE:
                        errorDescription = "Com port already in use";
                        break;

                    case BTIStatusCodes.BTI_SET_TIMEOUT_ERROR:
                        errorDescription = "Set timeout error";
                        break;

                    case BTIStatusCodes.BTI_CREATE_FILE_HANDLE_ERROR:
                        errorDescription = "Create file handle error";
                        break;

                    case BTIStatusCodes.BTI_CREATE_FILE_NO_PORT_ERROR:
                        errorDescription = "Create File no port error";
                        break;

                    case BTIStatusCodes.BTI_CREATE_FILE_PORT_IN_USE:
                        errorDescription = "Create File port in use";
                        break;

                    case BTIStatusCodes.BTI_COULD_NOT_MATCH_BAUD_RATE:
                        errorDescription = "Could not match baud rate";
                        break;
                }

                retStr += "TestReaderCommunication Failed.\n";
                retStr += $"Error #: {iError}";
                retStr += $"Error description: {errorDescription}";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // GetReaderStatus
        //===========================================================================================
        public string GetReaderStatus()
        {
            string retStr = "Running GetReaderStatus\n";
            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            long status = Gen5App.GetReaderStatus();


            if (status <= 0)
            {
                retStr += "GetReaderStatus OK.\n";
                string statusDescription = "No descriptions";
                switch ((EReaderStatus)status)
                {
                    case EReaderStatus.eReaderStatus_OK:
                        statusDescription = "Reader is ready with no errors.";
                        break;
                    case EReaderStatus.eReaderStatus_Busy:
                        statusDescription = "Reader is busy.";
                        break;
                    case EReaderStatus.eReaderStatus_NotCommunicating:
                        statusDescription = "Reader is not communicating.";
                        break;
                    case EReaderStatus.eReaderStatus_NotConfigured:
                        statusDescription = "No reader has been configured.";
                        break;
                }
                retStr += $"Status: {statusDescription}\n";
            }
            else
            {
                string errorCode = status.ToString("X");

                retStr += "GetReaderStatus returned with error.\n";
                retStr += $"Error code: {errorCode}";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // RunReaderControlCommand
        //===========================================================================================
        public string RunReaderControlCommand()
        {
            string retStr = "Running RunReaderControlCommand\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before running the reader control command.\n";
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                Gen5App.RunReaderControlCommand(0);
                retStr += "RunReaderControlCommand Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"RunReaderControlCommand Failed, {exception}.";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // GetCurrentTemperature
        //===========================================================================================
        public string GetCurrentTemperature()
        {
            string retStr = "Running GetCurrentTemperature\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before getting the temperature.\n";
                return retStr;
            }

            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                object tempValue = 0;
                object tempStatus = 0;

                Gen5App.GetCurrentTemperature(ref tempValue, ref tempStatus);

                retStr += "GetCurrentTemperature Successful\n";
                retStr += $"tempValue: {(int)tempValue}\n";
                retStr += $"tempStatus: {(int)tempStatus}\n";
            }
            catch (COMException exception)
            {
                retStr += $"GetCurrentTemperature Failed, {exception}.";
            }

            retStr += "\n";

            return retStr;
        }

        public string GetExperimentFilePath(string folder, string id)
        {
            string expPath = System.IO.Path.Combine(folder, id);
            expPath += ".xpt";
            return expPath;
        }

        //===========================================================================================
        // SaveAs
        //===========================================================================================
        public string ExpSaveAs()
        {
            string retStr = "Running ExpSaveAs\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before saving.\n";
                return retStr;
            }

            if (experiment == null)
            {
                retStr += "experiment is null\n";
                return retStr;
            }

            try
            {
                //ExperimentPath = System.IO.Path.Combine(ExperimentFolderPath,ExperimentID);
                //ExperimentPath += ".xpt";
                ExperimentPath = GetExperimentFilePath(ExperimentFolderPath, ExperimentID);
                experiment.SaveAs(ExperimentPath);
                retStr += "SaveAs Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"SaveAs Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // Save
        //===========================================================================================
        public string ExpSave()
        {
            string retStr = "Running ExpSave\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before saving.\n";
                return retStr;
            }

            if (experiment == null)
            {
                retStr += "experiment is null\n";
                retStr += "\n";
            }

            try
            {
                experiment.Save();
                retStr += "Save Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"Save Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // Close
        //===========================================================================================
        public string ExpClose()
        {
            string retStr = "Running ExpClose\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before closing experiment.\n";
                return retStr;
            }

            if (experiment == null)
            {
                retStr += "experiment is null\n";
                return retStr;
            }

            try
            {
                experiment.Close();
                retStr += "Close Successful\n";

                plateReadMonitor = null;
                plates = null;
                plate = null;
                experiment = null;

                IsReading = false; //just in case
            }
            catch (COMException exception)
            {
                retStr += $"Close Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;

        }

        //===========================================================================================
        // GetPlate
        //===========================================================================================
        public string PlatesGetPlate()
        {
            string retStr = "Running PlatesGetPlate\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before running this method.\n";
                return retStr;
            }

            if (plates == null)
            {
                retStr += "plates is null\n";
                return retStr;
            }

            try
            {
                plate = (Gen5.Plate)plates.GetPlate(1);
                retStr += "GetPlate Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"GetPlate Failed, {exception}\n";
            }

            GC.Collect();

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // StartRead
        //===========================================================================================
        public string PlateStartRead()
        {
            string retStr = "Running PlateStartRead\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before starting a new read.\n";
                return retStr;
            }

            if (plate == null)
            {
                retStr += "plate is null\n";
                return retStr;
            }

            try
            {
                plateReadMonitor = (Gen5.PlateReadMonitor)plate.StartReadEx(true);
                IsReading = true;
                retStr += "StartRead Successful, reverse plate orientation.\n";
            }
            catch (COMException exception)
            {
                retStr += $"StartRead Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // AbortRead
        //===========================================================================================
        public string PlateAbortRead()
        {
            string retStr = "Running PlateAbortRead\n";
            if (plate == null)
            {
                retStr += "plate is null\n";
                return retStr;
            }

            try
            {
                plate.AbortRead();
                retStr += "AbortRead Successful\n";
                //IsReading = false; Don't want to do this here becasue the reader may not aboort immediately; want to wait until status check indicates that read has been aborted
            }
            catch (COMException exception)
            {
                retStr += $"AbortRead Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // PlateReadStatus
        //===========================================================================================
        public string PlateReadStatus(ref Gen5ReadStatus status)
        {
            string retStr = "Running PlateReadStatus\n";

            if (plate == null)
            {
                retStr += "plate is null\n";
                return retStr;
            }

            try
            {
                status = plate.ReadStatus;
                retStr += $"{status.ToString()}\n";
                if (status == Gen5ReadStatus.eReadInProgress)
                {
                    IsReading = true;
                }
                else
                {
                    IsReading = false;
                }
            }
            catch (COMException exception)
            {
                retStr += $"ReadStatus Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // FileExport
        //===========================================================================================
        public string PlateFileExport()
        {
            string retStr = "Running PlateFileExport\n";

            if (IsReading)
            {
                retStr += "Read in progress, abort read or wait until end of read before exporting plate data.\n";
                return retStr;
            }

            if (plate == null)
            {
                retStr += "plate is null\n";
                return retStr;
            }

            try
            {
                ExportFilePath = System.IO.Path.Combine(ExperimentFolderPath, ExperimentID);
                ExportFilePath += ".txt";

                plate.FileExport(ExportFilePath);
                retStr += "FileExport Successful";
            }
            catch (COMException exception)
            {
                retStr += $"FileExport Failed, {exception}";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // GetFileExportNames
        //===========================================================================================
        //public string GetFileExportNames()
        //{
        //    string retStr = "Running GetFileExportNames\n";

        //    unsafe
        //    {
        //        int[] iArray = new int[10];
        //        fixed (int* ptr = iArray)
        //        {

        //        }

        //        string[] sArray = new string[10];
        //        fixed (object* ptr = sArray)
        //        {

        //        }
        //        //fixed (string* ptr = sArray)
        //        //{

        //        //}

        //        string[] exportNames = new string[] { };
        //        fixed (string* ptr = exportNames)
        //        {

        //        }
        //    }

        //        Object pvNames = (Object)exportNames;

        //    if (plate != null)
        //    {
        //        try
        //        {
        //            plate.GetFileExportNames(false, ref pvNames);
        //            foreach (string n in exportNames)
        //            {
        //                retStr += $"{n}, ";
        //            }
        //            retStr += "\n";
        //        }
        //        catch (COMException exception)
        //        {
        //            retStr += $"GetFileExportNames Failed, {exception}\n";
        //        }
        //    }

        //    retStr += "\n";

        //    return retStr;
        //}

    }
}
