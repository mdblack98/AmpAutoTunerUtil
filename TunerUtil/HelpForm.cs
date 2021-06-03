using System;
using System.IO;
using System.Reflection;
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
                //String path = Assembly.GetExecutingAssembly().CodeBase;
                String helpFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\AmpAutoTunerUtility.htm";
                //MessageBox.Show(helpFile);
                webBrowser1.Url = new System.Uri(helpFile);
            }

    }
}
