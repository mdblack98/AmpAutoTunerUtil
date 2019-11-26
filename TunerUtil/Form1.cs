using NAudio.Wave;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
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
        //int baudTuner;
        //string comPortRelay = "";
        //string comPortTuner = "";
        TcpClient rigClient;
        NetworkStream rigStream;
        double frequencyHz = 0;
        double lastfrequency = 0;
        double lastfrequencyTunedHz = 0;
        int tolTune = 0;
        Tuner tuner1 = null;
        Relay relay1 = null;
        Relay relay2 = null;
        Relay relay3 = null;
        Relay relay4 = null;
        bool formLoading = true;
        readonly Stopwatch stopWatchTuner = new Stopwatch();
        int freqStableCount = 0; // 
        int freqStableCountNeeded = 2; //need this number of repeat freqs before tuning starts
        private bool autopause;
        bool paused = false;
        private Audio audio;
        bool formClosing = false; // use this to detect shutdown condition
        bool getFreqIsRunning = false;
        private string modeCurrent = "UNK";
        private readonly string MFJ928 = "MFJ-928";
        //private int Capacitance = 0;
        //private int Inductance = 0;
        Tuner.DebugEnum debugLevel = Tuner.DebugEnum.WARN;
        private bool activatedHasExecuted = false;

        public Form1()
        {
            InitializeComponent();
        }

#pragma warning disable IDE0051 // Remove unused private members
        Tuner.DebugEnum DebugLevel()
#pragma warning restore IDE0051 // Remove unused private members
        {
            return (Tuner.DebugEnum)comboBoxDebugLevel.SelectedIndex;
        }

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
                return;
            }
            base.Dispose(disposing);
        }

        delegate void SetTextCallback(string text);

        public void SetTextRig(string text)
        {
            if (checkBoxPause.Checked) return;
            if (this.richTextBoxDebug.InvokeRequired)
            {
                SetTextCallback d = new SetTextCallback(SetTextRig);
                this.Invoke(d, new object[] { text });
            }
            else
            {
                this.Debug(Tuner.DebugEnum.ERR,text);

            }
        }

#pragma warning disable IDE0051 // Remove unused private members
        private bool SetSelectedIndexOf(ComboBox sender, string match)
