using AmpAutoTunerUtility;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using static System.Windows.Forms.VisualStyles.VisualStyleElement;

namespace RigTest
{
    public partial class Form1 : Form
    {
        RigFLRig myRig;
        public Form1()
        {
            InitializeComponent();
            timer1.Start();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            if (button1.Text == "Connect")
            {
                myRig = new RigFLRig();
                myRig.Open();
                if (myRig != null)
                {
                    button1.BackColor = Color.Green;
                    button1.ForeColor = Color.White;
                    button1.Text = "Disconnect";
                }
            }
            else
            {
                if (myRig != null) myRig.Close();
                button1.BackColor = Color.WhiteSmoke;
                button1.ForeColor = Color.Black;
                button1.Text = "Connect";
            }
        }

        private void labelVFOA_Click(object sender, EventArgs e)
        {
            myRig.VFO = 'A';
        }
        private void labelVFOB_Click(object sender, EventArgs e)
        {
            myRig.VFO = 'B';
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            if (myRig != null)
            {
                if (myRig.VFO == 'A')
                { 
                    labelVFO.Font = new Font(labelVFO.Font, FontStyle.Bold);
                    labelVFOB.Font = new Font(labelVFO.Font, FontStyle.Regular);
                }
                else
                {
                    labelVFO.Font = new Font(labelVFO.Font, FontStyle.Regular);
                    labelVFOB.Font = new Font(labelVFO.Font, FontStyle.Bold);

                }
                if (!textBoxFrequencyA.Focused)
                    textBoxFrequencyA.Text = myRig.FrequencyA.ToString(".0");
                if (!textBoxFrequencyB.Focused)
                    textBoxFrequencyB.Text = myRig.FrequencyB.ToString(".0");
            }
            timer1.Start();
        }

        private void label1_Click(object sender, EventArgs e)
        {

        }

        private void textBoxFrequencyA_TextChanged(object sender, EventArgs e)
        {
            //myRig.SetFrequency('A', Double.Parse(textBoxFrequencyA.Text));
        }

        private void textBoxFrequencyB_TextChanged(object sender, EventArgs e)
        {
            //myRig.SetFrequency('B', Double.Parse(textBoxFrequencyB.Text));
        }

        private void textBoxFrequencyB_Enter(object sender, EventArgs e)
        {
            //MessageBox.Show("Here");
        }

        private void textBoxFrequencyB_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double f = Double.Parse(textBoxFrequencyB.Text);
                myRig.SetFrequency('B', f);
                button1.Focus();
                myRig.FrequencyB = f; // to avoid flashing the freq to the old freq
            }
        }

        private void textBoxFrequencyA_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode == Keys.Enter)
            {
                double f = Double.Parse(textBoxFrequencyA.Text);
                myRig.SetFrequency('A', f);
                myRig.FrequencyA = f; // to avoid flashing the freq to the old freq
                button1.Focus();
            }
        }
    }
}
