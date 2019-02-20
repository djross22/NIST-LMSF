using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using LMSF_Utilities;

namespace LMSF_Scheduler
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application, ISingleInstanceApp
    {
        public static string[] commandLineArgs;

        //Code from: https://www.codeproject.com/Articles/84270/WPF-Single-Instance-Application
        private const string Unique = "LMSF_Scheduler_Unique_Hold_String";
        [STAThread]
        public static void Main(string[] args)
        {
            if (SingleInstance<App>.InitializeAsFirstInstance(Unique))
            {
                commandLineArgs = args;

                var application = new App();
                application.InitializeComponent();
                application.Run();
                // Allow single instance code to perform cleanup operations
                SingleInstance<App>.Cleanup();
            }
        }

        public bool SignalExternalCommandLineArgs(IList<string> args)
        {
            // handle command line arguments of second instance
            // ...
            MainWindow win = (MainWindow)this.MainWindow;
            win.SecondCommandRun(args);
            return true;
        }
    }
}
