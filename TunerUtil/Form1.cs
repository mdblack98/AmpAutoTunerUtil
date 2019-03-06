using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Media;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
// Todo list
// Add CW tuning
// Start up errors when Tuner or Relay not available
// Polymorphize Rig control and add rigctrld interface
// Effect of 12V power loss
// Effect of USB loss
// Rescan COM ports
// Rescan USB devices
//   https://stackoverflow.com/questions/620144/detecting-usb-drive-insertion-and-removal-using-windows-service-and-c-sharp
// Add ethernet switch
//   https://www.sainsmart.com/collections/internet-of-things/products/rj45-ethernet-control-board-for-8-16-ch-relays-1

namespace AmpAutoTunerUtility
{
    public partial class Form1 : Form
    {
        //int baudRelay =0;
        int baudTuner;
        //string comPortRelay = "";
        string comPortTuner = "";
        TcpClient rigClient;
        NetworkStream rigStream;
        double frequencyHz = 0;
        double lastfrequency = 0;
        double lastfrequencyTuned = 0;
        int tolTune = 0;
        Dictionary<int,int> bandTolerance = new Dictionary<int,int>();
        Tuner tuner1 = null;
        Relay relay1 = null;
        Relay relay2 = null;
        Relay relay3 = null;
        Relay relay4 = null;
        bool formLoading = true;
        Stopwatch stopWatchTuner = new Stopwatch();
        int freqStableCount = 0; // 
        int freqStableCountNeeded = 2; //need this number of repeat freqs before tuning starts
        //Form2 form2;
        bool relayMissingCheck = true;
        //Mutex mutex = new Mutex();

        public Form1()
        {
            InitializeComponent();
        }

        delegate void SetTextCallback(string text);

        public void SetTextRig(string text)
        {
            if (this.richTextBoxRig.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetTextRig);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.richTextBoxRig.AppendText(text);

            }
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



    private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
    {
        // Log the exception, display it, etc
        MyMessageBox(e.Exception.Message);
    }

