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

        private void RigOpen()
        {
            if (myRig == null) myRig = new RigFLRig();
            myRig.Open();
            if (myRig != null)
            {
                buttonConnect.BackColor = Color.Green;
                buttonConnect.ForeColor = Color.White;
                buttonConnect.Text = "Disconnect";
            }
        }

        private void RigClose()
        {
            if (myRig != null) myRig.Close();
            buttonConnect.BackColor = Color.WhiteSmoke;
            buttonConnect.ForeColor = Color.Black;
            buttonConnect.Text = "Connect";
        }
        private void button1_Click(object sender, EventArgs e)
        {
            if (buttonConnect.Text == "Connect")
            {
                RigOpen();
            }
            else
            {
                RigClose();
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
            if (!myRig.ModeA.Equals(comboBoxModeA.SelectedItem))
                comboBoxModeA.SelectedIndex = comboBoxModeA.FindStringExact(myRig.ModeA);
            if (!myRig.ModeB.Equals(comboBoxModeB.SelectedItem))
                comboBoxModeB.SelectedIndex = comboBoxModeB.FindStringExact(myRig.ModeB);
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
                buttonConnect.Focus();
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
                buttonConnect.Focus();
            }
        }

        private void buttonPTT_Click(object sender, EventArgs e)
        {
            if (buttonPTT.Text.Equals("PTT"))
            {
                buttonPTT.BackColor = Color.Red;
                buttonPTT.ForeColor = Color.Black;
                buttonPTT.Text = "TX";
                myRig.PTT = true;
            }
            else
            {
                buttonPTT.BackColor = Color.WhiteSmoke;
                buttonPTT.Text = "PTT";
                buttonPTT.ForeColor = Color.Black;
                myRig.PTT = false;
            }
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            RigOpen();
            List<string> modes = myRig.GetModes();
            myRig.GetMode('A');
            myRig.GetMode('B');
            foreach (string mode in modes)
            {
                comboBoxModeA.Items.Add(mode);
                comboBoxModeB.Items.Add(mode);
            }
            //comboBoxModeA.SelectedIndex = comboBoxModeA.FindStringExact(myRig.ModeA);
            //comboBoxModeB.SelectedIndex = comboBoxModeB.FindStringExact(myRig.ModeB);
        }

        private void comboBoxModeA_SelectedIndexChanged(object sender, EventArgs e)
        {
            myRig.ModeA = (string)comboBoxModeA.SelectedItem;
        }

        private void comboBoxModeB_SelectedIndexChanged(object sender, EventArgs e)
        {
            myRig.ModeB = (string)comboBoxModeB.SelectedItem;
        }

        private void comboBoxModeA_DropDown(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void comboBoxModeA_DropDownClosed(object sender, EventArgs e)
        {
            timer1.Start();
        }

        private void comboBoxModeB_DropDown(object sender, EventArgs e)
        {
            timer1.Stop();
        }

        private void comboBoxModeB_DropDownClosed(object sender, EventArgs e)
        {
            timer1.Start();
        }
    }
}
