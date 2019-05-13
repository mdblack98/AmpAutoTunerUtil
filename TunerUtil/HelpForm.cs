using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
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
                String path = Assembly.GetExecutingAssembly().CodeBase;
                String helpFile = Path.GetDirectoryName(Assembly.GetExecutingAssembly().CodeBase) + "\\AmpAutoTunerUtility.htm";
                //MessageBox.Show(helpFile);
                webBrowser1.Url = new System.Uri(helpFile);
            }

    }
}
