using System;
using System.IO;
using System.Reflection.Emit;
using System.Windows.Forms;

namespace AmpAutoTunerUtility
{
    static class Program
    {
        /// <summary>
        /// The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            try
            {
                AppDomain currentDomain = AppDomain.CurrentDomain;
                currentDomain.UnhandledException += new UnhandledExceptionEventHandler(UnhandledExceptions); Application.EnableVisualStyles();
                Application.SetCompatibleTextRenderingDefault(false);
                Form1 myForm = new();
                Application.Run(myForm);
                //myForm.Dispose();
            }
            catch (Exception ex)
            {
                HandleExceptionGracefully(ex);
            }
        }

        static void HandleExceptionGracefully(Exception ex)
        {   
            string path = System.IO.Path.GetTempPath() + "AmpAutoTunerUtility.log";
            File.AppendAllText(path, ex.Message + "\n" + ex.StackTrace);
        }
        static private void UnhandledExceptions(object sender, UnhandledExceptionEventArgs e)
        {
            // An unhandled Exception occurred!
            if (e.IsTerminating)
            {
                // The Runtime is terminating now, so log some error details
                string path = System.IO.Path.GetTempPath() + "AmpAutoTunerUtility.log";
                Exception ex = (Exception)e.ExceptionObject;
                File.AppendAllText(path, ex.Message + "\n" + ex.StackTrace);
            }
        }
    }
}
