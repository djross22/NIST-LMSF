using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;
using System.Windows;

namespace Overlord_Simulator
{
    /// <summary>
    /// Interaction logic for App.xaml
    /// </summary>
    public partial class App : Application
    {
        public static string[] commandLineArgs;

        private void Application_Startup(object sender, StartupEventArgs e)
        {
            //Simulate slow starting Overlord program
            //Thread.Sleep(5000);

            commandLineArgs = e.Args;
        }
    }
}
