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

namespace TunerTest
{
    public partial class Form1 : Form
    {
        private Tuner tuner1;
        public string tunermsg = "";

        public Form1()
        {
            InitializeComponent();
            timer1.Interval = 10;
            timer1.Start();
        }

        public delegate void SetTextCallback(string text);

        public void SetText(string msg)
        {
            richTextBox1.AppendText(msg);
        }

        public void HandleSomethingHappened(string text)
        {
            if (this.richTextBox1.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetText);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.richTextBox1.AppendText(text);

            }
        }

        private void button_Open_Click(object sender, EventArgs e)
        {
            //if (comboBox1.SelectedItem.Equals("LDG"))
            //{
            //    MessageBox.Show("Not testing LDG right now");
            //}
            //else
            {
                tuner1 = new TunerMFJ928X("MFJ-998","COM3","4800");
                //tuner1.Update += this.MyMethod;
            }
        }

        private void button_Tune_Click(object sender, EventArgs e)
        {
            tuner1.Tune();
        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            timer1.Stop();
            if (tuner1 != null)
            {
                string s = tuner1.GetText();
                if (s.Length > 0) richTextBox1.AppendText(s);
            }
            timer1.Start();
        }

        private void buttonAmpOff_Click(object sender, EventArgs e)
        {
            tuner1.CMD_Amp(0);
        }

        private void buttonAmpOn_Click(object sender, EventArgs e)
        {
            tuner1.CMD_Amp(1);
        }
    }
}
