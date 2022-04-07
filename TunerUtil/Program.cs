using System;
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
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Form1 myForm = new Form1();
            Application.Run(myForm);
            //myForm.Dispose();
        }
    }
}
