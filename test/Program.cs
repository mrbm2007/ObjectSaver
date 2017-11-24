using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Reflection;
using Saver;

namespace test
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        { 

            SaveAble.Settings.use_nrmap = true;
            DontSave.DisableGlobaly = true;
            SaveAs.DisableGlobaly = true; 


            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new Form1());
        } 
    }
}
