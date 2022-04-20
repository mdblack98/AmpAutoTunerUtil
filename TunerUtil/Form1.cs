//using NAudio.Wave;
using Microsoft.VisualBasic;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Management;
using System.Media;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static AmpAutoTunerUtility.DebugMsg;
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
        //int baudTuner;
        //string comPortRelay = "";
        //string comPortTuner = "";
        HelpForm helpForm;
        AboutForm aboutForm;
        TcpClient rigClient;
        NetworkStream rigStream;
        double frequencyHz = 0;
        double frequencyLast = 0;
        double frequencyLastTunedHz = 0;
        int tolTune = 0;
        Tuner tuner1 = null;
        Relay relay1 = null;
        Relay relay2 = null;
        Relay relay3 = null;
        Relay relay4 = null;
        bool formLoading = true;
        readonly Stopwatch stopWatchTuner = new Stopwatch();
        int freqStableCount = 0; // 
        readonly int freqStableCountNeeded = 2; //need this number of repeat freqs before tuning starts
        bool pausedTuning = false;
        private bool freqWalkIsRunning;
        private bool pauseButtonClicked;
        private Audio audio;
        bool formClosing = false; // use this to detect shutdown condition
        bool getFreqIsRunning = false;
        private string modeCurrent = "UNK";
        private readonly string MFJ928 = "MFJ-928";
        //private int Capacitance = 0;
        //private int Inductance = 0;
        DebugEnum debugLevel = DebugEnum.WARN;
        private bool activatedHasExecuted = false;
        private bool tuneIsRunning;
        private bool ampIsOn;
        private bool _disposed;
        private int lastAntennaNumberUsed = -1;
        public static bool clockIsZulu;


        public Form1()
        {
            InitializeComponent();
        }

        //DebugEnum DebugLevel()
        //{
        //    return (DebugEnum)comboBoxDebugLevel.SelectedIndex;
        //}

        protected override void Dispose(bool disposing)
        {
            if (_disposed) return;
            base.Dispose(disposing);
            if (disposing && (components != null))
            {
                components.Dispose();
                components = null;
            }
            if (disposing)
            {
                if (rigStream != null) { rigStream.Dispose(); rigStream = null; }
                if (rigClient != null) { rigClient.Dispose(); rigClient = null; }
                if (tuner1 != null) { tuner1.Dispose(); tuner1 = null; }
                if (audio != null) { audio.Dispose(); audio = null; }
            }
            _disposed = true;
        }


        //InvokeAndClose((MethodInvoker) delegate
        //   {
        //    this.Debug(DebugEnum.ERR, TextBox);
        //    });
        /*
       private delegate void SetTextCallback(string text);

        public void SetTextRig(string text)
        {
            if (checkBoxPause.Checked) return;
            if (this.richTextBoxDebug.InvokeRequired)
            {
                var d = new SetTextCallback(SetTextRig);
                this.BeginInvoke(d, new object[] { text });
            }
            else
            {
                this.Debug(DebugEnum.ERR,text);
            }
        }
        */

        /*
        private bool SetSelectedIndexOf(ComboBox sender, string match)
        {
            foreach (string s in sender.Items)
            {
                if (s.Equals(match, System.StringComparison.OrdinalIgnoreCase))
                {
                    //sender.Sel
                }
            }
            return false;
        }
        */


        private void Application_ThreadException(object sender, ThreadExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            MyMessageBox(e.Exception.Message);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            MessageBox.Show((e.ExceptionObject as Exception).Message + "\n" + (e.ExceptionObject as Exception).StackTrace);
        }

        private void AntennaAddRelay(string name)
        {
            comboBoxAntenna1Relay.Items.Add(name);
            comboBoxAntenna2Relay.Items.Add(name);
            comboBoxAntenna3Relay.Items.Add(name);
            comboBoxAntenna4Relay.Items.Add(name);
            comboBoxAntenna5Relay.Items.Add(name);
            comboBoxAntenna6Relay.Items.Add(name);
            comboBoxAntenna7Relay.Items.Add(name);
            comboBoxAntenna8Relay.Items.Add(name);
        }

        private void RelaySetButtons(Button button, byte status)
        {
            if (button == button1_1)
            {
                button1_1.BackColor = (status & 0x01) == 0 ? Color.Gray : Color.Green;
                button1_2.BackColor = (status & 0x02) == 0 ? Color.Gray : Color.Green;
                button1_3.BackColor = (status & 0x04) == 0 ? Color.Gray : Color.Green;
                button1_4.BackColor = (status & 0x08) == 0 ? Color.Gray : Color.Green;
                button1_5.BackColor = (status & 0x10) == 0 ? Color.Gray : Color.Green;
                button1_6.BackColor = (status & 0x20) == 0 ? Color.Gray : Color.Green;
                button1_7.BackColor = (status & 0x40) == 0 ? Color.Gray : Color.Green;
                button1_8.BackColor = (status & 0x80) == 0 ? Color.Gray : Color.Green;
            }
            else if (button == button2_1)
            {
                button2_1.BackColor = (status & 0x01) == 0 ? Color.Gray : Color.Green;
                button2_2.BackColor = (status & 0x02) == 0 ? Color.Gray : Color.Green;
                button2_3.BackColor = (status & 0x04) == 0 ? Color.Gray : Color.Green;
                button2_4.BackColor = (status & 0x08) == 0 ? Color.Gray : Color.Green;
                button2_5.BackColor = (status & 0x10) == 0 ? Color.Gray : Color.Green;
                button2_6.BackColor = (status & 0x20) == 0 ? Color.Gray : Color.Green;
                button2_7.BackColor = (status & 0x40) == 0 ? Color.Gray : Color.Green;
                button2_8.BackColor = (status & 0x80) == 0 ? Color.Gray : Color.Green;
            }
            else if (button == button3_1)
            {
                button3_1.BackColor = (status & 0x01) == 0 ? Color.Gray : Color.Green;
                button3_2.BackColor = (status & 0x02) == 0 ? Color.Gray : Color.Green;
                button3_3.BackColor = (status & 0x04) == 0 ? Color.Gray : Color.Green;
                button3_4.BackColor = (status & 0x08) == 0 ? Color.Gray : Color.Green;
                button3_5.BackColor = (status & 0x10) == 0 ? Color.Gray : Color.Green;
                button3_6.BackColor = (status & 0x20) == 0 ? Color.Gray : Color.Green;
                button3_7.BackColor = (status & 0x40) == 0 ? Color.Gray : Color.Green;
                button3_8.BackColor = (status & 0x80) == 0 ? Color.Gray : Color.Green;
            }
            else if (button == button4_1)
            {
                button4_1.BackColor = (status & 0x01) == 0 ? Color.Gray : Color.Green;
                button4_2.BackColor = (status & 0x02) == 0 ? Color.Gray : Color.Green;
                button4_3.BackColor = (status & 0x04) == 0 ? Color.Gray : Color.Green;
                button4_4.BackColor = (status & 0x08) == 0 ? Color.Gray : Color.Green;
                button4_5.BackColor = (status & 0x10) == 0 ? Color.Gray : Color.Green;
                button4_6.BackColor = (status & 0x20) == 0 ? Color.Gray : Color.Green;
                button4_7.BackColor = (status & 0x40) == 0 ? Color.Gray : Color.Green;
                button4_8.BackColor = (status & 0x80) == 0 ? Color.Gray : Color.Green;
            }
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
                Properties.Settings.Default.Upgrade();
                WindowLoadLocationMain();
                LoadComPorts();
                LoadBaudRates();

                // Tuner Properties
                textBoxFreqTol.Text = Properties.Settings.Default.tolTune;
                checkBoxRig.Checked = Properties.Settings.Default.rigEnabled;
                tolTune = Int32.Parse(textBoxFreqTol.Text, CultureInfo.InvariantCulture);
                radioButtonVFOA.Checked = Properties.Settings.Default.VFOA;
                radioButtonVFOB.Checked = Properties.Settings.Default.VFOB;
                checkBoxPTTEnabled.Checked = Properties.Settings.Default.checkBoxPTTEnabled;
                checkBoxToneEnabled.Checked = Properties.Settings.Default.checkBoxToneEnabled;
                string myAudio = Properties.Settings.Default.comboBoxAudioOut;
                checkBoxPowerSDR.Checked = Properties.Settings.Default.powerSDR;

                //Set selected items
                //comboBoxTunerModel.SelectedIndex = comboBoxTunerModel.FindStringExact(Properties.Settings.Default.TunerModel);
                comboBoxComTuner.SelectedIndex = comboBoxComTuner.FindStringExact(Properties.Settings.Default.TunerCom);
                comboBoxBaudTuner.SelectedIndex = comboBoxBaudTuner.FindStringExact(Properties.Settings.Default.TunerBaud);
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
                ComboBoxAntenna1Bits.Text = Properties.Settings.Default.textBoxAntenna1Bits;
                ComboBoxAntenna2Bits.Text = Properties.Settings.Default.textBoxAntenna2Bits;
                ComboBoxAntenna3Bits.Text = Properties.Settings.Default.textBoxAntenna3Bits;
                ComboBoxAntenna4Bits.Text = Properties.Settings.Default.textBoxAntenna4Bits;
                ComboBoxAntenna5Bits.Text = Properties.Settings.Default.textBoxAntenna5Bits;
                ComboBoxAntenna6Bits.Text = Properties.Settings.Default.textBoxAntenna6Bits;
                ComboBoxAntenna7Bits.Text = Properties.Settings.Default.textBoxAntenna7Bits;
                ComboBoxAntenna8Bits.Text = Properties.Settings.Default.textBoxAntenna8Bits;
                comboBoxAmpBits.Text = Properties.Settings.Default.AmpBits;

                // Power tab settings
                textBoxPower1From.Text = Properties.Settings.Default.Power1From;
                textBoxPower2From.Text = Properties.Settings.Default.Power2From;
                textBoxPower3From.Text = Properties.Settings.Default.Power3From;
                textBoxPower4From.Text = Properties.Settings.Default.Power4From;
                textBoxPower5From.Text = Properties.Settings.Default.Power5From;
                textBoxPower6From.Text = Properties.Settings.Default.Power6From;
                textBoxPower7From.Text = Properties.Settings.Default.Power7From;
                textBoxPower8From.Text = Properties.Settings.Default.Power8From;
                textBoxPower1To.Text = Properties.Settings.Default.Power1To;
                textBoxPower2To.Text = Properties.Settings.Default.Power2To;
                textBoxPower3To.Text = Properties.Settings.Default.Power3To;
                textBoxPower4To.Text = Properties.Settings.Default.Power4To;
                textBoxPower5To.Text = Properties.Settings.Default.Power5To;
                textBoxPower6To.Text = Properties.Settings.Default.Power6To;
                textBoxPower7To.Text = Properties.Settings.Default.Power7To;
                textBoxPower8To.Text = Properties.Settings.Default.Power8To;
                textBoxPower1Watts.Text = Properties.Settings.Default.Power1Watts;
                textBoxPower2Watts.Text = Properties.Settings.Default.Power2Watts;
                textBoxPower3Watts.Text = Properties.Settings.Default.Power3Watts;
                textBoxPower4Watts.Text = Properties.Settings.Default.Power4Watts;
                textBoxPower5Watts.Text = Properties.Settings.Default.Power5Watts;
                textBoxPower6Watts.Text = Properties.Settings.Default.Power6Watts;
                textBoxPower7Watts.Text = Properties.Settings.Default.Power7Watts;
                textBoxPower8Watts.Text = Properties.Settings.Default.Power8Watts;
                textBoxTune1Power.Text = Properties.Settings.Default.Tune1Power;
                textBoxTune2Power.Text = Properties.Settings.Default.Tune2Power;
                textBoxTune3Power.Text = Properties.Settings.Default.Tune3Power;
                textBoxTune4Power.Text = Properties.Settings.Default.Tune4Power;
                textBoxTune5Power.Text = Properties.Settings.Default.Tune5Power;
                textBoxTune6Power.Text = Properties.Settings.Default.Tune6Power;
                textBoxTune7Power.Text = Properties.Settings.Default.Tune7Power;
                textBoxTune8Power.Text = Properties.Settings.Default.Tune8Power;
                // enable after values are set so they can get checked properly
                checkBoxPower1Enabled.Checked = Properties.Settings.Default.Power1Enabled;
                checkBoxPower2Enabled.Checked = Properties.Settings.Default.Power2Enabled;
                checkBoxPower3Enabled.Checked = Properties.Settings.Default.Power3Enabled;
                checkBoxPower4Enabled.Checked = Properties.Settings.Default.Power4Enabled;
                checkBoxPower5Enabled.Checked = Properties.Settings.Default.Power5Enabled;
                checkBoxPower6Enabled.Checked = Properties.Settings.Default.Power6Enabled;
                checkBoxPower7Enabled.Checked = Properties.Settings.Default.Power7Enabled;
                checkBoxPower8Enabled.Checked = Properties.Settings.Default.Power8Enabled;
                checkBoxAmp1.Checked = Properties.Settings.Default.Amp1;
                checkBoxAmp2.Checked = Properties.Settings.Default.Amp2;
                checkBoxAmp3.Checked = Properties.Settings.Default.Amp3;
                checkBoxAmp4.Checked = Properties.Settings.Default.Amp4;
                checkBoxAmp5.Checked = Properties.Settings.Default.Amp5;
                checkBoxAmp6.Checked = Properties.Settings.Default.Amp6;
                checkBoxAmp7.Checked = Properties.Settings.Default.Amp7;
                checkBoxAmp8.Checked = Properties.Settings.Default.Amp8;

                //textBoxFrequencyWalkList.Text = Properties.Settings.Default.FrequencyWalkList;

            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading TunerUtil:\n" + ex.Message + "\n" + ex.StackTrace);
                throw;
            }
            //relay1 = new Relay(comboBoxComRelay1.SelectedText, comboBoxBaudRelay1.SelectedText);
            relay1 = new Relay();
            List<string> comPorts = relay1.ComList();
            if (relay1.DevCount() == 0)
            {
                //tabControl1.TabPages.Remove(tabPageRelay1);
                tabPage.TabPages.Remove(tabPageRelay2);
                tabPage.TabPages.Remove(tabPageRelay3);
                tabPage.TabPages.Remove(tabPageRelay4);
                //relay1.Close();
                relay1 = null;
            }
            else
            {
                relay1.Open(comPorts[0]);
                RelaySetButtons(button1_1, relay1.Status());
                Debug(DebugEnum.LOG, "Relay1 Opened, serial#" + relay1.SerialNumber() + "\n");
                labelRelay1SerialNumber.Text = relay1.SerialNumber();
                AntennaAddRelay("Relay1");
                RelaySetButtons(button1_1, relay1.Status());
                if (relay1.DevCount() > 1)
                {
                    relay2 = new Relay();
                    relay2.Open(comPorts[1]);
                    Debug(DebugEnum.LOG, "Relay2 Opened, serial#" + relay2.SerialNumber() + "\n");
                    labelRelay2SerialNumber.Text = relay2.SerialNumber();
                    AntennaAddRelay("Relay2");
                }
                if (relay1.DevCount() > 2)
                {
                    relay3 = new Relay();
                    relay3.Open(comPorts[2]);
                    Debug(DebugEnum.LOG, "Relay3 Opened, serial#" + relay3.SerialNumber() + "\n");
                    labelRelay3SerialNumber.Text = relay3.SerialNumber();
                    AntennaAddRelay("Relay3");
                }
                if (relay1.DevCount() > 3)
                {
                    relay4 = new Relay();
                    relay4.Open(comPorts[3]);
                    Debug(DebugEnum.LOG, "Relay4 Opened, serial#" + relay4.SerialNumber() + "\n");
                    labelRelay4SerialNumber.Text = relay4.SerialNumber();
                    AntennaAddRelay("Relay4");
                }
                switch (comPorts.Count)
                {
                    case 1:
                        tabPage.SelectTab(tabPageRelay1);
                        tabPage.TabPages.Remove(tabPageRelay2);
                        tabPage.TabPages.Remove(tabPageRelay3);
                        tabPage.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(2);
                        break;
                    case 2:
                        tabPage.SelectTab(tabPageRelay1);
                        tabPage.SelectTab(tabPageRelay2);

                        tabPage.TabPages.Remove(tabPageRelay3);
                        tabPage.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(3);
                        break;
                    case 3:
                        tabPage.SelectTab(tabPageRelay1);
                        tabPage.SelectTab(tabPageRelay2);
                        tabPage.SelectTab(tabPageRelay3);
                        tabPage.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(4);
                        break;
                    case 4:
                        tabPage.SelectTab(tabPageRelay1);
                        tabPage.SelectTab(tabPageRelay2);
                        tabPage.SelectTab(tabPageRelay3);
                        tabPage.SelectTab(tabPageRelay4);
                        break;
                }
            }
            if (comPorts.Count == 0)
            {
                Debug(DebugEnum.TRACE, "No FTDI relay switches found on USB bus\n");
            }
            else
            {
                foreach (string comPort in comPorts)
                {
                    comboBoxComRelay1.Items.Add(comPort);
                    comboBoxComRelay2.Items.Add(comPort);
                    comboBoxComRelay3.Items.Add(comPort);
                    comboBoxComRelay4.Items.Add(comPort);
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

            // Relay Properties
            checkBoxRelay1Enabled.Checked = Properties.Settings.Default.Relay1Enabled;
            checkBoxRelay2Enabled.Checked = Properties.Settings.Default.Relay2Enabled;
            checkBoxRelay3Enabled.Checked = Properties.Settings.Default.Relay3Enabled;
            checkBoxRelay4Enabled.Checked = Properties.Settings.Default.Relay4Enabled;
            if (relay1 == null)
                checkBoxRelay1Enabled.Checked = false;
            if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.Text.Length > 0)
            {
                if (relay1 == null)
                {
                    checkBoxRelay1Enabled.Checked = false;
                    Debug(DebugEnum.WARN, "Relay 1 not found...disabled\n");
                    MyMessageBox("Relay 1 not found...disabled");
                    return;
                }
                relay1.Open(comboBoxComRelay1.Text);
                RelaySetButtons(button1_1, relay1.Status());
                Debug(DebugEnum.WARN, "Relay 1 opened\n");
                Debug(DebugEnum.WARN, "Serial number " + relay1.SerialNumber() + "\n");
                //form2.ProgressBar(2);
            }
            else
            {
                checkBoxRelay1Enabled.Checked = true;
            }
            if (!checkBoxRelay1Enabled.Checked && checkBoxPowerSDR.Enabled == false)
            {
                MyMessageBox("Please set up Relay1 if not using PowerSDR");
            }
            if (relay1 == null) checkBoxRelay1Enabled.Checked = false;
            if (checkBoxRelay1Enabled.Checked && relay1.SerialNumber().Length == 0)
            {
                MyMessageBox("No serial#?  Relay1 is not responding!");
            }
            if (relay2 == null) checkBoxRelay2Enabled.Checked = false;
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.Text.Length > 0)
            {
                if (relay2 == null)
                {
                    checkBoxRelay2Enabled.Checked = false;
                    MyMessageBox("Relay 2 not found...disabled");
                    Debug(DebugEnum.WARN, "Relay 2 not found...disabled\n");
                    return;
                }
                relay2.Open(comboBoxComRelay2.Text);
                Debug(DebugEnum.LOG, "Relay 2 opened\n");
                Debug(DebugEnum.LOG, "Serial number " + relay2.SerialNumber() + "\n");
                //form2.ProgressBar(3);
            }
            else
            {
                checkBoxRelay2Enabled.Checked = true;
            }

            if (relay3 == null) checkBoxRelay3Enabled.Checked = false;
            if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.Text.Length > 0)
            {
                if (relay3 == null)
                {
                    checkBoxRelay3Enabled.Checked = false;
                    MyMessageBox("Relay 3 not found...disabled");
                    Debug(DebugEnum.WARN, "Relay 3 not found...disabled\n");
                    return;
                }
                relay3.Open(comboBoxComRelay3.Text);
                Debug(DebugEnum.LOG, "Relay 3 opened\n");
                Debug(DebugEnum.LOG, "Serial number " + relay3.SerialNumber() + "\n");
                //form2.ProgressBar(4);
            }
            else
            {
                checkBoxRelay3Enabled.Checked = true;
            }

            if (relay4 == null) checkBoxRelay4Enabled.Checked = false;
            if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.Text.Length > 0)
            {
                if (relay4 == null)
                {
                    checkBoxRelay4Enabled.Checked = false;
                    MyMessageBox("Relay 4 not found...disabled");
                    Debug(DebugEnum.WARN, "Relay 4 not found...disabled\n");
                    return;
                }
                relay4.Open(comboBoxComRelay4.Text);
                Debug(DebugEnum.LOG, "Relay 4 opened\n");
                Debug(DebugEnum.LOG, "Serial number " + relay4.SerialNumber() + "\n");
                //form2.ProgressBar(5);
            }
            else
            {
                checkBoxRelay4Enabled.Checked = false;
            }


            //form2.Close();
            //t.Abort();
            //CheckMissingRelay("FormOpen");
            //tabControl1.SelectedTab = tabPageRelay1;

            // Disable checkboxes if things are not set correctly
            if (comboBoxComTuner.SelectedIndex == -1 || comboBoxTunerModel.SelectedIndex == -1 || comboBoxBaudTuner.SelectedIndex == -1)
            {
                checkBoxTunerEnabled.Checked = false;
                //checkBoxTunerEnabled.Enabled = false;
            }
            // if any relay is implemented that has variable baud rate want to check the Baud too
            // Like the following line
            //if (comboBoxComRelay3.SelectedIndex == -1 || comboBoxBaudRelay3.SelectedIndex == -1)
            if (comboBoxComRelay1.SelectedIndex == -1)
            {
                checkBoxRelay1Enabled.Checked = false;
                //checkBoxRelay1Enabled.Enabled = false;
            }
            if (comboBoxComRelay2.SelectedIndex == -1)
            {
                checkBoxRelay2Enabled.Checked = false;
                //checkBoxRelay2Enabled.Enabled = false;
            }
            if (comboBoxComRelay3.SelectedIndex == -1)
            {
                checkBoxRelay3Enabled.Checked = false;
                //checkBoxRelay3Enabled.Enabled = false;
            }
            if (comboBoxComRelay4.SelectedIndex == -1)
            {
                checkBoxRelay4Enabled.Checked = false;
                //checkBoxRelay4Enabled.Enabled = false;
            }
            audio = new Audio();
            //if (audio.errMsg != null)
            //{
            //    MyMessageBox("Error opening audio device\n" + audio.errMsg);
            //}
            AudioDeviceOutputLoad();

            comboBoxAntSelect1.Text = Properties.Settings.Default.AntSelect1;
            comboBoxAntSelect2.Text = Properties.Settings.Default.AntSelect2;
            comboBoxAntSelect3.Text = Properties.Settings.Default.AntSelect3;
            comboBoxAntSelect4.Text = Properties.Settings.Default.AntSelect4;
            comboBoxAntSelect5.Text = Properties.Settings.Default.AntSelect5;
            comboBoxAntSelect6.Text = Properties.Settings.Default.AntSelect6;
            comboBoxAntSelect7.Text = Properties.Settings.Default.AntSelect7;
            comboBoxAntSelect8.Text = Properties.Settings.Default.AntSelect8;

            formLoading = false;
            /*
            // Check to ensure window is on the screen
            Rectangle rect = SystemInformation.VirtualScreen;
            if (this.Location.X < 0 || this.Location.Y < 0)
            {
                this.Top = 0;
                this.Left = 0;
                //this.Width = 300;
                //this.Height = 400;
            }
            if (this.Location.X > rect.Width || this.Location.Y > rect.Bottom)
            {
                this.Top = 0;
                this.Left = 0;
            }
            */
            // we have to select that tabs for the Application Properties to be loaded
            //tabControl1.SelectTab(tabPagePower);
            //tabControl1.SelectTab(tabPageAntenna);
            //tabControl1.SelectTab(tabPageRelay4);
            //tabControl1.SelectTab(tabPageRelay3);
            //tabControl1.SelectTab(tabPageRelay2);
            //tabControl1.SelectTab(tabPageRelay1);
            //tabControl1.SelectTab(tabPageTuner);
            //tabControl1.SelectTab(tabPageControl);
            tabPage.SelectTab(tabPageControl);
            //buttonAmp.BackColor = Color.Green;
            checkBoxAntenna1Amp.Checked = Properties.Settings.Default.AntennaAmp1;
            checkBoxAntenna2Amp.Checked = Properties.Settings.Default.AntennaAmp2;
            checkBoxAntenna3Amp.Checked = Properties.Settings.Default.AntennaAmp3;
            checkBoxAntenna4Amp.Checked = Properties.Settings.Default.AntennaAmp4;
            checkBoxAntenna5Amp.Checked = Properties.Settings.Default.AntennaAmp5;
            checkBoxAntenna6Amp.Checked = Properties.Settings.Default.AntennaAmp6;
            checkBoxAntenna7Amp.Checked = Properties.Settings.Default.AntennaAmp7;
            checkBoxAntenna8Amp.Checked = Properties.Settings.Default.AntennaAmp8;

            clockIsZulu = Properties.Settings.Default.ClockIsZulu;
            int index = 0;

            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkFT8))
            {
                Properties.Settings.Default.FrequenciesToWalkFT8.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        index = this.checkedListBoxWalkFT8.Items.IndexOf(item);
                        this.checkedListBoxWalkFT8.SetItemChecked(index, true);
                    });
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkFT4))
            {
                Properties.Settings.Default.FrequenciesToWalkFT4.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        index = this.checkedListBoxWalkFT4.Items.IndexOf(item);
                        this.checkedListBoxWalkFT4.SetItemChecked(index, true);
                    });
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkCustom))
            {
                Properties.Settings.Default.FrequenciesToWalkCustom.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        index = this.checkedListBoxWalkCustom.Items.IndexOf(item);
                        this.checkedListBoxWalkCustom.SetItemChecked(index, true);
                    });
            }
            var mystuff = Properties.Settings.Default.FrequenciesToWalkFT8List;
            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkFT8List))
            {
                index = 0;
                Properties.Settings.Default.FrequenciesToWalkFT8List.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        this.checkedListBoxWalkFT8.Items[index++] = item;
                    });
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkFT4List))
            {
                index = 0;
                var xxx = Properties.Settings.Default.FrequenciesToWalkFT4List;
                Properties.Settings.Default.FrequenciesToWalkFT4List.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        this.checkedListBoxWalkFT4.Items[index++] = item;
                    });
            }
            if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalkCustomList))
            {
                index = 0;
                Properties.Settings.Default.FrequenciesToWalkCustomList.Split(',')
                    .ToList()
                    .ForEach(item =>
                    {
                        this.checkedListBoxWalkCustom.Items[index++] = item;
                    });
            }


            Thread.Sleep(100);
            ComboBoxNRelaysSet();
            SetWalkAntennaToUse(Properties.Settings.Default.WalkAntenna);
            AntennaSetPickButtons();
            Application.DoEvents();
            checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
            //TunerOpen();
            //if (ampIsOn == 0) AmpToggle();
            stopWatchTuner.Start(); // Assume we're already tuned

            timerFreqWalkSeconds = Properties.Settings.Default.TimerFreqWalkSeconds;
            FreqWalkSetIntervalDisplay();
            timerGetFreq.Interval = 200;
            timerGetFreq.Enabled = true;
            timerDebug.Interval = 100;
            timerDebug.Enabled = true;
            timerDebug.Start();
        }

        private void GetModes()
        {
            List<string> modes = FLRigGetModes();
            if (modes != null)
            {
                modes.Sort();
                modes.Insert(0, "Any");
                foreach (string mode in modes)
                {
                    comboBoxPower1Mode.Items.Add(mode);
                    comboBoxPower2Mode.Items.Add(mode);
                    comboBoxPower3Mode.Items.Add(mode);
                    comboBoxPower4Mode.Items.Add(mode);
                    comboBoxPower5Mode.Items.Add(mode);
                    comboBoxPower6Mode.Items.Add(mode);
                    comboBoxPower7Mode.Items.Add(mode);
                    comboBoxPower8Mode.Items.Add(mode);
                }
            }

        }
        private void TunerClose()
        {
            if (tuner1 != null) tuner1.Close();
            tuner1 = null;
        }

        private void TunerOpen()
        {
            Application.DoEvents();
            try
            {
                string errorMsg = null;
                TunerClose();
                if (comboBoxTunerModel.Text.Equals("LDG", StringComparison.InvariantCulture))
                {
                    comboBoxBaudTuner.Text = "38400";
                    tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                }
                else if (comboBoxTunerModel.Text.Equals(MFJ928, StringComparison.InvariantCulture))
                {
                    comboBoxBaudTuner.Text = "4800";
                    tuner1 = new TunerMFJ928(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text, out errorMsg);
                    // We don't need any command information
                }

                if (tuner1 == null || tuner1.GetSerialPortTuner() == null || errorMsg != null)
                {
                    if (errorMsg != null)
                    {
                        MyMessageBox(errorMsg);
                    }
                    else
                    {
                        MyMessageBox("Error starting tuner!!!");
                    }
                }
                else
                {
                    Debug(DebugEnum.LOG, "Tuner opened on " + tuner1.GetSerialPortTuner() + "\n");
                    checkBoxTunerEnabled.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                MyMessageBox("Error starting tuner\nFix and reenable the Tuner" + ex.Message);
                checkBoxTunerEnabled.Checked = false;

            }

        }
        private void AudioDeviceOutputLoad()
        {
            //foreach (NAudio.Wave.DirectSoundDeviceInfo cap in audio.DeviceInfo)
            //{
            //    ComboBoxItem cbox = new ComboBoxItem
            //    {
            //        Text = cap.Description,
            //       MyGUID = cap.Guid
            //   };
            //   comboBoxAudioOut.Items.Add(cbox);
            //}
        }
        private void WindowLoadLocationMain()
        {
            if (Properties.Settings.Default.MaximizedMain)
            {
                WindowState = FormWindowState.Maximized;
                Location = Properties.Settings.Default.LocationMain;
                Size = Properties.Settings.Default.SizeMain;
            }
            else if (Properties.Settings.Default.MinimizedMain)
            {
                WindowState = FormWindowState.Minimized;
                Location = Properties.Settings.Default.LocationMain;
                Size = Properties.Settings.Default.SizeMain;
            }
            else
            {
                Location = Properties.Settings.Default.LocationMain;
                Size = Properties.Settings.Default.SizeMain;
            }
        }

        private void WindowSaveLocationMain()
        {
            if (WindowState == FormWindowState.Maximized)
            {
                Properties.Settings.Default.LocationMain = RestoreBounds.Location;
                Properties.Settings.Default.SizeMain = RestoreBounds.Size;
                Properties.Settings.Default.MaximizedMain = true;
                Properties.Settings.Default.MinimizedMain = false;
            }
            else if (WindowState == FormWindowState.Normal)
            {
                Properties.Settings.Default.LocationMain = Location;
                Properties.Settings.Default.SizeMain = Size;
                Properties.Settings.Default.MaximizedMain = false;
                Properties.Settings.Default.MinimizedMain = false;
            }
            else
            {
                Properties.Settings.Default.LocationMain = RestoreBounds.Location;
                Properties.Settings.Default.SizeMain = RestoreBounds.Size;
                Properties.Settings.Default.MaximizedMain = false;
                Properties.Settings.Default.MinimizedMain = true;
            }
            //Properties.Settings.Default.Save();
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timerFreqWalk.Stop();
            timerGetFreq.Stop();
            if (formClosing)
                return;
            Thread.Sleep(1000);
            formClosing = true;
            //tabControl1.Enabled = false;
            timerDebug.Stop();
            timerGetFreq.Stop();
            //Thread.Sleep(500);
            DisconnectFLRig();
            WindowSaveLocationMain();
            int loop = 60;
            while (getFreqIsRunning && --loop > 0)
            {
                Thread.Sleep(100);
            }
            //if (tuner1 != null) tuner1.Close();
            //if (relay1 != null) relay1.Close();
            //if (relay2 != null) relay2.Close();
            //if (relay3 != null) relay3.Close();
            //if (relay4 != null) relay4.Close();
            Thread.Sleep(1000);  // was getting memory overflow during shutdown under debug -- this seems to have fixed it
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
            Properties.Settings.Default.rigEnabled = checkBoxRig.Checked;
            Properties.Settings.Default.TunerEnabled = checkBoxTunerEnabled.Checked;
            Properties.Settings.Default.TunerModel = comboBoxTunerModel.Text;
            Properties.Settings.Default.TunerCom = comboBoxComTuner.Text;
            Properties.Settings.Default.TunerBaud = comboBoxBaudTuner.Text;
            Properties.Settings.Default.VFOA = radioButtonVFOA.Checked;
            Properties.Settings.Default.VFOB = radioButtonVFOB.Checked;
            Properties.Settings.Default.checkBoxPTTEnabled = checkBoxPTTEnabled.Checked;
            Properties.Settings.Default.checkBoxToneEnabled = checkBoxToneEnabled.Checked;
            Properties.Settings.Default.powerSDR = checkBoxPowerSDR.Checked;

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

            Properties.Settings.Default.Power1From = textBoxPower1From.Text;
            Properties.Settings.Default.Power2From = textBoxPower2From.Text;
            Properties.Settings.Default.Power3From = textBoxPower3From.Text;
            Properties.Settings.Default.Power4From = textBoxPower4From.Text;
            Properties.Settings.Default.Power5From = textBoxPower5From.Text;
            Properties.Settings.Default.Power6From = textBoxPower6From.Text;
            Properties.Settings.Default.Power7From = textBoxPower7From.Text;
            Properties.Settings.Default.Power8From = textBoxPower8From.Text;
            Properties.Settings.Default.Power1To = textBoxPower1To.Text;
            Properties.Settings.Default.Power2To = textBoxPower2To.Text;
            Properties.Settings.Default.Power3To = textBoxPower3To.Text;
            Properties.Settings.Default.Power4To = textBoxPower4To.Text;
            Properties.Settings.Default.Power5To = textBoxPower5To.Text;
            Properties.Settings.Default.Power6To = textBoxPower6To.Text;
            Properties.Settings.Default.Power7To = textBoxPower7To.Text;
            Properties.Settings.Default.Power8To = textBoxPower8To.Text;
            Properties.Settings.Default.Power1Enabled = checkBoxPower1Enabled.Checked;
            Properties.Settings.Default.Power2Enabled = checkBoxPower2Enabled.Checked;
            Properties.Settings.Default.Power3Enabled = checkBoxPower3Enabled.Checked;
            Properties.Settings.Default.Power4Enabled = checkBoxPower4Enabled.Checked;
            Properties.Settings.Default.Power5Enabled = checkBoxPower5Enabled.Checked;
            Properties.Settings.Default.Power6Enabled = checkBoxPower6Enabled.Checked;
            Properties.Settings.Default.Power7Enabled = checkBoxPower7Enabled.Checked;
            Properties.Settings.Default.Power8Enabled = checkBoxPower8Enabled.Checked;
            Properties.Settings.Default.Amp1 = checkBoxAmp1.Checked;
            Properties.Settings.Default.Amp2 = checkBoxAmp2.Checked;
            Properties.Settings.Default.Amp3 = checkBoxAmp3.Checked;
            Properties.Settings.Default.Amp4 = checkBoxAmp4.Checked;
            Properties.Settings.Default.Amp5 = checkBoxAmp5.Checked;
            Properties.Settings.Default.Amp6 = checkBoxAmp6.Checked;
            Properties.Settings.Default.Amp7 = checkBoxAmp7.Checked;
            Properties.Settings.Default.Amp8 = checkBoxAmp8.Checked;
            Properties.Settings.Default.Power1Watts = textBoxPower1Watts.Text;
            Properties.Settings.Default.Power2Watts = textBoxPower2Watts.Text;
            Properties.Settings.Default.Power3Watts = textBoxPower3Watts.Text;
            Properties.Settings.Default.Power4Watts = textBoxPower4Watts.Text;
            Properties.Settings.Default.Power5Watts = textBoxPower5Watts.Text;
            Properties.Settings.Default.Power6Watts = textBoxPower6Watts.Text;
            Properties.Settings.Default.Power7Watts = textBoxPower7Watts.Text;
            Properties.Settings.Default.Power8Watts = textBoxPower8Watts.Text;
            Properties.Settings.Default.Tune1Power = textBoxTune1Power.Text;
            Properties.Settings.Default.Tune2Power = textBoxTune2Power.Text;
            Properties.Settings.Default.Tune3Power = textBoxTune3Power.Text;
            Properties.Settings.Default.Tune4Power = textBoxTune4Power.Text;
            Properties.Settings.Default.Tune5Power = textBoxTune5Power.Text;
            Properties.Settings.Default.Tune6Power = textBoxTune6Power.Text;
            Properties.Settings.Default.Tune7Power = textBoxTune7Power.Text;
            Properties.Settings.Default.Tune8Power = textBoxTune8Power.Text;

            Properties.Settings.Default.AntSelect1 = comboBoxAntSelect1.Text;
            Properties.Settings.Default.AntSelect2 = comboBoxAntSelect2.Text;
            Properties.Settings.Default.AntSelect3 = comboBoxAntSelect3.Text;
            Properties.Settings.Default.AntSelect4 = comboBoxAntSelect4.Text;
            Properties.Settings.Default.AntSelect5 = comboBoxAntSelect5.Text;
            Properties.Settings.Default.AntSelect6 = comboBoxAntSelect6.Text;
            Properties.Settings.Default.AntSelect7 = comboBoxAntSelect7.Text;
            Properties.Settings.Default.AntSelect8 = comboBoxAntSelect8.Text;

            Properties.Settings.Default.textBoxAntenna1Bits = ComboBoxAntenna1Bits.Text;
            Properties.Settings.Default.textBoxAntenna2Bits = ComboBoxAntenna2Bits.Text;
            Properties.Settings.Default.textBoxAntenna3Bits = ComboBoxAntenna3Bits.Text;
            Properties.Settings.Default.textBoxAntenna4Bits = ComboBoxAntenna4Bits.Text;
            Properties.Settings.Default.textBoxAntenna5Bits = ComboBoxAntenna5Bits.Text;
            Properties.Settings.Default.textBoxAntenna6Bits = ComboBoxAntenna6Bits.Text;
            Properties.Settings.Default.textBoxAntenna7Bits = ComboBoxAntenna7Bits.Text;
            Properties.Settings.Default.textBoxAntenna8Bits = ComboBoxAntenna8Bits.Text;

            Properties.Settings.Default.AntennaAmp1 = checkBoxAntenna1Amp.Checked;
            Properties.Settings.Default.AntennaAmp2 = checkBoxAntenna2Amp.Checked;
            Properties.Settings.Default.AntennaAmp3 = checkBoxAntenna3Amp.Checked;
            Properties.Settings.Default.AntennaAmp4 = checkBoxAntenna4Amp.Checked;
            Properties.Settings.Default.AntennaAmp5 = checkBoxAntenna5Amp.Checked;
            Properties.Settings.Default.AntennaAmp6 = checkBoxAntenna6Amp.Checked;
            Properties.Settings.Default.AntennaAmp7 = checkBoxAntenna7Amp.Checked;
            Properties.Settings.Default.AntennaAmp8 = checkBoxAntenna8Amp.Checked;

            Properties.Settings.Default.AmpBits = comboBoxAmpBits.Text;

            //Properties.Settings.Default.FrequencyWalkList = textBoxFrequencyWalkList.Text;
            Properties.Settings.Default.TimerFreqWalkSeconds = timerFreqWalkSeconds;
            Properties.Settings.Default.ClockIsZulu = clockIsZulu;

            var indices = checkedListBoxWalkFT8.CheckedItems.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkFT8 = string.Join(",", indices);
            indices = checkedListBoxWalkFT4.CheckedItems.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkFT4 = string.Join(",", indices);
            indices = checkedListBoxWalkCustom.CheckedItems.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkCustom = string.Join(",", indices);
            
            var items = checkedListBoxWalkFT8.Items.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkFT8List = string.Join(",", items);
            items = checkedListBoxWalkFT4.Items.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkFT4List = string.Join(",", items);
            items = checkedListBoxWalkCustom.Items.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalkCustomList = string.Join(",", items);

            Properties.Settings.Default.Save();
            Exit();
        }


        private void LoadBaudRates()
        {
            comboBoxBaudRelay1.Items.Add("4800");
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
            comboBoxComTuner.Items.Clear();
            foreach (string s in System.IO.Ports.SerialPort.GetPortNames())
            {
                if (!s.StartsWith("COM")) continue;
                if (comboBoxComTuner.Items.Count == 0)
                {
                    comboBoxComTuner.Items.Add(s);
                }
                else
                {
                    int numSerPort = Int32.Parse(s.Substring(3), CultureInfo.InvariantCulture);
                    string s2 = comboBoxComTuner.Items[comboBoxComTuner.Items.Count - 1].ToString();
                    int numLastItem = Int32.Parse(s2.Substring(3), CultureInfo.InvariantCulture);
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
                            int numCurrentItem = Int32.Parse(s3.Substring(3), CultureInfo.InvariantCulture);
                            if (numSerPort < numCurrentItem)
                            {
                                comboBoxComTuner.Items.Insert(ii, s);
                                break;
                            }
                            ++ii;
                        }
                    }
                }
            }
        }

        private static void Debug(DebugEnum level, string msg)
        {
            //if (tuner1 == null) return;
            DebugAddMsg(level, msg);
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

        //private void ButtonTune_Click(object sender, EventArgs e)
        //{
        //    Debug("Tuning\n");
        //}

        private void ComboBoxTunerCom_SelectedIndexChanged(object sender, EventArgs e)
        {
            //comPortTuner = comboBoxComTuner.Text;
            //if (baudTuner > 0) richTextBoxRig.AppendText("Opening Tuner " + comPortTuner+":"+baudTuner+"\n");
        }

        private void ComboBoxTunerBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            //baudTuner = Int32.Parse(comboBoxBaudTuner.Text, CultureInfo.InvariantCulture);
            //if (comPortTuner.Length > 0) richTextBoxRig.AppendText("Opening Tuner " + comPortTuner + ":" + baudTuner+"\n");

        }

        private void RichTextBoxRelay_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxDebug.Clear();
        }

        private void RichTextBoxRig_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxDebug.Clear();
        }

        private void RichTextBoxFLRig_DoubleClick(object sender, EventArgs e)
        {
            richTextBoxDebug.Clear();
        }

        private void FLRigConnect()
        {
            int port = 12345;
            try
            {
                rigClient = new TcpClient("127.0.0.1", port);
                rigStream = rigClient.GetStream();
                rigStream.ReadTimeout = 2000;
                if (FLRigWait() == false)
                {
                    DebugAddMsg(DebugEnum.ERR, "Flrig not connected?");
                    return;
                }
                else
                {
                    GetModes();
                    Debug(DebugEnum.LOG, "FLRig connected\n");
                }
            }
            catch (Exception ex)
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    Debug(DebugEnum.ERR, "FLRig error...not responding...\n");
                }
                else
                {
                    Debug(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
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
            if (rigStream == null)
            {
                return false;
            }
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
                if (xml.Contains("rig.set") || xml.Contains("rig.cat_string"))
                {
                    int saveTimeout = rigStream.ReadTimeout;
                    // Just read the response and ignore it for now
                    rigStream.ReadTimeout = 2000;
                    byte[] data2 = new byte[4096];
                    Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                    String responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                    if (!responseData.Contains("200 OK"))
                    {
                        Debug(DebugEnum.ERR, "FLRig error: unknown response=" + responseData + "\n");
                        MyMessageBox("Unknown response from FLRig\n" + responseData);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "FLRig error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                //DisconnectFLRig();
                return false;
            }
            return true;
        }

        private bool TuneSequence()
        {
            bool retval;
            // Set power if needed to ensure not overdriving things -- we do it again after we start tuning
            //SetAntennaInUse();
            PowerSelect(frequencyHz, modeCurrent, true);
            if (ampIsOn) // we need to turn off amplifier
            {
                if (tuner1 == null)
                {
                    _ = MessageBox.Show("No tuner??");
                }
                Debug(DebugEnum.TRACE, "Turning amp off\n");
                tuner1.CMDAmp(0); // amp off
                if (relay1 != null) AmpSet(false);
                Thread.Sleep(500);
                retval = Tune();
                Debug(DebugEnum.TRACE, "Turning amp on\n");
                tuner1.CMDAmp(1);
                if (relay1 != null) AmpSet(true);
                Debug(DebugEnum.TRACE, "amp on\n");
                Thread.Sleep(500);
            }
            else // just tune 
            {
                retval = Tune();
            }
            return retval;
        }
        // returns true when Tuner and FLRig are talking to us
        // if ptt is true then will use ptt and audio tone for tuning -- e.g. MFJ-928 without radio interface
        private bool Tune()
        {
            string xml;
            if (pausedTuning)
            {
                Debug(DebugEnum.WARN, "Tuner is paused\n");
                return true;
            }
            tuneIsRunning = true;
            var ptt = checkBoxPTTEnabled.Checked;
            //var tone = checkBoxToneEnabled.Checked;
            var powerSDR = checkBoxPowerSDR.Checked;
            var offset = Int32.Parse(textBoxTuneOffset.Text, CultureInfo.InvariantCulture);
            var frequencyHzTune = frequencyHz - offset;
            if (modeCurrent.Contains("-R") || modeCurrent.Contains("LSB"))
            {
                frequencyHzTune = frequencyHz + 1000;
            }
            var myparam = "<params><param><value><double>" + frequencyHzTune + "</double></value></param></params";
            xml = FLRigXML("rig.set_vfo" + 'B', myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            Thread.Sleep(500);
            Debug(DebugEnum.LOG, "Tuning to " + frequencyHzTune + "\n");

            buttonTunerStatus.BackColor = Color.LightGray;
            Application.DoEvents();
            if (ptt && !powerSDR) // we turn on PTT and send the audio tone before starting tune
            {
                Debug(DebugEnum.LOG, "Audio tune started\n");
                audio.MyFrequency = 1000;
                audio.Volume = 1;
                audio.StartStopSineWave();
                Thread.Sleep(300); // give ptt a chance to crank up
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>1</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Thread.Sleep(Convert.ToInt32(numericUpDownPostPttDelay.Value)); // give ptt a chance to crank up
                Debug(DebugEnum.LOG, "Set ptt on\n");
                tuneIsRunning = true;
                tuner1.Tune();
                tuneIsRunning = false;
            }
            else if (powerSDR)
            {
                Debug(DebugEnum.LOG, "PowerSDR Tune started, amp off\n");
                //tuner1.CMDAmp(0); // amp off
                Debug(DebugEnum.LOG, "PowerSDR Tune start tone\n");
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU1;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Thread.Sleep(Convert.ToInt32(numericUpDownPostPttDelay.Value));
                Debug(DebugEnum.LOG, "PowerSDR Tune start tune\n");
                tuneIsRunning = true;
                tuner1.Tune();
                Thread.Sleep(2000);
                tuneIsRunning = false;
                Debug(DebugEnum.LOG, "PowerSDR Tune stop tone\n");
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
            }
            else
            {
                Debug(DebugEnum.LOG, "Generic tuner started\n");
                tuner1.Tune();
            }
            Thread.Sleep(200);
            char response = tuner1.ReadResponse();
            DebugAddMsg(DebugEnum.VERBOSE, "tuner1.ReadResponse " + response + "\n");
            // stop audio here
            Application.DoEvents();
            if (ptt && !powerSDR) // we turn off PTT now
            {
                audio.StartStopSineWave();
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>0</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Debug(DebugEnum.LOG, "ptt off\n");
            }
            else if (powerSDR)
            {
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Debug(DebugEnum.LOG, "PowerSDR Tune stopped\n");
            }
            Thread.Sleep(200); // give a little time for ptt off and audio to stop
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
                simpleSound.Dispose();
            }
            else if (response == 'F')
            {
                SoundPlayer simpleSound = new SoundPlayer("swr.wav");
                simpleSound.Play();
                buttonTunerStatus.BackColor = Color.Red;
                labelSWR.Text = "SWR > 3.0";
                simpleSound.Dispose();
            }
            else
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
                tabPage.SelectedTab = tabPageDebug;
                buttonTunerStatus.BackColor = Color.Transparent;
                MyMessageBox("Unknown response from tuner = '" + response + "'");
                Debug(DebugEnum.ERR, "Unknown response from tuner = '" + response + "'\n");
            }
            tuneIsRunning = false;
            //richTextBoxRig.AppendText("Tuning done SWR="+tuner1.SWR+"\n");
            //richTextBoxRig.AppendText("Tuning done\n");
            Debug(DebugEnum.TRACE, "Setting Tx power\n");
            PowerSelect(frequencyHz, modeCurrent, false);
            myparam = "<params><param><value><double>" + frequencyHz + "</double></value></param></params";
            xml = FLRigXML("rig.set_vfo" + 'B', myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            return true;
        }

        private List<string> FLRigGetModes()
        {
            List<string> modes = new List<string>();
            if (rigClient == null) { FLRigConnect(); }
            string xml2 = FLRigXML("rig.get_modes", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "FLRigGetModes error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabPage.SelectedTab = tabPageDebug;
                return null;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    char[] delims = { '<', '>', '\r', '\n' };
                    string[] tokens = responseData.Split(delims);
                    int i = 0;
                    bool value = false;
                    for (; i < tokens.Length; ++i)
                    {
                        if (tokens[i].Equals("data", StringComparison.InvariantCulture)) value = true;
                        if (value == true && tokens[i].Equals("value", StringComparison.InvariantCulture))
                        {
                            modes.Add(tokens[i + 1]);
                        }
                    }
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        //mode = responseData.Substring(offset1, offset2 - offset1);
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabPage.SelectedTab = tabPageDebug;
                    }
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "Error...Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            return modes;


        }
        // Wait for FLRig to return valid data
        private bool FLRigWait()
        {
            String xcvr;
            int n = 16;
            while ((xcvr = FLRigGetXcvr()) == null && formClosing == false && --n > 0)
            {
                Thread.Sleep(1000);
                DebugAddMsg(DebugEnum.LOG, "Waiting for FLRigv " + n + "\n");
                Application.DoEvents();
            }
            if (xcvr == null)
            {
                DebugAddMsg(DebugEnum.ERR, "No transceiver?  Aborting FLRigWait\n");
                return false;
            }
            if (formClosing == true)
            {
                return false;
            }
            Debug(DebugEnum.LOG, "Rig is " + xcvr + "\n");
            return true;
        }

        private string FLRigGetXcvr()
        {
            string xcvr = null;
            //if (!checkBoxRig.Checked) return null;
            if (rigClient == null || rigStream == null) { FLRigConnect(); }
            string xml2 = FLRigXML("rig.get_xcvr", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "FLRigGetXcvr error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabPage.SelectedTab = tabPageDebug;
                return null;
            }
            data = new Byte[4096];
            int timeoutSave = rigStream.ReadTimeout;
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        xcvr = responseData.Substring(offset1, offset2 - offset1);
                        if (xcvr.Length == 0) xcvr = null;
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabPage.SelectedTab = tabPageDebug;
                    }
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            rigStream.ReadTimeout = timeoutSave;
            return xcvr;
        }

        private string FLRigGetMode()
        {
            string mode = "Unknown";

            if (!checkBoxRig.Checked) return null;
            while (rigClient == null || rigStream == null)
            {
                for (int i = 1; i < 100; ++i)
                {
                    Application.DoEvents();
                    Thread.Sleep(20);
                }
                FLRigConnect();
            }
            string xml2 = FLRigXML("rig.get_modeA", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "FLRigGetMode error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabPage.SelectedTab = tabPageDebug;
                return null;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 2000;
            bool retry = true;
            int retryCount = 0;
            do
            {
                try
                {
                    Int32 bytes = rigStream.Read(data, 0, data.Length);
                    String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                    //richTextBoxRig.AppendText(responseData + "\n");
                    try
                    {
                        if (responseData.Contains("<value>")) // then we have a frequency
                        {
                            int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                            int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                            mode = responseData.Substring(offset1, offset2 - offset1);
                        }
                        else
                        {
                            labelFreq.Text = "?";
                            Debug(DebugEnum.ERR, responseData + "\n");
                            tabPage.SelectedTab = tabPageDebug;
                        }
                    }
                    catch (Exception)
                    {
                        Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                        frequencyHz = 0;
                    }
                    retry = false;
                }
                catch (Exception ex)
                {
                    Debug(DebugEnum.ERR, "Error...Rig get_modeA not responding try#" + ++retryCount + "\n" + ex.Message + "\n");
                    frequencyHz = 0;
                }
            } while (retry);
            return mode;
        }

        public static string MyTime()
        {
            string time;
            if (clockIsZulu)
                time = DateTime.UtcNow.ToString("HH:mm:ss.fffZ", CultureInfo.InvariantCulture) + ": ";
            else
                time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + ": ";
            return time;
        }

        private void SetDummyLoad()
        {
            const string dummy = "dummy";
            string antennaNumberNew;
            if (textBoxAntenna1.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna1.Checked = true;
                antennaNumberNew = "1";
                buttonAntenna1.BackColor = Color.Green;
            }
            else if (textBoxAntenna2.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna2.Checked = true;
                antennaNumberNew = "2";
                buttonAntenna2.BackColor = Color.Green;
            }
            else if (textBoxAntenna3.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna3.Checked = true;
                antennaNumberNew = "3";
                buttonAntenna3.BackColor = Color.Green;
            }
            else if (textBoxAntenna4.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna4.Checked = true;
                antennaNumberNew = "4";
                buttonAntenna4.BackColor = Color.Green;
            }
            else if (textBoxAntenna5.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna5.Checked = true;
                antennaNumberNew = "5";
                buttonAntenna5.BackColor = Color.Green;
            }
            else if (textBoxAntenna6.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna6.Checked = true;
                antennaNumberNew = "6";
                buttonAntenna6.BackColor = Color.Green;
            }
            else if (textBoxAntenna7.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna7.Checked = true;
                antennaNumberNew = "7";
                buttonAntenna7.BackColor = Color.Green;
            }
            else if (textBoxAntenna8.Text.ToLower().Contains(dummy))
            {
                checkBoxAntenna8.Checked = true;
                antennaNumberNew = "8";
                buttonAntenna8.BackColor = Color.Green;
            }
            else
            {
                MessageBox.Show("dummyload antenna not found");
                return;
            }
            labelAntennaSelected.Text = "Dummy Load";
            var tmp = Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture);
            SetAntennaRelayOn(tmp);
        }
        private void SetAntennaInUse()
        {
            var antennaNumberNew = "1"; // default to antenna#1
            //var modeCurrent = FLRigGetMode();
            //if (!tuneIsRunning) PowerSelect(frequencyHz, modeCurrent);

            double frequencyMHz = frequencyHz / 1000000;
            if (frequencyHz == 0) return;

            buttonAntenna1.BackColor = Color.LightGray;
            buttonAntenna2.BackColor = Color.LightGray;
            buttonAntenna3.BackColor = Color.LightGray;
            buttonAntenna4.BackColor = Color.LightGray;
            buttonAntenna5.BackColor = Color.LightGray;
            buttonAntenna6.BackColor = Color.LightGray;
            buttonAntenna7.BackColor = Color.LightGray;
            buttonAntenna8.BackColor = Color.LightGray;
            if (dummyLoad == true)
            {
                SetDummyLoad();
                return;
            }
            try
            {
                if (checkBoxAntenna1.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq1From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq1To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna1.BackColor == Color.LightYellow)))
                {
                    checkBoxAntenna1.Checked = true;
                    buttonAntenna1.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna1.Text;
                    ButtonAntennaPickSet(buttonAntennaPick1);
                    antennaNumberNew = "1";
                    //AmpSet(checkBoxAmp1.Checked);
                }
                else if (checkBoxAntenna2.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq2From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq2To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna2.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna2.Checked = true;
                    buttonAntenna2.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna2.Text;
                    ButtonAntennaPickSet(buttonAntennaPick2);
                    antennaNumberNew = "2";
                    //AmpSet(checkBoxAmp2.Checked);
                }
                else if (checkBoxAntenna3.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq3From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq3To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna3.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna3.Checked = true;
                    buttonAntenna3.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick3);
                    labelAntennaSelected.Text = textBoxAntenna3.Text;
                    antennaNumberNew = "3";
                    //AmpSet(checkBoxAmp3.Checked);
                }
                else if (checkBoxAntenna4.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq4From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq4To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna4.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna4.Checked = true;
                    buttonAntenna4.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick4);
                    labelAntennaSelected.Text = textBoxAntenna4.Text;
                    antennaNumberNew = "4";
                    //AmpSet(checkBoxAmp4.Checked);
                }
                else if (checkBoxAntenna5.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq5From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq5To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna5.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna5.Checked = true;
                    buttonAntenna5.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick5);
                    labelAntennaSelected.Text = textBoxAntenna5.Text;
                    antennaNumberNew = "5";
                    //AmpSet(checkBoxAmp5.Checked);
                }
                else if (checkBoxAntenna6.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq6From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq6To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna6.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna6.Checked = true;
                    buttonAntenna6.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick6);
                    labelAntennaSelected.Text = textBoxAntenna6.Text;
                    antennaNumberNew = "6";
                    //AmpSet(checkBoxAmp6.Checked);
                }
                else if (checkBoxAntenna7.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq7From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq7To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna7.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna7.Checked = true;
                    buttonAntenna7.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick7);
                    labelAntennaSelected.Text = textBoxAntenna7.Text;
                    antennaNumberNew = "7";
                    //AmpSet(checkBoxAmp7.Checked);
                }
                else if (checkBoxAntenna8.Checked == true && 
                    ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq8From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq8To.Text, CultureInfo.InvariantCulture)
                    ) || (freqWalkIsRunning && textBoxAntenna8.BackColor == Color.Yellow)))
                {
                    checkBoxAntenna8.Checked = true;
                    buttonAntenna8.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick8);
                    labelAntennaSelected.Text = textBoxAntenna8.Text;
                    antennaNumberNew = "8";
                    //AmpSet(checkBoxAmp8.Checked);
                }
            }
            catch (Exception)
            {
                // don't do anything here...just catching the parse errors from blank boxes
            }
            if (antennaNumberNew.Length == 0) antennaNumberNew = "1";
            var tmp = Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture);
            // if we want to use the Tuner antenna selection then we'll need to add that option
            //if (!pausedTuning && tuner1 != null && tmp != tuner1.AntennaNumber)
            //    tuner1.SetAntenna(Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture), tuneIsRunning);
            bool needTuning = false;
            if (tmp != tuner1.AntennaNumber && !freqWalkIsRunning)
            {
                needTuning = true;
            }
            SetAntennaRelayOn(tmp);
            if (needTuning)
            {
                //tuner1.Tune();
            }
        }

        void SetAntennaAmp(int antennaNumber)
        {
            // we use the Antenna Amp checkbox to turn the Amp on or off
            switch (antennaNumber)
            {
                case 1:
                    AmpSet(checkBoxAntenna1Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna1Amp.Checked;
                    break;
                case 2:
                    AmpSet(checkBoxAntenna2Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna2Amp.Checked;
                    break;
                case 3:
                    AmpSet(checkBoxAntenna3Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna3Amp.Checked;
                    break;
                case 4:
                    AmpSet(checkBoxAntenna4Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna4Amp.Checked;
                    break;
                case 5:
                    AmpSet(checkBoxAntenna5Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna5Amp.Checked;
                    break;
                case 6:
                    AmpSet(checkBoxAntenna6Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna6Amp.Checked;
                    break;
                case 7:
                    AmpSet(checkBoxAntenna7Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna7Amp.Checked;
                    break;
                case 8:
                    AmpSet(checkBoxAntenna8Amp.Checked);
                    buttonAmp.Enabled = checkBoxAntenna8Amp.Checked;
                    break;
            }
        }
        void SetAntennaRelayOn(int antennaNumber)
        {
            SetAntennaAmp(antennaNumber);
            string myRelayChosen = comboBoxAntenna1Relay.Text;
            if (myRelayChosen.Length == 0 && formLoading == false)
            {
                comboBoxAntenna1Relay.SelectedIndex = 0;
                //MessageBox.Show("No Relay picked");
                //return;
            }
            ComboBox bits;
            switch (antennaNumber)
            {
                case 1: bits = ComboBoxAntenna1Bits; break;
                case 2: bits = ComboBoxAntenna2Bits; break;
                case 3: bits = ComboBoxAntenna3Bits; break;
                case 4: bits = ComboBoxAntenna4Bits; break;
                case 5: bits = ComboBoxAntenna5Bits; break;
                case 6: bits = ComboBoxAntenna6Bits; break;
                case 7: bits = ComboBoxAntenna7Bits; break;
                case 8: bits = ComboBoxAntenna8Bits; break;
                default: bits = ComboBoxAntenna1Bits; break;
            }
            string relayBits = bits.Text;
            int relayValue = 1;
            if (relayBits.Equals("8/0")) relayValue = 0;
            else if (relayBits.Equals("8/1")) relayValue = 1;
            else if (relayBits.Equals("8/2")) relayValue = 2;
            else if (relayBits.Equals("8/3")) relayValue = 3;
            else if (relayBits.Equals("8/4")) relayValue = 4;
            else if (relayBits.Equals("8/5")) relayValue = 5;
            else if (relayBits.Equals("8/6")) relayValue = 6;
            else if (relayBits.Equals("8/7")) relayValue = 7;
            else if (relayBits.Equals("8/8")) relayValue = 8;
            else if (relayBits.Equals("4/1")) relayValue = 1;
            else if (relayBits.Equals("4/2")) relayValue = 2;
            else if (relayBits.Equals("4/3")) relayValue = 3;
            else if (relayBits.Equals("4/4")) relayValue = 4;
            else if (relayBits.Equals("4/5")) relayValue = 5;
            else if (relayBits.Equals("4/6")) relayValue = 6;
            else if (relayBits.Equals("4/7")) relayValue = 7;
            else if (relayBits.Equals("4/8")) relayValue = 8;

            //MessageBox.Show("Relay " + myRelayChosen);
            Relay myRelay = relay1;
            if (myRelay == null) return;
            if (myRelayChosen.Equals("Relay2")) myRelay = relay2;
            else if (myRelayChosen.Equals("Relay3")) myRelay = relay3;
            else if (myRelayChosen.Equals("Relay4")) myRelay = relay4;
            if (relayValue == 0) myRelay.AllOff();
            //if (lastAntennaUsed != 0 && lastAntennaUsed != relayValue) 
            //    myRelay.Set(lastAntennaUsed, 0);
            myRelay.Set(relayValue, 1);
            RelaySetButtons(button1_1, relay1.Status());
            AntennaUpdateSelected(antennaNumber);
            if (tuner1 != null && lastAntennaNumberUsed != antennaNumber)
            {
                if (lastAntennaNumberUsed >= 0)
                {
                    Cursor.Current = Cursors.WaitCursor;
                    timerGetFreq.Stop();
                    TuneSequence();
                    timerGetFreq.Start();
                    Cursor.Current = Cursors.Default;
                }
            }
            lastAntennaNumberUsed = antennaNumber;
            Application.DoEvents();
        }
        private string FLRigGetActiveVFO()
        {
            string vfo = "A";
            bool retry = true;
            int retryCount = 0;
            do
            {
                try
                {
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
                    retry = false; // got here so we're good
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "GetActiveVFO error #" + ++retryCount + "\n");
                    Thread.Sleep(2000);
                    rigStream.Close();
                    FLRigConnect();
                }
            } while (retry);
            return vfo;
        }

        private void FLRigSetActiveVFO(string mode)
        {
            Debug(DebugEnum.LOG, "FLRigSetActiveVFO=" + mode + "\n");

            string myparam = "<params><param><value>" + mode + "</value></param></params>";
            string xml2 = FLRigXML("rig.set_AB", myparam);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
                // Ignore the response for now
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
            }
            catch (Exception)
            {
                Debug(DebugEnum.ERR, "FLRigSetActiveVFO error\n");
                rigStream.Close();
                FLRigConnect();
                return;
            }
        }

        private int FLRigGetPower()
        {
            int power = 0;
            try
            {
                string xml2 = FLRigXML("rig.get_power", null);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
                if (rigStream == null) return 0;
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (responseData.Contains("<value>")) // then we have a frequency
                {
                    int offset1 = responseData.IndexOf("<i4>", StringComparison.InvariantCulture) + "<i4>".Length;
                    int offset2 = responseData.IndexOf("</i4>", StringComparison.InvariantCulture);
                    string powerstr = responseData.Substring(offset1, offset2 - offset1);
                    power = int.Parse(powerstr, CultureInfo.InvariantCulture);
                }
            }
            catch (Exception)
            {
                Debug(DebugEnum.ERR, "FLRigGetPower error\n");
            }
            return power;
        }
        private bool FLRigSetPower(Int32 value)
        {
            // Seems we can send this a bit too quickly so let's sleep for a short while before doing it
            // Then we'll do it again as it was still failing the 1st time
            DebugAddMsg(DebugEnum.VERBOSE, "FLRigSetPower\n");
            var power = FLRigGetPower();
            if (power == value) return true;
            Thread.Sleep(100);
            DebugAddMsg(DebugEnum.VERBOSE, "Power needs changing to" + value + "\n");
            string xml = FLRigXML("rig.set_power", "<params><param><value><i4>" + value + "</i4></value></param></params");
            int ntry = 0;
            do
            {
                DebugAddMsg(DebugEnum.VERBOSE, "Set power try#" + ntry + "\n");
                Application.DoEvents();
                try
                {
                    if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                    if (ntry > 0) Debug(DebugEnum.LOG, "Set power to " + value + " try#" + ++ntry + "\n");
                    Thread.Sleep(500);
                }
                catch (Exception ex)
                {
                    richTextBoxDebug.AppendText(ex.Message + "\n" + ex.StackTrace);
                    Thread.Sleep(500);
                    rigStream.Close();
                    FLRigConnect();
                }
                power = FLRigGetPower();
            } while (power != value && ++ntry <= 3);
            labelPower.Text = "RigPower = " + value;
            return true;
        }

        private bool PowerSelectOp(double frequencyMHz, CheckBox enabled, TextBox from, TextBox to, TextBox powerLevel, ComboBox mode, string modeChk, CheckBox amp, bool ampFlag, int passNum)
        {
            // On passNum=0 we check for specific mode matches
            // On passNum>0 we check for "Any" matches
            DebugAddMsg(DebugEnum.VERBOSE, "PowerSelectOp\n");
            try
            {
                if (enabled.Checked)
                {
                    if (from.Text.Length == 0)
                    {
                        //MyMessageBox("Power tab From MHz is empty");
                        from.Text = "0";
                        return false;
                    }
                    if (to.Text.Length == 0)
                    {
                        //MyMessageBox("Power tab To MHz is empty");
                        to.Text = "1000";
                        return false;
                    }
                    double frequencyFrom = Convert.ToDouble(from.Text, CultureInfo.InvariantCulture);
                    double frequencyTo = Convert.ToDouble(to.Text, CultureInfo.InvariantCulture);
                    if (frequencyMHz >= frequencyFrom && frequencyMHz <= frequencyTo)
                    {
                        if (mode.SelectedItem == null || mode.SelectedItem.Equals(modeChk) || (passNum > 0 && mode.SelectedItem.Equals("Any")))
                        {
                            if (!pausedTuning && !tuneIsRunning && powerLevel.Text.Length > 0)
                            {
                                Debug(DebugEnum.TRACE, "Set Power=" + powerLevel.Text + "\n");
                                Application.DoEvents();
                                int powerset = Convert.ToInt32(powerLevel.Text, CultureInfo.InvariantCulture);
                                //if (tuneIsRunning)
                                {
                                    FLRigSetPower(powerset);
                                    Thread.Sleep(100);
                                    int power = FLRigGetPower();
                                    labelPower.Text = "Power " + power;
                                    if (power != powerset)
                                    {
                                        Thread.Sleep(500);
                                        Debug(DebugEnum.TRACE, "Set Power again!!");
                                        FLRigSetPower(powerset);
                                    }
                                }
                                DebugAddMsg(DebugEnum.VERBOSE, "AmpSet being called\n");
                                AmpSet(ampFlag);
                            }
                            return true;
                        }
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                //MyMessageBox("Error parsing frequencies\nFrequencies need to be in Mhz\nPower not set");
            }
            return false;
        }

        private void PowerSelect(double frequencyHz, string modeChk, bool tuning = false)
        {
            double frequencyMHz = frequencyHz / 1e6;
            for (int passNum = 0; passNum < 2; ++passNum)
            {
                Debug(DebugEnum.TRACE, "1\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower1Enabled, textBoxPower1From, textBoxPower1To, tuning ? textBoxTune1Power : textBoxPower1Watts, comboBoxPower1Mode, modeChk, checkBoxAmp1, checkBoxAmp1.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "2\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower2Enabled, textBoxPower2From, textBoxPower2To, tuning ? textBoxTune2Power : textBoxPower2Watts, comboBoxPower2Mode, modeChk, checkBoxAmp2, checkBoxAmp2.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "3\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower3Enabled, textBoxPower3From, textBoxPower3To, tuning ? textBoxTune3Power : textBoxPower3Watts, comboBoxPower3Mode, modeChk, checkBoxAmp3, checkBoxAmp3.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "4\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower4Enabled, textBoxPower4From, textBoxPower4To, tuning ? textBoxTune4Power : textBoxPower4Watts, comboBoxPower4Mode, modeChk, checkBoxAmp4, checkBoxAmp4.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "5\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower5Enabled, textBoxPower5From, textBoxPower5To, tuning ? textBoxTune5Power : textBoxPower5Watts, comboBoxPower5Mode, modeChk, checkBoxAmp5, checkBoxAmp5.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "6\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower6Enabled, textBoxPower6From, textBoxPower6To, tuning ? textBoxTune6Power : textBoxPower6Watts, comboBoxPower6Mode, modeChk, checkBoxAmp6, checkBoxAmp6.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "7\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower7Enabled, textBoxPower7From, textBoxPower7To, tuning ? textBoxTune7Power : textBoxPower7Watts, comboBoxPower7Mode, modeChk, checkBoxAmp7, checkBoxAmp7.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "8\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower8Enabled, textBoxPower8From, textBoxPower8To, tuning ? textBoxTune8Power : textBoxPower8Watts, comboBoxPower8Mode, modeChk, checkBoxAmp8, checkBoxAmp8.Checked, passNum))
                    return;
                Debug(DebugEnum.TRACE, "9\n");
                labelPower.Text = "Power not set";
            }
        }

        private void FLRigGetFreq(bool needTuning = true)
        {
            getFreqIsRunning = true;
            if (!checkBoxRig.Checked || formClosing)
            {
                getFreqIsRunning = false;
                return;
            }
            if (rigClient == null)
            {
                FLRigConnect();
                if (rigClient == null)
                {
                    getFreqIsRunning = false;
                    return;
                }
            }
            string currVFO = FLRigGetActiveVFO();
            if (currVFO.Equals("B", StringComparison.InvariantCulture))
            {// could be rigctl temp change to VFOB so should be done in < 1 second
                Thread.Sleep(1000);
                currVFO = FLRigGetActiveVFO();
            }


            if (currVFO.Equals("B", StringComparison.InvariantCulture))
            {
                MyMessageBox("Auto tuning paused because VFOB is active, click OK when you're done", MessageBoxButtons.OK);
                FLRigSetActiveVFO("A");
            }

            string vfo = "B";
            if (radioButtonVFOA.Checked) vfo = "A";
            char cvfo = 'A';
            string mode = FLRigGetMode();
            //Debug(DebugEnum.VERBOSE, "VFOA mode is " + mode + "\n");
            if (radioButtonVFOA.Checked) cvfo = 'B';
            string xml = FLRigXML("rig.get_vfo" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return;
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    Debug(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    Debug(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabPage.SelectedTab = tabPageDebug;
                getFreqIsRunning = false;
                return;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        string freqString = responseData.Substring(offset1, offset2 - offset1);
                        frequencyHz = Double.Parse(freqString, CultureInfo.InvariantCulture);
                        if (frequencyHz != frequencyLast)
                        {
                            DebugAddMsg(DebugEnum.LOG, "Freq change from " + frequencyLast + " to " + frequencyHz + "\n");
                            PowerSelect(frequencyHz, modeCurrent, tuneIsRunning);
                            if (!pausedTuning) SetAntennaInUse();
                        }
                        string modeOld = modeCurrent;
                        modeCurrent = FLRigGetMode(); // get our current mode now
                        labelFreq.Text = (frequencyHz / 1e6).ToString(CultureInfo.InvariantCulture) + "MHz" + " " + modeCurrent;
                        if (comboBoxPower1Mode.SelectedItem != null && !modeCurrent.Equals(modeOld, StringComparison.InvariantCulture))
                            PowerSelect(frequencyHz, modeCurrent, tuneIsRunning);
                        if (frequencyLast == 0) frequencyLast = frequencyLastTunedHz = frequencyHz;
                        if (frequencyLast == frequencyHz && freqStableCount < freqStableCountNeeded)
                        {
                            ++freqStableCount;
                            stopWatchTuner.Restart();
                        }
                        if (freqStableCount >= freqStableCountNeeded && Math.Abs(frequencyHz - frequencyLastTunedHz) > tolTune && !pausedTuning & !pauseButtonClicked)
                        {
                            DebugAddMsg(DebugEnum.LOG, "Freq diff = " + Math.Abs(frequencyHz - frequencyLastTunedHz) + " tolTune=" + tolTune + "\n");
                            if (stopWatchTuner.IsRunning && stopWatchTuner.ElapsedMilliseconds < 1 * 1000)
                            {
                                stopWatchTuner.Stop();
                                //MyMessageBox("Rapid frequency changes...click OK when ready to tune");
                                Debug(DebugEnum.ERR, "Rapid frequency changes\n");
                                stopWatchTuner.Reset();
                                stopWatchTuner.Stop();
                                getFreqIsRunning = false;
                                return; // we'll tune on next poll
                            }
                            double frequencyHzVFOB = frequencyHz - 1000;
                            if (mode.Contains("LSB") || mode.Contains("-R"))
                            {
                                frequencyHzVFOB = frequencyHz + 1000;
                            }
                            xml = FLRigXML("rig.set_vfo" + cvfo, "<params><param><value><double> " + frequencyHzVFOB + " </double></value></param></params");
                            if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
                            Thread.Sleep(1000);  // give the rig a chance to restore it's band memory
                            string myparam = "<params><param><value>" + mode + "</value></param></params>";
                            xml = FLRigXML("rig.set_modeB", myparam);
                            if (FLRigSend(xml) == false)
                            { // Abort if FLRig is giving an error
                                Debug(DebugEnum.ERR, "FLRig Tune got an error??\n");
                            }
                            Debug(DebugEnum.LOG, "Rig mode VFOB set to " + mode + "\n");
                            stopWatchTuner.Restart();
                            if (checkBoxTunerEnabled.Checked && pausedTuning)
                            {
                                // if we're pause we just update this stuff to prevent it from thinking we need to do anything
                                frequencyLastTunedHz = frequencyLast = frequencyHz;
                            }
                            if (checkBoxTunerEnabled.Checked && !pausedTuning && !pauseButtonClicked)
                            {
                                char vfoOther = 'A';
                                if (radioButtonVFOA.Checked) vfoOther = 'B';
                                var frequencyHzTune = frequencyHz - 1000;
                                if (mode.Contains("-R") || mode.Contains("LSB"))
                                {
                                    frequencyHzTune = frequencyHz + 1000;
                                }
                                // Set VFO mode to match primary VFO
                                myparam = "<params><param><value>" + modeCurrent + "</value></param></params>";
                                xml = FLRigXML("rig.set_modeB", myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
                                }
                                Thread.Sleep(200);
                                frequencyLastTunedHz = frequencyHz;
                                //PowerSelect(frequencyHz, modeCurrent);
                                if (needTuning)
                                {
                                    timerGetFreq.Stop();
                                    TuneSequence();
                                    timerGetFreq.Start();
                                }
                                // Reset VFOB to same freq as VFOA
                                myparam = "<params><param><value><double>" + frequencyHz + "</double></value></param></params";
                                xml = FLRigXML("rig.set_vfo" + vfoOther, myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
                                }
                            }
                            else if (!pausedTuning && !pauseButtonClicked)
                            {
                                Debug(DebugEnum.ERR, "Tuner not enabled\n");
                                Debug(DebugEnum.ERR, "Simulate tuning to " + frequencyHz + "\n");
                                char vfoOther = 'A';
                                if (radioButtonVFOA.Checked) vfoOther = 'B';
                                myparam = "<params><param><value><double>" + frequencyHz + "</double></value></param></params";
                                xml = FLRigXML("rig.set_vfo" + vfoOther, myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
                                }
                                // Set VFO mode to match primary VFO
                                myparam = "<params><param><value>" + modeCurrent + "</value></param></params>";
                                xml = FLRigXML("rig.set_modeB", myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
                                }

                                frequencyLastTunedHz = frequencyLast = frequencyHz;
                                freqStableCount = 0;
                            }
                        }
                        else
                        {
                            frequencyLast = frequencyHz;
                        }
                    }
                    else
                    {
                        labelFreq.Text = "?";
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabPage.SelectedTab = tabPageDebug;
                    }
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    //frequencyHz = 0;
                }
            }
            catch (Exception ex)
            {
                Debug(DebugEnum.ERR, "Error...Rig not responding\n" + ex.Message + "\n");
                //frequencyHz = 0;
            }
            // set VFOB to match VFOA
            //xml = FLRigXML("rig.set_vfo" + cvfo, "<params><param><value><double> " + frequencyHz + " </double></value></param></params");
            //if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
            getFreqIsRunning = false;
        }

        public static String GetCommandLines(Process process)
        {
            Contract.Requires(process != null);
            ManagementObjectSearcher commandLineSearcher = new ManagementObjectSearcher(
                "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process.Id);
            String commandLine = "";
            foreach (ManagementObject commandLineObject in commandLineSearcher.Get())
            {
                commandLine += (String)commandLineObject["CommandLine"];
            }
            commandLineSearcher.Dispose();
            return commandLine;
        }
        private DialogResult RelayOops(String message)
        {
            //Debug(DebugEnum.ERR, message);
            return MyMessageBox(message);
        }

        private void ClockSet()
        {
            var myTime = MyTime();
            var z = clockIsZulu? "Z" : "";
            labelClock.Text = myTime.Substring(0, 8) + z;
        }
        private void TimerGetFreq_Tick(object sender, EventArgs e)
        {
            ClockSet();
            if (pausedTuning)
                return;
            timerGetFreq.Stop();
            //DebugAddMsg(DebugEnum.VERBOSE, "Timer tick\n");
            if (checkBoxRelay1Enabled.Checked && relay1 != null && relay1.relayError == true)
            {
                if (RelayOops("Relay1 closed unexpectedly...RFI??\n") == DialogResult.Retry)
                {
                    relay1.Open();
                    RelaySetButtons(button1_1, relay1.Status());

                }
                else
                {
                    checkBoxRelay1Enabled.Checked = false;
                }
            }
            if (checkBoxRelay2Enabled.Checked && relay2 != null && relay1.Status() == 0xff)
            {
                if (RelayOops("Relay2 closed unexpectedly...RFI??\n") == DialogResult.Retry)
                {
                    relay2.Open();
                }
                else
                {
                    checkBoxRelay2Enabled.Checked = false;
                }
            }
            if (checkBoxRelay3Enabled.Checked && relay3 != null && relay1.Status() == 0xff)
            {
                if (RelayOops("Relay3 closed unexpectedly...RFI??\n") == DialogResult.Retry)
                {
                    relay3.Open();
                }
                else
                {
                    checkBoxRelay3Enabled.Checked = false;
                }
            }
            if (checkBoxRelay4Enabled.Checked && relay4 != null && relay1.Status() == 0xff)
            {
                if (RelayOops("Relay4 closed unexpectedly...RFI??\n") == DialogResult.Retry)
                {
                    relay4.Open();
                }
                else
                {
                    checkBoxRelay4Enabled.Checked = false;
                }
            }

            //if (tuner1 == null || tuner1.GetComPort() == null) return;
            if (tuner1 != null)
            {
                string SWR = tuner1.GetSWRString();
                if (tuner1.GetModel().Equals(MFJ928, StringComparison.InvariantCulture))
                {
                    double Inductance = tuner1.GetInductance();
                    int Capacitance = tuner1.GetCapacitance();
                    if (numericUpDownCapacitance.Value != Capacitance)
                    {
                        numericUpDownCapacitance.Enabled = false;
                        numericUpDownCapacitance.Value = Capacitance;
                        Application.DoEvents();
                        numericUpDownCapacitance.Enabled = true;
                    }
                    if (numericUpDownInductance.Value != Capacitance)
                    {
                        numericUpDownInductance.Enabled = false;
                        numericUpDownInductance.Value = Convert.ToDecimal(Inductance) / 10;
                        Application.DoEvents();
                        numericUpDownInductance.Enabled = true;
                    }
                    Application.DoEvents();
                    if (_disposed) return;
                    labelSWR.Text = SWR;
                    if (tuner1.SWR == 0)
                    {
                        labelSWR.Text = "SWR Unknown";
                        buttonTunerStatus.BackColor = Color.Gray;
                    }
                    else if (tuner1.SWR < 2.0)
                    {
                        buttonTunerStatus.BackColor = Color.Green;
                    }
                    else if (tuner1.SWR < 3)
                    {
                        buttonTunerStatus.BackColor = Color.Yellow;
                    }
                    else
                    {
                        buttonTunerStatus.BackColor = Color.Red;
                    }
                }
            }
            if (tuner1 != null)
            {
                string FwdPwr = tuner1.GetPower();
                if (FwdPwr != null) labelPower.Text = FwdPwr;
            }

            if (checkBoxTunerEnabled.Checked && comboBoxTunerModel.Text == MFJ928)
            {
                numericUpDownCapacitance.Visible = true;
                numericUpDownInductance.Visible = true;
                buttonTunerSave.Visible = true;
                checkBox1.Visible = false;

            }
            else
            {
                numericUpDownCapacitance.Visible = false;
                numericUpDownInductance.Visible = false;
                buttonTunerSave.Visible = false;
                checkBox1.Visible = true;
            }
            if (checkBoxRig.Checked)
            {
                FLRigGetFreq();
                //timerGetFreq.Interval = 200;
                //DebugAddMsg(DebugEnum.LOG, "Get freq");
            }
            else
            {
                labelFreq.Text = "?";
            }

            // We'll update our Tuner Options in case it changes
            //groupBoxOptions.Visible = comboBoxTunerModel.SelectedItem.Equals(MFJ928);


            timerGetFreq.Start();
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxRig.Checked)
            {
                richTextBoxDebug.Clear();
                FLRigConnect();
                FLRigGetFreq();
            }
            else
            {
                rigClient = null;
                //DisconnectFLRig();
            }
        }

        private void TextBoxFreqTol_Leave(object sender, EventArgs e)
        {
            Properties.Settings.Default.tolTune = textBoxFreqTol.Text;
        }

        private void TabPage3_Click(object sender, EventArgs e)
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

        private void TextBoxRelay1On_Leave(object sender, EventArgs e)
        {
            TextBox textBox = (TextBox)sender;
            string s = textBox.Text;
            if (s.Length > 4)
            {
                string prefix = s.Substring(0, 2);
                if (!prefix.Equals("0x", StringComparison.InvariantCulture)) return; // not hex, must be string
            }
#pragma warning disable IDE0059 // Value assigned to symbol is never used
            if (!ToHex(s, out byte[] data))
#pragma warning restore IDE0059 // Value assigned to symbol is never used
            {
                MyMessageBox("Invalid command format\nExpected string or hex values e.g. 0x08 0x01");
            }
        }

        private void Form1_FormClosed(object sender, FormClosedEventArgs e)
        {
            Properties.Settings.Default.Save();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            int nRelays = 8;
            for (int i = 1; i <= nRelays; ++i)
            {
                Debug(DebugEnum.LOG, i + " On\n");
                //relay1.Set(i, 1);
                RelaySet(relay1, i, 1);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
            for (int i = 1; i <= nRelays; ++i)
            {
                Debug(DebugEnum.LOG, i + " Off\n");
                //relay1.Set(i, 0);
                RelaySet(relay1, i, 0);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
        }

        private bool RelaySet(Relay relay, int nRelay, int flag)
        {
            //if (!relay.IsOpen())
            //{
            //    MyMessageBox("Relay is not open");
            //    return false;
            //}
            if (relay == null)
            {
                MyMessageBox("Relay#" + nRelay + " is not configured!!");
                return false;
            }
            Button button; // the button we will mess with here to change color
            List<Button> buttons = new List<Button>();
            switch (relay.RelayNumber())
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
                    buttons.Add(button2_1);
                    buttons.Add(button2_2);
                    buttons.Add(button2_3);
                    buttons.Add(button2_4);
                    buttons.Add(button2_5);
                    buttons.Add(button2_6);
                    buttons.Add(button2_7);
                    buttons.Add(button2_8);
                    break;
                case 3:
                    buttons.Add(button3_1);
                    buttons.Add(button3_2);
                    buttons.Add(button3_3);
                    buttons.Add(button3_4);
                    buttons.Add(button3_5);
                    buttons.Add(button3_6);
                    buttons.Add(button3_7);
                    buttons.Add(button3_8);
                    break;
                case 4:
                    buttons.Add(button4_1);
                    buttons.Add(button4_2);
                    buttons.Add(button4_3);
                    buttons.Add(button4_4);
                    buttons.Add(button4_5);
                    buttons.Add(button4_6);
                    buttons.Add(button4_7);
                    buttons.Add(button4_8);
                    break;
            }
            if (buttons.Count == 0)
            {
                MyMessageBox("No relays open?");
                return false;
            }
            button = buttons[nRelay - 1];
            if (flag == 0) button.BackColor = Color.Gray;
            else button.BackColor = Color.Green;
            relay.Set(nRelay, (byte)flag);
            if (relay.errMsg != null)
            {
                MyMessageBox(relay.errMsg);
                return false;
            }
            //byte status = relay.Status();
            //Debug(DebugEnum.LOG, "status=0x" + status.ToString("X", CultureInfo.InvariantCulture) +"\n");
            Thread.Sleep(100);
            return true;
        }

        private void ButtonTune_Click_1(object sender, EventArgs e)
        {
            if (tuner1 == null)
            {
                MyMessageBox("Tuner not enabled");
                return;
            }
            if (freqWalkIsRunning) FreqWalkStop();
            //if (!relay1.IsOK())
            //{
            //    MyMessageBox("Relay1 is not communicating?");
            //    return;
            //}
            // MDB need to allow for different line in Power tab to check amplifier usage
            Cursor.Current = Cursors.WaitCursor;
            timerGetFreq.Stop();
            TuneSequence();
            timerGetFreq.Start();
            Cursor.Current = Cursors.Default;
            //if (checkBoxAmp1.Checked) relay1.Set(1, 0);
        }

        private static string FLRigXML(string cmd, string value)
        {
            //Debug(DebugEnum.LOG, "FLRig cmd=" + cmd + " value=" + value +"\n");
            string xmlHeader = "POST / RPC2 HTTP / 1.1\n";
            xmlHeader += "User - Agent: XMLRPC++ 0.8\n";
            xmlHeader += "Host: 127.0.0.1:12345\n";
            xmlHeader += "Content-type: text/xml\n";
            string xmlContent = "<?xml version=\"1.0\"?>\n<?clientid=\"AmpAutoTunerUtil\"?>\n";
            xmlContent += "<methodCall><methodName>";
            xmlContent += cmd;
            xmlContent += "</methodName>\n";
            if (value != null && value.Length > 0)
            {
                xmlContent += value;
            }
            xmlContent += "</methodCall>\n";
            xmlHeader += "Content-length: " + xmlContent.Length + "\n\n";
            string xml = xmlHeader + xmlContent;
            return xml;
        }

        public DialogResult MyMessageBox(string message, MessageBoxButtons buttons = MessageBoxButtons.RetryCancel)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            MessageBoxHelper.PrepToCenterMessageBoxOnForm(this);
            DebugAddMsg(DebugEnum.ERR, message);
            Application.DoEvents();
            //MessageBox.Show(message,Application.ProductName);
            var myForm = new Form { TopMost = true };
            var result = MessageBox.Show(myForm, message, "AmpAutoTunerUtility", buttons);
            myForm.Dispose();
            return result;
        }

        private void CheckBoxTunerEnabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading) return;
            if (checkBoxTunerEnabled.Checked)
            {
                try
                {
                    TunerOpen();
                    //tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                    //if (tuner1.GetSerialPortTuner() == null)
                    //{
                    //    richTextBoxRig.AppendText(" tuner open failed\n");
                    //    checkBoxTunerEnabled.Checked = false;
                    //    MyMessageBox("Tuner open failed");
                    //    return;
                    //}
                    //richTextBoxRig.AppendText("Tuner opened\n");
                    checkBoxTunerEnabled.Enabled = true;
                }
                catch (Exception ex)
                {
                    checkBoxTunerEnabled.Checked = false;
                    MyMessageBox("Error starting tuner\nFix problem and reenable the Tuner" + ex.Message);
                }
            }
            else
            {
                Debug(DebugEnum.LOG, "tuner closed\n");
                if (tuner1 != null && tuner1.GetSerialPortTuner() != null)
                {
                    tuner1.Close();
                }
            }
        }

        private void ComboBoxTunerModel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (formLoading || formClosing) return;
            if (comboBoxTunerModel.Text.Length > 0 && comboBoxComTuner.Text.Length > 0)
            {
                checkBoxTunerEnabled.Checked = true;
                //TunerOpen();
            }
            else
            {
                TunerClose();
                checkBoxTunerEnabled.Checked = false;
            }
            //string s = comboBoxTunerModel.SelectedText;
            //MessageBox.Show(System.Environment.StackTrace);
        }

        private void CheckBoxRelay1Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            checkBoxRelay1Enabled.Enabled = false;
            Application.DoEvents();
            if (comboBoxComRelay1.SelectedIndex >= 0)
            {
                checkBoxRelay1Enabled.Enabled = true;
            }
            else
            {
                checkBoxRelay1Enabled.Enabled = false;
            }
            if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.Text.Contains("COM") && comboBoxComRelay1.SelectedText == "")
            {
                //if (relay1 != null) MyMessageBox("Relay1 != null??");
                //relay1 = new Relay();
                //Debug(DebugEnum.LOG, "Relay1 open\n");
                relay1.Open(comboBoxComRelay1.Text);
                RelaySetButtons(button1_1, relay1.Status());

                Debug(DebugEnum.LOG, "Relay1 serial #" + relay1.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay1 closed\n");
                //checkBoxRelay1Enabled.Enabled = false;
                relay1.Close();
            }
            checkBoxRelay1Enabled.Enabled = true;
        }

        private void CheckBoxRelay2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            checkBoxRelay2Enabled.Enabled = true;
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                //relay2 = new Relay();
                //Debug(DebugEnum.LOG, "Relay2 open\n");
                relay2.Open(comboBoxComRelay2.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay2.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay2 closed\n");
                relay2.Close();
            }
            else if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex < 0)
            {
                MyMessageBox("Select COM port before enabling Relay2");
                checkBoxRelay2Enabled.Checked = false;
            }
        }

        private void RichTextBoxRig_TextChanged(object sender, EventArgs e)
        {

        }

        private void Button17_Click(object sender, EventArgs e)
        {
        }

        private void RelayToggle(Button relayButton, Relay relay, int nRelay)
        {
            if (relayButton.BackColor == Color.Gray)
            {
                RelaySet(relay, nRelay, 1);
            }
            else
            {
                RelaySet(relay, nRelay, 0);
            }
        }

        private void Button1_1_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 1);
        }

        private void Button1_2_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 2);
        }

        private void Button1_3_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 3);
        }

        private void Button1_4_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 4);
        }

        private void Button1_5_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 5);
        }

        private void Button1_6_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 6);
        }

        private void Button1_7_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 7);
        }

        private void Button1_8_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay1, 8);
        }


        private void Button25_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 1);
        }

        private void Button24_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 2);
        }

        private void Button23_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 3);
        }

        private void Button22_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 4);
        }

        private void Button21_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 5);
        }

        private void Button20_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 6);
        }

        private void Button19_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 7);
        }

        private void Button18_Click(object sender, EventArgs e)
        {
            RelayToggle((Button)sender, relay2, 8);
        }

        private void ButtonTune_Click_2(object sender, EventArgs e)
        {

        }

        private void Form1_HelpButtonClicked(object sender, CancelEventArgs e)
        {
            MessageBox.Show("Help");
        }

        private void Pause()
        {
            //if (toggle) paused = !paused;
            //else paused = true;
            if (pausedTuning)
            {
                buttonTunePause.Text = "Resume";
                buttonTunePause.BackColor = Color.Red;
                buttonTunePause.ForeColor = Color.White;
                buttonTunePause.Enabled = true;
                //buttonTune.Enabled = false;
                //labelSWR.Text = "SWR Paused";
                //buttonTunerStatus.BackColor = Color.Yellow;
                Debug(DebugEnum.LOG, "Tuning paused\n");
                Thread.Sleep(1000);
            }
            else if (!pauseButtonClicked)
            {
                buttonTunePause.Text = "Pause";
                buttonTunePause.BackColor = Color.Green;
                buttonTunePause.ForeColor = Color.White;
                //buttonTune.Enabled = true;
                labelSWR.Text = "SWR";
                timerGetFreq.Stop();
                FLRigGetFreq(false);
                TuneSequence();
                timerGetFreq.Start();
                Debug(DebugEnum.LOG, "Tuning resumed\n");
            }
        }
        private void ButtonTunePause_Click(object sender, EventArgs e)
        {
            if (pausedTuning)
            {
                if (buttonTunePause.Text.Equals("Resume", StringComparison.InvariantCulture))
                {
                    //System.IO.File.Delete(walkingRequestFile);
                    pausedTuning = false;
                    pauseButtonClicked = false;
                    Pause();
                }
            }
            else
            {
                if (buttonTunePause.Text.Equals("Pause", StringComparison.InvariantCulture))
                {
                    pauseButtonClicked = true;
                    pausedTuning = true;
                    Pause();
                }
                else // Must be "Resume"
                {
                    pauseButtonClicked = false;
                    pausedTuning = false;
                    Pause();
                }
            }
        }


        private void CheckBoxPTTEnabled_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void CheckBoxToneEnable_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void CheckEnable(ComboBox box1, ComboBox box2, CheckBox check1)
        {
            if (formLoading || formClosing) return;
            bool ok = box1.Text.Length > 0;
            if (box2 != null) ok = ok && (box2.Text.Length > 0);
            if (ok)
            {
                check1.Checked = true;
                check1.Enabled = true;
            }
            else
            {
                check1.Checked = false;
                check1.Enabled = false;
            }

        }

        private void ComboBoxComRelay1_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckEnable(comboBoxComRelay1, null, checkBoxRelay1Enabled);
        }

        private void ComboBoxBaudRelay1_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Baud not implemented");
            CheckEnable(comboBoxComRelay1, null, checkBoxRelay1Enabled);
        }

        private void ComboBoxComRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckEnable(comboBoxComRelay2, null, checkBoxRelay2Enabled);
        }

        private void ComboBoxBaudRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Baud not implemented");
            CheckEnable(comboBoxComRelay2, comboBoxBaudRelay2, checkBoxRelay2Enabled);
        }

        private void ComboBoxComRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Check Relay3 implementaion");
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxBaudRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Check Relay3 implementaion");
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxComRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Check Relay4 implementaion");
            CheckEnable(comboBoxComRelay4, comboBoxBaudRelay4, checkBoxRelay4Enabled);
        }

        private void ComboBoxBaudRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
            MyMessageBox("Check Relay4 implementaion");
            CheckEnable(comboBoxComRelay4, comboBoxBaudRelay4, checkBoxRelay4Enabled);
        }

        private void CheckBoxRelay4Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            checkBoxRelay4Enabled.Enabled = true;
            if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex >= 0)
            {
                //relay4 = new Relay();
                //Debug(DebugEnum.LOG, "Relay4 open\n");
                relay4.Open(comboBoxComRelay4.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay4.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay4 closed\n");
                relay4.Close();
            }
            else if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex < 0)
            {
                MyMessageBox("Select COM port before enabling Relay4");
                checkBoxRelay4Enabled.Checked = false;
            }

        }

        private bool AntennaTabCheckValues(TextBox from, TextBox to, TextBox power)
        {
            try
            {
                Convert.ToDouble(from.Text, CultureInfo.InvariantCulture);
                Convert.ToDouble(to.Text, CultureInfo.InvariantCulture);
                Convert.ToInt32(power.Text, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return false;
            }
            return true;
        }

        private void CheckBoxPower1Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower1From, textBoxPower1To, textBoxPower1Watts))
            {
                MyMessageBox("Power tab entry #1 values are not valid");
                checkBoxPower1Enabled.Checked = false;
            }
        }

        private void CheckBoxPower2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower2From, textBoxPower2To, textBoxPower2Watts))
            {
                MyMessageBox("Power tab entry #2 values are not valid");
                checkBoxPower2Enabled.Checked = false;
            }
        }

        private void CheckBoxPower3Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower3From, textBoxPower3To, textBoxPower3Watts))
            {
                MyMessageBox("Power tab entry #3 values are not valid");
                checkBoxPower3Enabled.Checked = false;
            }
        }

        private void CheckBoxPower4Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower4From, textBoxPower4To, textBoxPower4Watts))
            {
                MyMessageBox("Power tab entry #4 values are not valid");
                checkBoxPower4Enabled.Checked = false;
            }
        }

        private void CheckBoxPower5Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower5From, textBoxPower5To, textBoxPower5Watts))
            {
                MyMessageBox("Power tab entry #5 values are not valid");
                checkBoxPower5Enabled.Checked = false;
            }
        }

        private void CheckBoxPower6Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower6From, textBoxPower6To, textBoxPower6Watts))
            {
                MyMessageBox("Power tab entry #6 values are not valid");
                checkBoxPower6Enabled.Checked = false;
            }
        }

        private void CheckBoxPower7Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower7From, textBoxPower7To, textBoxPower7Watts))
            {
                MyMessageBox("Power tab entry #7 values are not valid");
                checkBoxPower7Enabled.Checked = false;
            }
        }

        private void CheckBoxPower8Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower8From, textBoxPower8To, textBoxPower8Watts))
            {
                MyMessageBox("Power tab entry #8 values are not valid");
                checkBoxPower8Enabled.Checked = false;
            }
        }

        private void TimerDebug_Tick(object sender, EventArgs e)
        {
            GC.Collect(0);
            GC.WaitForPendingFinalizers();
            if (tuner1 == null) return;
            if (checkBoxPause.Checked) return;
            DebugMsg msg = DebugMsg.DebugGetMsg();
            debugLevel = (DebugEnum)comboBoxDebugLevel.SelectedIndex + 1;
            if (debugLevel < 0) debugLevel = DebugEnum.WARN;
            if (tuner1 != null) tuner1.SetDebugLevel(debugLevel);
            while (msg != null)
            {
                if (msg.Level <= debugLevel || msg.Level == DebugEnum.LOG)
                {
                    richTextBoxDebug.AppendText(msg.Text);
                    if (msg.Level <= DebugEnum.WARN)
                    {
                        labelControlLog.Text = labelControlLog2.Text;
                        labelControlLog2.Text = msg.Text;
                    }
                    Application.DoEvents();
                    richTextBoxDebug.SelectionStart = 0;
                    richTextBoxDebug.ScrollToCaret();
                    while (richTextBoxDebug.Lines.Length > 1000)
                    {
                        richTextBoxDebug.Select(0, richTextBoxDebug.GetFirstCharIndexFromLine(1));
                        richTextBoxDebug.SelectedText = "";
                    }
                    richTextBoxDebug.Select(richTextBoxDebug.Text.Length, 0);
                }
                //Debug(msg.Level, msg.Text);
                msg = DebugGetMsg();
            }
        }

        private void ComboBoxNRelaysSet()
        {
            return;
            /*
            //int nRelays = Int32.Parse(comboBoxNRelays.Text, CultureInfo.InvariantCulture);
            tabControl1.TabPages.Remove(tabPageRelay1);
            tabControl1.TabPages.Remove(tabPageRelay2);
            tabControl1.TabPages.Remove(tabPageRelay3);
            tabControl1.TabPages.Remove(tabPageRelay4);
            //if (relay1ToolStripMenuItem.Checked)
            //{
                tabControl1.TabPages.Add(tabPageRelay1);
            //}
            if (relay2ToolStripMenuItem.Checked)
            {
                tabControl1.TabPages.Add(tabPageRelay2);
            }
            if (relay3ToolStripMenuItem.Checked)
            {
                tabControl1.TabPages.Add(tabPageRelay3);
            }
            if (relay4ToolStripMenuItem.Checked)
            {
                tabControl1.TabPages.Add(tabPageRelay4);
            }
            */
        }

        private void ComboBox1_SelectedIndexChanged(object sender, EventArgs e)
        {
            ComboBoxNRelaysSet();
        }

        private void CheckBoxAntenna_CheckedChanged(object sender, EventArgs e)
        {
            tabPage.TabPages.Remove(tabPageAntenna);
            if (antennaToolStripMenuItem.Checked && tabPage.TabPages.IndexOf(tabPageAntenna) < 0)
            {
                tabPage.TabPages.Add(tabPageAntenna);
            }
        }

        private void CheckBoxPower_CheckedChanged(object sender, EventArgs e)
        {
            TabPage thisPage = tabPagePower;
            tabPage.TabPages.Remove(thisPage);
            if (powerToolStripMenuItem.Checked && tabPage.TabPages.IndexOf(thisPage) < 0)
            {
                tabPage.TabPages.Add(thisPage);
            }
        }

        private void CheckBoxDebug_CheckedChanged(object sender, EventArgs e)
        {
            // We'll keep the debug tab open all the time
            // Do we want to only enable it when an error occurs perhaps?
            //TabPage thisPage = tabPageDebug;
            //tabControl1.TabPages.Remove(thisPage);
            //if (debugToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(thisPage) < 0)
            //{
            //    tabControl1.TabPages.Add(thisPage);
            //}
        }

        private void CheckBoxTuner_CheckedChanged(object sender, EventArgs e)
        {
            TabPage thisPage = tabPageTuner;
            tabPage.TabPages.Remove(thisPage);
            if (tunerToolStripMenuItem.Checked && tabPage.TabPages.IndexOf(thisPage) < 0)
            {
                tabPage.TabPages.Add(thisPage);
            }
        }

        private void Exit()
        {
            Cursor.Current = Cursors.WaitCursor;
            timerDebug.Stop();
            timerGetFreq.Stop();
            timerDebug.Enabled = false;
            timerGetFreq.Enabled = false;
            Application.Exit();
        }
        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Cursor.Current = Cursors.WaitCursor;
            Exit();
        }

        private void ComboBoxTunerModel_SelectedIndexChanged_1(object sender, EventArgs e)
        {
        }

        private void CheckBoxRelay3Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            checkBoxRelay3Enabled.Enabled = true;
            if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex >= 0)
            {
                //relay3 = new Relay();
                //Debug(DebugEnum.LOG, "Relay3 open\n");
                relay3.Open(comboBoxComRelay3.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay3.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay3 closed\n");
                relay3.Close();
            }
            else if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex < 0)
            {
                MyMessageBox("Select COM port before enabling Relay3");
                checkBoxRelay3Enabled.Checked = false;
            }

        }

        private void Relay1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            relay1ToolStripMenuItem.Checked = !relay1ToolStripMenuItem.Checked;
            tabPage.TabPages.Remove(tabPageRelay1);
            //if (relay1ToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageRelay1) < 0)
            //{
            tabPage.TabPages.Add(tabPageRelay1);
            //}
        }

        private void Relay2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            relay2ToolStripMenuItem.Checked = !relay2ToolStripMenuItem.Checked;
            tabPage.TabPages.Remove(tabPageRelay2);
            if (relay2ToolStripMenuItem.Checked && tabPage.TabPages.IndexOf(tabPageRelay2) < 0)
            {
                tabPage.TabPages.Add(tabPageRelay2);
            }
        }

        private void NumericUpDownCapacitance_MouseUp(object sender, MouseEventArgs e)
        {
        }

        private void ComboBoxDebugLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tuner1 == null)
                return;
            switch (comboBoxDebugLevel.SelectedIndex)
            {
                case -1: break;
                case 0: tuner1.SetDebugLevel(DebugEnum.ERR); break;
                case 1: tuner1.SetDebugLevel(DebugEnum.WARN); break;
                case 2: tuner1.SetDebugLevel(DebugEnum.TRACE); break;
                case 3: tuner1.SetDebugLevel(DebugEnum.VERBOSE); break;
                default: MyMessageBox("Invalid debug level?  level=" + comboBoxDebugLevel.SelectedIndex); break;
            }
        }
        private void AboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                if (aboutForm == null)
                    aboutForm = new AboutForm();
                aboutForm.Show();
                aboutForm.WindowState = FormWindowState.Normal;
            }
            catch
            {
                aboutForm = new AboutForm();
                aboutForm.Show();
            }
        }

        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            try
            {
                if (helpForm == null)
                    helpForm = new HelpForm();
                helpForm.Show();
                helpForm.WindowState = FormWindowState.Normal;
            }
            catch
            {
                helpForm = new HelpForm();
                helpForm.Show();
            }
        }

        private void ComboBoxBaudTuner_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (formLoading || formClosing) return;
            if (comboBoxTunerModel.Text.Length > 0 && comboBoxComTuner.Text.Length > 0)
            {
                checkBoxTunerEnabled.Checked = true;
            }
            else
            {
                checkBoxTunerEnabled.Checked = false;
            }

        }

        private void ComboBoxComTuner_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (formLoading || formClosing) return;
            if (comboBoxTunerModel.Text.Length > 0 && comboBoxComTuner.Text.Length > 0)
            {
                checkBoxTunerEnabled.Checked = true;
            }
            else
            {
                checkBoxTunerEnabled.Checked = false;
            }
            //string s = comboBoxTunerModel.SelectedIndex+"\n"+comboBoxBaudTuner.SelectedIndex+"\n";
            //MyMessageBox(s);
        }

        private void CheckBoxTunerEnabled_CheckedChanged_1(object sender, EventArgs e)
        {
            if (activatedHasExecuted && checkBoxTunerEnabled.Checked)
            {
                TunerOpen();
            }
            else if (!checkBoxTunerEnabled.Checked) TunerClose();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            if (activatedHasExecuted) return;
            activatedHasExecuted = true;
            TunerOpen();
            FLRigGetFreq();
            SetAntennaInUse();
        }

        private void RichTextBoxDebug_TextChanged(object sender, EventArgs e)
        {

        }

        private void AmpSetButton()
        {
            if (ampIsOn)
            {
                buttonAmp.BackColor = Color.Green;
                buttonAmp.ForeColor = Color.White;
            }
            else
            {
                buttonAmp.BackColor = Color.Yellow;
                buttonAmp.ForeColor= Color.Black;
            }
        }

       // private bool AmpIsOn()
       // {
       //     return ampIsOn;
       // }

        private void AmpSet(bool flag)
        {
            // false means turn amp off, true means turn it on
            // depends on how relay is wired
            // this implementation assumes wired on NC (normally closed)
            // so setting relay=1 opens the connection, disconnecting the amp
            if (tuner1 == null) return;
            int bit = flag == true ? 0 : 1;
            //tuner1.CMDAmp((byte)!bit);
            if (relay1 != null)
            {
                // MDB this needs to be more robust
                // For now 8/2 is the only exception for the Denkovi 4-channel relay
                // Otherwise amp should be on 8/1
                if (comboBoxAmpBits.Text.Equals("8/2"))
                    RelaySet(relay1, 2, bit);
                else
                    RelaySet(relay1, 1, bit);
            }
            ampIsOn = bit == 0;
            AmpSetButton();
        }

        private void AmpToggle()
        {
            if (tuner1 == null) return;
            ampIsOn = !ampIsOn;
            tuner1.CMDAmp((byte)(ampIsOn ? 1 : 0)); // amp toggle
            if (relay1 != null)
            {
                if (ampIsOn) RelaySet(relay1, 1, 0);
                else RelaySet(relay1, 1, 1);
            }
            AmpSetButton();
        }
        private void ButtonAmp_Click(object sender, EventArgs e)
        {
            AmpToggle();
            Debug(DebugEnum.LOG, "ampStatus=" + ampIsOn + "\n");
            AmpSetButton();
        }

        private void AntennaSet(Relay relay, int bits)
        {
            int val = 0;
            relay.Set(1, (byte)val);
        }

        private void CheckBoxAntenna1_CheckedChanged(object sender, EventArgs e)
        {
            Relay relay = null;
            if (comboBoxAntenna1Relay.Text.Equals("Relay1", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay1;
            }
            if (comboBoxAntenna1Relay.Text.Equals("Relay2", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay2;
            }
            if (comboBoxAntenna1Relay.Text.Equals("Relay3", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay3;
            }
            if (comboBoxAntenna1Relay.Text.Equals("Relay4", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay4;
            }
            if (relay != null)
            {
                AntennaSet(relay, 1);
                if (checkBoxAntenna1.Checked) AntennaSet(relay, 1);
                else AntennaSet(relay, 0);
            }
        }

        private void LinkLabel1_LinkClicked_1(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                //Call the Process.Start method to open the default browser   
                //with a URL:  
                System.Diagnostics.Process.Start("https://www.amazon.com/s?k=sainsmart+usb+4+channel&i=industrial&ref=nb_sb_noss");
            }
            catch (Exception)
            {
                MyMessageBox("Unable to open link that was clicked.");
            }
        }


        private void Button26_Click(object sender, EventArgs e)
        {

        }

        private void TabControl1_VisibleChanged(object sender, EventArgs e)
        {
            tabPage.Refresh();
            richTextBoxDebug.Update();
        }

        private void GroupBox2_Enter(object sender, EventArgs e)
        {

        }

        private void ButtonTunerSave_Click(object sender, EventArgs e)
        {
            tuner1.Save();
        }

        private void NumericUpDownCapacitance_ValueChanged(object sender, EventArgs e)
        {
            if (formLoading)
            {
                return;
            }

            NumericUpDown obj = (NumericUpDown)sender;
            if (obj.Enabled)
            {
                tuner1.SaveCapacitance(Convert.ToInt32(obj.Value));
            }

        }
        private void NumericUpDownInductance_ValueChanged(object sender, EventArgs e)
        {
            if (formLoading)
            {
                return;
            }
            NumericUpDown obj = (NumericUpDown)sender;
            if (obj.Enabled)
            {
                tuner1.SaveInductance(obj.Value);
            }
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            TabPage current = (sender as TabControl).SelectedTab;
            if (current.Text.Equals("Debug"))
                richTextBoxDebug.Refresh();
            current.Refresh();
        }

        private void NumericUpDownPostPttDelay_ValueChanged(object sender, EventArgs e)
        {

        }

        private void CheckBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            tuner1.TuneFull = checkBox1.Checked;
        }

        private void TabPageTuner_Click(object sender, EventArgs e)
        {
            LoadComPorts();
        }

        private void ComboBoxComRelay1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        private void TabPageRelay1_Click(object sender, EventArgs e)
        {
            comboBoxComRelay1.Items.Clear();
            List<string> comPorts = relay1.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay1.Items.Add(comPort);
            }
        }

        private void TabPageRelay2_Click(object sender, EventArgs e)
        {
            comboBoxComRelay2.Items.Clear();
            List<string> comPorts = relay2.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay2.Items.Add(comPort);
            }
        }

        private void TabPageRelay3_Click(object sender, EventArgs e)
        {
            comboBoxComRelay3.Items.Clear();
            List<string> comPorts = relay3.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay3.Items.Add(comPort);
            }

        }

        private void TabPageRelay4_Click(object sender, EventArgs e)
        {
            comboBoxComRelay4.Items.Clear();
            List<string> comPorts = relay4.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay4.Items.Add(comPort);
            }

        }

        private void MenuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void TabControl1_TabIndexChanged(object sender, EventArgs e)
        {
            tabPage.TabPages[tabPage.SelectedIndex].Refresh();
        }

        private void TextBoxAntenna1Bits_TextChanged(object sender, EventArgs e)
        {

        }

        private void ButtonAllOff1_Click(object sender, EventArgs e)
        {
            relay1.AllOff();
            RelaySetButtons(button1_1, relay1.Status());

        }

        private void ButtonAllOff2_Click(object sender, EventArgs e)
        {
            relay2.AllOff();
            RelaySetButtons(button2_1, relay2.Status());
        }

        private void ButtonAllOff3_Click(object sender, EventArgs e)
        {
            relay3.AllOff();
            RelaySetButtons(button3_1, relay3.Status());
        }
        private void ButtonAllOff4_Click(object sender, EventArgs e)
        {
            relay4.AllOff();
            RelaySetButtons(button4_1, relay4.Status());
        }

        private void ComboBoxAntenna1Bits_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetAntennaInUse();
        }

        private void TextBoxAntennaFreq1From_Leave(object sender, EventArgs e)
        {
            SetAntennaInUse();
        }

        private void ComboBoxAntSelect1_SelectedIndexChanged(object sender, EventArgs e)
        {
            
            SetAntennaInUse();
        }

        private void CheckBoxAntenna2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void SetWalkAntennaAsActive()
        {
            SetAntennaInUse();
            //SetAntennaRelayOn(Properties.Settings.Default.WalkAntenna);
        }
        private void SetWalkAntennaToUse(int antennaNumber)
        {
            textBoxAntenna1.BackColor = Color.White;
            textBoxAntenna2.BackColor = Color.White;
            textBoxAntenna3.BackColor = Color.White;
            textBoxAntenna4.BackColor = Color.White;
            textBoxAntenna5.BackColor = Color.White;
            textBoxAntenna6.BackColor = Color.White;
            textBoxAntenna7.BackColor = Color.White;
            textBoxAntenna8.BackColor = Color.White;
            switch (antennaNumber)
            {
                case 1:
                    textBoxAntenna1.BackColor = Color.Yellow;
                    break;
                case 2:
                    textBoxAntenna2.BackColor = Color.Yellow;
                    break;
                case 3:
                    textBoxAntenna3.BackColor = Color.Yellow;
                    break;
                case 4:
                    textBoxAntenna4.BackColor = Color.Yellow;
                    break;
                case 5:
                    textBoxAntenna5.BackColor = Color.Yellow;
                    break;
                case 6:
                    textBoxAntenna6.BackColor = Color.Yellow;
                    break;
                case 7:
                    textBoxAntenna7.BackColor = Color.Yellow;
                    break;
                case 8:
                    textBoxAntenna8.BackColor = Color.Yellow;
                    break;
            }
            Properties.Settings.Default.WalkAntenna = antennaNumber;
        }


        //readonly int[] frequenciesToWalk = { 50313000, 28074000, 24915000, 21074000, 18100000, 14074000, 10136000, 7074000, 3573000   };
        //readonly string frequenciesToWalkFT8 = "1.840 3.573 7.074 10.136 14.074 18.100 21.074  24.915 28.074 50.313";
        //readonly string frequenciesToWalkFT4 = "3.575 7.0475 10.14 14.08 18.104 21.140 24.919 28.180 50.318";
        List<int> frequenciesToWalk;
        int frequencyIndex = 0;
        private bool dummyLoad;
        private int timerFreqWalkSeconds;

        private void FreqWalkSetFreq(int index)
        {
            //if (index == 0) richTextBoxFreqWalk.Clear();
            //richTextBoxFreqWalk.AppendText(MyTime() + "Set VFOA " + frequenciesToWalk[index] / 1e6 + "MHz\n");
            var myparam = "<params><param><value><double>" + frequenciesToWalk[index] + "</double></value></param></params";
            var xml = FLRigXML("rig.set_split", "0");
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            xml = FLRigXML("rig.set_vfo" + 'A', myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            xml = FLRigXML("rig.set_vfo" + 'B', myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            labelFreq.Text = frequenciesToWalk[index] / 1e6 + "MHz";
            labelFreq.Text = (frequenciesToWalk[index] / 1e6).ToString(CultureInfo.InvariantCulture) + "MHz" + " " + modeCurrent;
        }
        private void TimerFreqWalk_Tick(object sender, EventArgs e)
        {
            try
            {
                timerFreqWalk.Stop();
                //richTextBox1.AppendText(MyTime() + "Tick\n");
                FLRigGetFreq(false);
                if (frequencyIndex >= 0 && frequencyHz != frequenciesToWalk[frequencyIndex]) {
                    FreqWalkStop();
                    return;
                }
                if (frequencyIndex < 0)
                {
                    frequencyIndex = 0;
                    FreqWalkSetFreq(frequencyIndex);
                }
                else {
                    double mySecond = ((double)DateTime.Now.Second + DateTime.Now.Millisecond / 1000.0);
                    double triggerTime = timerFreqWalkSeconds;
                    int interval = (int)((mySecond - (mySecond % timerFreqWalkSeconds)) / timerFreqWalkSeconds);
                    triggerTime += interval * timerFreqWalkSeconds;
                    // this should be 60 30 15 seconds
                    // we do our thing 700ms before the end of period
                    // this gives JTDX/WSJTX a chance to get the band correct on the decode window
                    triggerTime -= 0.7;
                    //richTextBoxFreqWalk.AppendText("Debug: " + mySecond + ">" + triggerTime + "\n");
                    if (mySecond > triggerTime)
                    {
                        frequencyIndex++;
                        if (frequencyIndex >= frequenciesToWalk.Count) 
                        { 
                            frequencyIndex = 0; 
                        }
                        FreqWalkSetFreq(frequencyIndex);
                        Thread.Sleep(800);
                        FLRigGetFreq(false);
                    }
                }
                timerFreqWalk.Start();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message+"\n"+ex.StackTrace);    
            }
        }

        List<int> FrequenciesToWalk()
        {
            if (frequenciesToWalk == null) frequenciesToWalk = new List<int>();
            frequenciesToWalk.Clear();
            CheckedListBox.CheckedItemCollection items = checkedListBoxWalkFT8.CheckedItems;
            foreach (string s in items)
            {
                var myFreq = (int)(double.Parse(s) * 1000000);
                if (myFreq > 0) {
                    frequenciesToWalk.Add(myFreq);
                }
            }
            items = checkedListBoxWalkFT4.CheckedItems;
            foreach (string s in items)
            {
                var myFreq = (int)(double.Parse(s) * 1000000);
                if (myFreq > 0)
                {
                    frequenciesToWalk.Add(myFreq);
                }
            }
            items = checkedListBoxWalkCustom.CheckedItems;
            foreach (string s in items)
            {
                var myFreq = (int)(double.Parse(s) * 1000000);
                if (myFreq > 0)
                {
                    frequenciesToWalk.Add(myFreq);
                }
            }
            return frequenciesToWalk;
        }
        private void FreqWalkStart()
        {
            frequencyIndex = -1;
            freqWalkIsRunning = true;
            SetWalkAntennaAsActive();
            buttonWalk.Text = "Walking";
            buttonWalk.BackColor = Color.LightGreen;
            pausedTuning = true;
            FrequenciesToWalk();
            /*
            if (textBoxFrequencyWalkList.Text.Length == 0)
            {
                textBoxFrequencyWalkList.Text = frequenciesToWalkFT8;
            }
            var tokens = textBoxFrequencyWalkList.Text.Split(' ');
            if (frequenciesToWalk == null) frequenciesToWalk = new List<int>();
            frequenciesToWalk.Clear();
            foreach (string s in tokens)
            {
                if (s.Length > 0)
                    frequenciesToWalk.Add((int)(double.Parse(s) * 1000000));
            }
            */
            FreqWalkSetFreq(0);
            timerFreqWalk.Start();
            //richTextBoxFreqWalk.AppendText(MyTime() + "Walking started\n");
        }

        private void FreqWalkStop()
        {
            buttonWalk.Text = "Walk";
            buttonWalk.BackColor = Color.LightGray;
            timerFreqWalk.Stop();
            Thread.Sleep(500);
            freqWalkIsRunning = false;
            pausedTuning = false;
            //richTextBoxFreqWalk.AppendText(MyTime() + "Walking stopped\n");
            SetAntennaInUse();
        }
        private void ButtonWalk_Click_1(object sender, EventArgs e)
        {
            if (freqWalkIsRunning == false)
            {
                FreqWalkStart();
            }
            else 
            {
                FreqWalkStop();
            }
        }

        private void ButtonDummyLoad_Click(object sender, EventArgs e)
        {
            if (dummyLoad == false)
            {
                dummyLoad = true;
                //buttonDummyLoad.BackColor = Color.Green;
                SetAntennaInUse();
            }
            else
            {
                dummyLoad = false;
                //buttonDummyLoad.BackColor = Color.LightGray;
                SetAntennaInUse();
            }
        }

        private void AntennaSetPickButtons()
        {
            buttonAntennaPick1.Visible = textBoxAntenna1.Text.Length > 0;
            buttonAntennaPick2.Visible = textBoxAntenna2.Text.Length > 0;
            buttonAntennaPick3.Visible = textBoxAntenna3.Text.Length > 0;
            buttonAntennaPick4.Visible = textBoxAntenna4.Text.Length > 0;
            buttonAntennaPick5.Visible = textBoxAntenna5.Text.Length > 0;
            buttonAntennaPick6.Visible = textBoxAntenna6.Text.Length > 0;
            buttonAntennaPick7.Visible = textBoxAntenna7.Text.Length > 0;
            buttonAntennaPick8.Visible = textBoxAntenna8.Text.Length > 0;

            buttonAntennaPick1.Text = textBoxAntenna1.Text;
            buttonAntennaPick2.Text = textBoxAntenna2.Text;
            buttonAntennaPick3.Text = textBoxAntenna3.Text;
            buttonAntennaPick4.Text = textBoxAntenna4.Text;
            buttonAntennaPick5.Text = textBoxAntenna5.Text;
            buttonAntennaPick6.Text = textBoxAntenna6.Text;
            buttonAntennaPick7.Text = textBoxAntenna7.Text;
            buttonAntennaPick8.Text = textBoxAntenna8.Text;
        }
        private void AntennaUpdateSelected(int antennaNumber)
        {
            buttonAntenna1.BackColor = Color.LightGray;
            buttonAntenna2.BackColor = Color.LightGray;
            buttonAntenna3.BackColor = Color.LightGray;
            buttonAntenna4.BackColor = Color.LightGray;
            buttonAntenna5.BackColor = Color.LightGray;
            buttonAntenna6.BackColor = Color.LightGray;
            buttonAntenna7.BackColor = Color.LightGray;
            buttonAntenna8.BackColor = Color.LightGray;
            switch (antennaNumber)
            { 
                case 1:
                    buttonAntenna1.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick1);
                    labelAntennaSelected.Text = textBoxAntenna1.Text;
                    break;
                case 2:
                    buttonAntenna2.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick2);
                    labelAntennaSelected.Text = textBoxAntenna2.Text;
                    break;
                case 3:
                    buttonAntenna3.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick3);
                    labelAntennaSelected.Text = textBoxAntenna3.Text;
                    break;
                case 4:
                    buttonAntenna4.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick4);
                    labelAntennaSelected.Text = textBoxAntenna4.Text;
                    break;
                case 5:
                    buttonAntenna5.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick5);
                    labelAntennaSelected.Text = textBoxAntenna5.Text;
                    break;
                case 6:
                    buttonAntenna6.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick6);
                    labelAntennaSelected.Text = textBoxAntenna6.Text;
                    break;
                case 7:
                    buttonAntenna7.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick7);
                    labelAntennaSelected.Text = textBoxAntenna7.Text;
                    break;
                case 8:
                    buttonAntenna8.BackColor = Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick8);
                    labelAntennaSelected.Text = textBoxAntenna8.Text;
                    break;
            }
        }
        private void ButtonAntenna1_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(1);
            labelAntennaSelected.Text = textBoxAntenna1.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna2_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(2);
            labelAntennaSelected.Text = textBoxAntenna2.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna3_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(3);
            labelAntennaSelected.Text = textBoxAntenna3.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna4_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(4);
            labelAntennaSelected.Text = textBoxAntenna4.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna5_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(5);
            labelAntennaSelected.Text = textBoxAntenna5.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna6_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(6);
            labelAntennaSelected.Text = textBoxAntenna6.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna7_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(7);
            labelAntennaSelected.Text = textBoxAntenna7.Text;
            SetAntennaInUse();
        }

        private void ButtonAntenna8_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(8);
            labelAntennaSelected.Text = textBoxAntenna8.Text;
            SetAntennaInUse();
        }

        private void ButtonAntennaPickReset()
        {
            buttonAntennaPick1.BackColor = Color.WhiteSmoke;
            buttonAntennaPick2.BackColor = Color.WhiteSmoke;
            buttonAntennaPick3.BackColor = Color.WhiteSmoke;
            buttonAntennaPick4.BackColor = Color.WhiteSmoke;
            buttonAntennaPick5.BackColor = Color.WhiteSmoke;
            buttonAntennaPick6.BackColor = Color.WhiteSmoke;
            buttonAntennaPick7.BackColor = Color.WhiteSmoke;
            buttonAntennaPick8.BackColor = Color.WhiteSmoke;

            buttonAntennaPick1.ForeColor = Color.Black;
            buttonAntennaPick2.ForeColor = Color.Black;
            buttonAntennaPick3.ForeColor = Color.Black;
            buttonAntennaPick4.ForeColor = Color.Black;
            buttonAntennaPick5.ForeColor = Color.Black;
            buttonAntennaPick6.ForeColor = Color.Black;
            buttonAntennaPick7.ForeColor = Color.Black;
            buttonAntennaPick8.ForeColor = Color.Black;
        }

        private void ButtonAntennaPickSet(Button myButton)
        {
            ButtonAntennaPickReset();
            myButton.BackColor = Color.Green;
            myButton.ForeColor = Color.White;   
        }

        private void ButtonAntennaPick1_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick1);
            SetAntennaRelayOn(1);
        }

        private void ButtonAntennaPick2_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick2);
            SetAntennaRelayOn(2);
        }

        private void ButtonAntennaPick3_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick3);
            SetAntennaRelayOn(3);
        }
        private void ButtonAntennaPick4_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick4);
            SetAntennaRelayOn(4);
        }

        private void ButtonAntennaPick5_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick5);
            SetAntennaRelayOn(5);
        }

        private void ButtonAntennaPick6_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick6);
            SetAntennaRelayOn(6);
        }

        private void ButtonAntennaPick7_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick7);
            SetAntennaRelayOn(7);
        }

        private void ButtonAntennaPick8_Click(object sender, EventArgs e)
        {
            ButtonAntennaPickSet(buttonAntennaPick8);
            SetAntennaRelayOn(8);
        }

        private void CheckBoxAntenna1Amp_CheckedChange(object sender, EventArgs e)
        {
            CheckBox myCheckBox = (CheckBox)sender;
            AmpSet(myCheckBox.Checked);
        }

        private void FreqWalkSetIntervalDisplay()
        {
            labelInterval.Text = "Interval " + timerFreqWalkSeconds + " secs";
        }
        private void RichTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            //richTextBoxFreqWalk.Undo();
            if (e.KeyCode == Keys.N)
            {
                frequencyIndex++;
                if (frequencyIndex >= frequenciesToWalk.Count)
                {
                    frequencyIndex = 0;
                }
            }
            else if (e.KeyCode == Keys.F1)
            {
                timerFreqWalkSeconds = 60;
            }
            else if (e.KeyCode== Keys.F2)
            {
                timerFreqWalkSeconds = 30;
            }
            else if (e.KeyCode== Keys.F3)
            {
                timerFreqWalkSeconds = 15;
            }
            FreqWalkSetIntervalDisplay();
        }

        private void LabelClock_Click(object sender, EventArgs e)
        {
            if (clockIsZulu)
            {
                clockIsZulu = false;
            }
            else
            {
                clockIsZulu = true;
            }
        }

        private void TextBoxAntenna1_MouseUp(object sender, MouseEventArgs e)
        {

        }

        private void TextBoxAntenna_MouseDown(object sender, MouseEventArgs e)
        {
            if (ModifierKeys.HasFlag(Keys.Control))
            {
                if (sender == textBoxAntenna1)
                    SetWalkAntennaToUse(1);
                else if (sender == textBoxAntenna2)
                    SetWalkAntennaToUse(2);
                else if (sender == textBoxAntenna3)
                    SetWalkAntennaToUse(3);
                else if (sender == textBoxAntenna4)
                    SetWalkAntennaToUse(4);
                else if (sender == textBoxAntenna5)
                    SetWalkAntennaToUse(5);
                else if (sender == textBoxAntenna6)
                    SetWalkAntennaToUse(6);
                else if (sender == textBoxAntenna7)
                    SetWalkAntennaToUse(7);
                else if (sender == textBoxAntenna8)
                    SetWalkAntennaToUse(8);
            }
            AntennaSetPickButtons();
        }

        private void CheckedListBoxWalk_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckedListBox myBox = (CheckedListBox)sender;
            var index = myBox.SelectedIndex;
            if (index == -1) return;
            try
            {
                if (ModifierKeys == Keys.Control)
                {
                    double freq = 0;
                    var answer = Interaction.InputBox("Enter Freq in MHz");
                    if (answer.Length == 0) return;
                    do
                    {
                        try
                        {
                            freq = double.Parse(answer) * 1e6;
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                        }
                        if (freq > 100e9)
                        {
                            MessageBox.Show("Frequency > 100GHz!!");
                            return;
                        }
                    } while (freq < 0 || freq > 100e9);
                    if (answer.Length > 0)
                    {
                        myBox.Items[index] = answer;
                    }
                }
                else if (ModifierKeys == Keys.Shift)
                {
                    for (int i=0; i< myBox.Items.Count; i++)
                    {
                        if (myBox.Items[i].ToString() != "0")
                        {
                            myBox.SetItemChecked(i, true);
                        }
                    }
                }
                else if (ModifierKeys == (Keys.Shift | Keys.Control))
                {
                    for (int i = 0; i < myBox.Items.Count; i++)
                    {
                        if (myBox.Items[i].ToString() != "0")
                        {
                            myBox.SetItemChecked(i, false);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            if (myBox.Items[index].ToString() == "0")
            {
                myBox.SetItemChecked(index, false);
            }
        }

        private void LabelInterval_Click(object sender, EventArgs e)
        {
            switch (timerFreqWalkSeconds)
            {
                case 15: timerFreqWalkSeconds = 30;break;
                case 30: timerFreqWalkSeconds = 60; break;
                case 60: timerFreqWalkSeconds = 120; break;
                case 120: timerFreqWalkSeconds = 180; break;
                case 180: timerFreqWalkSeconds = 240; break;
                case 240: timerFreqWalkSeconds = 300; break;
                case 300: timerFreqWalkSeconds = 15; break;
            }
            labelInterval.Text = "Interval " + timerFreqWalkSeconds.ToString() + " seconds";
        }
    }

    public class ComboBoxItem
    {
        public string Text { get; set; }
        public object MyGUID { get; set; }

        public override string ToString()
        {
            return Text;
        }

        public static bool operator ==(ComboBoxItem obj1, ComboBoxItem obj2)
        {
            if (obj1 != null && obj2 != null && obj1.MyGUID.Equals(obj2.MyGUID))
            {
                return true;
            }
            return false;
        }

        public static bool operator !=(ComboBoxItem obj1, ComboBoxItem obj2)
        {
            if (obj1 != null && obj2 != null && obj1.MyGUID.Equals(obj2.MyGUID))
            {
                return false;
            }
            return true;
        }

        public override bool Equals(object obj)
        {
            if (this.MyGUID.ToString().Equals(obj))
            //string val1 = GUID.ToString();
            //ComboBoxItem mybox = (obj as ComboBoxItem);
            //string val2 = (obj as ComboBoxItem).GUID.ToString();
            //if (val1.Equals(val2))
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Serializable]
    public class AmpAutoTunerUtilException : System.Exception
    {
        readonly Exception Inner;
        public AmpAutoTunerUtilException() : base() { }
        public AmpAutoTunerUtilException(string message) : base(message)
        {
            //MessageBox.Show(message+"\n"+ Inner.Message +"\n" + this.StackTrace);
        }
        public AmpAutoTunerUtilException(string message, System.Exception inner) : base(message, inner)
        {
            this.Inner = inner;
            if (Inner != null)
            {
                MessageBox.Show(message + "\n" + Inner.Message + "\n" + Inner.StackTrace);
            }
            else
            {
                MessageBox.Show(message + "\n" + "No Inner Message");
            }
        }

        // A constructor is needed for serialization when an
        // exception propagates from a remoting server to the client. 
        protected AmpAutoTunerUtilException(System.Runtime.Serialization.SerializationInfo info,
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
