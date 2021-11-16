using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Globalization;
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
        readonly string walkingRequestFile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "freqwalkrequest.txt";
        readonly string walkingRequestOKFile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "freqwalkok.txt";
        readonly string walkingOKFile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "freqwalkok.txt";

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
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            if (disposing)
            {
                if (rigStream != null) rigStream.Dispose();
                if (rigClient != null) rigClient.Dispose();
                if (tuner1 != null) tuner1.Dispose();
                if (audio != null) audio.Dispose();
                return;
            }
            base.Dispose(disposing);
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
                if (Properties.Settings.Default.comboBoxAudioOut.Length > 0)
                {
                    comboBoxAudioOut.SelectedItem = Properties.Settings.Default.comboBoxAudioOut as object;
                }
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
                radioButtonAntennaWire3.Checked = Properties.Settings.Default.radioButtonAntennaWire3;
                radioButtonAntennaWire8.Checked = Properties.Settings.Default.radioButtonAntennaWire8;

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
                tabControl1.TabPages.Remove(tabPageRelay2);
                tabControl1.TabPages.Remove(tabPageRelay3);
                tabControl1.TabPages.Remove(tabPageRelay4);
                //relay1.Close();
                relay1 = null;
            }
            else
            {
                relay1.Open(comPorts[0]);
                Debug(DebugEnum.LOG, "Relay1 Opened, serial#" + relay1.SerialNumber() +"\n");
                labelRelay1SerialNumber.Text = relay1.SerialNumber();
                AntennaAddRelay("Relay1");
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
                if (relay1.DevCount() > 3) {
                    relay4 = new Relay();
                    relay4.Open(comPorts[3]);
                    Debug(DebugEnum.LOG, "Relay4 Opened, serial#" + relay4.SerialNumber() + "\n");
                    labelRelay4SerialNumber.Text = relay4.SerialNumber();
                    AntennaAddRelay("Relay4");
                }
                switch (comPorts.Count)
                {
                    case 1:
                        tabControl1.SelectTab(tabPageRelay1);
                        tabControl1.TabPages.Remove(tabPageRelay2);
                        tabControl1.TabPages.Remove(tabPageRelay3);
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(2);
                        break;
                    case 2:
                        tabControl1.SelectTab(tabPageRelay1);
                        tabControl1.SelectTab(tabPageRelay2);

                        tabControl1.TabPages.Remove(tabPageRelay3);
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(3);
                        break;
                    case 3:
                        tabControl1.SelectTab(tabPageRelay1);
                        tabControl1.SelectTab(tabPageRelay2);
                        tabControl1.SelectTab(tabPageRelay3);
                        tabControl1.TabPages.Remove(tabPageRelay4);
                        //form2.ProgressBarSetMax(4);
                        break;
                    case 4:
                        tabControl1.SelectTab(tabPageRelay1);
                        tabControl1.SelectTab(tabPageRelay2);
                        tabControl1.SelectTab(tabPageRelay3);
                        tabControl1.SelectTab(tabPageRelay4);
                        break;
                }
            }
            if (comPorts.Count == 0)
            {
                Debug(DebugEnum.TRACE,"No FTDI relay switches found on USB bus\n");
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 1 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    return;
                }
                relay1.Open(comboBoxComRelay1.Text);
                Debug(DebugEnum.WARN, "Relay 1 opened\n");
                Debug(DebugEnum.WARN, "Serial number " + relay1.SerialNumber() + "\n");
                //form2.ProgressBar(2);
            }
            else
            {
                checkBoxRelay1Enabled.Checked = true;
            }
            if (!checkBoxRelay1Enabled.Checked && checkBoxPowerSDR.Enabled==false)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Please set up Relay1 if not using PowerSDR");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            if (relay1 == null) checkBoxRelay1Enabled.Checked = false;
            if (checkBoxRelay1Enabled.Checked && relay1.SerialNumber().Length == 0)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("No serial#?  Relay1 is not responding!");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            if (relay2 == null) checkBoxRelay2Enabled.Checked = false;
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.Text.Length > 0)
            {
                if (relay2 == null)
                {
                    checkBoxRelay2Enabled.Checked = false;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 2 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    Debug(DebugEnum.WARN,"Relay 2 not found...disabled\n");
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 3 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 4 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
            if (comboBoxAudioOut.SelectedIndex >= 0)
            {
                Properties.Settings.Default.comboBoxAudioOut = (comboBoxAudioOut.SelectedItem as ComboBoxItem).MyGUID.ToString();
            }

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
            tabControl1.SelectTab(tabPageControl);
            //buttonAmp.BackColor = Color.Green;
            Thread.Sleep(100);
            ComboBoxNRelaysSet();

            Application.DoEvents();
            checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
            //TunerOpen();
            //if (ampIsOn == 0) AmpToggle();
            stopWatchTuner.Start(); // Assume we're already tuned

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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        MyMessageBox("Error starting tuner!!!");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    }
                }
                else
                {
                    Debug(DebugEnum.LOG, "Tuner opened on " + tuner1.GetSerialPortTuner() + "\n");
                    checkBoxTunerEnabled.Enabled = true;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
            if (formClosing) 
                return;
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
            Properties.Settings.Default.powerSDR =checkBoxPowerSDR.Checked;

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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Relay On");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            else
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Relay Off");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    Debug(DebugEnum.ERR,"FLRig error...not responding...\n");
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
            bool retval = false;
            // Set power if needed to ensure not overdriving things -- we do it again after we start tuning
            SetAntennaInUse();
            PowerSelect(frequencyHz, modeCurrent, true);
            if (ampIsOn) // we need to turn off amplifier
            {
                if (tuner1 == null)
                {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    _ = MessageBox.Show("No tuner??");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
            Debug(DebugEnum.LOG, "Tuning to " + frequencyHzTune +"\n");

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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR < 1.5";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            else if (response == 'M')
            {
                SoundPlayer simpleSound = new SoundPlayer("swr.wav");
                simpleSound.Play();
                buttonTunerStatus.BackColor = Color.Yellow;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR 1.5-3.0";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                simpleSound.Dispose();
            }
            else if (response == 'F')
            {
                SoundPlayer simpleSound = new SoundPlayer("swr.wav");
                simpleSound.Play();
                buttonTunerStatus.BackColor = Color.Red;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR > 3.0";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                simpleSound.Dispose();
            }
            else
            {
                if (this.WindowState == FormWindowState.Minimized)
                    this.WindowState = FormWindowState.Normal;
                tabControl1.SelectedTab = tabPageDebug;
                buttonTunerStatus.BackColor = Color.Transparent;
                MyMessageBox("Unknown response from tuner = '" + response + "'");
                Debug(DebugEnum.ERR, "Unknown response from tuner = '" + response + "'\n");
            }
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
            tuneIsRunning = false;
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(DebugEnum.ERR, "FLRigGetModes error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabControl1.SelectedTab = tabPageDebug;
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
                    char[] delims = {'<','>', '\r','\n' };
                    string[] tokens = responseData.Split(delims);
                    int i = 0;
                    bool value = false;
                    for (;i<tokens.Length;++i)
                    {
                        if (tokens[i].Equals("data", StringComparison.InvariantCulture)) value = true;
                        if (value == true && tokens[i].Equals("value", StringComparison.InvariantCulture)) {
                            modes.Add(tokens[i + 1]);
                        }
                    }
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>",StringComparison.InvariantCulture);
                        //mode = responseData.Substring(offset1, offset2 - offset1);
                    }
                    else
                    {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
            while((xcvr = FLRigGetXcvr()) == null && formClosing == false && --n > 0) {
                Thread.Sleep(1000);
                DebugAddMsg(DebugEnum.LOG,"Waiting for FLRigv " + n + "\n");
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
            Debug(DebugEnum.LOG, "Rig is " + xcvr +"\n");
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(DebugEnum.ERR, "FLRigGetXcvr error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabControl1.SelectedTab = tabPageDebug;
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
                        int offset1 = responseData.IndexOf("<value>",StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>",StringComparison.InvariantCulture);
                        xcvr = responseData.Substring(offset1, offset2 - offset1);
                        if (xcvr.Length == 0) xcvr = null;
                    }
                    else
                    {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(DebugEnum.ERR, "FLRigGetMode error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                tabControl1.SelectedTab = tabPageDebug;
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                            labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                            Debug(DebugEnum.ERR, responseData + "\n");
                            tabControl1.SelectedTab = tabPageDebug;
                        }
                    }
#pragma warning disable CA1031 // Do not catch general exception types
                    catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                    {
                        Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                        frequencyHz = 0;
                    }
                    retry = false;
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(DebugEnum.ERR, "Error...Rig get_modeA not responding try#" + ++retryCount + "\n" + ex.Message + "\n");
                    frequencyHz = 0;
                }
            } while (retry);
            return mode;
        }

        public static string MyTime()
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff",CultureInfo.InvariantCulture) + ": "; 
            return time;
        }

        private void SetAntennaInUse()
        {
            var antennaNumberNew = "1"; // default to antenna#1
            var modeCurrent = FLRigGetMode();
            //if (!tuneIsRunning) PowerSelect(frequencyHz, modeCurrent);

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
                if (checkBoxAntenna1.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq1From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq1To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna1.Checked = true;
                    buttonAntenna1.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna1.Text;
                    antennaNumberNew = comboBoxAntSelect1.Text;
                    //AmpSet(checkBoxAmp1.Checked);
                }
                else if (checkBoxAntenna2.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq2From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq2To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna2.Checked = true;
                    buttonAntenna2.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna2.Text;
                    antennaNumberNew = comboBoxAntSelect2.Text;
                    //AmpSet(checkBoxAmp2.Checked);
                }
                else if (checkBoxAntenna3.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq3From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq3To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna3.Checked = true;
                    buttonAntenna3.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna3.Text;
                    antennaNumberNew = comboBoxAntSelect3.Text;
                    //AmpSet(checkBoxAmp3.Checked);
                }
                else if (checkBoxAntenna4.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq4From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq4To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna4.Checked = true;
                    buttonAntenna4.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna4.Text;
                    antennaNumberNew = comboBoxAntSelect4.Text;
                    //AmpSet(checkBoxAmp4.Checked);
                }
                else if (checkBoxAntenna5.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq5From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq5To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna5.Checked = true;
                    buttonAntenna5.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna5.Text;
                    antennaNumberNew = comboBoxAntSelect5.Text;
                    //AmpSet(checkBoxAmp5.Checked);
                }
                else if (checkBoxAntenna6.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq6From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq6To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna6.Checked = true;
                    buttonAntenna6.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna6.Text;
                    antennaNumberNew = comboBoxAntSelect6.Text;
                    //AmpSet(checkBoxAmp6.Checked);
                }
                else if (checkBoxAntenna7.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq7From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq7To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna7.Checked = true;
                    buttonAntenna7.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna7.Text;
                    antennaNumberNew = comboBoxAntSelect7.Text;
                    //AmpSet(checkBoxAmp7.Checked);
                }
                else if (checkBoxAntenna8.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq8From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq8To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna8.Checked = true;
                    buttonAntenna8.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna8.Text;
                    antennaNumberNew = comboBoxAntSelect8.Text;
                    //AmpSet(checkBoxAmp8.Checked);
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // don't do anything here...just catching the parse errors from blank boxes
            }
            if (antennaNumberNew.Length == 0) antennaNumberNew = "1";
            var tmp = Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture);
            if (!pausedTuning && tuner1 != null && tmp != tuner1.AntennaNumber)
                tuner1.SetAntenna(Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture), tuneIsRunning);
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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
            string xml = FLRigXML("rig.set_power", "<params><param><value><i4>"+value+"</i4></value></param></params");
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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        from.Text = "0";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        return false;
                    }
                    if (to.Text.Length == 0)
                    {
                        //MyMessageBox("Power tab To MHz is empty");
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        to.Text = "1000";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        return false;
                    }
                    double frequencyFrom = Convert.ToDouble(from.Text, CultureInfo.InvariantCulture);
                    double frequencyTo = Convert.ToDouble(to.Text, CultureInfo.InvariantCulture);
                    if (frequencyMHz >= frequencyFrom && frequencyMHz <= frequencyTo)
                    {
                        if (mode.SelectedItem==null || mode.SelectedItem.Equals(modeChk) || (passNum > 0 && mode.SelectedItem.Equals("Any"))) 
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                                    labelPower.Text = "Power "+power;
#pragma warning restore CA1303 // Do not pass literals as localized parameters

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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelPower.Text = "Power not set";
#pragma warning restore CA1303 // Do not pass literals as localized parameters

            }
        }

        private void FLRigGetFreq(bool needTuning=true)
        {
            getFreqIsRunning = true;
            if (!checkBoxRig.Checked)
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Auto tuning paused because VFOB is active, click OK when you're done", MessageBoxButtons.OK);
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
                tabControl1.SelectedTab = tabPageDebug;
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
                        labelFreq.Text = (frequencyHz / 1000).ToString(CultureInfo.InvariantCulture) + "kHz" + " " + modeCurrent;
                        if (comboBoxPower1Mode.SelectedItem != null && !modeCurrent.Equals(modeOld, StringComparison.InvariantCulture))
                            PowerSelect(frequencyHz, modeCurrent,tuneIsRunning);
                        if (frequencyLast == 0) frequencyLast = frequencyLastTunedHz = frequencyHz;
                        if (frequencyLast == frequencyHz && freqStableCount < freqStableCountNeeded)
                        {
                            ++freqStableCount;
                            stopWatchTuner.Restart();
                        }
                        if (freqStableCount >= freqStableCountNeeded && Math.Abs(frequencyHz - frequencyLastTunedHz) > tolTune && !pausedTuning &!pauseButtonClicked)
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
                            Debug(DebugEnum.LOG, "Rig mode VFOB set to "+mode+"\n");
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
                                myparam = "<params><param><value><double>"+frequencyHz+"</double></value></param></params";
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        Debug(DebugEnum.ERR, responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    //frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
#pragma warning restore CA1031 // Do not catch general exception types
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
        bool FreqWalkIsRunning()
        {
            Process[] pname = Process.GetProcessesByName("powershell");
            foreach (Process p in pname)
            {
                var cmdline = GetCommandLines(p);
                if (GetCommandLines(p).Contains("freqwalk"))
                {
                    return true;
                }
            }
            return false;
        }

        private DialogResult RelayOops(String message)
        {
            //Debug(DebugEnum.ERR, message);
            return MyMessageBox(message);
        }

        private void TimerGetFreq_Tick(object sender, EventArgs e)
        {
            if (pausedTuning) 
                return;
            timerGetFreq.Stop();
            //DebugAddMsg(DebugEnum.VERBOSE, "Timer tick\n");
            if (checkBoxRelay1Enabled.Checked &&  relay1 != null && relay1.Status() == 0xff)
            {
                if (RelayOops("Relay1 closed unexpectedly...RFI??\n") == DialogResult.Retry)
                {
                    relay1.Open();
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
            if (!FreqWalkIsRunning())
            {
                buttonTunePause.Enabled = true;
                labelFreqWalk.Text = "FreqWalk Off";
                if (pausedTuning)
                {
                    //pausedTuning = false;
                    Pause();
                }
            }
            else // see if we need to do something
            {
                if (!pauseButtonClicked) buttonTunePause.Enabled = false;
                if (System.IO.File.Exists(walkingRequestFile) && !pausedTuning)
                {
                    while(tuneIsRunning)
                    {
                        labelFreqWalk.Text = "FreqWalk waiting for tune";
                        Application.DoEvents();
                        tuneIsRunning = false;
                        Thread.Sleep(500);
                    }
                    labelFreqWalk.Text = "FreqWalk walking";
                    pausedTuning = true; // we pause when freqwalk wants us to
                    Pause(); // Pause will give permission to freqwalk
                }
                else if (!System.IO.File.Exists(walkingRequestFile) && pausedTuning && buttonTunePause.Text.Equals("Walking",StringComparison.InvariantCulture))
                {
                    labelFreqWalk.Text = "FreqWalk paused";
                    buttonTunePause.Enabled = true;
                    pausedTuning = false; // we run when freqwalk is paused
                    Pause();
                }
                else if (!System.IO.File.Exists(walkingRequestFile) && !pausedTuning && buttonTunePause.Text.Equals("Pause",StringComparison.InvariantCulture))
                {
                    labelFreqWalk.Text = "FreqWalk paused";
                    buttonTunePause.Enabled = true;
                }
                else if (System.IO.File.Exists(walkingRequestOKFile))
                {
                    labelFreqWalk.Text = "FreqWalk walking";
                    pausedTuning = true;
                }
                else
                {
                    labelFreqWalk.Text = "FreqWalk unknown";
                }
            }
            //else if (!freqWalkIsRunning && buttonTunePause.Text.Equals("Resume"))
            //{
            //    SetAntennaInUse();
            //    pausedTuning = false; // we run when freqwalk is paused
            //    Pause();
            //}

            //else if (!System.IO.File.Exists(walkingRequestFile) && pausedTuning && buttonTunePause.Text.Equals("Resume"))
            //{
            //    SetAntennaInUse();
            //    pausedTuning = true; // we run when freqwalk is paused
            //    Pause();
            //}

            if (!System.IO.File.Exists(walkingOKFile) && pausedTuning)
            {
                var dummy = System.IO.File.Create(walkingOKFile);
                dummy.Dispose();
            }
            else if (!pausedTuning && System.IO.File.Exists(walkingOKFile))
            {
                System.IO.File.Delete(walkingOKFile);
            }
            /*
            // This allows external control of the pause ability
            var pausedfile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "tunerutilpaused.txt";
            var walkingfile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "tunerutilwalking.txt";
            if (System.IO.File.Exists(walkingfile) && !pausedTuning)
            {
                pausedTuning = true; // we pause when freqwalk is running
                Pause();
            }
            else if (System.IO.File.Exists(pausedfile) && pausedTuning)
            { 
                SetAntennaInUse();
                pausedTuning = false; // we run when freqwalk is paused
                Pause();
            }
            */
            if (tuner1 != null) {
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
                        numericUpDownInductance.Value = Convert.ToDecimal(Inductance)/10;
                        Application.DoEvents();
                        numericUpDownInductance.Enabled = true;
                    }
                    Application.DoEvents();
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

            if (checkBoxTunerEnabled.Checked && comboBoxTunerModel.Text==MFJ928)
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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Invalid command format\nExpected string or hex values e.g. 0x08 0x01");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
            if (buttons.Count == 0)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("No relays open?");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                return false;
            }
            button = buttons[nRelay-1];
            if (flag == 0) button.BackColor = Color.Green;
            else button.BackColor = Color.Red;
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Tuner not enabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                return;
            }
            //if (!relay1.IsOK())
            //{
            //    MyMessageBox("Relay1 is not communicating?");
            //    return;
            //}
            Cursor.Current = Cursors.WaitCursor;
            timerGetFreq.Stop();
            TuneSequence();
            timerGetFreq.Start();
            Cursor.Current = Cursors.Default;
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
            xmlHeader += "Content-length: "+xmlContent.Length+"\n\n";
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            var myForm = new Form { TopMost = true };
            var result = MessageBox.Show(myForm, message, "AmpAutoTunerUtility", MessageBoxButtons.RetryCancel);
            myForm.Dispose();
            return result;
#pragma warning restore CA1303 // Do not pass literals as localized parameters

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
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
            if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                //if (relay1 != null) MyMessageBox("Relay1 != null??");
                //relay1 = new Relay();
                //Debug(DebugEnum.LOG, "Relay1 open\n");
                relay1.Open(comboBoxComRelay1.Text);
                Debug(DebugEnum.LOG, "Relay1 serial #" + relay1.SerialNumber() +"\n");
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Select COM port before enabling Relay2");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
            if (relayButton.BackColor == Color.Green)
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MessageBox.Show("Help");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
        }

        private void Pause()
        {
            //if (toggle) paused = !paused;
            //else paused = true;
            if (pausedTuning)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                buttonTunePause.Text = "Resume";
#pragma warning disable CA1307 // Specify StringComparison
                if (labelFreqWalk.Text.Equals("FreqWalk walking"))
#pragma warning restore CA1307 // Specify StringComparison
                {
                    buttonTunePause.Text = "Walking";
                    buttonTunePause.BackColor = Color.LightBlue;
                    buttonTunePause.ForeColor = Color.White;
                    buttonTunePause.Enabled = false;
                    Application.DoEvents();
                }
                else {
                    buttonTunePause.BackColor = Color.Red;
                    buttonTunePause.ForeColor = Color.White;
                    buttonTunePause.Enabled = true;
                }
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                //buttonTune.Enabled = false;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                //labelSWR.Text = "SWR Paused";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                //buttonTunerStatus.BackColor = Color.Yellow;
                Debug(DebugEnum.LOG, "Tuning paused\n");
                Thread.Sleep(1000);
            }
            else if (!pauseButtonClicked)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                buttonTunePause.Text = "Pause";
                buttonTunePause.BackColor = Color.Green;
                buttonTunePause.ForeColor = Color.White;
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                //buttonTune.Enabled = true;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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

        private void ComboBoxAudioOut_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (comboBoxAudioOut.SelectedIndex >= 0)
            {
                audio.DeviceNumber = comboBoxAudioOut.SelectedIndex;
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Baud not implemented");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            CheckEnable(comboBoxComRelay1, null, checkBoxRelay1Enabled);
        }

        private void ComboBoxComRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckEnable(comboBoxComRelay2, null, checkBoxRelay2Enabled);
        }

        private void ComboBoxBaudRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Baud not implemented");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            CheckEnable(comboBoxComRelay2, comboBoxBaudRelay2, checkBoxRelay2Enabled);
        }

        private void ComboBoxComRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Check Relay3 implementaion");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxBaudRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Check Relay3 implementaion");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxComRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Check Relay4 implementaion");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            CheckEnable(comboBoxComRelay4, comboBoxBaudRelay4, checkBoxRelay4Enabled);
        }

        private void ComboBoxBaudRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            MyMessageBox("Check Relay4 implementaion");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Select COM port before enabling Relay4");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #1 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower1Enabled.Checked = false;
            }
        }

        private void CheckBoxPower2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower2From, textBoxPower2To, textBoxPower2Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #2 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower2Enabled.Checked = false;
            }
        }

        private void CheckBoxPower3Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower3From, textBoxPower3To, textBoxPower3Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #3 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower3Enabled.Checked = false;
            }
        }

        private void CheckBoxPower4Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower4From, textBoxPower4To, textBoxPower4Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #4 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower4Enabled.Checked = false;
            }
        }

        private void CheckBoxPower5Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower5From, textBoxPower5To, textBoxPower5Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #5 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower5Enabled.Checked = false;
            }
        }

        private void CheckBoxPower6Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower6From, textBoxPower6To, textBoxPower6Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #6 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower6Enabled.Checked = false;
            }
        }

        private void CheckBoxPower7Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower7From, textBoxPower7To, textBoxPower7Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #7 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxPower7Enabled.Checked = false;
            }
        }

        private void CheckBoxPower8Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower8From, textBoxPower8To, textBoxPower8Watts))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Power tab entry #8 values are not valid");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
            debugLevel = (DebugEnum)comboBoxDebugLevel.SelectedIndex+1;
            if (debugLevel < 0) debugLevel = DebugEnum.WARN;
            if (tuner1 != null) tuner1.SetDebugLevel(debugLevel);
            while(msg != null) {
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
            tabControl1.TabPages.Remove(tabPageAntenna);
            if (antennaToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageAntenna) < 0)
            {
                tabControl1.TabPages.Add(tabPageAntenna);
            }
        }

        private void CheckBoxPower_CheckedChanged(object sender, EventArgs e)
        {
            TabPage thisPage = tabPagePower;
            tabControl1.TabPages.Remove(thisPage);
            if (powerToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(thisPage) < 0)
            {
                tabControl1.TabPages.Add(thisPage);
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
            tabControl1.TabPages.Remove(thisPage);
            if (tunerToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(thisPage) < 0)
            {
                tabControl1.TabPages.Add(thisPage);
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Select COM port before enabling Relay3");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                checkBoxRelay3Enabled.Checked = false;
            }

        }

        private void Relay1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            relay1ToolStripMenuItem.Checked = !relay1ToolStripMenuItem.Checked;
            tabControl1.TabPages.Remove(tabPageRelay1);
            //if (relay1ToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageRelay1) < 0)
            //{
                tabControl1.TabPages.Add(tabPageRelay1);
            //}
        }

        private void Relay2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            relay2ToolStripMenuItem.Checked = !relay2ToolStripMenuItem.Checked;
            tabControl1.TabPages.Remove(tabPageRelay2);
            if (relay2ToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageRelay2) < 0)
            {
                tabControl1.TabPages.Add(tabPageRelay2);
            }
        }

        private void NumericUpDownCapacitance_MouseUp(object sender, MouseEventArgs e)
        {
        }

        private void ComboBoxDebugLevel_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (tuner1 == null)
                return;
            switch(comboBoxDebugLevel.SelectedIndex)
            {
                case -1: break;
                case 0: tuner1.SetDebugLevel(DebugEnum.ERR);break;
                case 1: tuner1.SetDebugLevel(DebugEnum.WARN); break;
                case 2: tuner1.SetDebugLevel(DebugEnum.TRACE); break;
                case 3: tuner1.SetDebugLevel(DebugEnum.VERBOSE); break;
                default: MyMessageBox("Invalid debug level?  level=" + comboBoxDebugLevel.SelectedIndex);break;
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch
#pragma warning restore CA1031 // Do not catch general exception types
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
            if (ampIsOn) buttonAmp.BackColor = Color.Green;
            else buttonAmp.BackColor = Color.Yellow;
        }

        private bool AmpIsOn()
        {
            return ampIsOn;
        }

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
                RelaySet(relay1, 1, bit);
            }
            ampIsOn = bit==0;
            AmpSetButton();
        }

        private void AmpToggle()
        {
            if (tuner1 == null) return;
            ampIsOn = !ampIsOn;
            tuner1.CMDAmp((byte)(ampIsOn?1:0)); // amp toggle
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
            Debug(DebugEnum.LOG, "ampStatus=" + ampIsOn+"\n");
            AmpSetButton();
        }

        private void AntennaSet(Relay relay, int bits)
        {
            int val = 0;
            if (radioButtonAntennaWire3.Checked)
            {
                MyMessageBox("Wire3 bits=" + bits);
            }
            else if (radioButtonAntennaWire8.Checked)
            {
                MyMessageBox("Wire8 bits=" + bits);
            }
            else
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Unknown relay radio button??");
                return;
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
            relay.Set(1, (byte)val);
        }
        private void CheckBoxAntenna1_CheckedChanged(object sender, EventArgs e)
        {
            Relay relay = null;
            if (comboBoxAntenna1Relay.Text.Equals("Relay1",StringComparison.OrdinalIgnoreCase))
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Unable to open link that was clicked.");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
        }


        private void Button26_Click(object sender, EventArgs e)
        {

        }

        private void TabControl1_VisibleChanged(object sender, EventArgs e)
        {
            tabControl1.Refresh();
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

        private void menuStrip1_ItemClicked(object sender, ToolStripItemClickedEventArgs e)
        {

        }

        private void tabControl1_TabIndexChanged(object sender, EventArgs e)
        {
            tabControl1.TabPages[tabControl1.SelectedIndex].Refresh();
        }

        private void textBoxAntenna1Bits_TextChanged(object sender, EventArgs e)
        {

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

        public static bool operator == ( ComboBoxItem obj1, ComboBoxItem obj2 )
        {
            if (obj1 != null && obj2 != null && obj1.MyGUID.Equals(obj2.MyGUID))
            {
                return true;
            }
            return false;
        }

        public static bool operator != ( ComboBoxItem obj1, ComboBoxItem obj2)
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
