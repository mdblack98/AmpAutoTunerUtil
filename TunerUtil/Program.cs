using System;
using System.IO;
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 myForm = new Form1();
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
    }
}
