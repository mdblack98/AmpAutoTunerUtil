using System;
using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Forms;

namespace AmpAutoTunerUtility
{
    public partial class HelpForm : Form
    {
        public HelpForm()
        {
            InitializeComponent();
        }

            private void HelpForm_Load(object sender, EventArgs e)
            {
                var strExeFilePath = System.Reflection.Assembly.GetExecutingAssembly().Location;
                var helpFile = System.IO.Path.GetDirectoryName(strExeFilePath) + "\\AmpAutoTunerUtility.htm";
                webBrowser1.Url = new System.Uri(helpFile);
            }

    }
}
