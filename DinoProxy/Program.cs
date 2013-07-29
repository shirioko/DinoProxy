using System;
using System.ComponentModel;
using System.Diagnostics;
using System.Security.Principal;
using System.Windows.Forms;

namespace DinoProxy
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            
            WindowsPrincipal pricipal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
            bool hasAdministrativeRight = pricipal.IsInRole(WindowsBuiltInRole.Administrator);

            if (!hasAdministrativeRight)
            {
                RunElevated(Application.ExecutablePath);
                Application.Exit();
            }
            else
            {
                //context class:
                //I've embedded some DLL's into the EXE,
                //and in order to resolve them during runtime
                //you need a context, which the static Program
                //class doesn't have
                Context c = new Context();
                c.Run();
            }
        }

        private static bool RunElevated(string fileName)
        {
            ProcessStartInfo processInfo = new ProcessStartInfo();
            processInfo.Verb = "runas";
            processInfo.FileName = fileName;
            try
            {
                Process.Start(processInfo);
                return true;
            }
            catch (Win32Exception)
            {
                //Do nothing. Probably the user canceled the UAC window
            }
            return false;
        }
    }
}