    private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
    {
        // Log the exception, display it, etc
        MyMessageBox((e.ExceptionObject as Exception).Message);
    }
    private void Form1_Load(object sender, EventArgs e)
        {
        Application.ThreadException += new ThreadExceptionEventHandler(Application_ThreadException);
        AppDomain.CurrentDomain.UnhandledException += new UnhandledExceptionEventHandler(CurrentDomain_UnhandledException);

        //Thread t = new Thread(new ThreadStart(SplashStart));
           // t.Start();
            //Form2 form2 = new Form2();
            //form2.Show();

            try
            {
                stopWatchTuner.Start(); // Assume we're already tuned
                LoadComPorts();
                LoadBaudRates();
                FLRigGetFreq();

                // Band tolerance list
                for (int i = 1; i < 60; ++i) {
                    bandTolerance[i] = i*1000;
                }

                // Tuner Properties
                textBoxFreqTol.Text = Properties.Settings.Default.tolTune;
                checkBoxRig.Checked = Properties.Settings.Default.rigEnabled;
                tolTune = Int32.Parse(textBoxFreqTol.Text);
                checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
                radioButtonVFOA.Checked = Properties.Settings.Default.VFOA;
                radioButtonVFOB.Checked = Properties.Settings.Default.VFOB;
                numericUpDownSensitivity.Value = Convert.ToInt32(Properties.Settings.Default.TunerSensitivity);
                // Relay Properties
                checkBoxRelay1Enabled.Checked = Properties.Settings.Default.Relay1Enabled;
                checkBoxRelay2Enabled.Checked = Properties.Settings.Default.Relay2Enabled;
                checkBoxRelay3Enabled.Checked = Properties.Settings.Default.Relay3Enabled;
                checkBoxRelay4Enabled.Checked = Properties.Settings.Default.Relay4Enabled;
                //Set selected items
                //comboBoxTunerModel.SelectedIndex = comboBoxTunerModel.FindStringExact(Properties.Settings.Default.TunerModel);
                comboBoxComTuner.SelectedIndex   = comboBoxComTuner.FindStringExact(Properties.Settings.Default.TunerCom);
                comboBoxBaudTuner.SelectedIndex  = comboBoxBaudTuner.FindStringExact(Properties.Settings.Default.TunerBaud);
                comboBoxTunerModel.SelectedIndex = comboBoxTunerModel.FindStringExact(Properties.Settings.Default.TunerModel);
                comboBoxBaudRelay1.SelectedIndex = comboBoxBaudRelay1.FindStringExact(Properties.Settings.Default.Relay1Baud);
                comboBoxBaudRelay2.SelectedIndex = comboBoxBaudRelay2.FindStringExact(Properties.Settings.Default.Relay2Baud);
                comboBoxBaudRelay3.SelectedIndex = comboBoxBaudRelay3.FindStringExact(Properties.Settings.Default.Relay3Baud);
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

                // Antenna settings
                textBoxAntennaFreq1From.Text = Properties.Settings.Default.textBoxAntennaFreq1From;
                textBoxAntennaFreq2From.Text = Properties.Settings.Default.textBoxAntennaFreq2From;
                textBoxAntennaFreq3From.Text = Properties.Settings.Default.textBoxAntennaFreq3From;
                textBoxAntennaFreq4From.Text = Properties.Settings.Default.textBoxAntennaFreq4From;
                textBoxAntennaFreq5From.Text = Properties.Settings.Default.textBoxAntennaFreq5From;
                textBoxAntennaFreq6From.Text = Properties.Settings.Default.textBoxAntennaFreq6From;
                textBoxAntennaFreq7From.Text = Properties.Settings.Default.textBoxAntennaFreq7From;
                textBoxAntennaFreq8From.Text = Properties.Settings.Default.textBoxAntennaFreq8From;
                textBoxAntennaFreq1To.Text = Properties.Settings.Default.textBoxAntennaFreq1To;
                textBoxAntennaFreq2To.Text = Properties.Settings.Default.textBoxAntennaFreq2To;
                textBoxAntennaFreq3To.Text = Properties.Settings.Default.textBoxAntennaFreq3To;
                textBoxAntennaFreq4To.Text = Properties.Settings.Default.textBoxAntennaFreq4To;
                textBoxAntennaFreq5To.Text = Properties.Settings.Default.textBoxAntennaFreq5To;
                textBoxAntennaFreq6To.Text = Properties.Settings.Default.textBoxAntennaFreq6To;
                textBoxAntennaFreq7To.Text = Properties.Settings.Default.textBoxAntennaFreq7To;
                textBoxAntennaFreq8To.Text = Properties.Settings.Default.textBoxAntennaFreq8To;
                checkBoxAntenna1.Checked = Properties.Settings.Default.checkBoxAntenna1;
                checkBoxAntenna2.Checked = Properties.Settings.Default.checkBoxAntenna2;
                checkBoxAntenna3.Checked = Properties.Settings.Default.checkBoxAntenna3;
                checkBoxAntenna4.Checked = Properties.Settings.Default.checkBoxAntenna4;
                checkBoxAntenna5.Checked = Properties.Settings.Default.checkBoxAntenna5;
                checkBoxAntenna6.Checked = Properties.Settings.Default.checkBoxAntenna6;
                checkBoxAntenna7.Checked = Properties.Settings.Default.checkBoxAntenna7;
                checkBoxAntenna8.Checked = Properties.Settings.Default.checkBoxAntenna8;
                textBoxAntenna1.Text = Properties.Settings.Default.textBoxAntenna1;
                textBoxAntenna2.Text = Properties.Settings.Default.textBoxAntenna2;
                textBoxAntenna3.Text = Properties.Settings.Default.textBoxAntenna3;
                textBoxAntenna4.Text = Properties.Settings.Default.textBoxAntenna4;
                textBoxAntenna5.Text = Properties.Settings.Default.textBoxAntenna5;
                textBoxAntenna6.Text = Properties.Settings.Default.textBoxAntenna6;
                textBoxAntenna7.Text = Properties.Settings.Default.textBoxAntenna7;
                textBoxAntenna8.Text = Properties.Settings.Default.textBoxAntenna8;
                textBoxAntenna1Bits.Text = Properties.Settings.Default.textBoxAntenna1Bits;
                textBoxAntenna2Bits.Text = Properties.Settings.Default.textBoxAntenna2Bits;
                textBoxAntenna3Bits.Text = Properties.Settings.Default.textBoxAntenna3Bits;
                textBoxAntenna4Bits.Text = Properties.Settings.Default.textBoxAntenna4Bits;
                textBoxAntenna5Bits.Text = Properties.Settings.Default.textBoxAntenna5Bits;
                textBoxAntenna6Bits.Text = Properties.Settings.Default.textBoxAntenna6Bits;
                textBoxAntenna7Bits.Text = Properties.Settings.Default.textBoxAntenna7Bits;
                textBoxAntenna8Bits.Text = Properties.Settings.Default.textBoxAntenna8Bits;
                radioButtonAntennaWire3.Checked = Properties.Settings.Default.radioButtonAntennaWire3;
                radioButtonAntennaWire8.Checked = Properties.Settings.Default.radioButtonAntennaWire8;
            }
            catch (Exception ex)
            {
                MyMessageBox("Error loading TunerUtil:\n" + ex.Message);
            }
            //relay1 = new Relay(comboBoxComRelay1.SelectedText, comboBoxBaudRelay1.SelectedText);
            relay1 = new Relay();
            List<string> comPorts = relay1.ComList();
            if (relay1.DevCount() == 0)
            {
                tabControl1.TabPages.Remove(tabPageRelay1);
                tabControl1.TabPages.Remove(tabPageRelay2);
                tabControl1.TabPages.Remove(tabPageRelay3);
                tabControl1.TabPages.Remove(tabPageRelay4);
                relay1.Close();
                relay1 = null;
            }
            else
            {
                if (relay1.DevCount() > 1) relay2 = new Relay();
                if (relay1.DevCount() > 2) relay3 = new Relay();
                if (relay1.DevCount() > 3) relay4 = new Relay();
                switch (comPorts.Count)
                {
                    case 1:
                        tabControl1.TabPages.Remove(tabPageRelay2);
                        tabControl1.TabPages.Remove(tabPageRelay3);
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(2);
                        break;
                    case 2:
                        tabControl1.TabPages.Remove(tabPageRelay3);
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(3);
                        break;
                    case 3:
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(4);
                        break;
                }
            }
            //form2.ProgressBar(1);
            if (comPorts.Count == 0)
            {
                MyMessageBox("No FTDI relay switches found on USB bus");
            }
            else
            {
                foreach (string comPort in comPorts)
                {
                    comboBoxComRelay1.Items.Add(comPort);
                    comboBoxComRelay2.Items.Add(comPort);
                    comboBoxComRelay3.Items.Add(comPort);
                    comboBoxComRelay4.Items.Add(comPort);
                    comboBoxAntenna1Relay.Items.Add(comPort);
                    comboBoxAntenna2Relay.Items.Add(comPort);
                    comboBoxAntenna3Relay.Items.Add(comPort);
                    comboBoxAntenna4Relay.Items.Add(comPort);
                    comboBoxAntenna5Relay.Items.Add(comPort);
                    comboBoxAntenna6Relay.Items.Add(comPort);
                    comboBoxAntenna7Relay.Items.Add(comPort);
                    comboBoxAntenna8Relay.Items.Add(comPort);
                }
            }
            // We have to wait until the relay com ports are loaded before we restore properties for the comboBoxes
            comboBoxComRelay1.SelectedIndex = comboBoxComRelay1.FindStringExact(Properties.Settings.Default.Relay1Com);
            comboBoxComRelay2.SelectedIndex = comboBoxComRelay2.FindStringExact(Properties.Settings.Default.Relay2Com);
            comboBoxComRelay3.SelectedIndex = comboBoxComRelay3.FindStringExact(Properties.Settings.Default.Relay3Com);
            comboBoxComRelay4.SelectedIndex = comboBoxComRelay4.FindStringExact(Properties.Settings.Default.Relay4Com);
            comboBoxAntenna1Relay.SelectedIndex = comboBoxAntenna1Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna1Relay);
            comboBoxAntenna2Relay.SelectedIndex = comboBoxAntenna2Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna2Relay);
            comboBoxAntenna3Relay.SelectedIndex = comboBoxAntenna3Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna3Relay);
            comboBoxAntenna4Relay.SelectedIndex = comboBoxAntenna4Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna4Relay);
            comboBoxAntenna5Relay.SelectedIndex = comboBoxAntenna5Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna5Relay);
            comboBoxAntenna6Relay.SelectedIndex = comboBoxAntenna6Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna6Relay);
            comboBoxAntenna7Relay.SelectedIndex = comboBoxAntenna7Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna7Relay);
            comboBoxAntenna8Relay.SelectedIndex = comboBoxAntenna8Relay.FindStringExact(Properties.Settings.Default.comboBoxAntenna8Relay);

            if (relay1 == null) checkBoxRelay1Enabled.Checked = false;
            if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.Text.Length > 0)
            {
                if (relay1 == null)
                {
                    checkBoxRelay1Enabled.Checked = false;
                    MyMessageBox("Relay 1 not found...disabled");
                    return;
                }
                relay1.Open(comboBoxComRelay1.Text);
                richTextBoxRelay1.AppendText(MyTime() + "Relay 1 opened\n");
                richTextBoxRelay1.AppendText(MyTime() + "Serial number " + relay1.SerialNumber() + "\n");
                //form2.ProgressBar(2);
            }
            else
            {
                checkBoxRelay1Enabled.Checked = false;
            }

            if (relay2 == null) checkBoxRelay2Enabled.Checked = false;
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.Text.Length > 0)
            {
                if (relay2 == null)
                {
                    checkBoxRelay2Enabled.Checked = false;
                    MyMessageBox("Relay 2 not found...disabled");
                    return;
                }
                relay2.Open(comboBoxComRelay2.Text);
                richTextBoxRelay2.AppendText(MyTime() + "Relay 2 opened\n");
                richTextBoxRelay2.AppendText(MyTime() + "Serial number " + relay2.SerialNumber() + "\n");
                //form2.ProgressBar(3);
            }
            else
            {
                checkBoxRelay2Enabled.Checked = false;
            }

            if (relay3 == null) checkBoxRelay3Enabled.Checked = false;
            if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.Text.Length > 0)
            {
                if (relay3 == null)
                {
                    checkBoxRelay3Enabled.Checked = false;
                    MyMessageBox("Relay 3 not found...disabled");
                    return;
                }
                relay3.Open(comboBoxComRelay3.Text);
                richTextBoxRelay3.AppendText(MyTime() + "Relay 3 opened\n");
                richTextBoxRelay3.AppendText(MyTime() + "Serial number " + relay3.SerialNumber() + "\n");
                //form2.ProgressBar(4);
            }
            else
            {
                checkBoxRelay3Enabled.Checked = false;
            }

            if (relay4 == null) checkBoxRelay4Enabled.Checked = false;
            if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.Text.Length > 0)
            {
                if (relay4 == null)
                {
                    checkBoxRelay4Enabled.Checked = false;
                    MyMessageBox("Relay 4 not found...disabled");
                    return;
                }
                relay4.Open(comboBoxComRelay4.Text);
                richTextBoxRelay4.AppendText(MyTime() + "Relay 4 opened\n");
                richTextBoxRelay4.AppendText(MyTime() + "Serial number " + relay4.SerialNumber() + "\n");
                //form2.ProgressBar(5);
            }
            else
            {
                checkBoxRelay4Enabled.Checked = false;
            }

            if (checkBoxTunerEnabled.Checked)
            {
                try
                {
                    if (comboBoxTunerModel.Text.Equals("LDG"))
                    {
                        tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                    }
                    else if (comboBoxTunerModel.Text.Equals("MFJ-928"))
                    {
                        tuner1 = new TunerMFJ928(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                        // We don't need any command information
                    }

                    if (tuner1 == null || tuner1.GetSerialPortTuner() == null)
                    {
                        MyMessageBox("Error starting tuner!!!");
                    }
                    else
                    {
                        richTextBoxTuner.AppendText(MyTime() + "tuner opened\n");
                    }
                }
                catch (Exception ex)
                {
                    MyMessageBox("Error starting tuner\nFix and reenable the Tuner"+ex.Message);
                    checkBoxTunerEnabled.Checked = false;

                }
            }

            //form2.Close();
            //t.Abort();
            //CheckMissingRelay("FormOpen");
            //tabControl1.SelectedTab = tabPageRelay1;
            timerGetFreq.Interval = 500;
            timerGetFreq.Enabled = true;

            formLoading = false;
        }

        public void SplashStart()
        {
            //form2 = new Form2();
            //Application.Run(form2);
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timerGetFreq.Stop();
            if (relay1 != null) relay1.Close();
            if (relay2 != null) relay2.Close();
            if (relay3 != null) relay3.Close();
            if (relay4 != null) relay4.Close();
            DisconnectFLRig();
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
            Properties.Settings.Default.rigEnabled = checkBoxRig.Checked;
            Properties.Settings.Default.TunerEnabled = checkBoxTunerEnabled.Checked;
            Properties.Settings.Default.TunerModel = comboBoxTunerModel.Text;
            Properties.Settings.Default.TunerCom = comboBoxComTuner.Text;
            Properties.Settings.Default.TunerBaud = comboBoxBaudTuner.Text;
            Properties.Settings.Default.VFOA = radioButtonVFOA.Checked;
            Properties.Settings.Default.VFOB = radioButtonVFOB.Checked;
            Properties.Settings.Default.TunerSensitivity = Convert.ToInt32(numericUpDownSensitivity.Value);

            CheckMissingRelay("Closing");
            Properties.Settings.Default.Relay1Enabled = checkBoxRelay1Enabled.Checked;
            Properties.Settings.Default.Relay2Enabled = checkBoxRelay2Enabled.Checked;
            Properties.Settings.Default.Relay3Enabled = checkBoxRelay3Enabled.Checked;
            Properties.Settings.Default.Relay4Enabled = checkBoxRelay4Enabled.Checked;

            Properties.Settings.Default.Relay1Com = comboBoxComRelay1.Text;
            Properties.Settings.Default.Relay2Com = comboBoxComRelay2.Text;
            Properties.Settings.Default.Relay3Com = comboBoxComRelay3.Text;
            Properties.Settings.Default.Relay4Com = comboBoxComRelay4.Text;

            Properties.Settings.Default.Relay1Baud = comboBoxBaudRelay1.Text;
            Properties.Settings.Default.Relay2Baud = comboBoxBaudRelay2.Text;
            Properties.Settings.Default.Relay3Baud = comboBoxBaudRelay3.Text;
            Properties.Settings.Default.Relay4Baud = comboBoxBaudRelay4.Text;

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

            // Antenna settings
            Properties.Settings.Default.textBoxAntennaFreq1From = textBoxAntennaFreq1From.Text;
            Properties.Settings.Default.textBoxAntennaFreq2From = textBoxAntennaFreq2From.Text;
            Properties.Settings.Default.textBoxAntennaFreq3From = textBoxAntennaFreq3From.Text;
            Properties.Settings.Default.textBoxAntennaFreq4From = textBoxAntennaFreq4From.Text;
            Properties.Settings.Default.textBoxAntennaFreq5From = textBoxAntennaFreq5From.Text;
            Properties.Settings.Default.textBoxAntennaFreq6From = textBoxAntennaFreq6From.Text;
            Properties.Settings.Default.textBoxAntennaFreq7From = textBoxAntennaFreq7From.Text;
            Properties.Settings.Default.textBoxAntennaFreq8From = textBoxAntennaFreq8From.Text;
            Properties.Settings.Default.textBoxAntennaFreq1To = textBoxAntennaFreq1To.Text;
            Properties.Settings.Default.textBoxAntennaFreq2To = textBoxAntennaFreq2To.Text;
            Properties.Settings.Default.textBoxAntennaFreq3To = textBoxAntennaFreq3To.Text;
            Properties.Settings.Default.textBoxAntennaFreq4To = textBoxAntennaFreq4To.Text;
            Properties.Settings.Default.textBoxAntennaFreq5To = textBoxAntennaFreq5To.Text;
            Properties.Settings.Default.textBoxAntennaFreq6To = textBoxAntennaFreq6To.Text;
            Properties.Settings.Default.textBoxAntennaFreq7To = textBoxAntennaFreq7To.Text;
            Properties.Settings.Default.textBoxAntennaFreq8To = textBoxAntennaFreq8To.Text;
            Properties.Settings.Default.checkBoxAntenna1 = checkBoxAntenna1.Checked;
            Properties.Settings.Default.checkBoxAntenna2 = checkBoxAntenna2.Checked;
            Properties.Settings.Default.checkBoxAntenna3 = checkBoxAntenna3.Checked;
            Properties.Settings.Default.checkBoxAntenna4 = checkBoxAntenna4.Checked;
            Properties.Settings.Default.checkBoxAntenna5 = checkBoxAntenna5.Checked;
            Properties.Settings.Default.checkBoxAntenna6 = checkBoxAntenna6.Checked;
            Properties.Settings.Default.checkBoxAntenna7 = checkBoxAntenna7.Checked;
            Properties.Settings.Default.checkBoxAntenna8 = checkBoxAntenna8.Checked;
            Properties.Settings.Default.textBoxAntenna1 = textBoxAntenna1.Text;
            Properties.Settings.Default.textBoxAntenna2 = textBoxAntenna2.Text;
            Properties.Settings.Default.textBoxAntenna3 = textBoxAntenna3.Text;
            Properties.Settings.Default.textBoxAntenna4 = textBoxAntenna4.Text;
            Properties.Settings.Default.textBoxAntenna5 = textBoxAntenna5.Text;
            Properties.Settings.Default.textBoxAntenna6 = textBoxAntenna6.Text;
            Properties.Settings.Default.textBoxAntenna7 = textBoxAntenna7.Text;
            Properties.Settings.Default.textBoxAntenna8 = textBoxAntenna8.Text;
            Properties.Settings.Default.comboBoxAntenna1Relay = comboBoxAntenna1Relay.Text;
            Properties.Settings.Default.comboBoxAntenna2Relay = comboBoxAntenna2Relay.Text;
            Properties.Settings.Default.comboBoxAntenna3Relay = comboBoxAntenna3Relay.Text;
            Properties.Settings.Default.comboBoxAntenna4Relay = comboBoxAntenna4Relay.Text;
            Properties.Settings.Default.comboBoxAntenna5Relay = comboBoxAntenna5Relay.Text;
            Properties.Settings.Default.comboBoxAntenna6Relay = comboBoxAntenna6Relay.Text;
            Properties.Settings.Default.comboBoxAntenna7Relay = comboBoxAntenna7Relay.Text;
            Properties.Settings.Default.comboBoxAntenna8Relay = comboBoxAntenna8Relay.Text;
            Properties.Settings.Default.radioButtonAntennaWire3 = radioButtonAntennaWire3.Checked;
            Properties.Settings.Default.radioButtonAntennaWire8 = radioButtonAntennaWire8.Checked;

            Properties.Settings.Default.Save();

        }

        private void CheckMissingRelay(string info)
        {
            return;
            info = MyTime() + info + "\n";
            if (relayMissingCheck == false) return;
            if (!checkBoxRelay1Enabled.Checked)
            {
                richTextBoxRelay1.AppendText(info+"Relay 1 not set ???\n");
                MyMessageBox(info+"Relay 1 not set???");
                relayMissingCheck = false;
            }
            if (!comboBoxComRelay1.Text.Contains("COM"))
            {
                richTextBoxRelay1.AppendText(info+"Relay 1 missing COM port???\n");
                MyMessageBox(info+"Relay 1 missing COM port???");
                relayMissingCheck = false;
            }
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
                if (comboBoxComTuner.Items.Count == 0)
                {
                    comboBoxComTuner.Items.Add(s);
                }
                else
                {
                    int numSerPort = Int32.Parse(s.Substring(3));
                    string s2 = comboBoxComTuner.Items[comboBoxComTuner.Items.Count - 1].ToString();
                    int numLastItem = Int32.Parse(s2.Substring(3));
                    if (numSerPort > numLastItem)
                    {
                        comboBoxComTuner.Items.Add(s);
                    }
                    else
                    {
                        // Figure out where it goes
                        int ii = 0;
                        foreach (string s3 in comboBoxComTuner.Items)
                        {
                            int numCurrentItem = Int32.Parse(s3.Substring(3));
                            if (numSerPort < numCurrentItem) { 
                                comboBoxComTuner.Items.Insert(ii, s);
                                break;
                            }
                            ++ii;
                        }
                    }
                }
            }
        }

        private void Relay(bool on)
        {
            if (on == true)
            {
                // turn relay on
                MyMessageBox("Relay On");
            }
            else
            {
                MyMessageBox("Relay Off");
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
            richTextBoxTuner.AppendText(MyTime() + "Tuning\n");
        }

        private void comboBoxTunerCom_SelectedIndexChanged(object sender, EventArgs e)
        {
            comPortTuner = comboBoxComTuner.Text;
            if (baudTuner > 0) richTextBoxTuner.AppendText(MyTime() + "Opening Tuner " + comPortTuner+":"+baudTuner+"\n");
        }

        private void comboBoxTunerBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            baudTuner = Int32.Parse(comboBoxBaudTuner.Text);
            if (comPortTuner.Length > 0) richTextBoxTuner.AppendText(MyTime() + "Opening Tuner " + comPortTuner + ":" + baudTuner+"\n");

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
                rigStream.ReadTimeout = 500;
                FLRigWait();
                richTextBoxRig.AppendText(MyTime() + "FLRig connected\n");
            }
            catch (Exception ex)
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    richTextBoxRig.AppendText(MyTime() + "FLRig not responding...\n");
                }
                else
                {
                    richTextBoxRig.AppendText(MyTime() + "FLRig unexpected error:\n" + ex.Message + "\n");
                }
            }
        }

        private void DisconnectFLRig()
        {
            // Nothing to do
            // If we let it close naturally we don't get the TIME_WAIT 
            try
            {
                //LingerOption lingerOption = new LingerOption(true, 0);
                //rigClient.LingerState = lingerOption;
                //rigStream.Close(0);
                //rigClient.Close();
                //rigStream = null;
                //rigClient = null;
            }
            catch (Exception)
            {
                
            }
        }

        // Returns true if send is OK
        private bool FLRigSend(string xml)
        {
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
                if (xml.Contains("rig.set"))
                {
                    int saveTimeout = rigStream.ReadTimeout;
                    // Just read the response and ignore it for now
                    rigStream.ReadTimeout = 500;
                    byte[] data2 = new byte[4096];
                    Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                    String responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                    if (!responseData.Contains("200 OK"))
                    {
                        MyMessageBox("Unknown response from FLRig\n" + responseData);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText(MyTime() + "FLRig error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                tabControl1.SelectedTab = tabPageRig;
                //DisconnectFLRig();
                return false;
            }
            return true;
        }

        // returns true when Tuner and FLRig are talking to us
        // if ptt is true then will use ptt and audio tone for tuning -- e.g. MFJ-928 without radio interface
        private bool Tune(bool ptt)
        {
            //richTextBoxRig.AppendText(MyTime() + "Tune mutex wait\n");
            //mutex.WaitOne();
            //richTextBoxRig.AppendText(MyTime() + "Tune mutex gotit\n");
            Thread.Sleep(250);
            richTextBoxTuner.AppendText(MyTime() + "Tuning to " + frequencyHz+"\n");
            char vfo = 'A';
            if (radioButtonVFOA.Checked) vfo = 'B';
            string xml = FLRigXML("rig.set_vfo"+vfo,"<params><param><value><double> "+frequencyHz+" </double></value></param></params");
            if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error

            string mode = FLRigGetMode();
            string myparam = "<params><param><value>" + mode + "</value></param></params>";
            xml = FLRigXML("rig.set_modeB", myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                richTextBoxRig.AppendText(MyTime() + "FLRig Tune got an error??\n");
            }

            buttonTunerStatus.BackColor = Color.LightGray;
            RelaySet(relay1, 1, 1);
            Application.DoEvents();
            if (ptt) // we turn on PTT and send the audio tone before starting tune
            {
                xml = FLRigXML("rig.set_ptt" + vfo, "<params><param><value><i4>1</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                richTextBoxRig.AppendText(MyTime() + "ptt on\n");
            }
            // Wait a bit for amplifier to be disabled and PTT to take effect
            Thread.Sleep(500);
            // Now start our audio
            char response = tuner1.Tune();
            // stop audio here
            Application.DoEvents();
            if (ptt) // we turn on PTT and send the audio tone before starting tune
            {
                xml = FLRigXML("rig.set_ptt" + vfo, "<params><param><value><i4>0</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                richTextBoxRig.AppendText(MyTime() + "ptt off\n");
            }
            Thread.Sleep(500); // give a little time for ptt off and audio to stop
            RelaySet(relay1, 1, 0);
            Application.DoEvents();
            if (response == 'T')
            {
                buttonTunerStatus.BackColor = Color.Green;
                labelSWR.Text = "SWR < 1.5";
            }
            else if (response == 'M')
            {
                SoundPlayer simpleSound = new SoundPlayer("swr.wav");
                simpleSound.Play();
                buttonTunerStatus.BackColor = Color.Yellow;
                labelSWR.Text = "SWR 1.5-3.0";
            }
            else if (response == 'F')
            {
                SoundPlayer simpleSound = new SoundPlayer("swr.wav");
                simpleSound.Play();
                buttonTunerStatus.BackColor = Color.Red;
                labelSWR.Text = "SWR > 3.0";
            }
            else
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
                tabControl1.SelectedTab = tabPageRig;
                buttonTunerStatus.BackColor = Color.Transparent;
                MyMessageBox("Unknown response from tuner = '" + response + "'");
            }
            richTextBoxTuner.AppendText(MyTime() + "Tuning done\n");
            //mutex.ReleaseMutex();
            return true;
        }

        // Wait for FLRig to return valid data
        private void FLRigWait()
        {
            string xcvr = "";
            while((xcvr=FLRigGetXcvr()) == null) {
                Thread.Sleep(500);
            }
            richTextBoxRig.AppendText(MyTime() + "Rig is " + xcvr +"\n");
        }

        private string FLRigGetXcvr()
        {
            //richTextBoxRig.AppendText(MyTime() + "Xcvr mutex wait\n");
            //mutex.WaitOne();
            //richTextBoxRig.AppendText(MyTime() + "Xcvr mutex gotit\n");
            string xcvr = null;

            if (!checkBoxRig.Checked) return null;
            if (rigClient == null) { ConnectFLRig(); }
            string xml2 = FLRigXML("rig.get_xcvr", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText(MyTime() + "FLRigGetXcvr error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                tabControl1.SelectedTab = tabPageRig;
                //DisconnectFLRig();
                //mutex.ReleaseMutex();
                return null;
            }
            data = new Byte[4096];
            String responseData = String.Empty;
            int timeoutSave = rigStream.ReadTimeout;
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>") + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>");
                        xcvr = responseData.Substring(offset1, offset2 - offset1);
                        if (xcvr.Length == 0) xcvr = null;
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        richTextBoxRig.AppendText(MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageRig;
                    }
                }
                catch (Exception)
                {
                    richTextBoxRig.AppendText(MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText(MyTime() + "Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            rigStream.ReadTimeout = timeoutSave;
            //mutex.ReleaseMutex();
            return xcvr;
        }

        private string FLRigGetMode()
        {
            //richTextBoxRig.AppendText(MyTime() + "Mode mutex wait\n");
            //mutex.WaitOne();
            //richTextBoxRig.AppendText(MyTime() + "Mode mutex gotit\n");
            string mode = "Unknown";

            if (!checkBoxRig.Checked) return null;
            if (rigClient == null) { ConnectFLRig(); }
            string xml2 = FLRigXML("rig.get_modeA", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText(MyTime() + "FLRigGetMode error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                tabControl1.SelectedTab = tabPageRig;
                //DisconnectFLRig();
                //mutex.ReleaseMutex();
                return null;
            }
            data = new Byte[4096];
            String responseData = String.Empty;
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>") + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>");
                        mode = responseData.Substring(offset1, offset2 - offset1);
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        richTextBoxRig.AppendText(MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageRig;
                    }
                }
                catch (Exception)
                {
                    richTextBoxRig.AppendText(MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
            catch (Exception ex)
            {
                richTextBoxRig.AppendText(MyTime() + "Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            //mutex.ReleaseMutex();
            return mode;
        }

        private string MyTime()
        {
            string time = DateTime.Now.ToString("HH:mm:ss.f") + ": "; 
            return time;
        }

        private void SetAntennaInUse()
        {
            double frequencyMHz = frequencyHz / 1000000;

            buttonAntenna1.BackColor = Color.LightGray;
            buttonAntenna2.BackColor = Color.LightGray;
            buttonAntenna3.BackColor = Color.LightGray;
            buttonAntenna4.BackColor = Color.LightGray;
            buttonAntenna5.BackColor = Color.LightGray;
            buttonAntenna6.BackColor = Color.LightGray;
            buttonAntenna7.BackColor = Color.LightGray;
            buttonAntenna8.BackColor = Color.LightGray;
            try
            {
                if (checkBoxAntenna1.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq1From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq1To.Text))
                {
                    checkBoxAntenna1.Checked = true;
                    buttonAntenna1.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna1.Text;
                }
                else if (checkBoxAntenna2.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq2From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq2To.Text))
                {
                    checkBoxAntenna2.Checked = true;
                    buttonAntenna2.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna2.Text;
                }
                else if (checkBoxAntenna3.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq3From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq3To.Text))
                {
                    checkBoxAntenna3.Checked = true;
                    buttonAntenna3.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna3.Text;
                }
                else if (checkBoxAntenna4.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq4From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq4To.Text))
                {
                    checkBoxAntenna4.Checked = true;
                    buttonAntenna4.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna4.Text;
                }
                else if (checkBoxAntenna5.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq5From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq5To.Text))
                {
                    checkBoxAntenna5.Checked = true;
                    buttonAntenna5.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna5.Text;
                }
                else if (checkBoxAntenna6.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq6From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq6To.Text))
                {
                    checkBoxAntenna6.Checked = true;
                    buttonAntenna6.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna6.Text;
                }
                else if (checkBoxAntenna7.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq7From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq7To.Text))
                {
                    checkBoxAntenna7.Checked = true;
                    buttonAntenna7.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna7.Text;
                }
                else if (checkBoxAntenna8.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq8From.Text) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq8To.Text))
                {
                    checkBoxAntenna8.Checked = true;
                    buttonAntenna8.BackColor = Color.Green;
                    labelAntennaSelected.Text = "Antenna: " + textBoxAntenna8.Text;
                }
            }
            catch (Exception)
            {
                // don't do anything here...just catching the parse errors from blank boxes
            }
        }

        private string FLRigGetActiveVFO()
        {
            string vfo = "A";
            string xml2 = FLRigXML("rig.get_AB", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            rigStream.Write(data, 0, data.Length);
            Byte[] data2 = new byte[4096];
            Int32 bytes = rigStream.Read(data2, 0, data2.Length);
            string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
            if (responseData.Contains("<value>B"))
            {
                vfo = "B";
            }
            return vfo;
        }

        private void FLRigSetActiveVFO(string mode)
        {
            string myparam = "<params><param><value>" + mode + "</value></param></params>";
            string xml2 = FLRigXML("rig.set_AB", myparam);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            rigStream.Write(data, 0, data.Length);
            // Ignore the response for now
            Byte[] data2 = new byte[4096];
            Int32 bytes = rigStream.Read(data2, 0, data2.Length);
        }

        private void FLRigGetFreq()
        {
            //richTextBoxRig.AppendText(MyTime() + "Freq mutex wait\n");
            //mutex.WaitOne();
            //richTextBoxRig.AppendText(MyTime() + "Freq mutex gotit\n");
            if (!checkBoxRig.Checked) return;
            if (rigClient == null)
            {
                ConnectFLRig();
                if (rigClient == null) return;
            }
            string currVFO = FLRigGetActiveVFO();
            if (currVFO.Equals("B"))
            {
                MyMessageBox("Auto tuning paused...click OK to continue");
                FLRigSetActiveVFO("A");
            }
            string vfo = "B";
            if (radioButtonVFOA.Checked) vfo = "A";
            string xml2 = FLRigXML("rig.get_vfo" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    richTextBoxRig.AppendText(MyTime() + "Did FLRig shut down?\n");
                }
                else
                {
                    richTextBoxRig.AppendText(MyTime() + "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                tabControl1.SelectedTab = tabPageRig;
                //DisconnectFLRig();
                //mutex.ReleaseMutex();
                return;
            }
            data = new Byte[4096];
            String responseData = String.Empty;
            rigStream.ReadTimeout = 2000;
            try {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>") + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>");
                        string freqString = responseData.Substring(offset1, offset2 - offset1);
                        frequencyHz = Double.Parse(freqString);
                        labelFreq.Text = (frequencyHz / 1000).ToString() + "kHz";
                        if (lastfrequency == 0) lastfrequency = lastfrequencyTuned = frequencyHz;
                        if (lastfrequency == frequencyHz && freqStableCount < freqStableCountNeeded)
                        {
                            ++freqStableCount;
                            stopWatchTuner.Restart();
                        }
                        if (freqStableCount >= freqStableCountNeeded && frequencyHz != lastfrequencyTuned && Math.Abs(frequencyHz - lastfrequencyTuned) > tolTune)
                        {
                            if (stopWatchTuner.IsRunning && stopWatchTuner.ElapsedMilliseconds < 1 * 1000)
                            {
                                stopWatchTuner.Stop();
                                //MyMessageBox("Rapid frequency changes...click OK when ready to tune");
                                richTextBoxRig.AppendText(MyTime() + "Rapid frequency changes\n");
                                stopWatchTuner.Reset();
                                stopWatchTuner.Stop();
                                //mutex.ReleaseMutex();
                                return; // we'll tune on next poll
                            }
                            stopWatchTuner.Restart();
                            if (checkBoxTunerEnabled.Checked)
                            {
                                lastfrequencyTuned = frequencyHz;
                                Tune(true);
                            }
                            else
                            {
                                richTextBoxTuner.AppendText(MyTime() + "Tuner not enabled\n");
                                richTextBoxTuner.AppendText(MyTime() + "Simulate tuning to " + frequencyHz + "\n");
                                char vfoOther = 'A';
                                if (radioButtonVFOA.Checked) vfoOther = 'B';
                                string myparam = "<params><param><value><double>"+frequencyHz+"</double></value></param></params";
                                string xml = FLRigXML("rig.set_vfo" + vfoOther, myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    richTextBoxRig.AppendText(MyTime() + "FLRigSend got an error??\n");
                                }
                                // Set VFOB mode to match VFOa
                                string mode = FLRigGetMode();
                                myparam = "<params><param><value>" + mode + "</value></param></params>";
                                xml = FLRigXML("rig.set_modeB", myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    richTextBoxRig.AppendText(MyTime() + "FLRigSend got an error??\n");
                                }

                                lastfrequencyTuned = lastfrequency = frequencyHz;
                                relay1.Set(1, 1);
                                MyMessageBox("Click OK to continue");
                                relay1.Set(1, 0);
                                freqStableCount = 0;
                            }
                        }
                        else
                        {
                            lastfrequency = frequencyHz;
                        }
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        richTextBoxRig.AppendText(MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageRig;
                    }
                }
                catch (Exception)
                {
                    richTextBoxRig.AppendText(MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
            catch (Exception ex) {
                richTextBoxRig.AppendText(MyTime() + "Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            //mutex.ReleaseMutex();
        }

        private void TimerGetFreq_Tick(object sender, EventArgs e)
        {
            CheckMissingRelay("Timer tick");

            if (checkBoxRig.Checked)
            {
                FLRigGetFreq();
                SetAntennaInUse();
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
                rigClient = null;
                //DisconnectFLRig();
            }
        }

        private void textBoxFreqTol_Leave(object sender, EventArgs e)
        {
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
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
                MyMessageBox("Invalid command format\nExcpected string or hex values e.g. 0x08 0x01");
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void button1_Click(object sender, EventArgs e)
        {
            string currTime = MyTime();
            for (int i = 1; i <= 8; ++i)
            {
                richTextBoxRelay1.AppendText(MyTime() + i + " On\n");
                //relay1.Set(i, 1);
                RelaySet(relay1, i, 1);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
            for (int i = 1; i <= 8; ++i)
            {
                richTextBoxRelay1.AppendText(MyTime() + i + " Off\n");
                //relay1.Set(i, 0);
                RelaySet(relay1, i, 0);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
        }

        private void RelaySet(Relay relay, int nRelay, int flag)
        {
            Button button; // the button we will mess with here to change color
            List<Button> buttons = new List<Button>();
            switch(relay.RelayNumber())
            {
                case 1:
                    buttons.Add(button1_1);
                    buttons.Add(button1_2);
                    buttons.Add(button1_3);
                    buttons.Add(button1_4);
                    buttons.Add(button1_5);
                    buttons.Add(button1_6);
                    buttons.Add(button1_7);
                    buttons.Add(button1_8);
                    break;
                case 2:
                    buttons.Add(button25);
                    buttons.Add(button24);
                    buttons.Add(button23);
                    buttons.Add(button22);
                    buttons.Add(button21);
                    buttons.Add(button20);
                    buttons.Add(button19);
                    buttons.Add(button18);
                    break;
                case 3:
                    buttons.Add(button9);
                    buttons.Add(button8);
                    buttons.Add(button7);
                    buttons.Add(button6);
                    buttons.Add(button5);
                    buttons.Add(button4);
                    buttons.Add(button3);
                    buttons.Add(button2);
                    break;
                case 4:
                    buttons.Add(button17);
                    buttons.Add(button16);
                    buttons.Add(button15);
                    buttons.Add(button14);
                    buttons.Add(button13);
                    buttons.Add(button12);
                    buttons.Add(button11);
                    buttons.Add(button10);
                    break;
            }

            button = buttons[nRelay-1];
            if (flag == 0) button.BackColor = Color.Silver;
            else button.BackColor = Color.Green;
            relay.Set(nRelay, (byte)flag);
            byte status = relay.Status();
            richTextBoxRelay1.AppendText(MyTime() + "status=0x" + status.ToString("X")+"\n");
        }

        private void buttonTune_Click_1(object sender, EventArgs e)
        {
            if (tuner1 == null)
            {
                MyMessageBox("Tuner not enabled");
                return;
            }
            Tune(true);
        }

        private string FLRigXML(string cmd, string value)
        {
            string xmlHeader = "POST / RPC2 HTTP / 1.1\r\n";
            xmlHeader += "User - Agent: XMLRPC++ 0.8\r\n";
            xmlHeader += "Host: 127.0.0.1:12345\r\n";
            xmlHeader += "Content-type: text/xml\r\n";
            string xmlContent = "<?xml version=\"1.0\"?>\r\n";
            xmlContent += "<methodCall><methodName>";
            xmlContent += cmd;
            xmlContent += "</methodName>\r\n";
            if (value != null && value.Length > 0)
            {
                xmlContent += value;
            }
            xmlContent += "</methodCall>\r\n";
            xmlHeader += "Content-length: "+xmlContent.Length+"\r\n\r\n";
            string xml = xmlHeader + xmlContent;
            return xml;
        }

        public void MyMessageBox(string message)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            MessageBoxHelper.PrepToCenterMessageBoxOnForm(this);
            MessageBox.Show(message);

        }

        private void checkBoxTunerEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading) return;
            if (checkBoxTunerEnabled.Checked)
            {
                try
                {
                    tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                    if (tuner1.GetSerialPortTuner() == null)
                    {
                        richTextBoxTuner.AppendText(MyTime() + " tuner open failed\n");
                        MyMessageBox("Tuner open failed");
                        return;
                    }
                    richTextBoxTuner.AppendText(MyTime() + "tuner opened\n");
                }
                catch (Exception ex)
                {
                    checkBoxTunerEnabled.Checked = false;
                    MyMessageBox("Error starting tuner\nFix problem and reenable the Tuner" + ex.Message);
                }
            }
            else
            {
                richTextBoxTuner.AppendText(MyTime() + "tuner closed\n");
                tuner1.Close();
            }
        }

        private void comboBoxTunerModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            //string s = comboBoxTunerModel.SelectedText;
            //MyMessageBox(s);
        }

        private void comboBoxComTuner_SelectedIndexChanged(object sender, EventArgs e)
        {
            //string s = comboBoxTunerModel.SelectedIndex+"\n"+comboBoxBaudTuner.SelectedIndex+"\n";
            //MyMessageBox(s);
        }

        private void checkBoxRelay1Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                //if (relay1 != null) MyMessageBox("Relay1 != null??");
                relay1 = new Relay();
                richTextBoxRelay1.AppendText(MyTime() + "Relay1 open\n");
                relay1.Open(comboBoxComRelay1.Text);
                richTextBoxRelay1.AppendText(MyTime() + "Serial number " + relay1.SerialNumber() +"\n");
            }
            else if (!checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                richTextBoxRelay2.AppendText(MyTime() + "Relay1 close\n");
                relay1.Close();
            }
            else if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex < 0)
            {
                MyMessageBox("Select COM port before enabling Relay");
                checkBoxRelay1Enabled.Checked = false;
            }
        }

        private void richTextBoxTuner_TextChanged(object sender, EventArgs e)
        {

        }

        private void button17_Click(object sender, EventArgs e)
        {
        }

        private void RelayToggle(Button relayButton, Relay relay, int nRelay)
        {
            if (relayButton.BackColor == Color.Silver)
            {
                RelaySet(relay, nRelay, 1);
            }
            else
            {
                RelaySet(relay, nRelay, 0);
            }
        }

        private void button1_1_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 1);
        }

        private void button1_2_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 2);
        }

        private void button1_3_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 3);
        }

        private void button1_4_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 4);
        }

        private void button1_5_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 5);
        }

        private void button1_6_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 6);
        }

        private void button1_7_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 7);
        }

        private void button1_8_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 8);
        }

        private void numericUpDownSensitivity_ValueChanged(object sender, EventArgs e)
        {
            freqStableCountNeeded = (int)numericUpDownSensitivity.Value;
        }

        private void linkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                //Call the Process.Start method to open the default browser   
                //with a URL:  
                System.Diagnostics.Process.Start("https://www.amazon.com/gp/product/B009A5246E/ref=ppx_yo_dt_b_asin_title_o03_s00?ie=UTF8&psc=1");
            }
            catch (Exception)
            {
                MyMessageBox("Unable to open link that was clicked.");
            }
        }

        private void linkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                //Call the Process.Start method to open the default browser   
                //with a URL:  
                System.Diagnostics.Process.Start("https://www.amazon.com/SainSmart-Eight-Channel-Relay-Automation/dp/B0093Y89DE/ref=pd_sim_328_5/142-3490108-0951025?_encoding=UTF8&pd_rd_i=B0093Y89DE&pd_rd_r=5fec5905-3a9a-11e9-8c37-9bb5683a7901&pd_rd_w=JVLew&pd_rd_wg=ZMtfu&pf_rd_p=90485860-83e9-4fd9-b838-b28a9b7fda30&pf_rd_r=KYZW2V3QB1MS6TFW8Q1Q&psc=1&refRID=KYZW2V3QB1MS6TFW8Q1Q");
            }
            catch (Exception ex)
            {
                MyMessageBox("Unable to open link that was clicked\n"+ex.Message);
            }
        }

        private void button25_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 1);
        }

        private void button24_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 2);
        }

        private void button23_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 3);
        }

        private void button22_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 4);
        }

        private void button21_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 5);
        }

        private void button20_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 6);
        }

        private void button19_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 7);
        }

        private void button18_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 8);
        }

        private void checkBoxRelay2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                relay2 = new Relay();
                richTextBoxRelay2.AppendText(MyTime() + "Relay2 open\n");
                relay2.Open(comboBoxComRelay2.Text);
                richTextBoxRelay2.AppendText(MyTime() + "Serial number " + relay2.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                richTextBoxRelay2.AppendText(MyTime() + "Relay2 close\n");
                relay2.Close();
            }
            else if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex < 0)
            {
                MyMessageBox("Select COM port before enabling Relay2");
                checkBoxRelay2Enabled.Checked = false;
            }
        }

        private void buttonTune_Click_2(object sender, EventArgs e)
        {

        }

        private void Form1_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            MessageBox.Show("Help");
        }
    }
}
