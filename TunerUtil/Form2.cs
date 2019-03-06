using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmpAutoTunerUtility
{
    public partial class Form2 : Form
    {
        public Form2()
        {
            InitializeComponent();
        }

        private void Form2_Load(object sender, EventArgs e)
        {

        }

        public void ProgressBar(int n)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    progressBar1.Value = n;
                }));
            }
            else
            {
                progressBar1.Value = n;
            }
            Thread.Sleep(100);
            Application.DoEvents();
        }

        public void ProgressBarSetMax(int n)
        {
            if (InvokeRequired)
            {
                BeginInvoke(new Action(() =>
                {
                    progressBar1.Maximum = n;
                }));
            }
            else
            {
                progressBar1.Maximum = n;
            }
            Application.DoEvents();
        }
    }
}