#pragma warning restore IDE0051 // Remove unused private members
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
                LoadComPorts();
                LoadBaudRates();

                // Tuner Properties
                textBoxFreqTol.Text = Properties.Settings.Default.tolTune;
                checkBoxRig.Checked = Properties.Settings.Default.rigEnabled;
                tolTune = Int32.Parse(textBoxFreqTol.Text, CultureInfo.InvariantCulture);
                checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
                radioButtonVFOA.Checked = Properties.Settings.Default.VFOA;
                radioButtonVFOB.Checked = Properties.Settings.Default.VFOB;
                numericUpDownSensitivity.Value = Convert.ToInt32(Properties.Settings.Default.TunerSensitivity);
                checkBoxPTTEnabled.Checked = Properties.Settings.Default.checkBoxPTTEnabled;
                checkBoxToneEnabled.Checked = Properties.Settings.Default.checkBoxToneEnabled;
                string myAudio = Properties.Settings.Default.comboBoxAudioOut;
                if (Properties.Settings.Default.comboBoxAudioOut.Length > 0)
                {
                    comboBoxAudioOut.SelectedItem = Properties.Settings.Default.comboBoxAudioOut as object;
                }
                checkBoxPowerSDR.Checked = Properties.Settings.Default.powerSDR;

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
                tabControl1.TabPages.Remove(tabPageRelay1);
                tabControl1.TabPages.Remove(tabPageRelay2);
                tabControl1.TabPages.Remove(tabPageRelay3);
                tabControl1.TabPages.Remove(tabPageRelay4);
                //relay1.Close();
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
                Debug(Tuner.DebugEnum.TRACE,"No FTDI relay switches found on USB bus\n");
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
                    Debug(Tuner.DebugEnum.WARN, MyTime() + "Relay 1 not found...disabled\n");
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 1 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    return;
                }
                relay1.Open(comboBoxComRelay1.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay 1 opened\n");
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay1.SerialNumber() + "\n");
                //form2.ProgressBar(2);
            }
            else
            {
                checkBoxRelay1Enabled.Checked = false;
            }
            if (!checkBoxRelay1Enabled.Checked && checkBoxPowerSDR.Enabled==false)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Please set up Relay1 if not using PowerSDR");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
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
                    Debug(Tuner.DebugEnum.WARN,MyTime() + "Relay 2 not found...disabled\n");
                    return;
                }
                relay2.Open(comboBoxComRelay2.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay 2 opened\n");
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay2.SerialNumber() + "\n");
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 3 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    Debug(Tuner.DebugEnum.WARN, MyTime() + "Relay 3 not found...disabled\n");
                    return;
                }
                relay3.Open(comboBoxComRelay3.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay 3 opened\n");
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay3.SerialNumber() + "\n");
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                    MyMessageBox("Relay 4 not found...disabled");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                    Debug(Tuner.DebugEnum.WARN, MyTime() + "Relay 4 not found...disabled\n");
                    return;
                }
                relay4.Open(comboBoxComRelay4.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay 4 opened\n");
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay4.SerialNumber() + "\n");
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
                checkBoxTunerEnabled.Enabled = false;
            }
            // if any relay is implemented that has variable baud rate want to check the Baud too
            // Like the following line
            //if (comboBoxComRelay3.SelectedIndex == -1 || comboBoxBaudRelay3.SelectedIndex == -1)
            if (comboBoxComRelay1.SelectedIndex == -1)
            {
                checkBoxRelay1Enabled.Checked = false;
                checkBoxRelay1Enabled.Enabled = false;
            }
            if (comboBoxComRelay2.SelectedIndex == -1)
            {
                checkBoxRelay2Enabled.Checked = false;
                checkBoxRelay2Enabled.Enabled = false;
            }
            if (comboBoxComRelay3.SelectedIndex == -1)
            {
                checkBoxRelay3Enabled.Checked = false;
                checkBoxRelay3Enabled.Enabled = false;
            }
            if (comboBoxComRelay4.SelectedIndex == -1)
            {
                checkBoxRelay4Enabled.Checked = false;
                checkBoxRelay4Enabled.Enabled = false;
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

            formLoading = false;

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
            // we have to select that tabs for the Application Properties to be loaded
            //tabControl1.SelectTab(tabPagePower);
            //tabControl1.SelectTab(tabPageAntenna);
            //tabControl1.SelectTab(tabPageRelay4);
            //tabControl1.SelectTab(tabPageRelay3);
            //tabControl1.SelectTab(tabPageRelay2);
            //tabControl1.SelectTab(tabPageRelay1);
            //tabControl1.SelectTab(tabPageTuner);
            //tabControl1.SelectTab(tabPageControl);

            Thread.Sleep(100);
            timerGetFreq.Interval = 500;
            timerGetFreq.Enabled = true;
            timerDebug.Interval = 100;
            timerDebug.Enabled = true;
            timerDebug.Start();

            ComboBoxNRelaysSet();
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
            try
            {
                string errorMsg = null;
                TunerClose();
                if (comboBoxTunerModel.Text.Equals("LDG", StringComparison.InvariantCulture))
                {
                    tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                }
                else if (comboBoxTunerModel.Text.Equals(MFJ928, StringComparison.InvariantCulture))
                {
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
                    Debug(Tuner.DebugEnum.LOG, MyTime() + "Tuner opened on " + tuner1.GetSerialPortTuner() + "\n");
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
            foreach (DirectSoundDeviceInfo cap in audio.DeviceInfo)
            {
                ComboBoxItem cbox = new ComboBoxItem
                {
                    Text = cap.Description,
                    MyGUID = cap.Guid
                };
                int curIndex = comboBoxAudioOut.Items.Add(cbox);
            }
        }
        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            timerGetFreq.Stop();
            formClosing = true;
            while (getFreqIsRunning)
            {
                Thread.Sleep(100);
            }
            //if (tuner1 != null) tuner1.Close();
            //if (relay1 != null) relay1.Close();
            //if (relay2 != null) relay2.Close();
            //if (relay3 != null) relay3.Close();
            //if (relay4 != null) relay4.Close();
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

            Properties.Settings.Default.Save();
            Application.Exit();
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
            foreach (string s in System.IO.Ports.SerialPort.GetPortNames())
            {
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

        private void Debug(Tuner.DebugEnum level, string msg)
        {
            if (tuner1 == null) return;
            tuner1.DebugAddMsg(level, msg);
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
        //    Debug(MyTime() + "Tuning\n");
        //}

        private void ComboBoxTunerCom_SelectedIndexChanged(object sender, EventArgs e)
        {
            //comPortTuner = comboBoxComTuner.Text;
            //if (baudTuner > 0) richTextBoxRig.AppendText(MyTime() + "Opening Tuner " + comPortTuner+":"+baudTuner+"\n");
        }

        private void ComboBoxTunerBaud_SelectedIndexChanged(object sender, EventArgs e)
        {
            //baudTuner = Int32.Parse(comboBoxBaudTuner.Text, CultureInfo.InvariantCulture);
            //if (comPortTuner.Length > 0) richTextBoxRig.AppendText(MyTime() + "Opening Tuner " + comPortTuner + ":" + baudTuner+"\n");

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

        private void ConnectFLRig()
        {
            int port = 12345;
            try
            {
                rigClient = new TcpClient("127.0.0.1", port);
                rigStream = rigClient.GetStream();
                rigStream.ReadTimeout = 500;
                if (FLRigWait() == false)
                {
                    return;
                }
                else
                {
                    GetModes();
                    Debug(Tuner.DebugEnum.LOG, MyTime() + "FLRig connected\n");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig error...not responding...\n");
                }
                else
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig unexpected error:\n" + ex.Message + "\n");
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
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
                if (xml.Contains("rig.set") || xml.Contains("rig.cat_string"))
                {
                    int saveTimeout = rigStream.ReadTimeout;
                    // Just read the response and ignore it for now
                    rigStream.ReadTimeout = 500;
                    byte[] data2 = new byte[4096];
                    Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                    String responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                    if (!responseData.Contains("200 OK"))
                    {
                        Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig error: unknown reponse=" + responseData + "\n");
                        MyMessageBox("Unknown response from FLRig\n" + responseData);
                        return false;
                    }
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig error:\n" + ex.Message + "\n");
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

        // returns true when Tuner and FLRig are talking to us
        // if ptt is true then will use ptt and audio tone for tuning -- e.g. MFJ-928 without radio interface
        private bool Tune()
        {
            string xml;
            if (paused)
            {
                Debug(Tuner.DebugEnum.WARN, MyTime() + "Tuner is paused");
                return true;
            }
            var ptt = checkBoxPTTEnabled.Checked;
            //var tone = checkBoxToneEnabled.Checked;
            var powerSDR = checkBoxPowerSDR.Checked;
            // Set power if needed to ensure not overdriving things -- we do it again after we start tuning
            PowerSelect(frequencyHz, modeCurrent, true);
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
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
            }
            Thread.Sleep(1000);
            Debug(Tuner.DebugEnum.LOG, MyTime() + "Tuning to " + frequencyHzTune +"\n");

            buttonTunerStatus.BackColor = Color.LightGray;
            if (checkBoxRelay1Enabled.Checked) RelaySet(relay1, 1, 1);
            Application.DoEvents();
            tuner1.CMDAmp(0); // amp off
            if (ptt && !powerSDR) // we turn on PTT and send the audio tone before starting tune
            {
                audio.MyFrequency = 1000;
                audio.Volume = 1;
                audio.StartStopSineWave();
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>1</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Set ptt on\n");
                tuner1.Tune();
            }
            else if (powerSDR)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune started, amp off\n");
                //PowerSelect(frequencyHz, modeCurrent, true);
                //tuner1.CMD_Amp(0); // amp off
                Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune start tone\n");
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU1;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Thread.Sleep(200);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune start tune\n");
                tuner1.Tune();
                //Thread.Sleep(500);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune stop tone\n");
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Thread.Sleep(500);
                //Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune amp on\n");
                //tuner1.CMD_Amp(1); // amp back on
                //Thread.Sleep(200);
                // We set power again since PowerSDR remembers settings
                //Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune reset power to operational level\n");
            }
            else
            {
                tuner1.Tune();
            }
            Thread.Sleep(500);
            char response = tuner1.ReadResponse();
            // stop audio here
            Application.DoEvents();
            if (ptt && !powerSDR) // we turn off PTT now
            {
                audio.StartStopSineWave();
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>0</i4></value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Debug(Tuner.DebugEnum.LOG, MyTime() + "ptt off\n");
            }
            else if (powerSDR)
            {
                xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                Debug(Tuner.DebugEnum.LOG, MyTime() + "PowerSDR Tune stopped\n");
            }
            Thread.Sleep(500); // give a little time for ptt off and audio to stop
            if (checkBoxRelay1Enabled.Checked)
            {
                RelaySet(relay1, 1, 0);
                relay1.Close();
            }
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
                Debug(Tuner.DebugEnum.ERR, "Unknown response from tuner = '" + response + "'\n");
            }
            //richTextBoxRig.AppendText(MyTime() + "Tuning done SWR="+tuner1.SWR+"\n");
            //richTextBoxRig.AppendText(MyTime() + "Tuning done\n");
            tuner1.CMDAmp(1);
            Thread.Sleep(500);
            PowerSelect(frequencyHz, modeCurrent, false);
            myparam = "<params><param><value><double>" + frequencyHz + "</double></value></param></params";
            xml = FLRigXML("rig.set_vfo" + 'B', myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
            }
            return true;
        }

        private List<string> FLRigGetModes()
        {
            List<string> modes = new List<string>();
            if (rigClient == null) { ConnectFLRig(); }
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
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigGetModes error:\n" + ex.Message + "\n");
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
                        Debug(Tuner.DebugEnum.ERR, MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(Tuner.DebugEnum.ERR, MyTime() + "Error...Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            return modes;
        

    }
    // Wait for FLRig to return valid data
    private bool FLRigWait()
        {
            String xcvr;
            while((xcvr = FLRigGetXcvr()) == null && formClosing == false) {
                Thread.Sleep(500);
            }
            if (formClosing == true)
            {
                //Debug(Tuner.DebugEnum.ERR, MyTime() + "Aborting FLRigWait\n");
                return false;
            }
            Debug(Tuner.DebugEnum.LOG, MyTime() + "Rig is " + xcvr +"\n");
            return true;
        }

        private string FLRigGetXcvr()
        {
            string xcvr = null;

            //if (!checkBoxRig.Checked) return null;
            if (rigClient == null) { ConnectFLRig(); }
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
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigGetXcvr error:\n" + ex.Message + "\n");
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
                        Debug(Tuner.DebugEnum.ERR, MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(Tuner.DebugEnum.ERR, MyTime() + "Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            rigStream.ReadTimeout = timeoutSave;
            return xcvr;
        }

        private string FLRigGetMode()
        {
            string mode = "Unknown";

            if (!checkBoxRig.Checked) return null;
            if (rigClient == null) { ConnectFLRig(); }
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
                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigGetMode error:\n" + ex.Message + "\n");
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
                        Debug(Tuner.DebugEnum.ERR, MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(Tuner.DebugEnum.ERR, MyTime() + "Error...Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            return mode;
        }

        private static string MyTime()
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff",CultureInfo.InvariantCulture) + ": "; 
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
                if (checkBoxAntenna1.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq1From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq1To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna1.Checked = true;
                    buttonAntenna1.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna1.Text;
                }
                else if (checkBoxAntenna2.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq2From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq2To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna2.Checked = true;
                    buttonAntenna2.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna2.Text;
                }
                else if (checkBoxAntenna3.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq3From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq3To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna3.Checked = true;
                    buttonAntenna3.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna3.Text;
                }
                else if (checkBoxAntenna4.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq4From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq4To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna4.Checked = true;
                    buttonAntenna4.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna4.Text;
                }
                else if (checkBoxAntenna5.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq5From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq5To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna5.Checked = true;
                    buttonAntenna5.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna5.Text;
                }
                else if (checkBoxAntenna6.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq6From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq6To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna6.Checked = true;
                    buttonAntenna6.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna6.Text;
                }
                else if (checkBoxAntenna7.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq7From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq7To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna7.Checked = true;
                    buttonAntenna7.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna7.Text;
                }
                else if (checkBoxAntenna8.Checked == true && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq8From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq8To.Text, CultureInfo.InvariantCulture))
                {
                    checkBoxAntenna8.Checked = true;
                    buttonAntenna8.BackColor = Color.Green;
                    labelAntennaSelected.Text = textBoxAntenna8.Text;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                // don't do anything here...just catching the parse errors from blank boxes
            }
        }

        private string FLRigGetActiveVFO()
        {
            string vfo = "A";
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
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                Debug(Tuner.DebugEnum.ERR, MyTime() + "GetActiveVFO error\n");
            }
            return vfo;
        }

        private void FLRigSetActiveVFO(string mode)
        {
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
                Debug(Tuner.DebugEnum.ERR, "FLRigSetActiveVFO error");
                return;
            }
        }

        private bool PowerSet(Int32 value)
        {
            // Seems we can send this a bit too quickly so let's sleep for a short while before doing it
            // Then we'll do it again as it was still failing the 1st time
            //Thread.Sleep(500);
            string xml = FLRigXML("rig.set_power", "<params><param><value><i4>"+value+"</i4></value></param></params");
            if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
            //Thread.Sleep(500);
            if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
            Debug(Tuner.DebugEnum.LOG, MyTime() + "Set power to " + value + "\n");
            labelPower.Text = "Power = " + value;
            return true;
        }

        private bool PowerSelectOp(double frequencyMHz, CheckBox enabled, TextBox from, TextBox to, TextBox powerLevel, ComboBox mode, string modeChk, int passNum)
        {
            // On passNum=0 we check for specific mode matches
            // On passNum>0 we check for "Any" matches
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
                        //if (powerLevel.Text.Length == 0)
                        //{
                        //    MyMessageBox("Power tab Power is empty");
                        //    return false;
                        //}
                        if (mode.SelectedItem==null || mode.SelectedItem.Equals(modeChk) || (passNum > 0 && mode.SelectedItem.Equals("Any"))) 
                        {
                            if (powerLevel.Text.Length > 0)
                            {
                                Debug(Tuner.DebugEnum.TRACE, MyTime() + "Set Power=" + powerLevel.Text + "\n");
                                PowerSet(Convert.ToInt32(powerLevel.Text, CultureInfo.InvariantCulture));
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
            labelPower.Text = "Power not set";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            return false;
        }

        private void PowerSelect(double frequencyHz, string modeChk, bool tuning = false)
        {
            double frequencyMHz = frequencyHz / 1e6;
            for (int passNum = 0; passNum < 2; ++passNum)
            {
                if (PowerSelectOp(frequencyMHz, checkBoxPower1Enabled, textBoxPower1From, textBoxPower1To, tuning ? textBoxTune1Power : textBoxPower1Watts, comboBoxPower1Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower2Enabled, textBoxPower2From, textBoxPower2To, tuning ? textBoxTune2Power : textBoxPower2Watts, comboBoxPower2Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower3Enabled, textBoxPower3From, textBoxPower3To, tuning ? textBoxTune3Power : textBoxPower3Watts, comboBoxPower3Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower4Enabled, textBoxPower4From, textBoxPower4To, tuning ? textBoxTune4Power : textBoxPower4Watts, comboBoxPower4Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower5Enabled, textBoxPower5From, textBoxPower5To, tuning ? textBoxTune5Power : textBoxPower5Watts, comboBoxPower5Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower6Enabled, textBoxPower6From, textBoxPower6To, tuning ? textBoxTune6Power : textBoxPower6Watts, comboBoxPower6Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower7Enabled, textBoxPower7From, textBoxPower7To, tuning ? textBoxTune7Power : textBoxPower7Watts, comboBoxPower7Mode, modeChk, passNum))
                    return;
                if (PowerSelectOp(frequencyMHz, checkBoxPower8Enabled, textBoxPower8From, textBoxPower8To, tuning ? textBoxTune8Power : textBoxPower8Watts, comboBoxPower8Mode, modeChk, passNum))
                    return;
            }
        }

        private void FLRigGetFreq()
        {
            getFreqIsRunning = true;
            if (!checkBoxRig.Checked)
            {
                getFreqIsRunning = false;
                return;
            }
            if (rigClient == null)
            {
                ConnectFLRig();
                if (rigClient == null)
                {
                    getFreqIsRunning = false;
                    return;
                }
            }
            string currVFO = FLRigGetActiveVFO();
            if (currVFO.Equals("B", StringComparison.InvariantCulture))
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                MyMessageBox("Auto tuning paused...click OK to continue");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                FLRigSetActiveVFO("A");
            }
            string vfo = "B";
            if (radioButtonVFOA.Checked) vfo = "A";
            char cvfo = 'A';
            string mode = FLRigGetMode();
            if (radioButtonVFOA.Checked) cvfo = 'B';
            string xml = FLRigXML("rig.get_vfo" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "Error...Did FLRig shut down?\n");
                }
                else
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig unexpected error:\n" + ex.Message + "\n");
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
                        string modeOld = modeCurrent;
                        modeCurrent = FLRigGetMode(); // get our current mode now
                        labelFreq.Text = (frequencyHz / 1000).ToString(CultureInfo.InvariantCulture) + "kHz" + " " + modeCurrent;
                        if (comboBoxPower1Mode.SelectedItem != null && !modeCurrent.Equals(modeOld, StringComparison.InvariantCulture))
                            PowerSelect(frequencyHz, modeCurrent);
                        if (lastfrequency == 0) lastfrequency = lastfrequencyTunedHz = frequencyHz;
                        if (lastfrequency == frequencyHz && freqStableCount < freqStableCountNeeded)
                        {
                            ++freqStableCount;
                            stopWatchTuner.Restart();
                        }
                        if (freqStableCount >= freqStableCountNeeded && Math.Abs(frequencyHz - lastfrequencyTunedHz) > tolTune)
                        {
                            if (stopWatchTuner.IsRunning && stopWatchTuner.ElapsedMilliseconds < 1 * 1000)
                            {
                                stopWatchTuner.Stop();
                                //MyMessageBox("Rapid frequency changes...click OK when ready to tune");
                                Debug(Tuner.DebugEnum.ERR, MyTime() + "Rapid frequency changes\n");
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

                            string myparam = "<params><param><value>" + mode + "</value></param></params>";
                            xml = FLRigXML("rig.set_modeB", myparam);
                            if (FLRigSend(xml) == false)
                            { // Abort if FLRig is giving an error
                                Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRig Tune got an error??\n");
                            }
                            Debug(Tuner.DebugEnum.LOG, MyTime() + "Rig mode VFOB set to "+mode+"\n");
                            stopWatchTuner.Restart();
                            if (checkBoxTunerEnabled.Checked && !paused)
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
                                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
                                }
                                Thread.Sleep(200);
                                lastfrequencyTunedHz = frequencyHz;
                                //PowerSelect(frequencyHz, modeCurrent);
                                Tune();
                                // Reset VFOB to same freq as VFOA
                                myparam = "<params><param><value><double>" + frequencyHz + "</double></value></param></params";
                                xml = FLRigXML("rig.set_vfo" + vfoOther, myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
                                }
                            }
                            else if (!paused)
                            {
                                Debug(Tuner.DebugEnum.ERR, MyTime() + "Tuner not enabled\n");
                                Debug(Tuner.DebugEnum.ERR, MyTime() + "Simulate tuning to " + frequencyHz + "\n");
                                char vfoOther = 'A';
                                if (radioButtonVFOA.Checked) vfoOther = 'B';
                                myparam = "<params><param><value><double>"+frequencyHz+"</double></value></param></params";
                                xml = FLRigXML("rig.set_vfo" + vfoOther, myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
                                }
                                // Set VFO mode to match primary VFO
                                myparam = "<params><param><value>" + modeCurrent + "</value></param></params>";
                                xml = FLRigXML("rig.set_modeB", myparam);
                                if (FLRigSend(xml) == false)
                                { // Abort if FLRig is giving an error
                                    Debug(Tuner.DebugEnum.ERR, MyTime() + "FLRigSend got an error??\n");
                                }

                                lastfrequencyTunedHz = lastfrequency = frequencyHz;
                                relay1.Set(1, 1);
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                                MyMessageBox("Click OK to continue");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                        labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                        Debug(Tuner.DebugEnum.ERR, MyTime() + responseData + "\n");
                        tabControl1.SelectedTab = tabPageDebug;
                    }
                }
#pragma warning disable CA1031 // Do not catch general exception types
                catch (Exception)
#pragma warning restore CA1031 // Do not catch general exception types
                {
                    Debug(Tuner.DebugEnum.ERR, MyTime() + "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyHz = 0;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
            {
#pragma warning restore CA1031 // Do not catch general exception types
                Debug(Tuner.DebugEnum.ERR, MyTime() + "Error...Rig not responding\n" + ex.Message + "\n");
                frequencyHz = 0;
            }
            // set VFOB to match VFOA
            //xml = FLRigXML("rig.set_vfo" + cvfo, "<params><param><value><double> " + frequencyHz + " </double></value></param></params");
            //if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
            getFreqIsRunning = false;
        }

        private void TimerGetFreq_Tick(object sender, EventArgs e)
        {
            if (tuner1 == null || tuner1.GetComPort() == null) return;
            timerGetFreq.Stop();

            // This allows external control of the pause ability
            var tempfile = Environment.GetEnvironmentVariable("TEMP") + "\\" + "tunerutilpause.txt";
            if (System.IO.File.Exists(tempfile))
            {
                var pausecmd = System.IO.File.ReadAllText(tempfile);
                if (pausecmd.ToUpperInvariant().Contains("PAUSE") && !paused)
                {
                    paused = true; // we pause when freqwalk is running
                    Pause();
                }
                if (!pausecmd.ToUpperInvariant().Contains("PAUSE") && paused)
                {
                    FLRigGetFreq();
                    SetAntennaInUse();
                    paused = false; // we run when freqalk is paused
                    Pause();
                }
            }
            if (tuner1 != null) {
                string SWR = tuner1.GetSWR();
                if (tuner1.GetModel().Equals(MFJ928, StringComparison.InvariantCulture))
                {
                    int Inductance = tuner1.GetInductance();
                    int Capacitance = tuner1.GetCapacitance();
                    labelSWR.Text = SWR;
                    numericUpDownCapacitance.Value = Capacitance;
                    numericUpDownInductance.Value = Inductance;
                }
            }
            string FwdPwr = tuner1.GetPower();
            labelPower.Text = FwdPwr;

            if (checkBoxTunerEnabled.Checked && comboBoxTunerModel.Text==MFJ928)
            {
                numericUpDownCapacitance.Visible = true;
                numericUpDownInductance.Visible = true;
            }
            else
            {
                numericUpDownCapacitance.Visible = false;
                numericUpDownInductance.Visible = false;
            }
            if (checkBoxRig.Checked)
            {
                FLRigGetFreq();
                SetAntennaInUse();
            }
            else
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelFreq.Text = "?";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }

            // We'll update our Tuner Options in case it changes
            groupBoxOptions.Visible = comboBoxTunerModel.SelectedItem.Equals(MFJ928);


            timerGetFreq.Start();
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (checkBoxRig.Checked)
            {
                richTextBoxDebug.Clear();
                ConnectFLRig();
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
            for (int i = 1; i <= 8; ++i)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + i + " On\n");
                //relay1.Set(i, 1);
                RelaySet(relay1, i, 1);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
            for (int i = 1; i <= 8; ++i)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + i + " Off\n");
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
            if (relay.errMsg != null)
            {
                MyMessageBox(relay.errMsg);
                return false;
            }
            byte status = relay.Status();
            Debug(Tuner.DebugEnum.LOG, MyTime() + "status=0x" + status.ToString("X", CultureInfo.InvariantCulture) +"\n");
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
            Tune();
            Cursor.Current = Cursors.Default;
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
                    //    richTextBoxRig.AppendText(MyTime() + " tuner open failed\n");
                    //    checkBoxTunerEnabled.Checked = false;
                    //    MyMessageBox("Tuner open failed");
                    //    return;
                    //}
                    //richTextBoxRig.AppendText(MyTime() + "Tuner opened\n");
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
                Debug(Tuner.DebugEnum.LOG, MyTime() + "tuner closed\n");
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
                relay1 = new Relay();
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay1 open\n");
                relay1.Open(comboBoxComRelay1.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay1.SerialNumber() +"\n");
            }
            else if (!checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay1 closed\n");
                //checkBoxRelay1Enabled.Enabled = false;
                relay1.Close();
            }
        }

        private void CheckBoxRelay2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading)
                return;
            Application.DoEvents();
            checkBoxRelay2Enabled.Enabled = true;
            if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                relay2 = new Relay();
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay2 open\n");
                relay2.Open(comboBoxComRelay2.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay2.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay2 closed\n");
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
            if (relayButton.BackColor == Color.Silver)
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

        private void NumericUpDownSensitivity_ValueChanged(object sender, EventArgs e)
        {
            freqStableCountNeeded = (int)numericUpDownSensitivity.Value;
        }

        private void LinkLabel1_LinkClicked(object sender, System.Windows.Forms.LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                //Call the Process.Start method to open the default browser   
                //with a URL:  
                System.Diagnostics.Process.Start("https://www.amazon.com/gp/product/B009A5246E/ref=ppx_yo_dt_b_asin_title_o03_s00?ie=UTF8&psc=1");
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

        private void LinkLabel2_LinkClicked(object sender, LinkLabelLinkClickedEventArgs e)
        {
            try
            {
                linkLabel1.LinkVisited = true;
                //Call the Process.Start method to open the default browser   
                //with a URL:  
                System.Diagnostics.Process.Start("https://www.amazon.com/SainSmart-Eight-Channel-Relay-Automation/dp/B0093Y89DE/ref=pd_sim_328_5/142-3490108-0951025?_encoding=UTF8&pd_rd_i=B0093Y89DE&pd_rd_r=5fec5905-3a9a-11e9-8c37-9bb5683a7901&pd_rd_w=JVLew&pd_rd_wg=ZMtfu&pf_rd_p=90485860-83e9-4fd9-b838-b28a9b7fda30&pf_rd_r=KYZW2V3QB1MS6TFW8Q1Q&psc=1&refRID=KYZW2V3QB1MS6TFW8Q1Q");
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                MyMessageBox("Unable to open link that was clicked\n"+ex.Message);
            }
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

        private bool FrequencyChanged()
        {
            return Math.Abs(lastfrequency - lastfrequencyTunedHz) > Convert.ToDouble(textBoxFreqTol.Text,CultureInfo.InvariantCulture);
        }

        private void Pause()
        {
            //if (toggle) paused = !paused;
            //else paused = true;
            if (paused)
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                buttonTunePause.Text = "Resume";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                buttonTune.Enabled = false;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR Paused";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                buttonTunerStatus.BackColor = Color.Yellow;
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Tuning paused\n");
            }
            else
            {
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                buttonTunePause.Text = "Pause";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                buttonTune.Enabled = true;
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                labelSWR.Text = "SWR";
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                FLRigGetFreq();
                Tune();
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Tuning resumed\n");
            }
        }
        private void ButtonTunePause_Click(object sender, EventArgs e)
        {
            paused = !paused;
            Pause();
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
                relay4 = new Relay();
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay4 open\n");
                relay4.Open(comboBoxComRelay4.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay4.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex >= 0)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay4 closed\n");
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
            if (tuner1 == null) return;
            if (checkBoxPause.Checked) return;
            Tuner.DebugMsg msg = tuner1.DebugGetMsg();
            debugLevel = (Tuner.DebugEnum)comboBoxDebugLevel.SelectedIndex;
            if (tuner1 != null) tuner1.SetDebugLevel(debugLevel);
            while(msg != null) {
                if (msg.Level <= debugLevel || msg.Level == Tuner.DebugEnum.LOG)
                {
                    richTextBoxDebug.AppendText(msg.Text);
                    richTextBoxDebug.SelectionStart = richTextBoxDebug.Text.Length;
                    richTextBoxDebug.ScrollToCaret();
                    while (richTextBoxDebug.Lines.Length > 1000)
                    {
                        richTextBoxDebug.Select(0, richTextBoxDebug.GetFirstCharIndexFromLine(1));
                        richTextBoxDebug.SelectedText = "";
                    }
                }
                //Debug(msg.Level, msg.Text);
                msg = tuner1.DebugGetMsg();
            }
        }

        private void ComboBoxNRelaysSet()
        {
            return;
            //int nRelays = Int32.Parse(comboBoxNRelays.Text, CultureInfo.InvariantCulture);
            tabControl1.TabPages.Remove(tabPageRelay1);
            tabControl1.TabPages.Remove(tabPageRelay2);
            tabControl1.TabPages.Remove(tabPageRelay3);
            tabControl1.TabPages.Remove(tabPageRelay4);
            if (relay1ToolStripMenuItem.Checked)
            {
                tabControl1.TabPages.Add(tabPageRelay1);
            }
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

        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            Application.Exit();
        }

        private void NumericUpDownCapacitance_ValueChanged(object sender, EventArgs e)
        {
            if (formLoading)
            {
                //Capacitance = (int)numericUpDownCapacitance.Value;
                return;
            }
            // Values being read from tuner are in pF 0x0000 to 0x3926 (0 to 3926 pF)
            // But we have to set values in 0-255 range for Z=1, and 0-
            //int value = (int)numericUpDownCapacitance.Value*(255/3926);
            //tuner1.SetCapacitance(1);
        }

        private void NumericUpDownInductance_ValueChanged(object sender, EventArgs e)
        {
            if (formLoading)
            {
                //Inductance = (int)numericUpDownInductance.Value;
                return;
            }
            // Values being read from tuner are in uH 0x0000 to 0x2428 (0.00 to 24.28 uH)
            // But we have to set values in 0-255 range
            // So we divide our value by 
            //int flag = 1;
            //if (numericUpDownInductance.Value < Inductance) flag = -1;
            //tuner1.SetInductance(flag);
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
                relay3 = new Relay();
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay3 open\n");
                relay3.Open(comboBoxComRelay3.Text);
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Serial number " + relay3.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex >= 0)
            {
                Debug(Tuner.DebugEnum.LOG, MyTime() + "Relay3 closed\n");
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
            if (relay1ToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageRelay1) < 0)
            {
                tabControl1.TabPages.Add(tabPageRelay1);
            }
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
                case 0: tuner1.SetDebugLevel(Tuner.DebugEnum.ERR);break;
                case 1: tuner1.SetDebugLevel(Tuner.DebugEnum.WARN); break;
                case 2: tuner1.SetDebugLevel(Tuner.DebugEnum.LOG);break;
                case 3: tuner1.SetDebugLevel(Tuner.DebugEnum.TRACE); break;
                case 4: tuner1.SetDebugLevel(Tuner.DebugEnum.VERBOSE); break;
                default: MyMessageBox("Invalid debug level?  level=" + comboBoxDebugLevel.SelectedIndex);break;
            }
        }

        private void HelpToolStripMenuItem_Click(object sender, EventArgs e)
        {
            HelpForm helpForm = new HelpForm();
            helpForm.Show();
            helpForm.Dispose();
        }

        private void Button27_Click(object sender, EventArgs e)
        {
            bool _ampOn = tuner1.GetAmpStatus();
            if (_ampOn)
            {
                tuner1.SetAmp(false);
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
            stopWatchTuner.Start(); // Assume we're already tuned
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
