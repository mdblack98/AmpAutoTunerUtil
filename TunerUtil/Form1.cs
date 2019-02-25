using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TunerUtil
{
    public partial class Form1 : Form
    {
        int baudRelay =0;
        int baudTuner;
        string comPortRelay = "";
        string comPortTuner = "";
        TcpClient rigClient;
        NetworkStream rigStream;
        double frequency = 0;
        double lastfrequency =0;
        int tolTune = 0;
        Dictionary<int,int> bandTolerance = new Dictionary<int,int>();
        Tuner Tuner1;
        Relay relay1;

        public Form1()
        {
            InitializeComponent();
        }

        private bool SetSelectedIndexOf(ComboBox sender, string match)
        {
            foreach (string s in sender.Items)
            {
                if (s.Equals(match))
                {
                    //sender.Sel
                }
            }
            return false;
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            try
            {
                LoadComPorts();
                LoadBaudRates();
                richTextBoxRelay1.AppendText("Relay debug\n");
                richTextBoxTuner.AppendText("Tuner debug\n");
                richTextBoxRig.AppendText("FLRig debug\n");
                ConnectFLRig();
                FLRigGetFreq();
                timer1.Interval = 500;
                timer1.Enabled = true;

                // Band tolerance list
                for (int i = 1; i < 60; ++i) {
                    bandTolerance[i] = i*1000;
                }

                // Tuner Properties
                textBoxFreqTol.Text = Properties.Settings.Default.tolTune;
                checkBoxRig.Checked = Properties.Settings.Default.rigEnabled;
                tolTune = Int32.Parse(textBoxFreqTol.Text);
                checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
                // Relay Properties
                checkBoxRelay1Enabled.Checked = Properties.Settings.Default.Relay1Enabled;
                checkBoxRelay2Enabled.Checked = Properties.Settings.Default.Relay2Enabled;
                checkBoxRelay3Enabled.Checked = Properties.Settings.Default.Relay3Enabled;
                checkBoxRelay4Enabled.Checked = Properties.Settings.Default.Relay4Enabled;
                //Set selected items
                comboBoxTunerModel.SelectedIndex = comboBoxTunerModel.FindStringExact(Properties.Settings.Default.TunerModel);
                comboBoxComTuner.SelectedIndex   = comboBoxComRelay1.FindStringExact(Properties.Settings.Default.Relay1Com);
                comboBoxBaudTuner.SelectedIndex  = comboBoxBaudRelay1.FindStringExact(Properties.Settings.Default.Relay1Baud);
                comboBoxComRelay1.SelectedIndex  = comboBoxComRelay1.FindStringExact(Properties.Settings.Default.Relay1Com);
                comboBoxBaudRelay1.SelectedIndex = comboBoxBaudRelay1.FindStringExact(Properties.Settings.Default.Relay1Baud);
                comboBoxComRelay2.SelectedIndex  = comboBoxComRelay2.FindStringExact(Properties.Settings.Default.Relay2Com);
                comboBoxBaudRelay2.SelectedIndex = comboBoxBaudRelay2.FindStringExact(Properties.Settings.Default.Relay2Baud);
                comboBoxComRelay3.SelectedIndex  = comboBoxComRelay3.FindStringExact(Properties.Settings.Default.Relay3Com);
                comboBoxBaudRelay3.SelectedIndex = comboBoxBaudRelay3.FindStringExact(Properties.Settings.Default.Relay3Baud);
                comboBoxComRelay4.SelectedIndex  = comboBoxComRelay4.FindStringExact(Properties.Settings.Default.Relay4Com);
                comboBoxBaudRelay4.SelectedIndex = comboBoxBaudRelay4.FindStringExact(Properties.Settings.Default.Relay4Baud);
                textBoxRelay1Off.Text = Properties.Settings.Default.Relay1Off;
                textBoxRelay2Off.Text = Properties.Settings.Default.Relay2Off;
                textBoxRelay3Off.Text = Properties.Settings.Default.Relay3Off;
                textBoxRelay4Off.Text = Properties.Settings.Default.Relay4Off;
                textBoxRelay1On.Text = Properties.Settings.Default.Relay1On;
                textBoxRelay2On.Text = Properties.Settings.Default.Relay2On;
                textBoxRelay3On.Text = Properties.Settings.Default.Relay3On;
                textBoxRelay4On.Text = Properties.Settings.Default.Relay4On;
                textBoxRelay1Status.Text = Properties.Settings.Default.Relay1Status;
                textBoxRelay2Status.Text = Properties.Settings.Default.Relay2Status;
                textBoxRelay3Status.Text = Properties.Settings.Default.Relay3Status;
                textBoxRelay4Status.Text = Properties.Settings.Default.Relay4Status;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading TunerUtil:\n" + ex.Message);
            }
            tabControl1.SelectedIndex = 2;
            relay1 = new Relay("com15", "9600");
            Tuner1 = new Tuner(comboBoxTunerModel.SelectedText, comboBoxComTuner.SelectedText, comboBoxBaudTuner.Text);
        }

        private void LoadBaudRates()
        {
            comboBoxBaudRelay1.Items.Add("9600");
            comboBoxBaudRelay1.Items.Add("19200");
            comboBoxBaudRelay1.Items.Add("38400");
            comboBoxBaudRelay1.Items.Add("57600");
            comboBoxBaudRelay1.Items.Add("115200");
            foreach (string s in comboBoxBaudRelay1.Items)
            {
                comboBoxBaudTuner.Items.Add(s);
                comboBoxBaudRelay2.Items.Add(s);
                comboBoxBaudRelay3.Items.Add(s);
                comboBoxBaudRelay4.Items.Add(s);
            }
        }

        private void LoadComPorts()
        {
            foreach (string s in System.IO.Ports.SerialPort.GetPortNames())
            {
                if (comboBoxComRelay1.Items.Count == 0)
                {
                    comboBoxComRelay1.Items.Add(s);
                }
                else
                {
                    int numSerPort = Int32.Parse(s.Substring(3));
                    string s2 = comboBoxComRelay1.Items[comboBoxComRelay1.Items.Count - 1].ToString();
                    int numLastItem = Int32.Parse(s2.Substring(3));
                    if (numSerPort > numLastItem)
                    {
                        comboBoxComRelay1.Items.Add(s);
                    }
                    else
                    {
                        // Figure out where it goes
                        int ii = 0;
                        foreach (string s3 in comboBoxComRelay1.Items)
                        {
                            int numCurrentItem = Int32.Parse(s3.Substring(3));
                            if (numSerPort < numCurrentItem) { 
                                comboBoxComRelay1.Items.Insert(ii, s);
                                break;
                            }
                            ++ii;
                        }
                    }
                }
            }
            foreach (string s in comboBoxComRelay1.Items)
            {
                comboBoxComRelay2.Items.Add(s);
                comboBoxComRelay3.Items.Add(s);
                comboBoxComRelay4.Items.Add(s);
                comboBoxComTuner.Items.Add(s);
            }
        }

        private void Relay(bool on)
        {
            if (on == true)
            {
                // turn relay on
                MessageBox.Show("Relay On");
            }
            else
            {
                MessageBox.Show("Relay Off");
                // turn relay off
            }
        }

        private void CheckBoxRelayOn_CheckedChanged(object sender, EventArgs e)
        {
            // Turn relay on/off
            if (checkBoxRelay1Enabled.Checked)
            {
                Relay(true);
            }
            else
            {
                Relay(false);
            }
        }

        private void buttonTune_Click(object sender, EventArgs e)
        {
            richTextBoxTuner.AppendText("Tuning\n");
        }

        private void comboBoxRelayCom_SelectedIndexChanged(object sender, EventArgs e)
        {
            comPortRelay = comboBoxComRelay1.Text;
            if (baudRelay != 0) richTextBoxRelay1.AppendText("Opening Relay "+comPortRelay +":"+baudRelay+"\n");
        }

        private void comboBoxRelayBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            baudRelay = Int32.Parse(comboBoxBaudRelay1.Text);
            if (comPortRelay.Length > 0) richTextBoxRelay1.AppendText("Opening Relay "+comPortRelay+":"+baudRelay+"\n");
        }

        private void comboBoxTunerCom_SelectedIndexChanged(object sender, EventArgs e)
        {
            comPortTuner = comboBoxComTuner.Text;
            if (baudTuner > 0) richTextBoxTuner.AppendText("Opening Tuner " + comPortTuner+":"+baudTuner+"\n");
        }

        private void comboBoxTunerBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            baudTuner = Int32.Parse(comboBoxBaudTuner.Text);
            if (comPortTuner.Length > 0) richTextBoxTuner.AppendText("Opening Tuner " + comPortTuner + ":" + baudTuner+"\n");

        }

        private void richTextBoxRelay_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxRelay1.Clear();
        }

        private void richTextBoxTuner_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxTuner.Clear();
        }

        private void richTextBoxFLRig_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxRig.Clear();
        }

        private void ConnectFLRig()
        {
            int port = 12345;
            try
            {
                rigClient = new TcpClient("127.0.0.1", port);
                rigStream = rigClient.GetStream();
                richTextBoxRig.AppendText("FLRig connected\n");
            }
            catch (Exception ex)
            {
                checkBoxRig.Checked = false;
                richTextBoxRig.AppendText("Rig error:\n" + ex.Message+"\n");
            }
        }

        private void DisconnectFLRig()
        {
            try
            {
                rigStream.Close();
                rigClient.Close();
            }
            catch (Exception)
            {
                
            }
        }

        private void FLRigGetFreq()
        {
            if (!checkBoxRig.Checked) return;
            string xml = "POST /RPC2 HTTP/1.1\r\n";
            xml += "User-Agent: XMLRPC++ 0.8\r\n";
            xml += "Host: 127.0.0.1:12345\r\n";
            xml += "Content-type: text/xml\r\n";
            xml += "Content-Length: 89\r\n\r\n";
            xml += "<?xml version=\"1.0\"?>\r\n";
            xml += "<methodCall><methodName>rig.get_vfoA</methodName>\r\n";
            xml += "</methodCall>\r\n";
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText("FLRig error:\n"+ex.Message+"\n");
                checkBoxRig.Checked = false;
                DisconnectFLRig();
                return;
            }
            data = new Byte[4096];
            String responseData = String.Empty;
            rigStream.ReadTimeout = 2000;
            try {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                responseData = Encoding.ASCII.GetString(data, 0, bytes);
                richTextBoxRig.AppendText(responseData);
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>") + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>");
                        string freqString = responseData.Substring(offset1, offset2 - offset1);
                        frequency = Double.Parse(freqString);
                        labelFreq.Text = (frequency/1000).ToString()+"kHz";
                        if (frequency != lastfrequency && Math.Abs(frequency - lastfrequency) > tolTune)
                        {
                            richTextBoxTuner.AppendText("Tuning to " + labelFreq.Text + "\n");
                            if (checkBoxTunerEnabled.Checked)
                            {
                                buttonTunerStatus.BackColor = Color.LightGray;
                                Application.DoEvents();
                                char response = Tuner1.Tune();
                                if (response == 'T') buttonTunerStatus.BackColor = Color.Green;
                                else if (response == 'M') buttonTunerStatus.BackColor = Color.Yellow;
                                else if (response == 'F') buttonTunerStatus.BackColor = Color.Red;
                                else MessageBox.Show("Unknown response from tuner = '" + response + "'");
                                lastfrequency = frequency;
                            }
                        }
                    }
                    labelFreq.Text = "?";
                }
                catch (Exception)
                {
                    richTextBoxRig.AppendText("Error parsing freq from answer:\n" + responseData);
                    frequency = 0;
                }
            }
            catch (Exception ex) {
                richTextBoxRig.AppendText("Rig not responding\n" + ex.Message+"\n");
            }
        }

        private void Timer1_Tick(object sender, EventArgs e)
        {
            if (checkBoxRig.Checked)
            {
                timer1.Enabled = false;
                FLRigGetFreq();
                timer1.Enabled = true;
            }
            else
            {
                labelFreq.Text = "?";
            }
        }

        private void checkBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxRig.Checked)
            {
                richTextBoxRig.Clear();
                ConnectFLRig();
                FLRigGetFreq();
            }
            else
            {
                DisconnectFLRig();
            }
        }

        private void textBoxFreqTol_Leave(object sender, EventArgs e)
        {
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            DisconnectFLRig();
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
            Properties.Settings.Default.rigEnabled = checkBoxRig.Checked;
            Properties.Settings.Default.TunerEnabled = checkBoxTunerEnabled.Checked;
            Properties.Settings.Default.TunerModel = comboBoxTunerModel.Text;
            Properties.Settings.Default.Relay1Enabled = checkBoxRelay1Enabled.Checked;
            Properties.Settings.Default.Relay2Enabled = checkBoxRelay2Enabled.Checked;
            Properties.Settings.Default.Relay3Enabled = checkBoxRelay2Enabled.Checked;
            Properties.Settings.Default.Relay4Enabled = checkBoxRelay4Enabled.Checked;
            Properties.Settings.Default.Relay1Com = comboBoxComRelay1.SelectedText;
            Properties.Settings.Default.Relay2Com = comboBoxComRelay2.SelectedText;
            Properties.Settings.Default.Relay3Com = comboBoxComRelay3.SelectedText;
            Properties.Settings.Default.Relay4Com = comboBoxComRelay4.SelectedText;
            Properties.Settings.Default.Relay1Baud = comboBoxBaudRelay1.SelectedText;
            Properties.Settings.Default.Relay2Baud = comboBoxBaudRelay2.SelectedText;
            Properties.Settings.Default.Relay3Baud = comboBoxBaudRelay3.SelectedText;
            Properties.Settings.Default.Relay4Baud = comboBoxBaudRelay4.SelectedText;
            Properties.Settings.Default.Relay1Enabled = comboBoxBaudRelay1.Enabled;
            Properties.Settings.Default.Relay2Enabled = comboBoxBaudRelay2.Enabled;
            Properties.Settings.Default.Relay3Enabled = comboBoxBaudRelay3.Enabled;
            Properties.Settings.Default.Relay4Enabled = comboBoxBaudRelay4.Enabled;
            Properties.Settings.Default.Relay1Off = textBoxRelay1Off.Text;
            Properties.Settings.Default.Relay2Off = textBoxRelay2Off.Text;
            Properties.Settings.Default.Relay3Off = textBoxRelay3Off.Text;
            Properties.Settings.Default.Relay4Off = textBoxRelay4Off.Text;
            Properties.Settings.Default.Relay1On = textBoxRelay1On.Text;
            Properties.Settings.Default.Relay2On = textBoxRelay2On.Text;
            Properties.Settings.Default.Relay3On = textBoxRelay3On.Text;
            Properties.Settings.Default.Relay4On = textBoxRelay4On.Text;
            Properties.Settings.Default.Relay1Status = textBoxRelay1Status.Text;
            Properties.Settings.Default.Relay2Status = textBoxRelay2Status.Text;
            Properties.Settings.Default.Relay3Status = textBoxRelay3Status.Text;
            Properties.Settings.Default.Relay4Status = textBoxRelay4Status.Text;

            Properties.Settings.Default.Save();

        }

        private void tabPage3_Click(object sender, EventArgs e)
        {

        }

        bool ToHex(string s, out byte[] data)
        {
            char[] sep = { ' ' };
            string[] tokens = s.Split(sep);
            int i = 0;
            List<byte> dataList = new List<byte>();
            data = null;
            foreach (string t in tokens)
            {
                try
                {
                    dataList.Add(Convert.ToByte(t, 16));
                    ++i;
                }
                catch (Exception)
                {
                    return false;
                }
            }
            data = dataList.ToArray();
            return true;
        }

        private void textBoxRelay1On_Leave(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            string s = textBox.Text;
            if (s.Length > 4)
            {
                string prefix = s.Substring(0, 2);
                if (!prefix.Equals("0x")) return; // not hex, must be string
            }
            if (!ToHex(s, out byte[] data))
            {
                MessageBox.Show("Invalid command format\nExcpected string or hex values e.g. 0x08 0x01");
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            for (int i = 1; i <= 8; ++i)
            {
                richTextBoxRelay1.AppendText(i + " On\n");
                //relay1.Set(i, 1);
                RelaySet(i, 1);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
            for (int i = 1; i <= 8; ++i)
            {
                richTextBoxRelay1.AppendText(i + " Off\n");
                //relay1.Set(i, 0);
                RelaySet(i, 0);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
        }

        private void RelaySet(int nRelay, int flag)
        {
            Button button;
            switch (nRelay)
            {
                case 1: button = button1_1; break;
                case 2: button = button1_2; break;
                case 3: button = button1_3; break;
                case 4: button = button1_4; break;
                case 5: button = button1_5; break;
                case 6: button = button1_6; break;
                case 7: button = button1_7; break;
                case 8: button = button1_8; break;
                default: button = null; break;
            }
            if (flag == 0) button.BackColor = Color.LightGray;
            else button.BackColor = Color.Green;
            relay1.Set(nRelay, (byte)flag);
            byte status = relay1.Status();
            richTextBoxRelay1.AppendText("status=0x" + status.ToString("X"));
        }

        private void buttonTune_Click_1(object sender, EventArgs e)
        {
            char response = Tuner1.Tune();
            if (response == 'T') buttonTunerStatus.BackColor = Color.Green;
            else if (response == 'M') buttonTunerStatus.BackColor = Color.Yellow;
            else if (response == 'F') buttonTunerStatus.BackColor = Color.Red;
            else MessageBox.Show("Unknown response from tuner = '" + response + "'");
        }
    }
}
