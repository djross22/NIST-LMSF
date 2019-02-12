using System;
using System.Windows;
using System.Windows.Interop;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Runtime.InteropServices;
using Gen5;

namespace LMSF_Gen5_Reader
{
    public class Gen5Reader
    {
        private Gen5.Application Gen5App;
        private Gen5.Experiment experiment;
        private Gen5.Plates plates;
        private Gen5.Plate plate;
        private Gen5.PlateReadMonitor plateReadMonitor;

        public string ExperimentID { get; set; }
        public string ProtocolPath { get; set; }
        public string ExperimentPath { get; set; }
        public string ExperimentFolderPath { get; set; }

        public Gen5Reader()
        {

        }

        //===========================================================================================
        // Start Gen5
        //===========================================================================================
        private string StartGen5()
        {
            string retStr = "Running StartGen5\n";
            try
            {
                Gen5App = new Gen5.Application();
                retStr += "Gen5App created Successfully\n";

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
        private string TerminateGen5()
        {
            string retStr = "Running TerminateGen5\n";

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
        // NewExperiment
        //===========================================================================================
        private string NewExperiment()
        {
            string retStr = "Running NewExperiment\n";

            if (Gen5App == null)
            {

                retStr += "Gen5App is null\n";
                return retStr; 
            }

            try
            {
                experiment = (Gen5.Experiment)Gen5App.NewExperiment(ProtocolPath);
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
        private string NewExperiment(string protocolPath)
        {
            string retStr = "Running NewExperiment\n";
            ProtocolPath = protocolPath;

            if (Gen5App == null)
            {

                retStr += "Gen5App is null\n";
                return retStr;
            }

            try
            {
                experiment = (Gen5.Experiment)Gen5App.NewExperiment(ProtocolPath);
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
        // ConfigureUSBReader
        //===========================================================================================
        private string ConfigureUSBReader()
        {
            string retStr = "Running ConfigureUSBReader\n";
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
        private string SetClientWindow(Window win)
        {
            string retStr = "Running SetClientWindow\n";
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
        // CarrierIn
        //===========================================================================================
        private string CarrierIn()
        {
            string retStr = "Running CarrierIn\n";
            if (Gen5App == null)
            {
                retStr += "gen5App is null\n";
                return retStr;
            }

            try
            {
                Gen5App.CarrierIn();
                retStr += "CarrierIn Successful\n";
            }
            catch (COMException exception)
            {
                retStr += $"CarrierIn Failed, {exception}.\n";
            }

            retStr += "\n";

            return retStr;
        }

        //===========================================================================================
        // CarrierOut
        //===========================================================================================
        private string CarrierOut()
        {
            string retStr = "Running CarrierOut\n";
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
        private string TestReaderCommunication()
        {
            string retStr = "Running TestReaderCommunication\n";
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
        private string GetReaderStatus()
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
        private string RunReaderControlCommand()
        {
            string retStr = "Running RunReaderControlCommand\n";
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
        // SaveAs
        //===========================================================================================
        private string ExpSaveAs()
        {
            string retStr = "Running ExpSaveAs\n";
            if (experiment == null)
            {
                retStr += "experiment is null\n";
                return retStr;
            }

            try
            {
                ExperimentPath = System.IO.Path.Combine(ExperimentFolderPath,ExperimentID);
                ExperimentPath += ".xpt";
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
        private string ExpSave()
        {
            string retStr = "Running ExpSave\n";
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
        private string ExpClose()
        {
            string retStr = "Running ExpSave\n";
            if (experiment == null)
            {
                retStr += "experiment is null\n";
                return retStr;
            }

            try
            {
                experiment.Close();
                retStr += "Close Successful\n";
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
        private string PlatesGetPlate()
        {
            string retStr = "Running PlatesGetPlate\n";
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
        private string PlateStartRead()
        {
            string retStr = "Running PlateStartRead\n";
            if (plate == null)
            {
                retStr += "plate is null\n";
                return retStr;
            }

            try
            {
                plateReadMonitor = (Gen5.PlateReadMonitor)plate.StartRead();
                retStr += "StartRead Successful\n";
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
        private string PlateAbortRead()
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
            }
            catch (COMException exception)
            {
                retStr += $"AbortRead Failed, {exception}\n";
            }

            retStr += "\n";

            return retStr;
        }

    }
}
