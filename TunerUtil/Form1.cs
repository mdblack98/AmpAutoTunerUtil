﻿//using NAudio.Wave;
using AmpAutoTunerUtility.Properties;
using Microsoft.VisualBasic;
using System;
using System.CodeDom.Compiler;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data.SqlTypes;
using System.Diagnostics;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Design;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Management;
using System.Media;
using System.Net;
using System.Net.Sockets;
using System.Reflection;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Windows.Media;
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
#nullable enable
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
        static TcpClient? rigClient;
        //static NetworkStream rigStream;
        public static Rig? myRig;
        static public double frequencyHz = 0;
        double frequencyLast = 0;
        double frequencyLastTunedHz = 0;
        int tolTune = 0;
        private Tuner? tuner1 = null;
        Relay? relay1 = null;
        Relay? relay2 = null;
        Relay? relay3 = null;
        Relay? relay4 = null;
        bool formLoading = true;
        readonly Stopwatch stopWatchTuner = new();
        int freqStableCount = 0; // 
        readonly int freqStableCountNeeded = 2; //need this number of repeat freqs before tuning starts
        bool pausedTuning = false;
        public bool freqWalkIsRunning;
        private bool pauseButtonClicked;
        private Audio? audio;
        bool formClosing = false; // use this to detect shutdown condition
        bool getFreqIsRunning = false;
        private string modeCurrent = "UNK";
        private readonly string MFJ928 = "MFJ-928";
        private readonly string EXPERTLINEARS = "SPE";
        //private int Capacitance = 0;
        //private int Inductance = 0;
        DebugEnum debugLevel = DebugEnum.WARN;
        private bool activatedHasExecuted = false;
        private bool tuneIsRunning;
        private bool ampIsOn;
        private bool _disposed;
        private int lastAntennaNumber = -1;
        readonly object expertLock = new();
        //private int lastRelayUsed = -1;
        //bool tuning;
        private double f1From,f2From,f3From,f4From,f5From,f6From,f7From,f8From;
        private double f1To,f2To,f3To,f4To,f5To,f6To,f7To,f8To;
        private int f1Ant, f2Ant, f3Ant, f4Ant, f5Ant, f6Ant, f7Ant, f8Ant;


#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public Form1()
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            InitializeComponent();
            Settings.Default.Upgrade();
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
                //if (rigStream != null) { rigStream.Dispose(); rigStream = null; }
                if (rigClient != null) { rigClient.Dispose(); rigClient = null; }
                if (tuner1 != null) { tuner1.Close(); tuner1.Dispose(); tuner1 = null; }
                if (audio != null) { audio.Dispose(); audio = null; }
            }
            Thread.Sleep(1000);
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
            string path = System.IO.Path.GetTempPath() + "AmpAutoTunerUtility.log";
            using FileStream fs = File.Create(path);
            Byte[] info = new System.Text.UTF8Encoding(true).GetBytes(e.Exception.Message + "\n" + e.Exception.StackTrace);
            fs.Write(info, 0, info.Length);
        }

        private void CurrentDomain_UnhandledException(object sender, UnhandledExceptionEventArgs e)
        {
            // Log the exception, display it, etc
            //MessageBox.Show((e.ExceptionObject as Exception).Message + "\n" + (e.ExceptionObject as Exception).StackTrace);
            string path = System.IO.Path.GetTempPath() + "AmpAutoTunerUtility.log";
            using FileStream fs = File.Create(path);
            if (e != null && e.ExceptionObject != null)
            {
                Exception? ex = (e.ExceptionObject as Exception);
                if (ex != null && ex.Message != null && ex.StackTrace != null)
                {
                    Byte[] info = new System.Text.UTF8Encoding(true).GetBytes(ex.Message + "\n" + ex.StackTrace);
                    fs.Write(info, 0, info.Length);
                }
            }
        }

        private void AntennaAddRelay(string name)
        {
            comboBoxAntenna1Controller.Items.Add(name);
            comboBoxAntenna2Controller.Items.Add(name);
            comboBoxAntenna3Controller.Items.Add(name);
            comboBoxAntenna4Controller.Items.Add(name);
            comboBoxAntenna5Controller.Items.Add(name);
            comboBoxAntenna6Controller.Items.Add(name);
            comboBoxAntenna7Controller.Items.Add(name);
            comboBoxAntenna8Controller.Items.Add(name);
        }

        private void RelaySetButtons(Button button, byte status)
        {
            if (button == button1_1)
            {
                button1_1.BackColor = (status & 0x01) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_2.BackColor = (status & 0x02) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_3.BackColor = (status & 0x04) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_4.BackColor = (status & 0x08) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_5.BackColor = (status & 0x10) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_6.BackColor = (status & 0x20) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_7.BackColor = (status & 0x40) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button1_8.BackColor = (status & 0x80) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
            }
            else if (button == button2_1)
            {
                button2_1.BackColor = (status & 0x01) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_2.BackColor = (status & 0x02) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_3.BackColor = (status & 0x04) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_4.BackColor = (status & 0x08) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_5.BackColor = (status & 0x10) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_6.BackColor = (status & 0x20) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_7.BackColor = (status & 0x40) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button2_8.BackColor = (status & 0x80) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
            }
            else if (button == button3_1)
            {
                button3_1.BackColor = (status & 0x01) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_2.BackColor = (status & 0x02) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_3.BackColor = (status & 0x04) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_4.BackColor = (status & 0x08) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_5.BackColor = (status & 0x10) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_6.BackColor = (status & 0x20) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_7.BackColor = (status & 0x40) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button3_8.BackColor = (status & 0x80) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
            }
            else if (button == button4_1)
            {
                button4_1.BackColor = (status & 0x01) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_2.BackColor = (status & 0x02) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_3.BackColor = (status & 0x04) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_4.BackColor = (status & 0x08) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_5.BackColor = (status & 0x10) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_6.BackColor = (status & 0x20) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
                button4_8.BackColor = (status & 0x80) == 0 ? System.Drawing.Color.Gray : System.Drawing.Color.Green;
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

                comboBoxDebugLevel.SelectedIndex = comboBoxDebugLevel.FindStringExact(Properties.Settings.Default.DebugLevel);
                // Not currently used
                tabPage.TabPages.Remove(tabPageExpertLinears);

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
                numericUpDownFLRigBeforeWalk.Value = Properties.Settings.Default.FLRigStartCmd;
                numericUpDownFLRigAfterWalk.Value = Properties.Settings.Default.FLRigStopCmd;

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
                comboBoxFreqWalkAntenna.SelectedIndex = comboBoxFreqWalkAntenna.FindStringExact(Properties.Settings.Default.FreqWalkAntenna);
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
                textBoxPower1MaxWatts.Text = Properties.Settings.Default.Power1MaxWatts;
                textBoxPower2MaxWatts.Text = Properties.Settings.Default.Power2MaxWatts;
                textBoxPower3MaxWatts.Text = Properties.Settings.Default.Power3MaxWatts;
                textBoxPower4MaxWatts.Text = Properties.Settings.Default.Power4MaxWatts;
                textBoxPower5MaxWatts.Text = Properties.Settings.Default.Power5MaxWatts;
                textBoxPower6MaxWatts.Text = Properties.Settings.Default.Power6MaxWatts;
                textBoxPower7MaxWatts.Text = Properties.Settings.Default.Power7MaxWatts;
                textBoxPower8MaxWatts.Text = Properties.Settings.Default.Power8MaxWatts;
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

                checkBoxTunePwr.Checked = Properties.Settings.Default.checkBoxTunePwr;

                antennaToolStripMenuItem.Checked = Properties.Settings.Default.ViewAntenna;
                relay1ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay1;
                relay2ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay2;
                relay3ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay3;
                relay4ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay4;
                powerToolStripMenuItem.Checked = Properties.Settings.Default.ViewPower;
                debugToolStripMenuItem.Checked = Properties.Settings.Default.ViewDebug;
                tunerToolStripMenuItem.Checked = Properties.Settings.Default.ViewTuner;
                expertLinearsToolStripMenuItem.Checked = Properties.Settings.Default.ViewExpertLinears;
                if (!expertLinearsToolStripMenuItem.Checked) tabPage.TabPages.Remove(tabPageExpertLinears);
                if (!antennaToolStripMenuItem.Checked)
                {
                    tabPage.TabPages.Remove(tabPageAntenna);
                    buttonAntennaPick1.Visible = false;
                    buttonAntennaPick2.Visible = false;
                    buttonAntennaPick3.Visible = false;
                    buttonAntennaPick4.Visible = false;
                    buttonAntennaPick5.Visible = false;
                    buttonAntennaPick6.Visible = false;
                    buttonAntennaPick7.Visible = false;
                    buttonAntennaPick8.Visible = false;
                }
                //textBoxFrequencyWalkList.Text = Properties.Settings.Default.FrequencyWalkList;

                //relay1 = new Relay(comboBoxComRelay1.SelectedText, comboBoxBaudRelay1.SelectedText);
                relay1 = new Relay();
                List<string> comPorts = [];
                if (relay1.DevCount() == 0 || !relay1ToolStripMenuItem.Checked)
                {
                    if (!relay1ToolStripMenuItem.Checked) tabPage.TabPages.Remove(tabPageRelay1);
                    if (!relay2ToolStripMenuItem.Checked) tabPage.TabPages.Remove(tabPageRelay2);
                    if (!relay3ToolStripMenuItem.Checked) tabPage.TabPages.Remove(tabPageRelay3);
                    if (!relay4ToolStripMenuItem.Checked) tabPage.TabPages.Remove(tabPageRelay4);
                    //relay1.Close();
                    relay1 = null;
                    AntennaAddRelay(EXPERTLINEARS);
                }
                else
                {
                    comPorts = relay1.ComList();
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
                    comboBoxComRelay1.Items.Clear();
                    comboBoxComRelay2.Items.Clear();
                    comboBoxComRelay3.Items.Clear();
                    comboBoxComRelay4.Items.Clear();
                    comboBoxComRelay1.Text = "";
                    comboBoxComRelay2.Text = "";
                    comboBoxComRelay3.Text = "";
                    comboBoxComRelay4.Text = "";
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
                //if (relay1 != null && relay1.DevCount() > 0)
                {
                    comboBoxComRelay1.SelectedIndex = comboBoxComRelay1.FindStringExact(Properties.Settings.Default.Relay1Com);
                    comboBoxComRelay2.SelectedIndex = comboBoxComRelay2.FindStringExact(Properties.Settings.Default.Relay2Com);
                    comboBoxComRelay3.SelectedIndex = comboBoxComRelay3.FindStringExact(Properties.Settings.Default.Relay3Com);
                    comboBoxComRelay4.SelectedIndex = comboBoxComRelay4.FindStringExact(Properties.Settings.Default.Relay4Com);
                    comboBoxAntenna1Controller.SelectedIndex = comboBoxAntenna1Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna1Relay);
                    comboBoxAntenna2Controller.SelectedIndex = comboBoxAntenna2Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna2Relay);
                    comboBoxAntenna3Controller.SelectedIndex = comboBoxAntenna3Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna3Relay);
                    comboBoxAntenna4Controller.SelectedIndex = comboBoxAntenna4Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna4Relay);
                    comboBoxAntenna5Controller.SelectedIndex = comboBoxAntenna5Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna5Relay);
                    comboBoxAntenna6Controller.SelectedIndex = comboBoxAntenna6Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna6Relay);
                    comboBoxAntenna7Controller.SelectedIndex = comboBoxAntenna7Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna7Relay);
                    comboBoxAntenna8Controller.SelectedIndex = comboBoxAntenna8Controller.FindStringExact(Properties.Settings.Default.comboBoxAntenna8Relay);

                    // Relay Properties
                    checkBoxRelay1Enabled.Checked = Properties.Settings.Default.Relay1Enabled;
                    checkBoxRelay2Enabled.Checked = Properties.Settings.Default.Relay2Enabled;
                    checkBoxRelay3Enabled.Checked = Properties.Settings.Default.Relay3Enabled;
                    checkBoxRelay4Enabled.Checked = Properties.Settings.Default.Relay4Enabled;

                    // Menu items
                    antennaToolStripMenuItem.Checked = Properties.Settings.Default.ViewAntenna;
                    relay1ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay1;
                    relay2ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay2;
                    relay3ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay3;
                    relay4ToolStripMenuItem.Checked = Properties.Settings.Default.ViewRelay4;
                    powerToolStripMenuItem.Checked = Properties.Settings.Default.ViewPower;
                    debugToolStripMenuItem.Checked = Properties.Settings.Default.ViewDebug;
                    tunerToolStripMenuItem.Checked = Properties.Settings.Default.ViewTuner;
                }
                if (relay1 == null)
                    checkBoxRelay1Enabled.Checked = false;
                if (checkBoxRelay1Enabled.Checked && comboBoxComRelay1.Text.Length > 0)
                {
                    if (relay1 == null)
                    {
                        checkBoxRelay1Enabled.Checked = false;
                        Debug(DebugEnum.WARN, "Relay 1 not found...disabled\n");
                        ShowCustomMessageBox("Relay 1 not found...disabled");
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
                    //checkBoxRelay1Enabled.Checked = true;
                }
                if (!checkBoxRelay1Enabled.Checked && checkBoxPowerSDR.Enabled == false)
                {
                    //ShowCustomMessageBox("Please set up Relay1 if not using PowerSDR");
                }
                if (relay1 == null) checkBoxRelay1Enabled.Checked = false;
                else
                {
                    if (checkBoxRelay1Enabled.Checked && relay1.SerialNumber().Length == 0)
                    {
                        ShowCustomMessageBox("No serial#?  Relay1 is not responding!");
                    }
                }
                if (relay2 == null) checkBoxRelay2Enabled.Checked = false;
                if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.Text.Length > 0)
                {
                    if (relay2 == null)
                    {
                        checkBoxRelay2Enabled.Checked = false;
                        ShowCustomMessageBox("Relay 2 not found...disabled");
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
                    //checkBoxRelay2Enabled.Checked = true;
                }

                if (relay3 == null) checkBoxRelay3Enabled.Checked = false;
                if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.Text.Length > 0)
                {
                    if (relay3 == null)
                    {
                        checkBoxRelay3Enabled.Checked = false;
                        ShowCustomMessageBox("Relay 3 not found...disabled");
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
                    //checkBoxRelay3Enabled.Checked = true;
                }

                if (relay4 == null) checkBoxRelay4Enabled.Checked = false;
                if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.Text.Length > 0)
                {
                    if (relay4 == null)
                    {
                        checkBoxRelay4Enabled.Checked = false;
                        ShowCustomMessageBox("Relay 4 not found...disabled");
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
                    //checkBoxRelay4Enabled.Checked = false;
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
                //    ShowCustomMessageBox("Error opening audio device\n" + audio.errMsg);
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

                Properties.Settings.Default.Save();


                clockIsZulu = Properties.Settings.Default.ClockIsZulu;
                int index = 0;

                checkedListBoxWalk1.Enabled = checkBoxWalk1.Checked = Properties.Settings.Default.checkBoxWalk1;
                checkedListBoxWalk2.Enabled = checkBoxWalk2.Checked = Properties.Settings.Default.checkBoxWalk2;
                checkedListBoxWalk3.Enabled = checkBoxWalk3.Checked = Properties.Settings.Default.checkBoxWalk3;
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk1List))
                {
                    index = 0;
                    Properties.Settings.Default.FrequenciesToWalk1List.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            this.checkedListBoxWalk1.Items[index++] = item;
                        });
                }
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk2List))
                {
                    index = 0;
                    Properties.Settings.Default.FrequenciesToWalk2List.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            this.checkedListBoxWalk2.Items[index++] = item;
                        });
                }
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk3List))
                {
                    index = 0;
                    Properties.Settings.Default.FrequenciesToWalk3List.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            this.checkedListBoxWalk3.Items[index++] = item;
                        });
                }

                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk1))
                {
                    Properties.Settings.Default.FrequenciesToWalk1.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            index = this.checkedListBoxWalk1.Items.IndexOf(item);
                            if (index >= 0)
                                this.checkedListBoxWalk1.SetItemChecked(index, true);
                        });
                }
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk2List))
                {
                    Properties.Settings.Default.FrequenciesToWalk2.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            index = this.checkedListBoxWalk2.Items.IndexOf(item);
                            if (index >= 0)
                                this.checkedListBoxWalk2.SetItemChecked(index, true);
                        });
                }
                var stest = Properties.Settings.Default.FrequenciesToWalk3List;
                if (!string.IsNullOrEmpty(Properties.Settings.Default.FrequenciesToWalk3List))
                {
                    stest = Properties.Settings.Default.FrequenciesToWalk3;
                    Properties.Settings.Default.FrequenciesToWalk3.Split(',')
                        .ToList()
                        .ForEach(item =>
                        {
                            index = this.checkedListBoxWalk3.Items.IndexOf(item);
                            if (index >= 0)
                                this.checkedListBoxWalk3.SetItemChecked(index, true);
                        });
                }
                checkedListBoxWalk1.Enabled = Properties.Settings.Default.FrequenciesToWalk1Enabled;
                checkedListBoxWalk2.Enabled = Properties.Settings.Default.FrequenciesToWalk2Enabled;
                checkedListBoxWalk3.Enabled = Properties.Settings.Default.FrequenciesToWalk3Enabled;

                labelFreqWalk1.Text = Properties.Settings.Default.labelFreqWalk1;
                labelFreqWalk2.Text = Properties.Settings.Default.labelFreqWalk2;
                labelFreqWalk3.Text = Properties.Settings.Default.labelFreqWalk3;


                numericUpDownFreqWalkDelay.Value = Properties.Settings.Default.FreqWalkDelay;
                //comboBoxFreqWalkAntenna.SelectedIndex = comboBoxFreqWalkAntenna.FindStringExact(Properties.Settings.Default.FreqWalkAntenna);

                Thread.Sleep(100);
                ComboBoxNRelaysSet();
                Application.DoEvents();
                formLoading = false;
                SetWalkAntennaToUse(Properties.Settings.Default.WalkAntenna);
                AntennaSetPickButtons();
                SetAntennaInUseForGUI(true);
                checkBoxTunerEnabled.Checked = Properties.Settings.Default.TunerEnabled;
                //TunerOpen();
                //if (ampIsOn == 0) AmpToggle();
                stopWatchTuner.Start(); // Assume we're already tuned

                timerFreqWalkSeconds = Properties.Settings.Default.TimerFreqWalkSeconds;
                FreqWalkSetIntervalDisplay();
                timerGetFreq.Interval = 500;
                timerGetFreq.Enabled = true;
                timerDebug.Interval = 500;
                timerDebug.Enabled = true;
                timerDebug.Start();
                Cursor = Cursors.Default;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error loading TunerUtil:\n" + ex.Message + "\n" + ex.StackTrace);
                throw;
            }
        }

        private void TunerClose()
        {
            tuner1?.Close();
            tuner1 = null;
        }

        void TunerSetAntennaSPE()
        {
            if (tuner1 == null) return;
            buttonSPEAnt.Text = "Ant " + tuner1.GetAntenna();
            /*
            if (tuner1.GetAntenna() == 1)
            {
                buttonSPEAnt.Text = "Ant 1";
            }
            else
            {
                buttonSPEAnt.Text = "Ant 2";
            }
            */
        }
        private void TunerOpen()
        {
            if (checkBoxTunerEnabled.Checked == false) return;
            Application.DoEvents();
            try
            {
                string? errorMsg = null;
                TunerClose();
                //toolTip1 = new ToolTip();
                //toolTip1.ForeColor = System.Drawing.Color.Black;
                if (comboBoxTunerModel.Text.Equals("LDG", StringComparison.InvariantCulture))
                {
                    comboBoxBaudTuner.Text = "38400";
                    tuner1 = new TunerLDG(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text);
                    toolTip1.SetToolTip(textBoxTune1Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune2Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune3Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune4Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune5Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune6Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune7Power, "Power settting for Tune -- if 0 then tune not performed");
                    toolTip1.SetToolTip(textBoxTune8Power, "Power settting for Tune -- if 0 then tune not performed");
                }
                else if (comboBoxTunerModel.Text.Equals(MFJ928, StringComparison.InvariantCulture))
                {
                    comboBoxBaudTuner.Text = "4800";
                    tuner1 = new TunerMFJ928(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text, out errorMsg);
                    toolTip1.SetToolTip(textBoxTune1Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune2Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune3Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune4Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune5Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune6Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune7Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    toolTip1.SetToolTip(textBoxTune8Power, "Power settting for Tune -- if 0 then tune not performed\nSPE recommends 40W max");
                    // We don't need any command information
                }
                else if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
                {
                    comboBoxBaudTuner.Text = "115200";
                    tuner1 = new TunerExpertLinears(comboBoxTunerModel.Text, comboBoxComTuner.Text, comboBoxBaudTuner.Text, out errorMsg);
                    //tuner1.GetStatus();
                    if (tuner1 != null && tuner1.IsOn)
                    {
                        buttonTunerPwr.BackColor = System.Drawing.Color.Green;
                        buttonTunerPwr.ForeColor = System.Drawing.Color.White;
                        //toolTip1 = new ToolTip();
                    }
                    else
                    {
                        buttonTunerPwr.BackColor = System.Drawing.Color.LightGray;
                        buttonTunerPwr.ForeColor = System.Drawing.Color.Black;
                    }
                    tabPageExpertLinears.Text = tuner1!.GetModel();
                    buttonAmp.Visible = false;
                    TunerSetAntennaSPE();
                    toolTip1.ForeColor = System.Drawing.Color.Black;
                    toolTip1.SetToolTip(textBoxTune1Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune2Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune3Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune4Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune5Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune6Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune7Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    toolTip1.SetToolTip(textBoxTune8Power, "Power settting for Tune -- if 0 then tune not performed\n30-40W recommended by SPE");
                    // We don't need any command information
                }

                if (checkBoxTunerEnabled.Checked && tuner1 == null || tuner1!.GetSerialPortTuner() == null || errorMsg != null)
                {
                    if (errorMsg != null && !errorMsg.Equals("OK"))
                    {
                        ShowCustomMessageBox(errorMsg);
                    }
                    else
                    {
                        DebugAddMsg(DebugEnum.ERR, "Tuner not working\n");
                        labelControlLog.Text = "Tuner not working\n";
                        //ShowCustomMessageBox("Error starting tuner!!!");
                    }
                }
                else
                {
                    Debug(DebugEnum.LOG, "Tuner opened on " + tuner1.GetSerialPortTuner() + "\n");
                    Application.DoEvents();
                    checkBoxTunerEnabled.Enabled = true;
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("Error starting tuner\nFix and reenable the Tuner" + ex.Message);
                checkBoxTunerEnabled.Checked = false;

            }
            if (tuner1 != null && (tuner1.GetModel() is not null) && tuner1.GetModel().Equals("20K"))
            {
                comboBoxExpertLinears4_1.Items.Add("5");
                comboBoxExpertLinears4_1.Items.Add("6");
                comboBoxExpertLinears4_2.Items.Add("5");
                comboBoxExpertLinears4_2.Items.Add("6");
                comboBoxExpertLinears6_1.Items.Add("5");
                comboBoxExpertLinears6_1.Items.Add("6");
                comboBoxExpertLinears6_2.Items.Add("5");
                comboBoxExpertLinears6_2.Items.Add("6");
                comboBoxExpertLinears10_1.Items.Add("5");
                comboBoxExpertLinears10_1.Items.Add("6");
                comboBoxExpertLinears10_2.Items.Add("5");
                comboBoxExpertLinears10_2.Items.Add("6");
                comboBoxExpertLinears12_1.Items.Add("5");
                comboBoxExpertLinears12_1.Items.Add("6");
                comboBoxExpertLinears12_2.Items.Add("5");
                comboBoxExpertLinears12_2.Items.Add("6");
                comboBoxExpertLinears15_1.Items.Add("5");
                comboBoxExpertLinears15_1.Items.Add("6");
                comboBoxExpertLinears15_2.Items.Add("5");
                comboBoxExpertLinears15_2.Items.Add("6");
                comboBoxExpertLinears17_1.Items.Add("5");
                comboBoxExpertLinears17_1.Items.Add("6");
                comboBoxExpertLinears17_2.Items.Add("5");
                comboBoxExpertLinears17_2.Items.Add("6");
                comboBoxExpertLinears20_1.Items.Add("5");
                comboBoxExpertLinears20_1.Items.Add("6");
                comboBoxExpertLinears20_2.Items.Add("5");
                comboBoxExpertLinears20_2.Items.Add("6");
                comboBoxExpertLinears30_1.Items.Add("5");
                comboBoxExpertLinears30_1.Items.Add("6");
                comboBoxExpertLinears30_2.Items.Add("5");
                comboBoxExpertLinears30_2.Items.Add("6");
                comboBoxExpertLinears40_1.Items.Add("5");
                comboBoxExpertLinears40_1.Items.Add("6");
                comboBoxExpertLinears40_2.Items.Add("5");
                comboBoxExpertLinears40_2.Items.Add("6");
                comboBoxExpertLinears60_1.Items.Add("5");
                comboBoxExpertLinears60_1.Items.Add("6");
                comboBoxExpertLinears60_2.Items.Add("5");
                comboBoxExpertLinears60_2.Items.Add("6");
                comboBoxExpertLinears80_1.Items.Add("5");
                comboBoxExpertLinears80_1.Items.Add("6");
                comboBoxExpertLinears80_2.Items.Add("5");
                comboBoxExpertLinears80_2.Items.Add("6");
                comboBoxExpertLinears160_1.Items.Add("5");
                comboBoxExpertLinears160_1.Items.Add("6");
                comboBoxExpertLinears160_2.Items.Add("5");
                comboBoxExpertLinears160_2.Items.Add("6");
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
            //Thread.Sleep(1000);
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
            //Thread.Sleep(1000);  // was getting memory overflow during shutdown under debug -- this seems to have fixed it
            Properties.Settings.Default.DebugLevel = comboBoxDebugLevel.Text;
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
            Properties.Settings.Default.FLRigStartCmd = numericUpDownFLRigBeforeWalk.Value;
            Properties.Settings.Default.FLRigStopCmd = numericUpDownFLRigAfterWalk.Value;


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
            Properties.Settings.Default.comboBoxAntenna1Relay = comboBoxAntenna1Controller.Text;
            Properties.Settings.Default.comboBoxAntenna2Relay = comboBoxAntenna2Controller.Text;
            Properties.Settings.Default.comboBoxAntenna3Relay = comboBoxAntenna3Controller.Text;
            Properties.Settings.Default.comboBoxAntenna4Relay = comboBoxAntenna4Controller.Text;
            Properties.Settings.Default.comboBoxAntenna5Relay = comboBoxAntenna5Controller.Text;
            Properties.Settings.Default.comboBoxAntenna6Relay = comboBoxAntenna6Controller.Text;
            Properties.Settings.Default.comboBoxAntenna7Relay = comboBoxAntenna7Controller.Text;
            Properties.Settings.Default.comboBoxAntenna8Relay = comboBoxAntenna8Controller.Text;

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
            
            Properties.Settings.Default.checkBoxTunePwr = checkBoxTunePwr.Checked;

            Properties.Settings.Default.Power1Watts = textBoxPower1Watts.Text;
            Properties.Settings.Default.Power2Watts = textBoxPower2Watts.Text;
            Properties.Settings.Default.Power3Watts = textBoxPower3Watts.Text;
            Properties.Settings.Default.Power4Watts = textBoxPower4Watts.Text;
            Properties.Settings.Default.Power5Watts = textBoxPower5Watts.Text;
            Properties.Settings.Default.Power6Watts = textBoxPower6Watts.Text;
            Properties.Settings.Default.Power7Watts = textBoxPower7Watts.Text;
            Properties.Settings.Default.Power8Watts = textBoxPower8Watts.Text;
            Properties.Settings.Default.Power1MaxWatts = textBoxPower1MaxWatts.Text;
            Properties.Settings.Default.Power2MaxWatts = textBoxPower2MaxWatts.Text;
            Properties.Settings.Default.Power3MaxWatts = textBoxPower3MaxWatts.Text;
            Properties.Settings.Default.Power4MaxWatts = textBoxPower4MaxWatts.Text;
            Properties.Settings.Default.Power5MaxWatts = textBoxPower5MaxWatts.Text;
            Properties.Settings.Default.Power6MaxWatts = textBoxPower6MaxWatts.Text;
            Properties.Settings.Default.Power7MaxWatts = textBoxPower7MaxWatts.Text;
            Properties.Settings.Default.Power8MaxWatts = textBoxPower8MaxWatts.Text;
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

            Properties.Settings.Default.AmpBits = comboBoxAmpBits.Text;

            //Properties.Settings.Default.FrequencyWalkList = textBoxFrequencyWalkList.Text;
            Properties.Settings.Default.TimerFreqWalkSeconds = timerFreqWalkSeconds;
            Properties.Settings.Default.ClockIsZulu = clockIsZulu;

            Properties.Settings.Default.labelFreqWalk1 = labelFreqWalk1.Text;
            Properties.Settings.Default.labelFreqWalk2 = labelFreqWalk2.Text;
            Properties.Settings.Default.labelFreqWalk3 = labelFreqWalk3.Text;

            var items = checkedListBoxWalk1.Items.Cast<string>().ToArray();
            Properties.Settings.Default.FrequenciesToWalk1List = string.Join(",", items);
            items = [.. checkedListBoxWalk2.Items.Cast<string>()];
            Properties.Settings.Default.FrequenciesToWalk2List = string.Join(",", items);
            items = [.. checkedListBoxWalk3.Items.Cast<string>()];
            Properties.Settings.Default.FrequenciesToWalk3List = string.Join(",", items);

            items = [.. checkedListBoxWalk1.CheckedItems.Cast<string>()];
            Properties.Settings.Default.FrequenciesToWalk1 = string.Join(",", items);
            items = [.. checkedListBoxWalk2.CheckedItems.Cast<string>()];
            Properties.Settings.Default.FrequenciesToWalk2 = string.Join(",", items);
            items = [.. checkedListBoxWalk3.CheckedItems.Cast<string>()];
            Properties.Settings.Default.FrequenciesToWalk3 = string.Join(",", items);

            Properties.Settings.Default.FreqWalkDelay = numericUpDownFreqWalkDelay.Value;
            Properties.Settings.Default.FreqWalkAntenna = (string)comboBoxFreqWalkAntenna.SelectedItem;

            Properties.Settings.Default.checkBoxWalk1 = checkBoxWalk1.Checked;
            Properties.Settings.Default.checkBoxWalk2 = checkBoxWalk2.Checked;
            Properties.Settings.Default.checkBoxWalk3 = checkBoxWalk3.Checked;

            Properties.Settings.Default.ViewAntenna = antennaToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewRelay1 = relay1ToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewRelay2 = relay2ToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewRelay3 = relay3ToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewRelay4 = relay4ToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewPower = powerToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewDebug = debugToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewTuner = tunerToolStripMenuItem.Checked;
            Properties.Settings.Default.ViewExpertLinears = expertLinearsToolStripMenuItem.Checked;
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
            try
            {
                comboBoxComTuner.Items.Clear();
                foreach (string s in System.IO.Ports.SerialPort.GetPortNames())
                {
                    string[] tokens = s.Split(' ');
                    string comName = "";
                    if (tokens[0].Contains("COM")) comName = tokens[0];
                    if (!comName.StartsWith("COM")) continue;
                    if (comboBoxComTuner.Items.Count == 0)
                    {
                        comboBoxComTuner.Items.Add(s);
                    }
                    else
                    {
                        int numSerPort = Int32.Parse(comName.Substring(3), CultureInfo.InvariantCulture);
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
                                tokens = s3.Split(' ');
                                int numCurrentItem = Int32.Parse(tokens[0].Substring(3), CultureInfo.InvariantCulture);
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
            catch (Exception ex)
            {
                MessageBox.Show("Error logind COM ports\n" + ex.Message + "\n" + ex.StackTrace);
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
                ShowCustomMessageBox("Relay On");
            }
            else
            {
                ShowCustomMessageBox("Relay Off");
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
            if (myRig == null)
            {
                myRig = new RigFLRig();
                myRig.Open();
            }
            return;
        }

        private void DisconnectFLRig()
        {
            myRig?.Close();
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
            myRig = null;
        }

        // Returns true if send is OK
        /*
        static public bool FLRigSend(string xml)
        {
            if (rigStream == null)
            {
                return false;
            }
            Monitor.Enter("FlRigSend");
            rigStream.Flush();
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
                        //ShowCustomMessageBox("Unknown response from FLRig\n" + responseData);
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
        */

        /*
        private bool TuneSequence([CallerMemberName] string name = "",
                   [CallerLineNumber] int line = -1)
                   //[CallerFilePath] string path = "")
        {
            bool retval;
            if (tuning == true) return true;
            //if (tuner1.State != Tuner.TunerState.NeedsTuning) return;
            tuning = true;
            DebugAddMsg(DebugEnum.LOG, "TuneSequence called from " + name + "@line(" + line + ")\n");
            // Set power if needed to ensure not overdriving things -- we do it again after we start tuning
            //SetAntennaInUse();
            PowerSelect(frequencyHz, modeCurrent, true);
            if (freqWalkIsRunning) return true;
            if (ampIsOn) // we need to turn off amplifier
            {
                if (tuner1 == null)
                {
                    _ = MessageBox.Show("No tuner??");
                }
                Debug(DebugEnum.LOG, "Turning amp off\n");
                tuner1.CMDAmp(0); // amp off
                AmpSet(false);
                //Thread.Sleep(500);
                retval = Tune();
                Debug(DebugEnum.LOG, "Turning amp on\n");
                //tuner1.CMDAmp(1);
                SetAntennaInUseForGUI(true);
                //AmpSet(true);
                //Debug(DebugEnum.TRACE, "amp on\n");
                //Thread.Sleep(500);
            }
            else // just tune 
            {
                retval = Tune();
                SetAntennaInUseForGUI(true);
            }
            tuning = false;
            string tunerStatus = Environment.GetEnvironmentVariable("TEMP") + "\\AmpAutoTunerDone.txt";
            System.IO.File.Create(tunerStatus);
            return retval;
        }
        */
        // returns true when Tuner and FLRig are talking to us
        // if ptt is true then will use ptt and audio tone for tuning -- e.g. MFJ-928 without radio interface
        private bool Tune()
        {
            //string xml;
            if (myRig is null)
            {
                MessageBox.Show("Rig is null???");
                return false;
            }
            if (pausedTuning)
            {
                Debug(DebugEnum.WARN, "Tuner is paused\n");
                return true;
            }
            try
            {
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
                var modeaSave = myRig.ModeA;
                var modebSave = myRig.ModeB;
                myRig.ModeA = "FM";
                myRig.ModeB = "FM";
                myRig.FrequencyA += 1000;
                myRig.FrequencyB += 1000;
                Thread.Sleep(500);
                Debug(DebugEnum.LOG, "Tuning to 1000+ offset\n");

                buttonTunerStatus.BackColor = System.Drawing.Color.LightGray;
                Application.DoEvents();
                if (ptt && !powerSDR) // we turn on PTT and send the audio tone before starting tune
                {
                    Debug(DebugEnum.LOG, "Audio tune started\n");
                    audio!.MyFrequency = 1000;
                    audio!.Volume = 1;
                    audio!.StartStopSineWave();
                    Thread.Sleep(300); // give ptt a chance to crank up
                    myRig.SetPTT(true);
                    Thread.Sleep(Convert.ToInt32(numericUpDownPostPttDelay.Value)); // give ptt a chance to crank up
                    Debug(DebugEnum.LOG, "Set ptt on\n");
                    tuneIsRunning = true;
                    tuner1!.Tune();
                    tuneIsRunning = false;
                }
                else if (powerSDR)
                {
                    Debug(DebugEnum.LOG, "PowerSDR Tune started, amp off\n");
                    //tuner1.CMDAmp(0); // amp off
                    Debug(DebugEnum.LOG, "PowerSDR Tune start tone\n");
                    //xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU1;</value></param></params");
                    //if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                    Thread.Sleep(Convert.ToInt32(numericUpDownPostPttDelay.Value));
                    Debug(DebugEnum.LOG, "PowerSDR Tune start tune\n");
                    tuneIsRunning = true;
                    tuner1!.Tune();
                    Thread.Sleep(2000);
                    tuneIsRunning = false;
                    Debug(DebugEnum.LOG, "PowerSDR Tune stop tone\n");
                    //xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                    //if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                }
                else if (tuner1 is not null && tuner1.GetModel() is not null && tuner1.GetModel().Contains(EXPERTLINEARS))
                {
                    // nothing to do
                    return true;
                }
                else
                {
                    Debug(DebugEnum.LOG, "Generic tuner started\n");
                    tuner1!.Tune();
                }
                Thread.Sleep(200);
                char response = tuner1.ReadResponse();
                DebugAddMsg(DebugEnum.VERBOSE, "tuner1.ReadResponse " + response + "\n");
                // stop audio here
                Application.DoEvents();
                if (ptt && !powerSDR) // we turn off PTT now
                {
                    audio!.StartStopSineWave();
                    myRig.SetPTT(false);
                    Debug(DebugEnum.LOG, "ptt off\n");
                }
                else if (powerSDR)
                {
                    //xml = FLRigXML("rig.cat_string", "<params><param><value>ZZTU0;</value></param></params");
                    //if (FLRigSend(xml) == false) return false; // Abort if FLRig is giving an error
                    Debug(DebugEnum.LOG, "PowerSDR Tune stopped\n");
                }
                Thread.Sleep(200); // give a little time for ptt off and audio to stop
                Application.DoEvents();
                if (response == 'T')
                {
                    buttonTunerStatus.BackColor = System.Drawing.Color.Green;
                    labelSWR.Text = "SWR < 1.5";
                }
                else if (response == 'M')
                {
                    SoundPlayer simpleSound = new("swr.wav");
                    simpleSound.Play();
                    buttonTunerStatus.BackColor = System.Drawing.Color.Yellow;
                    labelSWR.Text = "SWR 1.5-3.0";
                    simpleSound.Dispose();
                }
                else if (response == 'F')
                {
                    SoundPlayer simpleSound = new("swr.wav");
                    simpleSound.Play();
                    buttonTunerStatus.BackColor = System.Drawing.Color.Red;
                    labelSWR.Text = "SWR > 3.0";
                    simpleSound.Dispose();
                }
                else
                {
                    if (this.WindowState == FormWindowState.Minimized)
                        this.WindowState = FormWindowState.Normal;
                    tabPage.SelectedTab = tabPageDebug;
                    buttonTunerStatus.BackColor = System.Drawing.Color.Transparent;
                    ShowCustomMessageBox("Unknown response from tuner = '" + response + "'");
                    Debug(DebugEnum.ERR, "Unknown response from tuner = '" + response + "'\n");
                }
                tuneIsRunning = false;
                myRig.FrequencyA -= 1000;
                myRig.FrequencyB -= 1000;
                myRig.ModeA = modeaSave;
                myRig.ModeB = modebSave;
                //richTextBoxRig.AppendText("Tuning done SWR="+tuner1.SWR+"\n");
                //richTextBoxRig.AppendText("Tuning done\n");
                Debug(DebugEnum.TRACE, "Setting Tx power\n");
                PowerSelect(frequencyHz, modeCurrent, false);
                myRig.SetFrequency(frequencyHz);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            return true;
        }
        private void SetDummyLoad()
        {
            try
            {
                const string dummy = "dummy";
                string antennaNumberNew;
                //bool antennaAmpChecked;
                if (textBoxAntenna1.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna1.Checked = true;
                    antennaNumberNew = "1";
                    buttonAntenna1.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna2.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna2.Checked = true;
                    antennaNumberNew = "2";
                    buttonAntenna2.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna3.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna3.Checked = true;
                    antennaNumberNew = "3";
                    buttonAntenna3.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna4.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna4.Checked = true;
                    antennaNumberNew = "4";
                    buttonAntenna4.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna5.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna5.Checked = true;
                    antennaNumberNew = "5";
                    buttonAntenna5.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna6.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna6.Checked = true;
                    antennaNumberNew = "6";
                    buttonAntenna6.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna7.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna7.Checked = true;
                    antennaNumberNew = "7";
                    buttonAntenna7.BackColor = System.Drawing.Color.Green;
                }
                else if (textBoxAntenna8.Text.ToLower().Contains(dummy))
                {
                    checkBoxAntenna8.Checked = true;
                    antennaNumberNew = "8";
                    buttonAntenna8.BackColor = System.Drawing.Color.Green;
                }
                else
                {
                    MessageBox.Show("dummyload antenna not found");
                    return;
                }
                labelAntennaSelected.Text = "Dummy Load";
                var tmp = Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture);
                SetAntennaRelayOn(tmp, true);
                //AmpSet(antennaAmpChecked);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }

        private void SetAntennaInUseByTuner()
        {
            switch (tuner1!.AntennaNumber)
            {
                case 1:
                    break;
                case 2:
                    break;
                case 3:
                    break;
                case 4:
                    break;
                case 5:
                    break;
                case 6:
                    break;
                case 7:
                    break;
                case 8:
                    break;
            }
        }
        private void SetAntennaInUseForGUI(bool tune, int tunerAntenna = 0)
        {
            try
            {
                if (myRig is null)
                {
                    //MessageBox.Show("myRig is null???");
                    return;
                }
                if (formLoading) return;
                if (antennaLocked) return;
                bool antennaAmpChecked = false;
                var antennaNumberNew = "1"; // default to antenna#1
                                            //var modeCurrent = FLRigGetMode();
                                            //if (!tuneIsRunning) PowerSelect(frequencyHz, modeCurrent);

                double frequencyMHz = frequencyHz / 1000000;
                if (frequencyHz == 0) return;

                buttonAntenna1.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna2.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna3.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna4.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna5.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna6.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna7.BackColor = System.Drawing.Color.LightGray;
                buttonAntenna8.BackColor = System.Drawing.Color.LightGray;
                if (dummyLoad == true)
                {
                    SetDummyLoad();
                    return;
                }
                int antennaOnTuner = 1;
                if (antennaToolStripMenuItem.Checked)
                {
                    //try
                    {
                        //UpdateAntennaFrequencies();
                    }
                    //catch (Exception ex)
                    //{
                    //MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                    //}
                    try
                    {
                        if (checkBoxAntenna1.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq1From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq1To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna1.BackColor == System.Drawing.Color.LightYellow))
                            || tunerAntenna == 1)
                        {
                            checkBoxAntenna1.Checked = true;
                            buttonAntenna1.BackColor = System.Drawing.Color.Green;
                            labelAntennaSelected.Text = textBoxAntenna1.Text;
                            ButtonAntennaPickSet(buttonAntennaPick1);
                            antennaNumberNew = "1";
                            //antennaAmpChecked = checkBoxAntenna1Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect1.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna2.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq2From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq2To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna2.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 2)
                        {
                            checkBoxAntenna2.Checked = true;
                            buttonAntenna2.BackColor = System.Drawing.Color.Green;
                            labelAntennaSelected.Text = textBoxAntenna2.Text;
                            ButtonAntennaPickSet(buttonAntennaPick2);
                            antennaNumberNew = "2";
                            //antennaAmpChecked = checkBoxAntenna2Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect2.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna3.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq3From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq3To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna3.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 3)
                        {
                            checkBoxAntenna3.Checked = true;
                            buttonAntenna3.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick3);
                            labelAntennaSelected.Text = textBoxAntenna3.Text;
                            antennaNumberNew = "3";
                            //antennaAmpChecked = checkBoxAntenna3Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect3.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna4.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq4From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq4To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna4.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 4)
                        {
                            checkBoxAntenna4.Checked = true;
                            buttonAntenna4.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick4);
                            labelAntennaSelected.Text = textBoxAntenna4.Text;
                            antennaNumberNew = "4";
                            //antennaAmpChecked = checkBoxAntenna4Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect4.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna5.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq5From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq5To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna5.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 5)
                        {
                            checkBoxAntenna5.Checked = true;
                            buttonAntenna5.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick5);
                            labelAntennaSelected.Text = textBoxAntenna5.Text;
                            antennaNumberNew = "5";
                            //antennaAmpChecked = checkBoxAntenna5Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect5.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna6.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq6From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq6To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna6.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 6)
                        {
                            checkBoxAntenna6.Checked = true;
                            buttonAntenna6.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick6);
                            labelAntennaSelected.Text = textBoxAntenna6.Text;
                            antennaNumberNew = "6";
                            //antennaAmpChecked = checkBoxAntenna6Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect6.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna7.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq7From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq7To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna7.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 7)
                        {
                            checkBoxAntenna7.Checked = true;
                            buttonAntenna7.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick7);
                            labelAntennaSelected.Text = textBoxAntenna7.Text;
                            antennaNumberNew = "7";
                            //antennaAmpChecked = checkBoxAntenna7Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect7.Text, out antennaOnTuner);
                        }
                        else if (checkBoxAntenna8.Checked == true &&
                            ((!freqWalkIsRunning && frequencyMHz >= Convert.ToDouble(textBoxAntennaFreq8From.Text, CultureInfo.InvariantCulture) && frequencyMHz <= Convert.ToDouble(textBoxAntennaFreq8To.Text, CultureInfo.InvariantCulture)
                            ) || (freqWalkIsRunning && textBoxAntenna8.BackColor == System.Drawing.Color.Yellow))
                            || tunerAntenna == 8)
                        {
                            checkBoxAntenna8.Checked = true;
                            buttonAntenna8.BackColor = System.Drawing.Color.Green;
                            ButtonAntennaPickSet(buttonAntennaPick8);
                            labelAntennaSelected.Text = textBoxAntenna8.Text;
                            antennaNumberNew = "8";
                            //antennaAmpChecked = checkBoxAntenna8Amp.Checked;
                            Int32.TryParse(comboBoxAntSelect8.Text, out antennaOnTuner);
                        }
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                        // don't do anything here...just catching the parse errors from blank boxes
                    }
                }
                else if (tuner1 != null && tuner1.GetModel().Contains("SPE"))
                {
                    labelAntennaSelected.Text = "SPE Ant#" + tuner1.GetAntenna();
                }
                if (freqWalkIsRunning)
                {
                    if (comboBoxFreqWalkAntenna.SelectedIndex >= 0)
                    {
                        var s = (string)comboBoxFreqWalkAntenna.Items[comboBoxFreqWalkAntenna.SelectedIndex];
                        if (tuner1 != null && !comboBoxAntenna1Controller.Text.Contains(EXPERTLINEARS))
                            tuner1.SetAntenna(int.Parse(s));
                    }
                    else
                    {
                        MessageBox.Show("Need to select walk antenna in FreqWalk tab, defaulting to antenna#1", "FreqWalk", MessageBoxButtons.OK);
                        if (tuner1 != null && !comboBoxAntenna1Controller.Text.Contains(EXPERTLINEARS))
                            tuner1.SetAntenna(1);
                        return;
                    }
                }
                else
                {
                    // we don't need to set antenna here when tuner automatically selects it
                    //if (tuner1 != null && !comboBoxAntenna1Controller.Text.Equals(EXPERTLINEARS))
                    //tuner1.SetAntenna(antennaOnTuner);
                }
                if (antennaNumberNew.Length == 0) antennaNumberNew = "1";
                var maxPower = 1;
                switch(antennaNumberNew)
                {
                    case "1": maxPower = Int32.Parse(textBoxPower1MaxWatts.Text);break;
                    case "2": maxPower = Int32.Parse(textBoxPower2MaxWatts.Text); break;
                    case "3": maxPower = Int32.Parse(textBoxPower3MaxWatts.Text); break;
                    case "4": maxPower = Int32.Parse(textBoxPower4MaxWatts.Text); break;
                    case "5": maxPower = Int32.Parse(textBoxPower5MaxWatts.Text); break;
                    case "6": maxPower = Int32.Parse(textBoxPower6MaxWatts.Text); break;
                    case "7": maxPower = Int32.Parse(textBoxPower7MaxWatts.Text); break;
                    case "8": maxPower = Int32.Parse(textBoxPower8MaxWatts.Text); break;
                    default:
                        DebugMsg.DebugAddMsg(DebugEnum.ERR, "Selected antenna has no maximum power...defaulting to 1W");
                        maxPower = 1;
                        break;
                }
                myRig.MaxPower = maxPower;
                var tmp = Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture);
                // if we want to use the Tuner antenna selection then we'll need to add that option
                //if (!pausedTuning && tuner1 != null && tmp != tuner1.AntennaNumber)
                //    tuner1.SetAntenna(Convert.ToInt32(antennaNumberNew, CultureInfo.InvariantCulture), tuneIsRunning);
                bool needTuning = false;
                if (tune && tuner1 != null && tmp != tuner1.AntennaNumber && !freqWalkIsRunning)
                {
                    needTuning = true;
                }
                SetAntennaRelayOn(tmp, needTuning);
                AmpSet(antennaAmpChecked);
                if (needTuning)
                {
                    //tuner1.Tune();
                }
                //SetAntennaAmp(tmp);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

        }

        private void UpdateAntennaFrequencies()
        {
            UpdateAntennaFrequencies2();
            UpdateAntennasInPower();

        }
        private void UpdateAntennaFrequencies2()
        { 
            f1From = f2From = f3From = f4From = f5From = f6From = f7From = f8From = 0;
            f1To = f2To = f3To = f4To = f5To = f6To = f7To = f8To = 0;
            f1Ant = f2Ant = f3Ant = f4Ant = f5Ant = f6Ant = f7Ant = f8Ant = 0;
            if (checkBoxAntenna1.Checked)
            {
                f1From = Convert.ToDouble(textBoxAntennaFreq1From.Text.ToString(), CultureInfo.InvariantCulture);
                f1To = Convert.ToDouble(textBoxAntennaFreq1To.Text.ToString(), CultureInfo.InvariantCulture);
                //f1Ant = comboBoxAntSelect1.SelectedText;
                Int32.TryParse(comboBoxAntSelect1.Text, out f1Ant);
            }
            if (checkBoxAntenna2.Checked)
            {
                f2From = Convert.ToDouble(textBoxAntennaFreq2From.Text.ToString(), CultureInfo.InvariantCulture);
                f2To = Convert.ToDouble(textBoxAntennaFreq2To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect2.Text, out f2Ant);
            }
            if (checkBoxAntenna3.Checked)
            {
                f3From = Convert.ToDouble(textBoxAntennaFreq3From.Text.ToString(), CultureInfo.InvariantCulture);
                f3To = Convert.ToDouble(textBoxAntennaFreq3To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect3.Text, out f3Ant);
            }
            if (checkBoxAntenna4.Checked)
            {
                f4From = Convert.ToDouble(textBoxAntennaFreq4From.Text.ToString(), CultureInfo.InvariantCulture);
                f4To = Convert.ToDouble(textBoxAntennaFreq4To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect4.Text, out f4Ant);
            }
            if (checkBoxAntenna5.Checked)
            {
                f5From = Convert.ToDouble(textBoxAntennaFreq5From.Text.ToString(), CultureInfo.InvariantCulture);
                f5To = Convert.ToDouble(textBoxAntennaFreq5To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect5.Text, out f5Ant);
            }
            if (checkBoxAntenna6.Checked)
            {
                f6From = Convert.ToDouble(textBoxAntennaFreq6From.Text.ToString(), CultureInfo.InvariantCulture);
                f6To = Convert.ToDouble(textBoxAntennaFreq6To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect6.Text, out f6Ant);
            }
            if (checkBoxAntenna7.Checked)
            {
                f7From = Convert.ToDouble(textBoxAntennaFreq7From.Text.ToString(), CultureInfo.InvariantCulture);
                f7To = Convert.ToDouble(textBoxAntennaFreq7To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect7.Text, out f7Ant);
            }
            if (checkBoxAntenna8.Checked)
            {
                f8From = Convert.ToDouble(textBoxAntennaFreq8From.Text.ToString(), CultureInfo.InvariantCulture);
                f8To = Convert.ToDouble(textBoxAntennaFreq8To.Text.ToString(), CultureInfo.InvariantCulture);
                Int32.TryParse(comboBoxAntSelect8.Text, out f8Ant);
            }
        }

        void SetAntennaAmp(int antennaNumber, [CallerMemberName] string name = "",
                   [CallerLineNumber] int line = -1)
        {
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.TRACE, "SetAntennaAmp(" + antennaNumber + ") called from " + name + "@line(" + line + ")\n");
            if (formLoading) return;
            if (lastAntennaNumber == antennaNumber) return;
            // we use the Antenna Amp checkbox to turn the Amp on or off
            switch (antennaNumber)
            {
                case 1:
                    //AmpSet(checkBoxAntenna1Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna1Amp.Checked;
                    break;
                case 2:
                    //AmpSet(checkBoxAntenna2Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna2Amp.Checked;
                    break;
                case 3:
                    //AmpSet(checkBoxAntenna3Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna3Amp.Checked;
                    break;
                case 4:
                    //AmpSet(checkBoxAntenna4Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna4Amp.Checked;
                    break;
                case 5:
                    //AmpSet(checkBoxAntenna5Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna5Amp.Checked;
                    break;
                case 6:
                    //AmpSet(checkBoxAntenna6Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna6Amp.Checked;
                    break;
                case 7:
                    //AmpSet(checkBoxAntenna7Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna7Amp.Checked;
                    break;
                case 8:
                    //AmpSet(checkBoxAntenna8Amp.Checked);
                    //buttonAmp.Enabled = checkBoxAntenna8Amp.Checked;
                    break;
            }
        }

        void SetControllerFromAntenna(int antennaNumber)
        {
            if (tuner1 is null)
            {
                MessageBox.Show("tuner1 is null???");
                return;
            }
            switch (antennaNumber)
            {
                case 1:
                    if (comboBoxAntenna1Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna1Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 2:
                    if (comboBoxAntenna2Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna1Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 3:
                    if (comboBoxAntenna3Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna1Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 4:
                    if (comboBoxAntenna4Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna1Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 5:
                    if (comboBoxAntenna5Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna5Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 6:
                    if (comboBoxAntenna6Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna6Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 7:
                    if (comboBoxAntenna7Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna7Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
                case 8:
                    if (comboBoxAntenna8Controller.Text.Contains(EXPERTLINEARS))
                    {
                        if (tuner1.AntennaNumber != antennaNumber)
                            tuner1!.SetAntenna(antennaNumber, false);
                    }
                    else if (comboBoxAntenna8Controller.Text.Contains("relay"))
                    {
                        SetRelayFromAntenna(antennaNumber);
                    }
                    break;
            }
            Thread.Sleep(1000);
            if (tuner1.AntennaNumber != antennaNumber)
            {
                // toggle back
                tuner1!.SetAntenna(antennaNumber, false);
                ShowCustomMessageBox("Cannot select antenna#"+antennaNumber+"\nIf you want to select this antenna you must change the Antena selectons in the SPE Amp configuration");
            }

        }
        private void SetRelayFromAntenna(int antennaNumber)
        {
            ComboBox bits = antennaNumber switch
            {
                1 => ComboBoxAntenna1Bits,
                2 => ComboBoxAntenna2Bits,
                3 => ComboBoxAntenna3Bits,
                4 => ComboBoxAntenna4Bits,
                5 => ComboBoxAntenna5Bits,
                6 => ComboBoxAntenna6Bits,
                7 => ComboBoxAntenna7Bits,
                8 => ComboBoxAntenna8Bits,
                _ => ComboBoxAntenna1Bits,
            };
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
            relay1!.Set(1, (byte)relayValue);
        }
        void SetAntennaRelayOn(int antennaNumber, bool needTuning = true)
        {
            if (formLoading) return;
            //if (antennaLocked) return;
            if (!checkBoxRelay1Enabled.Checked) return;
            SetAntennaAmp(antennaNumber);
            string myRelayChosen = comboBoxAntenna1Controller.Text;
            if (myRelayChosen.Length == 0 && formLoading == false)
            {
                if (comboBoxAntenna1Controller.SelectedIndex < 0)
                {
                    //MessageBox.Show("No Relay picked");
                    return;
                }
                comboBoxAntenna1Controller.SelectedIndex = 0;
            }

            ComboBox bits = antennaNumber switch
            {
                1 => ComboBoxAntenna1Bits,
                2 => ComboBoxAntenna2Bits,
                3 => ComboBoxAntenna3Bits,
                4 => ComboBoxAntenna4Bits,
                5 => ComboBoxAntenna5Bits,
                6 => ComboBoxAntenna6Bits,
                7 => ComboBoxAntenna7Bits,
                8 => ComboBoxAntenna8Bits,
                _ => ComboBoxAntenna1Bits,
            };
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
            Relay? myRelay = relay1;
            if (myRelay == null) return;
            if (myRelayChosen.Equals("Relay2")) myRelay = relay2;
            else if (myRelayChosen.Equals("Relay3")) myRelay = relay3;
            else if (myRelayChosen.Equals("Relay4")) myRelay = relay4;
            //if (lastAntennaUsed != 0 && lastAntennaUsed != relayValue) 
            //    myRelay.Set(lastAntennaUsed, 0);
            //if (tuner1 != null && (lastAntennaNumberUsed != antennaNumber || lastRelayUsed != relayValue))
            if (tuner1 != null)
            {
                //myRelay.AllOff();
                myRelay!.Set(relayValue, 1);
                RelaySetButtons(button1_1, relay1!.Status());
                AntennaUpdateSelected(antennaNumber);
                if (lastAntennaNumber >= 0 && needTuning && lastAntennaNumber != antennaNumber & !antennaLocked)
                {
                    //Cursor.Current = Cursors.WaitCursor;
                    timerGetFreq.Stop();
                    if (tuner1.GetModel() is not null && !tuner1.GetModel().Contains(EXPERTLINEARS))
                        TuneSequence();
                    timerGetFreq.Start();
                    //Cursor.Current = Cursors.Default;
                }
            }
            lastAntennaNumber = antennaNumber;
            //lastRelayUsed = relayValue;
            Application.DoEvents();
        }
        /*
        private void FLRigSetActiveVFO(string mode)
        {
            Monitor.Enter(12345);
            bool errFlag = false;
            Debug(DebugEnum.LOG, "FLRigSetActiveVFO=" + mode + "\n");

            string myparam = "<params><param><value>" + mode + "</value></param></params>";
            string xml2 = FLRigXML("rig.set_AB", myparam);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                if (rigStream == null) return;
                rigStream.Write(data, 0, data.Length);
                // Ignore the response for now
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
            }
            catch (Exception)
            {
                Debug(DebugEnum.ERR, "FLRigSetActiveVFO error\n");
                errFlag = true;
            }
            if (errFlag)
            {
                try
                {
                    rigStream!.Close();
                    FLRigConnect();
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "FLRig reset error\n");
                }
            }
            return;
        }
        */
        private int FLRigGetPower()
        {
            int power = 0;
            try
            {
                return myRig!.Power;

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
            var power = myRig!.Power;
            if (power == value) return true;
            Thread.Sleep(100);
            DebugAddMsg(DebugEnum.VERBOSE, "Power needs changing to " + value + "\n");
            
            //string xml = FLRigXML("rig.set_power", "<params><param><value><i4>" + value + "</i4></value></param></params");
            int ntry = 0;
            do
            {
                DebugAddMsg(DebugEnum.VERBOSE, "Set power try#" + ntry + "\n");
                //if (tuner1!.GetModel().Contains(EXPERTLINEARS) && value > 30)
                //{
                //    value = 30;
                //    DebugMsg.DebugAddMsg(DebugEnum.LOG,"Power reduced to " + value + "W to avoid overdrive");
                //}
                myRig.Power = value;
                Application.DoEvents();
                try
                {
                    if (ntry > 0)
                    {
                        Debug(DebugEnum.LOG, "Set power to " + value + " try#" + ++ntry + "\n");
                        Thread.Sleep(500);
                    }
                }
                catch (Exception ex)
                {
                    richTextBoxDebug.AppendText(ex.Message + "\n" + ex.StackTrace);
                }
                power = myRig.Power;
            } while (power != value && ++ntry <= 3);
            labelPower.Text = "RigPower = " + value;
            return true;
        }

        private void UpdateAntennasInPower()
        {
            AntennaSetPickButtons();
            comboBox1.Items.Clear();
            comboBox2.Items.Clear();
            comboBox3.Items.Clear();
            comboBox4.Items.Clear();
            comboBox5.Items.Clear();
            comboBox6.Items.Clear();
            comboBox7.Items.Clear();
            comboBox8.Items.Clear();
            if (f1From < f1To) comboBox1.Items.Add("1");
            if (f2From < f2To) comboBox1.Items.Add("2");
            if (f3From < f3To) comboBox1.Items.Add("3");
            if (f4From < f4To) comboBox1.Items.Add("4");
            if (f5From < f5To) comboBox1.Items.Add("5");
            if (f6From < f6To) comboBox1.Items.Add("6");
            if (f7From < f7To) comboBox1.Items.Add("7");
            if (f8From < f8To) comboBox1.Items.Add("8");
            foreach (var item in comboBox1.Items)
            {
                comboBox2.Items.Add(item);
                comboBox3.Items.Add(item);
                comboBox4.Items.Add(item);
                comboBox5.Items.Add(item);
                comboBox6.Items.Add(item);
                comboBox7.Items.Add(item);
                comboBox8.Items.Add(item);
            }
        }
#pragma warning disable IDE0060 // Remove unused parameter
        private bool PowerSelectOp(double frequencyMHz, CheckBox enabled, TextBox from, TextBox to, TextBox powerLevel, ComboBox mode, string modeChk, CheckBox amp, bool ampFlag, int passNum, TextBox maxPower, TextBox tunePower)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            UpdateAntennaFrequencies();
            if (tuner1 is null)
            {
                //MessageBox.Show("tuner1 is null???");
                return false;
            }
            if (myRig is null)
            {
                MessageBox.Show("myRig is null???");
                return false;
            }
            // On passNum=0 we check for specific mode matches
            // On passNum>0 we check for "Any" matches
            if (tunePower.Text.Equals("0")) buttonTune.Enabled = false;
            else buttonTune.Enabled = true;
            DebugAddMsg(DebugEnum.VERBOSE, "PowerSelectOp\n");
            try
            {
                if (enabled.Checked)
                {
                    if (from.Text.Length == 0)
                    {
                        //ShowCustomMessageBox("Power tab From MHz is empty");
                        from.Text = "0";
                        return false;
                    }
                    if (to.Text.Length == 0)
                    {
                        //ShowCustomMessageBox("Power tab To MHz is empty");
                        to.Text = "1000";
                        return false;
                    }
                    if (ampFlag)
                    {
                        buttonOperate.Enabled = true;
                    }
                    else
                    {
                        buttonOperate.Enabled = false;
                        tuner1.Operate(false);
                    }
                    double frequencyFrom = Convert.ToDouble(from.Text, CultureInfo.InvariantCulture);
                    double frequencyTo = Convert.ToDouble(to.Text, CultureInfo.InvariantCulture);
                    if (frequencyMHz >= frequencyFrom && frequencyMHz <= frequencyTo)
                    {
                        // Any more other than CW we make power MID
                        if (!myRig.ModeA.Contains("CW") && tuner1.GetPowerLevel().Equals("MAX"))
                        {
                            DebugAddMsg(DebugEnum.VERBOSE, "Power limited to Mid for non-CW mode");
                            tuner1.SetPowerLevel("Mid");
                        }
                        if (mode.SelectedItem == null || mode.SelectedItem.Equals(modeChk) || (passNum > 0 && mode.SelectedItem.Equals("Any")))
                        {
                            if (!pausedTuning && !tuneIsRunning && powerLevel.Text.Length > 0)
                            {
                                Debug(DebugEnum.TRACE, "Set Power=" + powerLevel.Text + "\n");
                                Application.DoEvents();
                                maxRigPower = Convert.ToInt32(maxPower.Text);
                                int powerset = Convert.ToInt32(powerLevel.Text, CultureInfo.InvariantCulture);
                                if (powerset == 0 || tuner1.IsOperate)
                                {
                                    buttonTune.Enabled = false;
                                }
                                else
                                {
                                    buttonTune.Enabled = true;
                                }
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
                                //AmpSet(ampFlag);
                            }
                            return true;
                        }
                        return false;
                    }
                }
            }
            catch (Exception)
            {
                //ShowCustomMessageBox("Error parsing frequencies\nFrequencies need to be in Mhz\nPower not set");
            }
            return false;
        }
        /*
        private int GetPower(int antennaNumber)
        {
            switch (antennaNumber)
            {
                case 1:
                    return Int32.Parse(textBoxPower1MaxWatts.Text);
                case 2:
                    return Int32.Parse(textBoxPower2MaxWatts.Text);
                case 3:
                    return Int32.Parse(textBoxPower3MaxWatts.Text);
                case 4:
                    return Int32.Parse(textBoxPower4MaxWatts.Text);
                case 5:
                    return Int32.Parse(textBoxPower5MaxWatts.Text);
                case 6:
                    return Int32.Parse(textBoxPower6MaxWatts.Text);
                case 7:
                    return Int32.Parse(textBoxPower7MaxWatts.Text);
                case 8:
                    return Int32.Parse(textBoxPower8MaxWatts.Text);
            }
            return 5;
        }
        */
        private void PowerSelect(double frequencyHz, string modeChk, bool tuning = false, bool use_pwr = false)
        {
            double frequencyMHz = frequencyHz / 1e6;
            for (int passNum = 0; passNum < 2; ++passNum)
            {
                //Debug(DebugEnum.TRACE, "1\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower1Enabled, textBoxPower1From, textBoxPower1To, tuning && use_pwr ? textBoxTune1Power : textBoxPower1Watts, comboBoxPower1Mode, modeChk, checkBoxAmp1, checkBoxAmp1.Checked, passNum, textBoxPower1MaxWatts, textBoxTune1Power))
                    return;
                //Debug(DebugEnum.TRACE, "2\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower2Enabled, textBoxPower2From, textBoxPower2To, tuning && use_pwr ? textBoxTune2Power : textBoxPower2Watts, comboBoxPower2Mode, modeChk, checkBoxAmp2, checkBoxAmp2.Checked, passNum, textBoxPower2MaxWatts, textBoxTune2Power))
                    return;
                //Debug(DebugEnum.TRACE, "3\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower3Enabled, textBoxPower3From, textBoxPower3To, tuning && use_pwr ? textBoxTune3Power : textBoxPower3Watts, comboBoxPower3Mode, modeChk, checkBoxAmp3, checkBoxAmp3.Checked, passNum, textBoxPower3MaxWatts, textBoxTune3Power))
                    return;
                //Debug(DebugEnum.TRACE, "4\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower4Enabled, textBoxPower4From, textBoxPower4To, tuning && use_pwr ? textBoxTune4Power : textBoxPower4Watts, comboBoxPower4Mode, modeChk, checkBoxAmp4, checkBoxAmp4.Checked, passNum, textBoxPower4MaxWatts, textBoxTune4Power))
                    return;
                //Debug(DebugEnum.TRACE, "5\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower5Enabled, textBoxPower5From, textBoxPower5To, tuning && use_pwr ? textBoxTune5Power : textBoxPower5Watts, comboBoxPower5Mode, modeChk, checkBoxAmp5, checkBoxAmp5.Checked, passNum, textBoxPower5MaxWatts, textBoxTune5Power))
                    return;
                //Debug(DebugEnum.TRACE, "6\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower6Enabled, textBoxPower6From, textBoxPower6To, tuning && use_pwr ? textBoxTune6Power : textBoxPower6Watts, comboBoxPower6Mode, modeChk, checkBoxAmp6, checkBoxAmp6.Checked, passNum, textBoxPower6MaxWatts, textBoxTune6Power))
                    return;
                //Debug(DebugEnum.TRACE, "7\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower7Enabled, textBoxPower7From, textBoxPower7To, tuning && use_pwr ? textBoxTune7Power : textBoxPower7Watts, comboBoxPower7Mode, modeChk, checkBoxAmp7, checkBoxAmp7.Checked, passNum, textBoxPower7MaxWatts, textBoxTune7Power))
                    return;
                //Debug(DebugEnum.TRACE, "8\n");
                if (PowerSelectOp(frequencyMHz, checkBoxPower8Enabled, textBoxPower8From, textBoxPower8To, tuning && use_pwr ? textBoxTune8Power : textBoxPower8Watts, comboBoxPower8Mode, modeChk, checkBoxAmp8, checkBoxAmp8.Checked, passNum, textBoxPower8MaxWatts, textBoxTune8Power))
                    return;
                //Debug(DebugEnum.TRACE, "9\n");
                labelPower.Text = "Power not set";
            }
        }

        private void FLRigGetFreq(bool needTuning = true)
        {
            bool isExpertSPE = false;
            if (tuner1 is not null) isExpertSPE = tuner1!.GetModel().Contains(EXPERTLINEARS);
            getFreqIsRunning = true;
            if (!checkBoxRig.Checked || formClosing)
            {
                getFreqIsRunning = false;
                return;
            }
            if (myRig == null)
                return;
            //if (rigStream == null) 
            //    return;
            //rigStream.Flush();
            var frequencyHzSave = frequencyHz;
            frequencyHz = myRig.GetFrequency('A');
            if (Math.Abs(frequencyHz - frequencyHzSave) > 1e6)
                PowerSelect(myRig!.FrequencyA, myRig.ModeA, true, false);
            myRig.GetFrequency('B');
            char currVFO = myRig.VFO;
            if (currVFO == 'B')
            {// could be rigctl temp change to VFOB so should be done in < 1 second
                Thread.Sleep(5000);
                currVFO = myRig.VFO;
            }


            if (currVFO == 'B')
            {
                //ShowCustomMessageBox("Auto tuning paused because VFOB is active, click OK when you're done", MessageBoxButtons.OK);
                DebugAddMsg(DebugEnum.LOG, "VFOB active...pausing\n");
                pausedTuning = true;
                getFreqIsRunning = false;
                Pause();
                return;
                //FLRigSetActiveVFO("A");
            }

            char cvfo = 'A';
            string mode = myRig.VFO == 'A' ? myRig.ModeA : myRig.ModeB;
            labelFreq.Text = (frequencyHz / 1e6).ToString(CultureInfo.InvariantCulture) + " " + mode;
            mode = myRig.ModeA;
            // Any more other than CW we make power MID
            //var pwrLevel = tuner1.GetPowerLevel();
            if (lastmode != mode)
            {
                DebugAddMsg(DebugEnum.LOG, "VFOA mode is " + mode + "\n");
                if (!mode.Contains("CW") && tuner1 is not null && tuner1.GetPowerLevel().Equals("Max"))
                {
                    DebugAddMsg(DebugEnum.VERBOSE, "Power limited to Mid for non-CW mode");
                    tuner1.SetPowerLevel("Mid");
                }
            }
            lastmode = mode;
            if (radioButtonVFOB.Checked) cvfo = 'B';
            try
            {
                try
                {
                    //if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        myRig.GetFrequency(cvfo);
                        frequencyHz = cvfo == 'A' ? myRig.FrequencyA : myRig.FrequencyB;
                        if (frequencyHz == 0)
                        {
                            //DebugAddMsg(DebugEnum.ERR, "FLRig disconnected?");
                            Application.DoEvents();
                            Thread.Sleep(1000);
                            myRig.GetFrequency(cvfo);
                            frequencyHz = cvfo == 'A' ? myRig.FrequencyA : myRig.FrequencyB;
                            //DebugAddMsg(DebugEnum.ERR, "retrying A=" + myRig.FrequencyA + ", B=" + myRig.FrequencyB);
                            myRig.Open();
                            return;
                        }
                        // if our frequency changes by more than 10KHz make VFOB match VFOA
                        if ((myRig.ModeA == "FM" || myRig.ModeA != "CW") && frequencyHz < 60000000 && frequencyLast != 0 && Math.Abs(frequencyHz - frequencyLast) > 200)
                        {
                            DebugAddMsg(DebugEnum.LOG, "Changing VFOB to match last freq " + frequencyLast + " to " + frequencyHz + "\n");
                            myRig.FrequencyB = myRig.FrequencyA;
                        }
                        if (frequencyLast != 0 && Math.Abs(frequencyHz - frequencyLast) > tolTune)
                        {
                            DebugAddMsg(DebugEnum.LOG, "Freq change from " + frequencyLast + " to " + frequencyHz + "\n");
                            //if (frequencyLast == 0) frequencyLast = frequencyLastTunedHz = frequencyHz;
                            //frequencyLast = frequencyHz;
                            PowerSelect(frequencyHz, modeCurrent, tuneIsRunning);
                            if (!pausedTuning) SetAntennaInUseForGUI(frequencyLast != 0);
                        }
                        string modeOld = modeCurrent;
                        modeCurrent = myRig.ModeA; // FLRigGetMode(); // get our current mode now
                        labelFreq.Text = (frequencyHz / 1e6).ToString(CultureInfo.InvariantCulture) + " " + modeCurrent;
                        if (comboBoxPower1Mode.SelectedItem != null && !modeCurrent.Equals(modeOld, StringComparison.InvariantCulture))
                            PowerSelect(frequencyHz, modeCurrent, tuneIsRunning);
                        if (frequencyLast == 0)
                        {
                            needTuning = false;
                            frequencyLast = frequencyLastTunedHz = frequencyHz;
                        }
                        if (frequencyLast == frequencyHz && freqStableCount < freqStableCountNeeded)
                        {
                            ++freqStableCount;
                            stopWatchTuner.Restart();
                        }
                        if (freqStableCount >= freqStableCountNeeded && Math.Abs(frequencyHz - frequencyLastTunedHz) > tolTune && !pausedTuning & !pauseButtonClicked)
                        {
                            DebugAddMsg(DebugEnum.LOG, "Freq diff = " + Math.Abs(frequencyHz - frequencyLastTunedHz) + " tolTune=" + tolTune + "\n");
                            if (!isExpertSPE && stopWatchTuner.IsRunning && stopWatchTuner.ElapsedMilliseconds < 1 * 1000)
                            {
                                stopWatchTuner.Stop();
                                //ShowCustomMessageBox("Rapid frequency changes...click OK when ready to tune");
                                Debug(DebugEnum.ERR, "Rapid frequency changes\n");
                                stopWatchTuner.Reset();
                                stopWatchTuner.Stop();
                                getFreqIsRunning = false;
                                return; // we'll tune on next poll
                            }
                            if (!mode.Equals("USB-D"))
                            {
                                double frequencyHzVFOB = frequencyHz - 1000;
                                if (mode.Contains("LSB") || mode.Contains("-R"))
                                {
                                    frequencyHzVFOB = frequencyHz + 1000;
                                }
                                Debug(DebugEnum.LOG, "VFOB freq change from " + myRig.FrequencyB + " to " + frequencyHzVFOB);
                                myRig.FrequencyB = frequencyHzVFOB;
                                Thread.Sleep(1000);  // give the rig a chance to restore it's band memory
                            }
                            myRig.GetMode('A');
                            myRig.GetMode('B');
                            bool leaveVFOBAlone = false;
                            // Need to check for split and ignore VFOB if not in split
                            if (myRig.ModeA.Equals("USB-D"))
                            {
                                leaveVFOBAlone = true;
                                Debug(DebugEnum.LOG, "Leaving VFOB alone\n");
                            }
                            stopWatchTuner.Restart();
                            if (checkBoxTunerEnabled.Checked && pausedTuning)
                            {
                                // if we're pause we just update this stuff to prevent it from thinking we need to do anything
                                frequencyLastTunedHz = frequencyLast = frequencyHz;
                            }
                            if (checkBoxTunerEnabled.Checked && !pausedTuning && !pauseButtonClicked)
                            {
                                var frequencyHzTune = frequencyHz - 1000;
                                if (mode.Contains("-R") || mode.Contains("LSB"))
                                {
                                    frequencyHzTune = frequencyHz + 1000;
                                }
                                // Set VFO mode to match primary VFO
                                //myRig.ModeB = modeCurrent;
                                //PowerSelect(frequencyHz, modeCurrent);
                                if (needTuning)
                                {
                                    timerGetFreq.Stop();
                                    if (!tuner1!.GetModel().Contains(EXPERTLINEARS))
                                        TuneSequence();
                                    timerGetFreq.Start();
                                    frequencyLastTunedHz = frequencyHz;
                                }
                                // Reset VFOB to same freq as VFOA
                                if (!leaveVFOBAlone)
                                {
                                    Debug(DebugEnum.LOG, "Set VFOB=" + frequencyHz + "\n");
                                    myRig.FrequencyB = frequencyHz;
                                }
                            }
                            else if (!pausedTuning && !pauseButtonClicked)
                            {
                                Debug(DebugEnum.ERR, "Tuner not enabled\n");
                                Debug(DebugEnum.ERR, "Simulate tuning to " + frequencyHz + "\n");
                                char vfoOther = 'A';
                                if (radioButtonVFOA.Checked) vfoOther = 'B';
                                myRig.SetFrequency(vfoOther, frequencyHz);
                                myRig.SetMode(vfoOther, modeCurrent);
                                frequencyLastTunedHz = frequencyLast = frequencyHz;
                                freqStableCount = 0;
                            }
                        }
                        else
                        {
                            frequencyLast = frequencyHz;
                        }
                    }
                }
                catch (Exception)
                {
                    Debug(DebugEnum.ERR, "Error in " + System.Reflection.MethodBase.GetCurrentMethod().Name + "\n");
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
            if (frequencyHz != 0) frequencyLast = frequencyHz;
            getFreqIsRunning = false;
        }

        public static String GetCommandLines(Process process)
        {
            Contract.Requires(process != null);
            ManagementObjectSearcher commandLineSearcher = new(
                "SELECT CommandLine FROM Win32_Process WHERE ProcessId = " + process!.Id);
            String commandLine = "";
            foreach (ManagementObject commandLineObject in commandLineSearcher.Get().Cast<ManagementObject>())
            {
                commandLine += (String)commandLineObject["CommandLine"];
            }
            commandLineSearcher.Dispose();
            return commandLine;
        }
        private DialogResult RelayOops(String message)
        {
            //Debug(DebugEnum.ERR, message);
            return ShowCustomMessageBox(message);
        }

        private string SWRUpdate()
        {
            string SWR = "0.00";
            if (tuner1 is null)
            {
                MessageBox.Show("tuner1 is null???");
                return SWR;
            }
            if (myRig is null)
            {
                MessageBox.Show("myRig is null???");
                return SWR;
            }
            if (tuner1.GetModel().Contains(EXPERTLINEARS))
            {
                //string myswr = "SWR " + myRig.SWR;
                if (!tuner1.IsOn)
                    labelSWR.Text = "SWR " + myRig.SWR.ToString("F2");
                else if (tuner1.SWRATU > 0)
                    labelSWR.Text = "SWR " + tuner1.SWRATU.ToString("F2");
                else  SWR = tuner1.GetSWRString();
            }
            Application.DoEvents();
            return SWR;
        }
        private void ClockSetGUI()
        {
            var myTime = MyTime();
            var z = clockIsZulu ? "Z" : "";
            labelClock.Text = myTime.Substring(0, 8) + z;
        }
        private void TimerGetFreq_Tick(object sender, EventArgs e)
        {
            if (myRig is null)
            {
                return;
            }
            // Do we need to let the use choose what power to show?
            //labelPower.Text = "";
            if (Monitor.TryEnter(timerGetFreqMutex))
            {
                frequencyHz = myRig.GetFrequency('A');
                if (tuner1 is not null && tuner1.IsOperate && tuner1.IsOn)
                {
                    buttonTune.Enabled = false;
                    buttonOperate.BackColor = System.Drawing.Color.Green;
                    buttonOperate.ForeColor = System.Drawing.Color.White;
                }
                else
                {
                    buttonTune.Enabled = true;
                    buttonOperate.BackColor = System.Drawing.Color.LightGray;
                    buttonOperate.ForeColor = System.Drawing.Color.Black;
                }
                if (labelFreq != null && myRig != null)
                    labelFreq.Text = (myRig.FrequencyA / 1e6).ToString(CultureInfo.InvariantCulture) + " " + myRig.ModeA;

                if (tuner1 != null && tuner1.IsOn)
                {
                    buttonTunerPwr.BackColor = System.Drawing.Color.Green;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.White;
                }
                else
                {
                    buttonTunerPwr.BackColor = System.Drawing.Color.LightGray;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.Black;
                }
                if (tuner1 != null && tuner1.GetModel().Contains(EXPERTLINEARS))
                {
                    TunerSetAntennaSPE();
                    if (buttonPowerLevel.Visible == false)
                    {
                        buttonPowerLevel.Visible = true;
                    }
                    buttonPowerLevel.Text = tuner1.GetPowerLevel();
                    buttonPowerLevel.ForeColor = System.Drawing.Color.White;
                    buttonPowerLevel.BackColor = System.Drawing.Color.Green;
                    if (buttonPowerLevel.Text == "Max")
                    {
                        buttonPowerLevel.ForeColor = System.Drawing.Color.Black;
                        buttonPowerLevel.BackColor = System.Drawing.Color.Red;
                    }
                    else if (buttonPowerLevel.Text == "Mid")
                    {
                        buttonPowerLevel.ForeColor = System.Drawing.Color.Black;
                        buttonPowerLevel.BackColor = System.Drawing.Color.Yellow;
                    }
                    {

                        radioButtonBankA.CheckedChanged -= RadioButtonBankA_CheckedChanged;
                    }
                    radioButtonBankB.CheckedChanged -= RadioButtonBankB_CheckedChanged;
                    if (tuner1.bank == 'A')
                    {
                        radioButtonBankA.Checked = true;
                        radioButtonBankB.Checked = false;
                    }
                    else if (tuner1.bank == 'B')
                    {
                        radioButtonBankA.Checked = false;
                        radioButtonBankB.Checked = true;
                    }
                    radioButtonBankA.CheckedChanged += RadioButtonBankA_CheckedChanged;
                    radioButtonBankB.CheckedChanged += RadioButtonBankB_CheckedChanged;

                }
                ClockSetGUI();
                if (tuner1 != null && !tuner1.poweringDown && tuner1.IsOn) SetAntennaInUseForGUI(false, tuner1.AntennaNumber);
                if (pausedTuning)
                {
                    //timerGetFreq.Enabled = true;
                    Monitor.Exit(timerGetFreqMutex);
                    return;
                }
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
                if (checkBoxRelay2Enabled.Checked && relay2 != null && relay1!.Status() == 0xff)
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
                if (checkBoxRelay3Enabled.Checked && relay3 != null && relay1!.Status() == 0xff)
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
                if (checkBoxRelay4Enabled.Checked && relay4 != null && relay1!.Status() == 0xff)
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
                int power = 0;
                if (myRig is not null)
                { 
                    power = myRig.GetPower();
                    if (myRig.MaxPower > 0 && power > myRig.MaxPower)
                    {
                        //myRig.SetPower(myRig.MaxPower);
                    }
                }

                //if (tuner1 == null || tuner1.GetComPort() == null) return;
                if (tuner1 != null)
                {
                    string SWR = tuner1.GetSWRString();
                    if (!tuner1.IsOn || tuner1.poweringDown) return;
                    if (tuner1.IsOn && tuner1.GetModel().Contains(EXPERTLINEARS))
                    {
                        SWR = SWRUpdate();
                        if (!tuner1.IsOn || tuner1.poweringDown) return;
                        AntennaUpdateSelected(tuner1.AntennaNumber);
                        /*string myswr = "SWR " + myRig.SWR;
                        if (!tuner1.IsOn) 
                            labelSWR.Text = "SWR " + myRig.SWR.ToString("F2");
                        else if (tuner1.SWRATU > 0) 
                            labelSWR.Text = "SWR " + tuner1.SWRATU.ToString("F2");
                        */
                    }
                    if (tuner1.GetModel().Equals(MFJ928, StringComparison.InvariantCulture))
                    {
                        /*
                        decimal Inductance = tuner1.GetInductance();
                        int Capacitance = tuner1.GetCapacitance();
                        if (numericUpDownCapacitance.Value != Capacitance)
                        {
                            numericUpDownCapacitance.Enabled = false;
                            numericUpDownCapacitance.Value = Capacitance;
                            Application.DoEvents();
                            numericUpDownCapacitance.Enabled = true;
                        }
                        if (numericUpDownInductance.Value != Inductance)
                        {
                            numericUpDownInductance.Enabled = false;
                            numericUpDownInductance.Value = Convert.ToDecimal(Inductance) / 10;
                            Application.DoEvents();
                            numericUpDownInductance.Enabled = true;
                        }
                        */
                        Application.DoEvents();
                        if (_disposed) return;
                        labelSWR.Text = SWR;
                        if (tuner1.GetSWR() == 0)
                        {
                            labelSWR.Text = "SWR Unknown";
                            buttonTunerStatus.BackColor = System.Drawing.Color.Gray;
                        }
                        else if (tuner1.GetSWR() < 2.0)
                        {
                            buttonTunerStatus.BackColor = System.Drawing.Color.Green;
                        }
                        else if (tuner1.GetSWR() < 3)
                        {
                            buttonTunerStatus.BackColor = System.Drawing.Color.Yellow;
                        }
                        else
                        {
                            buttonTunerStatus.BackColor = System.Drawing.Color.Red;
                        }
                    }
                }
                if (tuner1 != null)
                {
                    string FwdPwr = tuner1.GetPower();
                    if (FwdPwr != null) labelPower.Text = FwdPwr;
                }
                /*
                if (checkBoxTunerEnabled.Checked)
                {
                    if (comboBoxTunerModel.Text.Equals(MFJ928))
                    {
                        numericUpDownCapacitance.Visible = true;
                        numericUpDownInductance.Visible = true;
                        buttonTunerSave.Visible = true;
                        checkBox1.Visible = false;
                        groupBoxOptions.Enabled = true;
                        groupBoxOptions.Visible = true;
                    }
                    else if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
                    {
                        numericUpDownCapacitance.Visible = false;
                        numericUpDownInductance.Visible = false;
                        buttonTunerSave.Visible = false;
                        checkBox1.Visible = false;
                        groupBoxOptions.Visible = true;
                    }

                }*/
                //timerGetFreq.Enabled = true;
                if (checkBoxRig.Checked && myRig is not null)
                {
                    var myFreq = myRig.FrequencyA;
                    FLRigGetFreq();
                    var myPower = myRig.GetPower();
                    /*
                    if (power > maxRigPower)
                    {
                        myRig.Power = maxRigPower;
                    }
                    */
                    // MDB
                    //if (myFreq != myRig.FrequencyA)
                    //{
                    //    timerFreqWalk.Enabled = false;
                    //}

                    //timerGetFreq.Interval = 200;
                    //DebugAddMsg(DebugEnum.LOG, "Get freq");
                }
                else
                {
                    labelFreq!.Text = "?";
                }
                Monitor.Exit(timerGetFreqMutex);
            }
        }

        private void CheckBox1_CheckedChanged(object sender, EventArgs e)
        {
            if (formLoading) return;
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
            char[] sep = [' '];
            string[] tokens = s.Split(sep);
            int i = 0;
            List<byte> value = [];
            List<byte> dataList = value;
            foreach (string t in tokens)
            {
                try
                {
                    dataList.Add(Convert.ToByte(t, 16));
                    ++i;
                }
                catch (Exception)
                {
                    data = [.. dataList];
                    return false;
                }
            }
            data = [.. dataList];
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

            if (!ToHex(s, out _))
            {
                ShowCustomMessageBox("Invalid command format\nExpected string or hex values e.g. 0x08 0x01");
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
                if (relay1 != null)
                    RelaySet(relay1, i, 1);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
            for (int i = 1; i <= nRelays; ++i)
            {
                Debug(DebugEnum.LOG, i + " Off\n");
                //relay1.Set(i, 0);
                if (relay1 != null)
                    RelaySet(relay1, i, 0);
                Application.DoEvents();
                Thread.Sleep(1000);
            }
        }

#pragma warning disable IDE0060 // Remove unused parameter
        private bool RelaySet(Relay relay, int nRelay, int flag, bool force = false, [CallerMemberName] string name = "",
                   [CallerLineNumber] int line = -1)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "RelaySet(" + nRelay + "," + flag + ") called from " + name + "@line(" + line + ")\n");
            //if (!relay.IsOpen())
            //{
            //    ShowCustomMessageBox("Relay is not open");
            //    return false;
            //}
            if (relay == null)
            {
                ShowCustomMessageBox("Relay#" + nRelay + " is not configured!!");
                return false;
            }
            Button button; // the button we will mess with here to change color
            List<Button> buttons = [];
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
                ShowCustomMessageBox("No relays open?");
                return false;
            }
            button = buttons[nRelay - 1];
            if (flag == 0) button.BackColor = System.Drawing.Color.Gray;
            else button.BackColor = System.Drawing.Color.Green;
            relay.Set(nRelay, (byte)flag);
            if (nRelay == 1) buttonAmp.BackColor = flag == 1 ? System.Drawing.Color.Green : System.Drawing.Color.Yellow;
            if (relay.errMsg != null)
            {
                ShowCustomMessageBox(relay.errMsg);
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
                ShowCustomMessageBox("Tuner not enabled");
                return;
            }
            DebugAddMsg(DebugEnum.TRACE, "Tune started\n");
            if (myRig is not null)
            {
                DebugAddMsg(DebugEnum.VERBOSE, "Tune#1 ModeA=" + myRig.ModeA + ", " + "ModeB=" + myRig.ModeB + "\n");
            }

            Cursor.Current = Cursors.WaitCursor;
            if (ModifierKeys == Keys.Control && comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
            {
                
                tuner1!.Operate(false);
                tuner1!.GetStatus();
                timerGetFreq.Enabled = false;
                Thread.Sleep(1000);
                PowerSelect(myRig!.FrequencyA, "FM", true);
                buttonTune.BackColor = System.Drawing.Color.Red;
                buttonTune.Enabled = false;
                Application.DoEvents();
                tuner1!.TuneCurrentBand(true);
                buttonTune.Enabled = true;
                buttonTune.BackColor = System.Drawing.Color.LightGray;
                timerGetFreq.Enabled = true;
            }

            if (ModifierKeys == Keys.Alt && comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
            {
                tuner1!.Operate(false);
                tuner1!.GetStatus();
                timerGetFreq.Enabled = false;
                Thread.Sleep(1000);
                PowerSelect(myRig!.FrequencyA, "FM", true);
                buttonTune.BackColor = System.Drawing.Color.Red;
                buttonTune.Enabled = false;
                Application.DoEvents();
                tuner1!.TuneCurrentBand(false); // Let the ATU do the tune
                buttonTune.Enabled = true;
                buttonTune.BackColor = System.Drawing.Color.LightGray;
                timerGetFreq.Enabled = true;
                return;
            }
            // regular tune 

            if (freqWalkIsRunning)
            {
                DebugMsg.DebugAddMsg(DebugEnum.LOG, "FrequencyWalk Stop Request#1");
                FreqWalkStop();
            }
            //if (!relay1.IsOK())
            //{
            //    ShowCustomMessageBox("Relay1 is not communicating?");
            //    return;
            //}
            // MDB need to allow for different line in Power tab to check amplifier usage
            //Cursor.Current = Cursors.WaitCursor;
            timerGetFreq.Stop();
            timerSWR.Start();
            //if (!tuner1.GetModel().Contains(EXPERTLINEARS))
            TuneSequence();
            timerSWR.Stop();
            timerGetFreq.Start();
            //Cursor.Current = Cursors.Default;
            //if (checkBoxAmp1.Checked) relay1.Set(1, 0);
            Cursor.Current = Cursors.Default;
        }

        public static string FLRigXML(string cmd, string value)
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

        private DialogResult ShowCustomMessageBox(string message, string title = "AmpAutoTunerUtil", MessageBoxButtons buttons=MessageBoxButtons.OK)
        {
            using var customMessageBox = new CustomMessageBox(message, title, buttons);
            return customMessageBox.ShowDialog(this);
        }
        /*
        public DialogResult ShowCustomMessageBox(string message, MessageBoxButtons buttons = MessageBoxButtons.RetryCancel)
        {
            if (this.WindowState == FormWindowState.Minimized)
                this.WindowState = FormWindowState.Normal;
            //MessageBoxHelper.PrepToCenterMessageBoxOnForm(this);
            DebugAddMsg(DebugEnum.ERR, message);
            Application.DoEvents();
            //MessageBox.Show(message,Application.ProductName);
            var myForm = new Form { TopMost = true };
            var result = MessageBox.Show(myForm, message, "AmpAutoTunerUtility", buttons);
            myForm.Dispose();
            return result;
        }
        */
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
                    //    ShowCustomMessageBox("Tuner open failed");
                    //    return;
                    //}
                    //richTextBoxRig.AppendText("Tuner opened\n");
                    checkBoxTunerEnabled.Enabled = true;
                }
                catch (Exception ex)
                {
                    checkBoxTunerEnabled.Checked = false;
                    Debug(DebugEnum.ERR, "Tuner error:" + ex.Message + "\n");
                    labelControlLog.Text = "Tuner error";
                    //ShowCustomMessageBox("Error starting tuner\nFix problem and reenable the Tuner" + ex.Message);
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
                //if (relay1 != null) ShowCustomMessageBox("Relay1 != null??");
                relay1?.Close();
                relay1 = new Relay();
                Debug(DebugEnum.LOG, "Relay1 open\n");
                relay1.Open(comboBoxComRelay1.Text);
                RelaySetButtons(button1_1, relay1.Status());

                Debug(DebugEnum.LOG, "Relay1 serial #" + relay1.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay1Enabled.Checked && comboBoxComRelay1.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay1 closed\n");
                //checkBoxRelay1Enabled.Enabled = false;
                relay1?.Close();
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
                relay2?.Open(comboBoxComRelay2.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay2?.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay2 closed\n");
                relay2?.Close();
            }
            else if (checkBoxRelay2Enabled.Checked && comboBoxComRelay2.SelectedIndex < 0)
            {
                ShowCustomMessageBox("Select COM port before enabling Relay2");
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
            if (relayButton.BackColor == System.Drawing.Color.Gray)
            {
                RelaySet(relay, nRelay, 1);
            }
            else
            {
                RelaySet(relay, nRelay, 0, true);
            }
        }

        private void Button1_1_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 1);
        }

        private void Button1_2_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 2);
        }

        private void Button1_3_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 3);
        }

        private void Button1_4_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 4);
        }

        private void Button1_5_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 5);
        }

        private void Button1_6_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 6);
        }

        private void Button1_7_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 7);
        }

        private void Button1_8_Click(object sender, EventArgs e)
        {
            if (relay1 != null)
                RelayToggle((Button)sender, relay1, 8);
        }


        private void Button25_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 1);
        }

        private void Button24_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 2);
        }

        private void Button23_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 3);
        }

        private void Button22_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 4);
        }

        private void Button21_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 5);
        }

        private void Button20_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 6);
        }

        private void Button19_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
                RelayToggle((Button)sender, relay2, 7);
        }

        private void Button18_Click(object sender, EventArgs e)
        {
            if (relay2 != null)
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
                buttonTunePause.BackColor = System.Drawing.Color.Red;
                buttonTunePause.ForeColor = System.Drawing.Color.White;
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
                buttonTunePause.BackColor = System.Drawing.Color.Green;
                buttonTunePause.ForeColor = System.Drawing.Color.White;
                //buttonTune.Enabled = true;
                labelSWR.Text = "SWR";
                timerGetFreq.Stop();
                FLRigGetFreq(false);
                if (tuner1 != null && !tuner1.GetModel().Contains(EXPERTLINEARS))
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
                    if (myRig is not null) myRig.VFO = 'A';
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

        private void CheckEnable(ComboBox box1, ComboBox? box2, CheckBox check1)
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
            ShowCustomMessageBox("Baud not implemented");
            CheckEnable(comboBoxComRelay1, null, checkBoxRelay1Enabled);
        }

        private void ComboBoxComRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
            CheckEnable(comboBoxComRelay2, null, checkBoxRelay2Enabled);
        }

        private void ComboBoxBaudRelay2_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowCustomMessageBox("Baud not implemented");
            CheckEnable(comboBoxComRelay2, comboBoxBaudRelay2, checkBoxRelay2Enabled);
        }

        private void ComboBoxComRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowCustomMessageBox("Check Relay3 implementaion");
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxBaudRelay3_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowCustomMessageBox("Check Relay3 implementaion");
            CheckEnable(comboBoxComRelay3, comboBoxBaudRelay3, checkBoxRelay3Enabled);
        }

        private void ComboBoxComRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowCustomMessageBox("Check Relay4 implementaion");
            CheckEnable(comboBoxComRelay4, comboBoxBaudRelay4, checkBoxRelay4Enabled);
        }

        private void ComboBoxBaudRelay4_SelectedIndexChanged(object sender, EventArgs e)
        {
            ShowCustomMessageBox("Check Relay4 implementaion");
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
                relay4?.Open(comboBoxComRelay4.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay4?.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay4 closed\n");
                relay4?.Close();
            }
            else if (checkBoxRelay4Enabled.Checked && comboBoxComRelay4.SelectedIndex < 0)
            {
                ShowCustomMessageBox("Select COM port before enabling Relay4");
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
                ShowCustomMessageBox("Power tab entry #1 values are not valid");
                checkBoxPower1Enabled.Checked = false;
            }
        }

        private void CheckBoxPower2Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower2From, textBoxPower2To, textBoxPower2Watts))
            {
                ShowCustomMessageBox("Power tab entry #2 values are not valid");
                checkBoxPower2Enabled.Checked = false;
            }
        }

        private void CheckBoxPower3Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower3From, textBoxPower3To, textBoxPower3Watts))
            {
                ShowCustomMessageBox("Power tab entry #3 values are not valid");
                checkBoxPower3Enabled.Checked = false;
            }
        }

        private void CheckBoxPower4Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower4From, textBoxPower4To, textBoxPower4Watts))
            {
                ShowCustomMessageBox("Power tab entry #4 values are not valid");
                checkBoxPower4Enabled.Checked = false;
            }
        }

        private void CheckBoxPower5Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower5From, textBoxPower5To, textBoxPower5Watts))
            {
                ShowCustomMessageBox("Power tab entry #5 values are not valid");
                checkBoxPower5Enabled.Checked = false;
            }
        }

        private void CheckBoxPower6Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower6From, textBoxPower6To, textBoxPower6Watts))
            {
                ShowCustomMessageBox("Power tab entry #6 values are not valid");
                checkBoxPower6Enabled.Checked = false;
            }
        }

        private void CheckBoxPower7Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower7From, textBoxPower7To, textBoxPower7Watts))
            {
                ShowCustomMessageBox("Power tab entry #7 values are not valid");
                checkBoxPower7Enabled.Checked = false;
            }
        }

        private void CheckBoxPower8Enabled_CheckedChanged(object sender, EventArgs e)
        {
            if (!((CheckBox)sender).Checked) return;
            if (!AntennaTabCheckValues(textBoxPower8From, textBoxPower8To, textBoxPower8Watts))
            {
                ShowCustomMessageBox("Power tab entry #8 values are not valid");
                checkBoxPower8Enabled.Checked = false;
            }
        }

        private void TimerDebug_Tick(object sender, EventArgs e)
        {
            timerDebug.Enabled = false;
            GC.Collect(0);
            GC.WaitForPendingFinalizers();
            //if (tuner1 == null) { timerDebug.Enabled = true; return; }
            relay1?.Status();
            if (checkBoxPause.Checked) { timerDebug.Enabled = true; return; }
            DebugMsg? msg = DebugGetMsg();
            debugLevel = (DebugEnum)comboBoxDebugLevel.SelectedIndex + 1;
            if (debugLevel < 0) debugLevel = DebugEnum.WARN;
            tuner1?.SetDebugLevel(debugLevel);
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
                    if (richTextBoxDebug.Disposing) return;
                    richTextBoxDebug.SelectionStart = 0;
                    richTextBoxDebug.ScrollToCaret();
                    while (richTextBoxDebug.Lines.Length > 5000)
                    {
                        richTextBoxDebug.Select(0, richTextBoxDebug.GetFirstCharIndexFromLine(1));
                        richTextBoxDebug.SelectedText = "";
                    }
                    richTextBoxDebug.Select(richTextBoxDebug.Text.Length, 0);
                }
                //Debug(msg.Level, msg.Text);
                msg = DebugGetMsg();
            }
            timerDebug.Enabled = true;
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
            //Cursor.Current = Cursors.WaitCursor;
            timerDebug.Stop();
            timerGetFreq.Stop();
            timerFreqWalk.Stop();
            timerDebug.Enabled = false;
            timerGetFreq.Enabled = false;
            timerFreqWalk.Enabled = false;
            Application.Exit();
        }
        private void CloseToolStripMenuItem_Click(object sender, EventArgs e)
        {
            //Cursor.Current = Cursors.WaitCursor;
            Exit();
        }

        private void ComboBoxTunerModel_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (comboBoxTunerModel.Text.Equals(MFJ928))
            {
                numericUpDownCapacitance.Visible = true;
                numericUpDownInductance.Visible = true;
                buttonTunerSave.Visible = true;
                checkBox1.Visible = false;
                groupBoxOptions.Enabled = true;
                groupBoxOptions.Visible = true;
            }
            else //if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
            {
                numericUpDownCapacitance.Visible = false;
                numericUpDownInductance.Visible = false;
                buttonTunerSave.Visible = false;
                checkBox1.Visible = false;
                groupBoxOptions.Enabled = true;
                groupBoxOptions.Visible = true;
            }
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
                relay3?.Open(comboBoxComRelay3.Text);
                Debug(DebugEnum.LOG, "Serial #" + relay3?.SerialNumber() + "\n");
            }
            else if (!checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex >= 0)
            {
                Debug(DebugEnum.LOG, "Relay3 closed\n");
                relay3?.Close();
            }
            else if (checkBoxRelay3Enabled.Checked && comboBoxComRelay3.SelectedIndex < 0)
            {
                ShowCustomMessageBox("Select COM port before enabling Relay3");
                checkBoxRelay3Enabled.Checked = false;
            }

        }

        private void Relay1ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (relay1ToolStripMenuItem.Checked)
            {
                relay1ToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageRelay1);
            }
            else
            {
                relay1ToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageRelay1);
            }
            //relay1ToolStripMenuItem.Checked = !relay1ToolStripMenuItem.Checked;
            //tabPage.TabPages.Remove(tabPageRelay1);
            //if (relay1ToolStripMenuItem.Checked && tabControl1.TabPages.IndexOf(tabPageRelay1) < 0)
            //{
            //tabPage.TabPages.Add(tabPageRelay1);
            //}
        }

        private void Relay2ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (relay2ToolStripMenuItem.Checked)
            {
                relay2ToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageRelay2);
            }
            else
            {
                relay2ToolStripMenuItem.Checked = true;
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
                default: ShowCustomMessageBox("Invalid debug level?  level=" + comboBoxDebugLevel.SelectedIndex); break;
            }
        }
        private void AboutToolStripMenuItem1_Click(object sender, EventArgs e)
        {
            try
            {
                aboutForm ??= new AboutForm();
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
                helpForm ??= new HelpForm();
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
            //if (comboBoxTunerModel.Text.Length > 0 && comboBoxComTuner.Text.Length > 0)
            //{
            //    checkBoxTunerEnabled.Checked = true;
            //}
            //else
            //{
            //    checkBoxTunerEnabled.Checked = false;
            //}

        }

        private void ComboBoxComTuner_SelectedIndexChanged_1(object sender, EventArgs e)
        {
            if (formLoading || formClosing) return;
            /*
            if (comboBoxTunerModel.Text.Length > 0 && comboBoxComTuner.Text.Length > 0)
            {
                checkBoxTunerEnabled.Checked = true;
            }
            else
            {
                checkBoxTunerEnabled.Checked = false;
            }
            //string s = comboBoxTunerModel.SelectedIndex+"\n"+comboBoxBaudTuner.SelectedIndex+"\n";
            //ShowCustomMessageBox(s);
            */
        }

        private void CheckBoxTunerEnabled_CheckedChanged_1(object sender, EventArgs e)
        {
            buttonTunerPwr.Enabled = checkBoxTunerEnabled.Checked;
            if (activatedHasExecuted && checkBoxTunerEnabled.Checked)
            {
                Cursor.Current = Cursors.WaitCursor;
                checkBoxTunerEnabled.Refresh();
                Application.DoEvents();
                TunerOpen();
                Cursor.Current = Cursors.Default;
            }
            else if (!checkBoxTunerEnabled.Checked) TunerClose();
        }

        private void Form1_Activated(object sender, EventArgs e)
        {
            if (activatedHasExecuted) return;
            activatedHasExecuted = true;
            TunerOpen();
            FLRigConnect();
            FLRigGetFreq();
            SetAntennaInUseForGUI(true);
            this.Refresh();
            UpdateAntennaFrequencies();
        }

        private void RichTextBoxDebug_TextChanged(object sender, EventArgs e)
        {

        }

        private void AmpSetButton()
        {
            if (ampIsOn)
            {
                buttonAmp.BackColor = System.Drawing.Color.Green;
                buttonAmp.ForeColor = System.Drawing.Color.White;
            }
            else
            {
                buttonAmp.BackColor = System.Drawing.Color.Yellow;
                buttonAmp.ForeColor = System.Drawing.Color.Black;
            }
        }

        // private bool AmpIsOn()
        // {
        //     return ampIsOn;
        // }

        private void AmpSet(bool flag, [CallerMemberName] string name = "",
                   [CallerLineNumber] int line = -1)
        {
            if (relay1 == null || relay1.AmpRelay == 0 || checkBoxRelay1Enabled.Checked == false) return;
            try
            {
                DebugAddMsg(DebugEnum.TRACE, "AmpSet flag=" + flag + " called from " + name + "@line(" + line + ")\n");
                // false means turn amp off, true means turn it on
                // depends on how relay is wired
                // this implementation assumes wired on NC (normally closed)
                // so setting relay=1 opens the connection, disconnecting the amp
                if (tuner1 == null) return;
                int onOff = flag == true ? 0 : 1;
                //tuner1.CMDAmp((byte)!bit);
                if (relay1 != null)
                {
                    // MDB this needs to be more robust
                    // For now 8/2 is the only exception for the Denkovi 4-channel relay
                    // Otherwise amp should be on 8/0
                    var s = comboBoxAmpBits.Text;
                    var tokens = s.Split('/');
                    if (s.Length == 2 && int.TryParse(tokens[1], out int ampRelayBit))
                    {
                        relay1.AmpRelay = (byte)ampRelayBit;
                        Debug(DebugEnum.LOG, "Calling RelaySet ampStatus=" + onOff + "\n");
                        //RelaySet(relay1, ampRelayBit, onOff);
                        relay1.Set(ampRelayBit, (byte)onOff);
                    }
                }
                ampIsOn = onOff == 0;
                AmpSetButton();
            }
            catch (Exception ex)
            {
                MessageBox.Show("AmpSet error!\n" + ex.Message + "\n" + ex.StackTrace);
            }
            Application.DoEvents();
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

#pragma warning disable IDE0060 // Remove unused parameter
        private void AntennaSet(Relay relay, int bits)
#pragma warning restore IDE0060 // Remove unused parameter
        {
            int val = 0;
            //relay.AllOff();
            relay.Set(1, (byte)val);
        }

        private void CheckBoxAntenna1_CheckedChanged(object sender, EventArgs e)
        {
            Relay? relay = null;
            if (comboBoxAntenna1Controller.Text.Equals("Relay1", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay1;
            }
            if (comboBoxAntenna1Controller.Text.Equals("Relay2", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay2;
            }
            if (comboBoxAntenna1Controller.Text.Equals("Relay3", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay3;
            }
            if (comboBoxAntenna1Controller.Text.Equals("Relay4", StringComparison.OrdinalIgnoreCase))
            {
                relay = relay4;
            }
            if (relay != null)
            {
                AntennaSet(relay, 1);
                if (checkBoxAntenna1.Checked) AntennaSet(relay, 1);
                else AntennaSet(relay, 0);
            }
            UpdateAntennaFrequencies();
            UpdateAntennasInPower();
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
                ShowCustomMessageBox("Unable to open link that was clicked.");
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
            tuner1?.Save();
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
                tuner1?.SetCapacitance(Convert.ToInt32(obj.Value));
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
                tuner1?.SetInductance(obj.Value);
            }
        }

        private void TabControl1_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (sender is null) return;
            if (sender is not TabControl tabControl) return;
            TabPage current = tabControl.SelectedTab;
            if (current.Text.Equals("Relay1")) Relay1_LoadComPorts();
            if (current.Text.Equals("Debug"))
                richTextBoxDebug.Refresh();
            current.Refresh();
            AntennaSetPickButtons();
        }

        private void NumericUpDownPostPttDelay_ValueChanged(object sender, EventArgs e)
        {

        }

        private void CheckBox1_CheckedChanged_1(object sender, EventArgs e)
        {
            if (tuner1 == null)
            {
                return;
            }
            tuner1.TuneFull = checkBox1.Checked;
        }

        private void TabPageTuner_Click(object sender, EventArgs e)
        {
            LoadComPorts();
        }

        private void ComboBoxComRelay1_SelectedIndexChanged_1(object sender, EventArgs e)
        {

        }

        private void Relay1_LoadComPorts()
        {
            comboBoxComRelay1.Items.Clear();
            if (relay1 == null || !relay1.IsOpen()) return;
            List<string> comPorts = relay1.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay1.Items.Add(comPort);
            }
        }
        private void TabPageRelay1_Click(object sender, EventArgs e)
        {
            Relay1_LoadComPorts();
        }

        private void TabPageRelay2_Click(object sender, EventArgs e)
        {
            comboBoxComRelay2.Items.Clear();
            if (relay2 == null) return;
            List<string> comPorts = relay2.ComList();
            if (comPorts == null) return;
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay2.Items.Add(comPort);
            }
        }

        private void TabPageRelay3_Click(object sender, EventArgs e)
        {
            comboBoxComRelay3.Items.Clear();
            if (relay3 == null) return;
            List<string> comPorts = relay3.ComList();
            foreach (string comPort in comPorts)
            {
                comboBoxComRelay3.Items.Add(comPort);
            }

        }

        private void TabPageRelay4_Click(object sender, EventArgs e)
        {
            comboBoxComRelay4.Items.Clear();
            if (relay4 == null) return;
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
            if (tabPage.SelectedIndex == 0)
            {
                AntennaSetPickButtons();
            }
        }

        private void TextBoxAntenna1Bits_TextChanged(object sender, EventArgs e)
        {

        }

        private void ButtonAllOff1_Click(object sender, EventArgs e)
        {
            if (relay1 == null) return;
            relay1.AllOff();
            RelaySetButtons(button1_1, relay1.Status());

        }

        private void ButtonAllOff2_Click(object sender, EventArgs e)
        {
            if (relay2 == null) return;
            relay2.AllOff();
            RelaySetButtons(button2_1, relay2.Status());
        }

        private void ButtonAllOff3_Click(object sender, EventArgs e)
        {
            if (relay3 == null) return;
            relay3.AllOff();
            RelaySetButtons(button3_1, relay3.Status());
        }
        private void ButtonAllOff4_Click(object sender, EventArgs e)
        {
            if (relay4 == null) return;
            relay4.AllOff();
            RelaySetButtons(button4_1, relay4.Status());
        }

        private void ComboBoxAntenna1Bits_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetAntennaInUseForGUI(true);
        }

        private void TextBoxAntennaFreq1From_Leave(object sender, EventArgs e)
        {
            SetAntennaInUseForGUI(true);
        }

        private void ComboBoxAntSelect1_SelectedIndexChanged(object sender, EventArgs e)
        {
            SetAntennaInUseForGUI(true);
        }

        private void CheckBoxAntenna2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void SetWalkAntennaAsActive()
        {
            SetAntennaInUseForGUI(true);
            //SetAntennaRelayOn(Properties.Settings.Default.WalkAntenna);
        }
        private void SetWalkAntennaToUse(int antennaNumber)
        {
            try
            {
                textBoxAntenna1.BackColor = System.Drawing.Color.White;
                textBoxAntenna2.BackColor = System.Drawing.Color.White;
                textBoxAntenna3.BackColor = System.Drawing.Color.White;
                textBoxAntenna4.BackColor = System.Drawing.Color.White;
                textBoxAntenna5.BackColor = System.Drawing.Color.White;
                textBoxAntenna6.BackColor = System.Drawing.Color.White;
                textBoxAntenna7.BackColor = System.Drawing.Color.White;
                textBoxAntenna8.BackColor = System.Drawing.Color.White;
                switch (antennaNumber)
                {
                    case 1:
                        textBoxAntenna1.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 2:
                        textBoxAntenna2.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 3:
                        textBoxAntenna3.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 4:
                        textBoxAntenna4.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 5:
                        textBoxAntenna5.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 6:
                        textBoxAntenna6.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 7:
                        textBoxAntenna7.BackColor = System.Drawing.Color.Yellow;
                        break;
                    case 8:
                        textBoxAntenna8.BackColor = System.Drawing.Color.Yellow;
                        break;
                }
                Properties.Settings.Default.WalkAntenna = antennaNumber;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }


        //readonly int[] frequenciesToWalk = { 50313000, 28074000, 24915000, 21074000, 18100000, 14074000, 10136000, 7074000, 3573000   };
        //readonly string frequenciesToWalkFT8 = "1.840 3.573 7.074 10.136 14.074 18.100 21.074  24.915 28.074 50.313";
        //readonly string frequenciesToWalkFT4 = "3.575 7.0475 10.14 14.08 18.104 21.140 24.919 28.180 50.318";
        List<long> frequenciesToWalk;
        int frequencyIndex = 0;
        private bool dummyLoad;
        private int timerFreqWalkSeconds;
        private bool antennaLocked;
        private string lastmode = "";
        private readonly object timerFreqWalkMutex = new();
        private readonly object timerGetFreqMutex = new();

        private void FreqWalkSetFreq(int index)
        {
            try
            {
                if (frequenciesToWalk == null) return;
                if (frequenciesToWalk.Count == 0)
                {
                    DebugMsg.DebugAddMsg(DebugEnum.LOG,"There are no frequencies to walk");
                    return;
                }
                if (myRig == null) return;
                //if (index == 0) richTextBoxFreqWalk.Clear();
                if (index < 0 || index >= frequenciesToWalk.Count)
                {
                    //MessageBox.Show("walk index " + index + " outside " + frequenciesToWalk.Count + "walk entries");
                    index = 0;
                }
                Debug(DebugEnum.TRACE, "Set VFOA " + frequenciesToWalk[index] / 1e6 + "MHz\n");
                myRig.FrequencyA = frequenciesToWalk[index];
                myRig.FrequencyB = frequenciesToWalk[index];
                //labelFreq.Text = frequenciesToWalk[index] / 1e6;
                labelFreq.Text = (frequenciesToWalk[index] / 1e6).ToString(CultureInfo.InvariantCulture) + " " + modeCurrent;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

        }
        private void TimerFreqWalk_Tick(object sender, EventArgs e)
        {
            DebugAddMsg(DebugEnum.LOG, "FreqWalkTick\n");
            { // we can bail if we're not close to doing anything
                TimeSpan t = DateTime.UtcNow - new DateTime(2023, 1, 1);
                ulong mySecond = (ulong)t.TotalSeconds; // + t.Milliseconds / 1000.0;
                ulong triggerTime = (ulong)timerFreqWalkSeconds;
                if ((mySecond % triggerTime) < triggerTime - 1) 
                    return;
            }
            DebugAddMsg(DebugEnum.LOG, "FreqWalkTick#2\n");
            if (Monitor.TryEnter(timerFreqWalkMutex))
            {
                if (frequenciesToWalk == null)
                {
                    DebugMsg.DebugAddMsg(DebugEnum.ERR, "No frequencies to walk...disabling");
                    timerFreqWalk.Stop();
                    FreqWalkStop();
                    Monitor.Exit(timerFreqWalkMutex);
                    return;
                }
                try
                {
                    timerFreqWalk.Enabled = false;
                    //richTextBox1.AppendText(MyTime() + "Tick\n");
                    FLRigGetFreq(false);
                    if (frequencyHz == 0)
                    {
                        DebugMsg.DebugAddMsg(DebugEnum.ERR, "Frequency == 0");
                        timerFreqWalk.Enabled = true;
                        Monitor.Exit(timerFreqWalkMutex);
                        return;
                    }
                    //var freq = frequenciesToWalk[(int)frequencyIndex];
                    if (frequenciesToWalk.Count == 0) return;
                    if (frequencyIndex >= frequenciesToWalk.Count) frequencyIndex = 0;
                    if (frequencyIndex >= 0 && !frequenciesToWalk.Contains((long)frequencyHz))
                    {
                        DebugMsg.DebugAddMsg(DebugEnum.LOG, "FrequencyWalk Stop Request#2 frequencyHz=" + frequencyHz);
                        FreqWalkStop();
                        //timerFreqWalk.Enabled = false;
                        Monitor.Exit(timerFreqWalkMutex);
                        return;
                    }
                    if (frequencyIndex < 0)
                    {
                        frequencyIndex = 0;
                        FreqWalkSetFreq(frequencyIndex);
                    }
                    else
                    {
                        //TimeSpan t = DateTime.UtcNow - new DateTime(1970, 1, 1); // this is for epoch seconds
                        TimeSpan t = DateTime.UtcNow - new DateTime(2023, 1, 1);
                        double mySecond = t.TotalSeconds + t.Milliseconds / 1000.0;
                        double triggerTime = timerFreqWalkSeconds;
                        int interval = (int)((mySecond - (mySecond % timerFreqWalkSeconds)) / timerFreqWalkSeconds);
                        triggerTime += interval * timerFreqWalkSeconds;
                        // this should be 60 30 15 seconds
                        // we do our thing ~700ms before the end of period
                        // this gives JTDX/WSJTX a chance to get the band correct on the decode window
                        // triggerTime -= 0.7;
                        triggerTime += (double)numericUpDownFreqWalkDelay.Value / 1000.0;
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
                }
                catch (Exception ex)
                {
                    MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
                }
            }
            timerFreqWalk.Enabled = true;
            Monitor.Exit(timerFreqWalkMutex);   
        }

        List<long> FrequenciesToWalk()
        {
            frequenciesToWalk ??= [];
            try
            {
                frequenciesToWalk.Clear();
                CheckedListBox.CheckedItemCollection items;
                checkedListBoxWalk1.Enabled = false;
                checkedListBoxWalk2.Enabled = false;
                checkedListBoxWalk3.Enabled = false;
                if (checkBoxWalk1.Checked)
                {
                    checkedListBoxWalk1.Enabled = true;
                    items = checkedListBoxWalk1.CheckedItems;
                    foreach (string s in items)
                    {
                        long myFreq = (long)(double.Parse(s) * 1000000);
                        if (myFreq > 0)
                        {
                            frequenciesToWalk.Add(myFreq);
                        }
                    }
                }
                if (checkBoxWalk2.Checked)
                {
                    checkedListBoxWalk2.Enabled = true;
                    items = checkedListBoxWalk2.CheckedItems;
                    foreach (string s in items)
                    {
                        var myFreq = (int)(double.Parse(s) * 1000000);
                        if (myFreq > 0)
                        {
                            frequenciesToWalk.Add(myFreq);
                        }
                    }
                }
                if (checkBoxWalk3.Checked)
                {
                    checkedListBoxWalk3.Enabled = true;
                    items = checkedListBoxWalk3.CheckedItems;
                    foreach (string s in items)
                    {
                        var myFreq = (int)(double.Parse(s) * 1000000);
                        if (myFreq > 0)
                        {
                            frequenciesToWalk.Add(myFreq);
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            return frequenciesToWalk;
        }
        private void FreqWalkStart()
        {
            try
            {
                frequencyIndex = 0;
                freqWalkIsRunning = true;
                if (tuner1 != null) tuner1.freqWalkIsRunning = true;
                SetWalkAntennaAsActive();
                buttonWalk.Text = "Walking";
                buttonWalk.BackColor = System.Drawing.Color.LightGreen;
                pausedTuning = true;
                FrequenciesToWalk();
                /*
                if (textBoxFrequencyWalkList.Text.Length == 0)
                {
                    textBoxFrequencyWalkList.Text = frequenciesToWalkFT8;
                }
                var tokens = textBoxFrequencyWalkList.Text.Split(' ');
                if (frequenciesToWalk == null) frequenciesToWalk = new List<int>();
                if (frequenciesToWalk == null) return;
                frequenciesToWalk.Clear();
                foreach (string s in tokens)
                {
                    if (s.Length > 0)
                        frequenciesToWalk.Add((int)(double.Parse(s) * 1000000));
                }
                */
                if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
                {
                    //  We need to disable the Icom ANT transceive
                    //  Then set the antenna to the receive antenna
                    myRig!.Transceive = false;
                    //var xml = FLRigXML("rig.cat_string", "<params><param><value>xfe xfe x94 xe0 x1a x05 x00 x73 x00 xfd</value></param></params");
                    //FLRigSend(xml);
                }
                FreqWalkSetFreq(0);
                timerFreqWalk.Start();
                //richTextBoxFreqWalk.AppendText(MyTime() + "Walking started\n");
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

        }

        private void FreqWalkStop()
        {
            try
            {
                buttonWalk.Text = "Walk";
                buttonWalk.BackColor = System.Drawing.Color.LightGray;
                freqWalkIsRunning = false;
                timerFreqWalk.Stop();
                Thread.Sleep(500);
                if (tuner1 != null) tuner1.freqWalkIsRunning = false;
                pausedTuning = false;
                //richTextBoxFreqWalk.AppendText(MyTime() + "Walking stopped\n");
                // Turn ANT transceive on
                myRig!.Transceive = true;
                //var xml = FLRigXML("rig.cat_string", "<params><param><value>xfe xfe x94 xe0 x1a x05 x00 x73 x01 xfd</value></param></params");
                //FLRigSend(xml);
                Thread.Sleep(500);
                // Assign freq so that SPE amp gets the frequency we're on
                myRig!.FrequencyA = myRig!.FrequencyA;
                SetAntennaInUseForGUI(true);
                //TuneSequence();  // disabling as don't want tune when we may want to change band to chase DX
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        private void ButtonWalk_Click_1(object sender, EventArgs e)
        {
            try
            {
                Cursor = Cursors.WaitCursor;
                if (tuner1 != null)
                {
                    // Decided we want the tuner on for antenna selection during walk
                    //if (tuner1.isOn) tuner1.Off();
                }
                //timerGetFreq.Stop();
                timerFreqWalk.Stop();
                Thread.Sleep(500);
                myRig!.ModeA = "USB-D";
                myRig!.ModeB = "USB-D";
                FrequenciesToWalk();
                //FreqWalkSetFreq(0);
                if (frequenciesToWalk == null || (freqWalkIsRunning == false && frequenciesToWalk.Count == 0))
                {
                    DebugMsg.DebugAddMsg(DebugEnum.LOG,"No walk frequencies selected in FreqWalk tab!!");
                    //timerGetFreq.Start();
                    Cursor = Cursors.Default;
                    return;
                }
                if (freqWalkIsRunning == false)
                {
                    myRig.SendCommand((int)numericUpDownFLRigBeforeWalk.Value);
                    FreqWalkStart();
                    DebugMsg.DebugAddMsg(DebugEnum.LOG, "FrequencyWalk Started");
                }
                else
                {
                    //timerGetFreq.Stop();
                    Thread.Sleep(200);
                    DebugMsg.DebugAddMsg(DebugEnum.LOG, "FrequencyWalk Stop Request#3");
                    FreqWalkStop();
                    myRig.SendCommand((int)numericUpDownFLRigAfterWalk.Value);
                    //timerGetFreq.Start();
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Error: " + ex.Message + ex.StackTrace);
            }
            //timerGetFreq.Start();
            //{
            //    timerGetFreq.Enabled = true;
            //}
            Cursor = Cursors.Default;
        }

        private void ButtonDummyLoad_Click(object sender, EventArgs e)
        {
            if (dummyLoad == false)
            {
                dummyLoad = true;
                //buttonDummyLoad.BackColor = Color.Green;
                SetAntennaInUseForGUI(true);
            }
            else
            {
                dummyLoad = false;
                //buttonDummyLoad.BackColor = Color.LightGray;
                SetAntennaInUseForGUI(true);
            }
        }

        private void AntennaEnableButtons(bool enable)
        {
            buttonAntennaPick1.Enabled = enable;
            buttonAntennaPick2.Enabled = enable;
            buttonAntennaPick3.Enabled = enable;
            buttonAntennaPick4.Enabled = enable;
            buttonAntennaPick5.Enabled = enable;
            buttonAntennaPick6.Enabled = enable;
            buttonAntennaPick7.Enabled = enable;
            buttonAntennaPick8.Enabled = enable;
            Application.DoEvents(); 
        }
        private void AntennaSetPickButtons()
        {
            if (antennaToolStripMenuItem.Checked == false)
            {
                buttonAntennaPick1.Visible = false;
                buttonAntennaPick2.Visible = false;
                buttonAntennaPick3.Visible = false;
                buttonAntennaPick4.Visible = false;
                buttonAntennaPick5.Visible = false;
                buttonAntennaPick6.Visible = false;
                buttonAntennaPick7.Visible = false;
                buttonAntennaPick8.Visible = false;
                labelAntennaSelected.Text = "";
                return;
            }

            buttonAntennaPick1.Visible = textBoxAntenna1.Text.Length > 0 && checkBoxAntenna1.Checked;
            buttonAntennaPick2.Visible = textBoxAntenna2.Text.Length > 0 && checkBoxAntenna2.Checked;
            buttonAntennaPick3.Visible = textBoxAntenna3.Text.Length > 0 && checkBoxAntenna3.Checked;
            buttonAntennaPick4.Visible = textBoxAntenna4.Text.Length > 0 && checkBoxAntenna4.Checked;
            buttonAntennaPick5.Visible = textBoxAntenna5.Text.Length > 0 && checkBoxAntenna5.Checked;
            buttonAntennaPick6.Visible = textBoxAntenna6.Text.Length > 0 && checkBoxAntenna6.Checked;
            buttonAntennaPick7.Visible = textBoxAntenna7.Text.Length > 0 && checkBoxAntenna7.Checked;
            buttonAntennaPick8.Visible = textBoxAntenna8.Text.Length > 0 && checkBoxAntenna8.Checked;

            buttonAntennaPick1.Text = textBoxAntenna1.Text;
            buttonAntennaPick2.Text = textBoxAntenna2.Text;
            buttonAntennaPick3.Text = textBoxAntenna3.Text;
            buttonAntennaPick4.Text = textBoxAntenna4.Text;
            buttonAntennaPick5.Text = textBoxAntenna5.Text;
            buttonAntennaPick6.Text = textBoxAntenna6.Text;
            buttonAntennaPick7.Text = textBoxAntenna7.Text;
            buttonAntennaPick8.Text = textBoxAntenna8.Text;
        }

        static int lastAntenna;
        private void AntennaUpdateSelected(int antennaNumber)
        {
            if (antennaNumber == lastAntenna) return;
            lastAntenna = antennaNumber;
            buttonAntenna1.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna2.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna3.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna4.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna5.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna6.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna7.BackColor = System.Drawing.Color.LightGray;
            buttonAntenna8.BackColor = System.Drawing.Color.LightGray;
            switch (antennaNumber)
            {
                case 1:
                    buttonAntenna1.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick1);
                    labelAntennaSelected.Text = textBoxAntenna1.Text;
                    break;
                case 2:
                    buttonAntenna2.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick2);
                    labelAntennaSelected.Text = textBoxAntenna2.Text;
                    break;
                case 3:
                    buttonAntenna3.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick3);
                    labelAntennaSelected.Text = textBoxAntenna3.Text;
                    break;
                case 4:
                    buttonAntenna4.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick4);
                    labelAntennaSelected.Text = textBoxAntenna4.Text;
                    break;
                case 5:
                    buttonAntenna5.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick5);
                    labelAntennaSelected.Text = textBoxAntenna5.Text;
                    break;
                case 6:
                    buttonAntenna6.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick6);
                    labelAntennaSelected.Text = textBoxAntenna6.Text;
                    break;
                case 7:
                    buttonAntenna7.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick7);
                    labelAntennaSelected.Text = textBoxAntenna7.Text;
                    break;
                case 8:
                    buttonAntenna8.BackColor = System.Drawing.Color.Green;
                    ButtonAntennaPickSet(buttonAntennaPick8);
                    labelAntennaSelected.Text = textBoxAntenna8.Text;
                    break;
            }
            if (tuner1 is not null && tuner1.Bypassed) labelAntennaSelected.Text += " Bypassed";
        }
        private void ButtonAntenna1_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(1);
            labelAntennaSelected.Text = textBoxAntenna1.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna2_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(2);
            labelAntennaSelected.Text = textBoxAntenna2.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna3_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(3);
            labelAntennaSelected.Text = textBoxAntenna3.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna4_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(4);
            labelAntennaSelected.Text = textBoxAntenna4.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna5_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(5);
            labelAntennaSelected.Text = textBoxAntenna5.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna6_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(6);
            labelAntennaSelected.Text = textBoxAntenna6.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna7_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(7);
            labelAntennaSelected.Text = textBoxAntenna7.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntenna8_Click(object sender, EventArgs e)
        {
            SetAntennaRelayOn(8);
            labelAntennaSelected.Text = textBoxAntenna8.Text;
            SetAntennaInUseForGUI(true);
        }

        private void ButtonAntennaPickReset()
        {
            try
            {
                buttonAntennaPick1.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick2.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick3.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick4.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick5.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick6.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick7.BackColor = System.Drawing.Color.WhiteSmoke;
                buttonAntennaPick8.BackColor = System.Drawing.Color.WhiteSmoke;

                buttonAntennaPick1.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick2.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick3.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick4.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick5.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick6.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick7.ForeColor = System.Drawing.Color.Black;
                buttonAntennaPick8.ForeColor = System.Drawing.Color.Black;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }

        }

        // toggles antenna pick color and sets lock
        // return true if set or false if toggled off
        private bool ButtonAntennaPickSet(Button myButton)
        {
            try
            {
                if (myButton.BackColor == System.Drawing.Color.Green)
                {
                    // we just unlock so the other routines can set the antenna
                    antennaLocked = false;
                    //ButtonAntennaPickReset();
                    return false;
                }
                ButtonAntennaPickReset();
                myButton.BackColor = System.Drawing.Color.Green;
                myButton.ForeColor = System.Drawing.Color.White;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }

            return true;
        }

        readonly int antennaPickSleep = 1200;
        private int maxRigPower;

        private void ButtonAntennaPick1_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick1))
                {
                    //AmpSet(checkBoxAntenna1Amp.Checked);
                    //SetAntennaRelayOn(1);
                    SetControllerFromAntenna(1);
                    Thread.Sleep(antennaPickSleep);
                    antennaLocked = true;
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick2_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick2))
                {
                    //AmpSet(checkBoxAntenna2Amp.Checked);
                    //SetAntennaRelayOn(2);
                    SetControllerFromAntenna(2);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick3_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick3))
                {
                    //AmpSet(checkBoxAntenna3Amp.Checked);
                    //SetAntennaRelayOn(3);
                    SetControllerFromAntenna(3);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }
        private void ButtonAntennaPick4_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick4))
                {
                    //AmpSet(checkBoxAntenna4Amp.Checked);
                    //SetAntennaRelayOn(4);
                    SetControllerFromAntenna(4);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick5_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick5))
                {
                   // AmpSet(checkBoxAntenna5Amp.Checked);
                    //SetAntennaRelayOn(5);
                    SetControllerFromAntenna(5);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick6_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick6))
                {
                    //AmpSet(checkBoxAntenna6Amp.Checked);
                    //SetAntennaRelayOn(6);
                    SetControllerFromAntenna(6);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick7_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick7))
                {
                    //AmpSet(checkBoxAntenna7Amp.Checked);
                    //SetAntennaRelayOn(7);
                    SetControllerFromAntenna(7);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void ButtonAntennaPick8_Click(object sender, EventArgs e)
        {
            AntennaEnableButtons(false);
            try
            {
                if (ButtonAntennaPickSet(buttonAntennaPick8))
                {
                    //AmpSet(checkBoxAntenna8Amp.Checked);
                    //SetAntennaRelayOn(8);
                    SetControllerFromAntenna(8);
                    antennaLocked = true;
                    Thread.Sleep(antennaPickSleep);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
            AntennaEnableButtons(true);
        }

        private void CheckBoxAntennaAmp_CheckedChange(object sender, EventArgs e)
        {
            CheckBox myCheckBox = (CheckBox)sender;
            AmpSet(myCheckBox.Checked);
        }

        private void FreqWalkSetIntervalDisplay()
        {
            labelInterval.Text = "Interval " + timerFreqWalkSeconds + " sec";
        }
        private void RichTextBox1_KeyUp(object sender, KeyEventArgs e)
        {
            try
            {
                //richTextBoxFreqWalk.Undo();
                if (e.KeyCode == Keys.N && frequenciesToWalk != null)
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
                else if (e.KeyCode == Keys.F2)
                {
                    timerFreqWalkSeconds = 30;
                }
                else if (e.KeyCode == Keys.F3)
                {
                    timerFreqWalkSeconds = 15;
                }
                FreqWalkSetIntervalDisplay();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
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
            try
            {
                CheckedListBox myBox = (CheckedListBox)sender;

                var index = myBox.SelectedIndex;
                if (index == -1) return;
                bool check = myBox.GetItemChecked(index);
                if (myBox.SelectedItem.ToString() != "0")
                    myBox.SetItemChecked(index, true);
                if (ModifierKeys == Keys.Control)
                {
                    double freq = 0;
                    string answer;
                    bool okinput = true;
                    do
                    {
                        answer = Interaction.InputBox("Enter Freq in MHz");
                        okinput = true;
                        //answer = answer.Replace(',', '.');
                        if (answer.Length == 0) return;
                        foreach (char c in answer)
                        {
                            if ((c < '0' || c > '9') && c != '.' && c != 'E' && c != 'e')
                            {
                                okinput = false;
                                MessageBox.Show("Bad char in frequency value. Only numeric, period and e/E expected");
                            }
                        }
                    }
                    while (!okinput);

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
                    for (int i = 0; i < myBox.Items.Count; i++)
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
                if (myBox.Items[index].ToString() == "0")
                {
                    myBox.SetItemChecked(index, false);
                }
                FrequenciesToWalk();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + "\n" + ex.StackTrace);
            }
        }

        private void LabelInterval_Click(object sender, EventArgs e)
        {
            try
            {
                switch (timerFreqWalkSeconds)
                {
                    case 15: timerFreqWalkSeconds = 30; break;
                    case 30: timerFreqWalkSeconds = 60; break;
                    case 60: timerFreqWalkSeconds = 120; break;
                    case 120: timerFreqWalkSeconds = 180; break;
                    case 180: timerFreqWalkSeconds = 240; break;
                    case 240: timerFreqWalkSeconds = 300; break;
                    case 300: timerFreqWalkSeconds = 15; break;

                }
                labelInterval.Text = "Interval " + timerFreqWalkSeconds.ToString() + " sec";
                Properties.Settings.Default.TimerFreqWalkSeconds = timerFreqWalkSeconds;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

        }

        private void ComboBoxAmpBits_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (relay1 == null) return;
                // assumes single relay
                var s = comboBoxAmpBits.Text;
                var tokens = s.Split('/');
                if (tokens.Length >= 2 && int.TryParse(tokens[1], out int relayNumber))
                {
                    relay1.AmpRelay = (byte)relayNumber;
                    if (relay1.AmpRelay != 0)
                    {
                        AmpSet(true);
                    }
                }
                else
                {
                    MessageBox.Show("Unknown relay number from " + s);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }

        }

        private string GetLabel()
        {

            string answer;
            answer = Interaction.InputBox("Enter new label");
            return answer;

        }

        private void ButtonAntennaPick8_Enter(object sender, EventArgs e)
        {
        }

        private void LabelAntennaSelected_Click(object sender, EventArgs e)
        {
            if (labelAntennaSelected.ForeColor == System.Drawing.Color.Red)
                labelAntennaSelected.ForeColor = System.Drawing.Color.Black;
            else
                labelAntennaSelected.ForeColor = System.Drawing.Color.Red;
            antennaLocked = labelAntennaSelected.ForeColor == System.Drawing.Color.Red;
        }

        private void CheckBoxWalkFT8_CheckedChanged(object sender, EventArgs e)
        {
            checkedListBoxWalk1.Enabled = checkBoxWalk1.Checked;
            FrequenciesToWalk();
        }

        private void CheckBoxWalkFT4_CheckedChanged(object sender, EventArgs e)
        {
            checkedListBoxWalk2.Enabled = checkBoxWalk2.Checked;
            FrequenciesToWalk();
        }

        private void CheckBoxWalkCustom_CheckedChanged(object sender, EventArgs e)
        {
            checkedListBoxWalk3.Enabled = checkBoxWalk3.Checked;
            FrequenciesToWalk();
        }

        private void LabelFreqWalk1_Click(object sender, EventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                String newLabel = GetLabel();
                if (newLabel.Length > 0) labelFreqWalk1.Text = newLabel;
            }
        }
        private void LabelFreqWalk2_Click(object sender, EventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                String newLabel = GetLabel();
                if (newLabel.Length > 0) labelFreqWalk2.Text = newLabel;
            }
        }
        private void LabelFreqWalk3_Click(object sender, EventArgs e)
        {
            if (ModifierKeys == Keys.Control)
            {
                String newLabel = GetLabel();
                if (newLabel.Length > 0) labelFreqWalk3.Text = newLabel;
            }
        }

        private void Label16_Click(object sender, EventArgs e)
        {

        }

        private void AntennaToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (antennaToolStripMenuItem.Checked)
            {
                antennaToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageAntenna);
                buttonAntennaPick1.Visible = false;
                buttonAntennaPick2.Visible = false;
                buttonAntennaPick3.Visible = false;
                buttonAntennaPick4.Visible = false;
                buttonAntennaPick5.Visible = false;
                buttonAntennaPick6.Visible = false;
                buttonAntennaPick7.Visible = false;
                buttonAntennaPick8.Visible = false;

            }
            else
            {
                antennaToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageAntenna);
            }
        }

        private void DebugToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (debugToolStripMenuItem.Checked)
            {
                debugToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageDebug);

            }
            else
            {
                debugToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageDebug);
            }
        }

        private void TunerToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (tunerToolStripMenuItem.Checked)
            {
                tunerToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageTuner);

            }
            else
            {
                tunerToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageTuner);
            }
        }

        private void Relay3ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (relay3ToolStripMenuItem.Checked)
            {
                relay3ToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageRelay3);

            }
            else
            {
                relay3ToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageRelay3);
            }
        }

        private void Relay4ToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (relay4ToolStripMenuItem.Checked)
            {
                relay4ToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageRelay4);

            }
            else
            {
                relay4ToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageRelay4);
            }
        }

        private void ComboBoxFreqWalkAntenna_SelectedIndexChanged(object sender, EventArgs e)
        {

        }

        private void ExpertLinearsToolStripMenuItem_Click(object sender, EventArgs e)
        {
            if (expertLinearsToolStripMenuItem.Checked)
            {
                expertLinearsToolStripMenuItem.Checked = false;
                tabPage.TabPages.Remove(tabPageExpertLinears);
            }
            else
            {
                expertLinearsToolStripMenuItem.Checked = true;
                tabPage.TabPages.Add(tabPageExpertLinears);
            }
        }

        // band = 0-11 (160-4) and antennaNumber is 1 or 2

        //private readonly string[] bandList = ["160M", "80M", "60M", "40M", "30M", "20M", "17M", "15M", "12M", "10M", "6M", "4M"];
        private void TuneAll(int band)
        {
            try
            {
                if (tuner1 is null || tuner1.tuneFrequencies is null)
                {
                    MessageBox.Show("tuner1 problem in " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                    return;
                }
                int freqN = tuner1!.tuneFrequencies[band, 0];
                int freqStep = tuner1!.tuneFrequencies[band, 1];
                int freqStart = tuner1!.tuneFrequencies[band, 2];
                for (int step = 0; step < freqN; ++step)
                {
                    int freq = freqStart + (step * freqStep);
                    labelExpertLinearsInfo.Text = "Tuning " + freq;
                    Application.DoEvents();
                    if (myRig is not null)
                    {
                        myRig.SetFrequency('A', freq * 1000);
                        myRig.SetFrequency('B', freq * 1000);
                    }
                    tuner1!.Tune();
                    Application.DoEvents();
                    Thread.Sleep(500);
                }
                /*
                xml = FLRigXML("rig.set_vfo" + 'B', myparam);
                if (FLRigSend(xml) == false)
                { // Abort if FLRig is giving an error
                    Debug(DebugEnum.ERR, "FLRigSend got an error??\n");
                }
                tuner1.Tune();
                Thread.Sleep(100);
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>1</i4></value></param></params");
                if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
                labelExpertLinearsInfo.Text = "Tuned " + frequencyHzTune;
                */
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        private void TuneSequence()
        {
            try
            {
                labelExpertLinearsInfo.Text = "Tuning SPE";
                Application.DoEvents();
                string saveModeA = myRig!.ModeA;
                string saveModeB = myRig!.ModeB;
                DebugAddMsg(DebugEnum.VERBOSE, "ModeA=" + saveModeA + ", " + "ModeB=" + saveModeB+"\n");
                string desiredMode = "FM";
                //if (!saveModeA.Equals(desiredMode) || !saveModeB.Equals(desiredMode))
                //{
                    myRig!.ModeA = desiredMode;
                    myRig!.ModeB = desiredMode;
                //}
                PowerSelect(myRig!.FrequencyA, desiredMode, true, checkBoxTunePwr.Checked);
                myRig!.PTT = true;
                Thread.Sleep(500); // let amp get started
                int nloop = 40;
                double swr = 0;
                double swrsave = -1;
                int stable = 0;
                do
                {
                    Thread.Sleep(500);
                    if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
                    {
                        swrsave = swr;
                        swr = tuner1!.SWRATU;
                        if (swrsave == swr) stable++;
                        else stable = 0;
                        Application.DoEvents();
                    }
                } while (--nloop > 0  && stable < 3);
                if (nloop==0)
                {
                    DebugAddMsg(DebugEnum.VERBOSE, "Tune stopped loops exceeded\n");
                }
                //DebugAddMsg(DebugEnum.VERBOSE, "nloop=" + nloop + ", swr=" + swr + ", swrsave=" + swrsave + ", stable=" + stable + "\n");

                Thread.Sleep(500);
                myRig!.PTT = false;
                Thread.Sleep(100);
                if (swr >= 1.7)
                {
                    MessageBox.Show("Warning SWR is 1.7 or greater: " + swr + "\n", "Tuner", MessageBoxButtons.OK);
                }
                PowerSelect(myRig!.FrequencyA, desiredMode, false);
                /*
                tuner1!.SelectDisplayPage();
                if (checkBoxExpertLinears160_1.Checked) TuneAll(0, 1, comboBoxExpertLinears160_1.SelectedItem.ToString());
                if (checkBoxExpertLinears160_2.Checked) TuneAll(0, 2, comboBoxExpertLinears160_2.SelectedItem.ToString());
                if (checkBoxExpertLinears80_1.Checked) TuneAll(1, 1, comboBoxExpertLinears80_1.SelectedItem.ToString());
                if (checkBoxExpertLinears80_2.Checked) TuneAll(1, 2, comboBoxExpertLinears80_2.SelectedItem.ToString());
                if (checkBoxExpertLinears60_1.Checked) TuneAll(2, 1, comboBoxExpertLinears60_1.SelectedItem.ToString());
                if (checkBoxExpertLinears60_2.Checked) TuneAll(2, 2, comboBoxExpertLinears60_2.SelectedItem.ToString());
                if (checkBoxExpertLinears40_1.Checked) TuneAll(3, 1, comboBoxExpertLinears40_1.SelectedItem.ToString());
                if (checkBoxExpertLinears40_2.Checked) TuneAll(3, 2, comboBoxExpertLinears40_2.SelectedItem.ToString());
                if (checkBoxExpertLinears30_1.Checked) TuneAll(4, 1, comboBoxExpertLinears30_1.SelectedItem.ToString());
                if (checkBoxExpertLinears30_2.Checked) TuneAll(4, 2, comboBoxExpertLinears30_2.SelectedItem.ToString());
                if (checkBoxExpertLinears20_1.Checked) TuneAll(5, 1, comboBoxExpertLinears20_1.SelectedItem.ToString());
                if (checkBoxExpertLinears20_2.Checked) TuneAll(5, 2, comboBoxExpertLinears20_2.SelectedItem.ToString());
                if (checkBoxExpertLinears17_1.Checked) TuneAll(6, 1, comboBoxExpertLinears17_1.SelectedItem.ToString());
                if (checkBoxExpertLinears17_2.Checked) TuneAll(6, 2, comboBoxExpertLinears17_2.SelectedItem.ToString());
                if (checkBoxExpertLinears15_1.Checked) TuneAll(7, 1, comboBoxExpertLinears15_1.SelectedItem.ToString());
                if (checkBoxExpertLinears15_2.Checked) TuneAll(7, 2, comboBoxExpertLinears15_2.SelectedItem.ToString());
                if (checkBoxExpertLinears12_1.Checked) TuneAll(8, 1, comboBoxExpertLinears12_1.SelectedItem.ToString());
                if (checkBoxExpertLinears12_2.Checked) TuneAll(8, 2, comboBoxExpertLinears12_2.SelectedItem.ToString());
                if (checkBoxExpertLinears10_1.Checked) TuneAll(9, 1, comboBoxExpertLinears10_1.SelectedItem.ToString());
                if (checkBoxExpertLinears10_2.Checked) TuneAll(9, 2, comboBoxExpertLinears10_2.SelectedItem.ToString());
                if (checkBoxExpertLinears6_1.Checked) TuneAll(10, 1, comboBoxExpertLinears6_1.SelectedItem.ToString());
                if (checkBoxExpertLinears6_2.Checked) TuneAll(10, 2, comboBoxExpertLinears6_2.SelectedItem.ToString());
                */
                labelExpertLinearsInfo.Text = "Tuning done";
                //tuner1.SelectDisplayPage();
                myRig.ModeA = saveModeA;
                myRig.ModeB = saveModeB;
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        private void ButtonExpertLinearsTune_Click(object sender, EventArgs e)
        {
            //string myStuff = "";
            var oldBackColor = buttonExpertLinearsTune.BackColor;
            buttonExpertLinearsTune.BackColor = System.Drawing.Color.Red;
            if (ModifierKeys == Keys.Control)
            {
                /*
                //tuner1.SelectManualTunePage();
                byte cmdDown = 0x07; // C Down
                //byte cmdUp = 0x08; // C Up
                int bytes = 1024; // 128 for L 1024 for C
                //byte cmdBack = 0x05; // L Down
                //byte cmdFwd = 0x06; // L Up
                //int bytes = 128; // 128 for L 1024 for C
                tuner1.SendCmd(cmdDown); // step C back to get status msg
                tuner1.GetStatus2(Tuner.Screen.Tune);
                myStuff += (tuner1.cIndex.ToString("X") + "\n");
                for (int i = 1;i<=bytes;++i)
                {
                    DebugMsg.DebugAddMsg(DebugEnum.LOG, "Special execuction #" + i + "\n");
                    tuner1.SendCmd(cmdDown);  // 126 settings of L, 1024 of C
                    tuner1.GetStatus2(Tuner.Screen.Tune);
                    myStuff += (tuner1.lIndex.ToString("X")+"\n");
                    //Application.DoEvents();
                    //Thread.Sleep(50);
                }
                DebugAddMsg(DebugEnum.LOG, myStuff);
                */
                //tuner1!.SetInductance(5.0);
                //TuneCurrentBand();
            }
            else
            {
                TuneGetLC();
            }
            buttonExpertLinearsTune.BackColor = oldBackColor;
        }

        /*
        private void TuneCurrentBand()
        {
            try
            {
                byte cmdTune = 0x09;
                labelExpertLinearsInfo.Text = "Tuning " + frequencyHz;
                tuner1!.GetStatus();
                tuner1!.GetStatus2(Tuner.Screen.ManualTune);
                double l = tuner1.GetInductance();
                double c = tuner1.GetCapacitance();
                DebugMsg.DebugAddMsg(DebugEnum.LOG, "L=" + l.ToString("0.0") + "C=" + c.ToString("0,0"));
                string myparam = "<params><param><value>" + "AM" + "</value></param></params>";
                var xml = FLRigXML("rig.set_modeA", myparam);
                if (FLRigSend(xml) == false)
                { // Abort if FLRig is giving an error
                    Debug(DebugEnum.ERR, "FLRig set_modeA got an error #1??\n");
                }
                xml = FLRigXML("rig.set_modeB", myparam);
                if (FLRigSend(xml) == false)
                { // Abort if FLRig is giving an error
                    Debug(DebugEnum.ERR, "FLRig set_modeB got an error #1??\n");
                }

                // Ready for finding min SWR test
                double SWRStart;
                // need the band
                if (tuner1 is null || tuner1.tuneFrequencies is null)
                {
                    MessageBox.Show("tuner problem ins ", System.Reflection.MethodBase.GetCurrentMethod().Name);
                    return;
                }
                var nFreqs = tuner1.tuneFrequencies[tuner1.band, 0];
                var step = tuner1.tuneFrequencies[tuner1.band, 1];
                var freq = tuner1.tuneFrequencies[tuner1.band, 2];
                // build freq table
                int[] freqTable = new int[nFreqs];
                for (int band = 0; band < nFreqs; band++)
                {
                    if (tuner1.band == band)
                        freqTable[band] = (freq + (step * band)) * 1000;
                }
                myparam = "<params><param><value><double>" + freqTable[0] + "</double></value></param></params";
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
                tuner1.SelectManualTunePage();
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>1</i4></value></param></params");
                if (FLRigSend(xml) == false)
                { // Abort if FLRig is giving an error
                    Debug(DebugEnum.ERR, "FLRig set_ptt got an error??\n");
                }
                Thread.Sleep(500);
                tuner1.GetStatus2(Tuner.Screen.ManualTune);
                var lastL = tuner1.GetInductance();
                var lastC = tuner1.GetCapacitance();
                bool firstFlag = true;
                // loop freq table
                foreach (int freq2Tune in freqTable)
                {
                    if (freq2Tune == 0) continue;
                    myparam = "<params><param><value><double>" + freq2Tune + "</double></value></param></params";
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
                    if (!firstFlag)
                    {
                        Thread.Sleep(500);
                        tuner1.SetCapacitance(lastC);
                        tuner1.SetInductance(lastL);
                    }
                    firstFlag = false;
                    do
                    {
                        SWRStart = tuner1.GetSWR();
                        FindMinSWR_L();
                        FindMinSWR_C();
                        var logmsg = frequencyHz + "\t" + tuner1.GetSWR() + "\t" + tuner1.GetInductance().ToString("{.0}") + "\t" + tuner1.GetCapacitance().ToString("{.0}") + "\n";
                        labelExpertLinearsInfo.Text = logmsg;
                        DebugMsg.DebugAddMsg(DebugEnum.LOG, logmsg);
                    } while (tuner1.GetSWR() < SWRStart && tuner1.GetSWR() > 1);
                    lastC = tuner1.GetCapacitance();
                    lastL = tuner1.GetInductance();
                    if (lastC == 0 || lastL == 0)
                    {
                        lastC = 0;
                    }
                    tuner1.SendCmd(cmdTune);  // remember out tuning values
                    Thread.Sleep(200);
                }
                xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>0</i4></value></param></params");
                if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
        }
        */
        private void TuneGetLC()
        {
            MessageBox.Show("This will get the L/C values and SWR (if transmitting) for the current band\nResults will be in the clipboard for pasting into Excel for example", "SPE SWR/L/C");
            try
            {
                UseWaitCursor = true;
                Application.DoEvents();
                Clipboard.Clear();
                //byte cmdTune = 0x09;
                tuner1!.GetStatus();
                tuner1!.GetStatus2(Tuner.Screen.ManualTune);
                double l = tuner1.GetInductance();
                double c = tuner1.GetCapacitance();
                if (myRig != null)
                {
                    myRig.ModeA = "FM";
                    myRig.ModeB = "FM";
                }

                // Ready for finding min SWR test
                //double SWRStart;
                // need the band
                if (tuner1 is null || tuner1.tuneFrequencies is null)
                {
                    UseWaitCursor = false;
                    MessageBox.Show("tuner problem ins ", System.Reflection.MethodBase.GetCurrentMethod().Name);
                    return;
                }
                Monitor.Enter(expertLock);
                var myBand = tuner1.band;
                var nFreqs = tuner1.tuneFrequencies[myBand, 0];
                var step = tuner1.tuneFrequencies[myBand, 1];
                var freq = tuner1.tuneFrequencies[myBand, 2];
                // build freq table
                int[] freqTable = new int[nFreqs];
                for (int i = 0; i < nFreqs; i++)
                {
                    //if (tuner1.band == 1 && i != 0 && i != 1 && i != 27 && i != 28)
                        freqTable[i] = (freq + (step * i)) * 1000;
                }
                myRig!.FrequencyA = freqTable[0];
                //myRig.VFO = 'B';
                tuner1.SelectManualTunePage();
                Thread.Sleep(500);
                tuner1.GetStatus2(Tuner.Screen.ManualTune);
                var lastL = tuner1.GetInductance();
                var lastC = tuner1.GetCapacitance();
                //bool firstFlag = true;
                // loop freq table
                foreach (int freq2Tune in freqTable)
                {
                    if (freq2Tune == 0) continue;
                    myRig.FrequencyA = freq2Tune;
                    myRig.FrequencyB = freq2Tune;
                    Thread.Sleep(2000);
                    //if (!firstFlag)
                    //{
                    //    Thread.Sleep(500);
                    //    tuner1.SetCapacitance(lastC);
                    //    tuner1.SetInductance(lastL);
                    //}
                    //bool firstFlag = false;
                    /*
                    do
                    {
                        SWRStart = tuner1.GetSWR();
                        FindMinSWR_L();
                        FindMinSWR_C();
                        var logmsg = frequencyHz + "\t" + tuner1.GetSWR() + "\t" + tuner1.GetInductance().ToString("{.0}") + "\t" + tuner1.GetCapacitance().ToString("{.0}") + "\n";
                        labelExpertLinearsInfo.Text = logmsg;
                        DebugMsg.DebugAddMsg(DebugEnum.LOG, logmsg);
                    } while (tuner1.GetSWR() < SWRStart && tuner1.GetSWR() > 1);
                    */
                    lastC = tuner1.GetCapacitance();
                    lastL = tuner1.GetInductance();
                    var swr = tuner1.GetSWR();
                    var logmsg = freq2Tune.ToString("N0") + "\t" + tuner1.GetSWR() + "\t" + tuner1.GetInductance().ToString("N2") + "\t" + tuner1.GetCapacitance().ToString("N2") + "\n";
                    labelExpertLinearsInfo.Text = logmsg;
                    DebugMsg.DebugAddMsg(DebugEnum.LOG, "\t" + logmsg);
                    Clipboard.SetText(Clipboard.GetText() + logmsg);
                    Application.DoEvents();
                    if (lastC == 0 || lastL == 0)
                    {
                        lastC = 0;
                    }
                    //tuner1.SendCmd(cmdTune);  // remember out tuning values
                    Thread.Sleep(200);
                }
                //xml = FLRigXML("rig.set_ptt", "<params><param><value><i4>0</i4></value></param></params");
                //if (FLRigSend(xml) == false) return; // Abort if FLRig is giving an error
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            UseWaitCursor = false;
            labelExpertLinearsInfo.Text = "Results are in clipboard";
            Monitor.Exit(expertLock);
        }

        private void ComboBoxExpertLinears4_2_Enter(object sender, EventArgs e)
        {

        }

        void TabPageExpertLinears_Init()
        {
            if (tuner1 is not null && !tuner1.IsOn) return;
            //TabPage myPage = (TabPage)sender;
            //myPage.SuspendLayout();
            //myPage.Refresh();
            tabPageExpertLinears.SuspendLayout();
            tabPageExpertLinears.Refresh();
            if (tuner1 is null)
            {
                return;
            }
            //Cursor.Current = Cursors.WaitCursor;
            tabPageExpertLinears.Refresh();
            //tuner1.Poll();
            //tuner1.SelectAntennaPage();
            tuner1.GetStatus2(Tuner.Screen.Antenna);

            Application.DoEvents();
            tuner1.GetStatus2(Tuner.Screen.Antenna);
            Application.DoEvents();
            if (tuner1.antennas is null)
            {
                MessageBox.Show("tuner1.antennas=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }
            if (comboBoxExpertLinears160_1.Items.Count == 0)
            {
                MessageBox.Show("SPE tab must be selected 1st with amp on");
                return;
            }
            comboBoxExpertLinears160_1.SelectedIndex = comboBoxExpertLinears160_1.FindStringExact(tuner1.antennas[0, 0]);
            if (comboBoxExpertLinears160_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears160_2.SelectedIndex = comboBoxExpertLinears160_1.FindStringExact(tuner1.antennas[0, 1]);
            if (comboBoxExpertLinears160_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears80_1.SelectedIndex = comboBoxExpertLinears80_1.FindStringExact(tuner1.antennas[1, 0]);
            if (comboBoxExpertLinears80_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears80_2.SelectedIndex = comboBoxExpertLinears80_1.FindStringExact(tuner1.antennas[1, 1]);
            if (comboBoxExpertLinears80_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears60_1.SelectedIndex = comboBoxExpertLinears60_1.FindStringExact(tuner1.antennas[2, 0]);
            if (comboBoxExpertLinears60_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears60_2.SelectedIndex = comboBoxExpertLinears60_1.FindStringExact(tuner1.antennas[2, 1]);
            if (comboBoxExpertLinears60_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears40_1.SelectedIndex = comboBoxExpertLinears40_1.FindStringExact(tuner1.antennas[3, 0]);
            if (comboBoxExpertLinears40_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears40_2.SelectedIndex = comboBoxExpertLinears40_1.FindStringExact(tuner1.antennas[3, 1]);
            if (comboBoxExpertLinears40_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears30_1.SelectedIndex = comboBoxExpertLinears30_1.FindStringExact(tuner1.antennas[4, 0]);
            if (comboBoxExpertLinears30_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears30_2.SelectedIndex = comboBoxExpertLinears30_1.FindStringExact(tuner1.antennas[4, 1]);
            if (comboBoxExpertLinears30_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears20_1.SelectedIndex = comboBoxExpertLinears20_1.FindStringExact(tuner1.antennas[5, 0]);
            if (comboBoxExpertLinears20_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears20_2.SelectedIndex = comboBoxExpertLinears20_1.FindStringExact(tuner1.antennas[5, 1]);
            if (comboBoxExpertLinears20_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears17_1.SelectedIndex = comboBoxExpertLinears17_1.FindStringExact(tuner1.antennas[6, 0]);
            if (comboBoxExpertLinears17_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears17_2.SelectedIndex = comboBoxExpertLinears17_1.FindStringExact(tuner1.antennas[6, 1]);
            if (comboBoxExpertLinears17_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears15_1.SelectedIndex = comboBoxExpertLinears15_1.FindStringExact(tuner1.antennas[7, 0]);
            if (comboBoxExpertLinears15_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears15_2.SelectedIndex = comboBoxExpertLinears15_1.FindStringExact(tuner1.antennas[7, 1]);
            if (comboBoxExpertLinears15_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears12_1.SelectedIndex = comboBoxExpertLinears12_1.FindStringExact(tuner1.antennas[8, 0]);
            if (comboBoxExpertLinears12_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears12_2.SelectedIndex = comboBoxExpertLinears12_1.FindStringExact(tuner1.antennas[8, 1]);
            if (comboBoxExpertLinears12_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears10_1.SelectedIndex = comboBoxExpertLinears10_1.FindStringExact(tuner1.antennas[9, 0]);
            if (comboBoxExpertLinears10_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears10_2.SelectedIndex = comboBoxExpertLinears10_1.FindStringExact(tuner1.antennas[9, 1]);
            if (comboBoxExpertLinears10_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears6_1.SelectedIndex = comboBoxExpertLinears6_1.FindStringExact(tuner1.antennas[10, 0]);
            if (comboBoxExpertLinears6_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears6_2.SelectedIndex = comboBoxExpertLinears6_1.FindStringExact(tuner1.antennas[10, 1]);
            if (comboBoxExpertLinears6_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears4_1.SelectedIndex = comboBoxExpertLinears4_1.FindStringExact(tuner1.antennas[11, 0]);
            if (comboBoxExpertLinears4_1.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            comboBoxExpertLinears4_2.SelectedIndex = comboBoxExpertLinears4_1.FindStringExact(tuner1.antennas[11, 1]);
            if (comboBoxExpertLinears4_2.SelectedIndex < 0) comboBoxExpertLinears160_1.SelectedIndex = 0;
            tabPageExpertLinears.Refresh();
            Application.DoEvents();
            if (comboBoxExpertLinears160_1.SelectedItem != null)
                checkBoxExpertLinears160_1.Enabled = !comboBoxExpertLinears160_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears160_2.SelectedItem != null)
                checkBoxExpertLinears160_2.Enabled = !comboBoxExpertLinears160_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears80_1.SelectedItem != null)
                checkBoxExpertLinears80_1.Enabled = !comboBoxExpertLinears80_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears80_2.SelectedItem != null)
                checkBoxExpertLinears80_2.Enabled = !comboBoxExpertLinears80_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears60_1.SelectedItem != null)
                checkBoxExpertLinears60_1.Enabled = !comboBoxExpertLinears60_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears60_2.SelectedItem != null)
                checkBoxExpertLinears60_2.Enabled = !comboBoxExpertLinears60_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears40_1.SelectedItem != null)
                checkBoxExpertLinears40_1.Enabled = !comboBoxExpertLinears40_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears40_2.SelectedItem != null)
                checkBoxExpertLinears40_2.Enabled = !comboBoxExpertLinears40_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears30_1.SelectedItem != null)
                checkBoxExpertLinears30_1.Enabled = !comboBoxExpertLinears30_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears30_2.SelectedItem != null)
                checkBoxExpertLinears30_2.Enabled = !comboBoxExpertLinears30_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears20_1.SelectedItem != null)
                checkBoxExpertLinears20_1.Enabled = !comboBoxExpertLinears20_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears20_2.SelectedItem != null)
                checkBoxExpertLinears20_2.Enabled = !comboBoxExpertLinears20_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears17_1.SelectedItem != null)
                checkBoxExpertLinears17_1.Enabled = !comboBoxExpertLinears17_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears17_2.SelectedItem != null)
                checkBoxExpertLinears17_2.Enabled = !comboBoxExpertLinears17_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears15_1.SelectedItem != null)
                checkBoxExpertLinears15_1.Enabled = !comboBoxExpertLinears15_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears15_2.SelectedItem != null)
                checkBoxExpertLinears15_2.Enabled = !comboBoxExpertLinears15_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears12_1.SelectedItem != null)
                checkBoxExpertLinears12_1.Enabled = !comboBoxExpertLinears12_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears12_2.SelectedItem != null)
                checkBoxExpertLinears12_2.Enabled = !comboBoxExpertLinears12_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears10_1.SelectedItem != null)
                checkBoxExpertLinears10_1.Enabled = !comboBoxExpertLinears10_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears10_2.SelectedItem != null)
                checkBoxExpertLinears10_2.Enabled = !comboBoxExpertLinears10_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears6_1.SelectedItem != null)
                checkBoxExpertLinears6_1.Enabled = !comboBoxExpertLinears6_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears6_2.SelectedItem != null)
                checkBoxExpertLinears6_2.Enabled = !comboBoxExpertLinears6_2.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears4_1.SelectedItem != null)
                checkBoxExpertLinears4_1.Enabled = !comboBoxExpertLinears4_1.SelectedItem.ToString().Equals("NO");
            if (comboBoxExpertLinears4_2.SelectedItem != null)
                checkBoxExpertLinears4_2.Enabled = !comboBoxExpertLinears4_2.SelectedItem.ToString().Equals("NO");
            Cursor.Current = Cursors.Default;
        }
        private void TabPageExpertLinears_Enter(object sender, EventArgs e)
        {
            this.BeginInvoke(new Action(() => TabPageExpertLinears_Init()));
        }

        private void CheckBoxExpertLinearsCheckAll(bool isChecked)
        {
            if (checkBoxExpertLinears160_1.Enabled) checkBoxExpertLinears160_1.Checked = isChecked;
            if (checkBoxExpertLinears160_2.Enabled) checkBoxExpertLinears160_2.Checked = isChecked;
            if (checkBoxExpertLinears80_1.Enabled) checkBoxExpertLinears80_1.Checked = isChecked;
            if (checkBoxExpertLinears80_2.Enabled) checkBoxExpertLinears80_2.Checked = isChecked;
            if (checkBoxExpertLinears60_1.Enabled) checkBoxExpertLinears60_1.Checked = isChecked;
            if (checkBoxExpertLinears60_2.Enabled) checkBoxExpertLinears60_2.Checked = isChecked;
            if (checkBoxExpertLinears40_1.Enabled) checkBoxExpertLinears40_1.Checked = isChecked;
            if (checkBoxExpertLinears40_2.Enabled) checkBoxExpertLinears40_2.Checked = isChecked;
            if (checkBoxExpertLinears30_1.Enabled) checkBoxExpertLinears30_1.Checked = isChecked;
            if (checkBoxExpertLinears30_2.Enabled) checkBoxExpertLinears30_2.Checked = isChecked;
            if (checkBoxExpertLinears20_1.Enabled) checkBoxExpertLinears20_1.Checked = isChecked;
            if (checkBoxExpertLinears20_2.Enabled) checkBoxExpertLinears20_2.Checked = isChecked;
            if (checkBoxExpertLinears17_1.Enabled) checkBoxExpertLinears17_1.Checked = isChecked;
            if (checkBoxExpertLinears17_2.Enabled) checkBoxExpertLinears17_2.Checked = isChecked;
            if (checkBoxExpertLinears15_1.Enabled) checkBoxExpertLinears15_1.Checked = isChecked;
            if (checkBoxExpertLinears15_2.Enabled) checkBoxExpertLinears15_2.Checked = isChecked;
            if (checkBoxExpertLinears12_1.Enabled) checkBoxExpertLinears12_1.Checked = isChecked;
            if (checkBoxExpertLinears12_2.Enabled) checkBoxExpertLinears12_2.Checked = isChecked;
            if (checkBoxExpertLinears10_1.Enabled) checkBoxExpertLinears10_1.Checked = isChecked;
            if (checkBoxExpertLinears10_2.Enabled) checkBoxExpertLinears10_2.Checked = isChecked;
            if (checkBoxExpertLinears6_1.Enabled) checkBoxExpertLinears6_1.Checked = isChecked;
            if (checkBoxExpertLinears6_2.Enabled) checkBoxExpertLinears6_2.Checked = isChecked;
            if (checkBoxExpertLinears4_1.Enabled) checkBoxExpertLinears4_1.Checked = isChecked;
            if (checkBoxExpertLinears4_2.Enabled) checkBoxExpertLinears4_2.Checked = isChecked;
        }
        private void CheckBoxExpertLinearsCheckedChanged(object sender, EventArgs e)
        {
            CheckBox myCheckBox = (CheckBox)sender;

            if (ModifierKeys.HasFlag(Keys.Control))
            {
                CheckBoxExpertLinearsCheckAll(myCheckBox.Checked);
            }
        }

        private void CheckBoxExpertLinears160_2_CheckedChanged(object sender, EventArgs e)
        {

        }

        private void CheckBoxExpertLinears80_1_CheckedChanged(object sender, EventArgs e)
        {
            CheckBox myCheckBox = (CheckBox)sender;

            if (ModifierKeys.HasFlag(Keys.Control))
            {
                CheckBoxExpertLinearsCheckAll(myCheckBox.Checked);
            }
        }

        private void Button1_Click_1(object sender, EventArgs e)
        {
            tuner1!.On();
        }

        private void Button2_Click(object sender, EventArgs e)
        {
            tuner1!.Off();
        }

        private void Button3_Click(object sender, EventArgs e)
        {
            tuner1!.On();
        }

        private void Button4_Click(object sender, EventArgs e)
        {
            tuner1!.Off();
        }

        private void ButtonTunePwr_Click(object sender, EventArgs e)
        {
            try
            {
                tuner1!.Operate(false);
                Thread.Sleep(500);
                if (buttonTunerPwr.BackColor == System.Drawing.Color.Yellow)
                    return;
                Cursor = Cursors.WaitCursor;

                if (buttonTunerPwr.BackColor != System.Drawing.Color.Green)
                {
                    buttonTunerPwr.BackColor = System.Drawing.Color.Yellow;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.Black;
                    buttonTunerPwr.Enabled = false;
                    buttonTunerPwr.Refresh();
                    if (tuner1 == null) return;
                    tuner1.On();
                    Thread.Sleep(100);
                    tuner1.On();
                    Thread.Sleep(2000);
                    tuner1.SetPowerLevel("Mid");
                    if (myRig != null)
                    {
                        myRig!.FrequencyA = myRig!.FrequencyA;
                        myRig!.FrequencyB = myRig!.FrequencyB;
                    }
                    buttonTunerPwr.Enabled = true;
                    buttonOperate.Enabled = true;
                    buttonPowerLevel.Enabled = true;
                    buttonTunerPwr.Refresh();
                    buttonTunerPwr.BackColor = System.Drawing.Color.Green;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.White;
                }
                else
                {
                    tuner1!.SetPowerLevel("Mid");
                    tuner1!.poweringDown = true;
                    Thread.Sleep(200);
                    Application.DoEvents();
                    buttonOperate.Enabled = false;
                    buttonPowerLevel.Enabled= false;
                    buttonTunerPwr.BackColor = System.Drawing.Color.Yellow;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.Black;
                    buttonTunerPwr.Enabled = false;
                    buttonTunerPwr.Refresh();
                    tuner1!.IsOn = false;
                    tuner1!.Off();
                    Thread.Sleep(100);
                    tuner1!.Off();
                    Thread.Sleep(3000);
                    tuner1!.poweringDown = false;
                    buttonTunerPwr.Enabled = true;
                    buttonTunerPwr.Refresh();
                    buttonTunerPwr.BackColor = System.Drawing.Color.LightGray;
                    buttonTunerPwr.ForeColor = System.Drawing.Color.Black;
                    if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS))
                        AntennaUpdateSelected(1);
                }
            }
            catch (Exception ex)
            {
                ShowCustomMessageBox("Tuner power on error: " + ex.Message + "\n" + ex.StackTrace);
            }
            Cursor = Cursors.Default;
        }

        private void TabPageExpertLinears_Click(object sender, EventArgs e)
        {
            //Cursor = Cursors.WaitCursor;
            //Cursor = Cursors.Default;
        }

        private void TabPageExpertLinears_Validated(object sender, EventArgs e)
        {
            Cursor = Cursors.Default;
        }

        private void TabPageExpertLinears_Validating(object sender, CancelEventArgs e)
        {
            Cursor = Cursors.Default;
        }

        private void TabPageExpertLinears_Paint(object sender, PaintEventArgs e)
        {
            Cursor = Cursors.Default;
        }

        private void TabPage_Selecting(object sender, TabControlCancelEventArgs e)
        {
            Cursor = Cursors.Default;
            TabControl tabControl = (TabControl)sender;
            tabControl.Show();
            Application.DoEvents();
            Cursor = Cursors.Default;
        }

        private void TabPage_Validated(object sender, EventArgs e)
        {
            Cursor = Cursors.Default;
        }

        private void TabPage_Selected(object sender, TabControlEventArgs e)
        {
            Cursor = Cursor.Current;
        }

        private void TabPage_Enter(object sender, EventArgs e)
        {
            //Application.DoEvents();
            //this.ResumeLayout();
        }

        private void RadioButtonBankA_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonBankA.Checked)
            {
                tuner1!.SelectDisplayPage();
                byte right = 0x10;
                byte left = 0x0f;
                byte set = 0x11;
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(left);
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(set);
                Thread.Sleep(1000);
                tuner1!.SelectDisplayPage();
                TabPageExpertLinears_Init();
                //radioButtonBankB.Checked = false;
            }
            else
            {
                radioButtonBankB.Checked = true;
                /*
                 * Bank B sequence
Set 
Set (Other Settings)
Right
Set
Right 4
Set

                 */
                tuner1!.SelectDisplayPage();
                byte right = 0x10;
                byte set = 0x11;
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(set);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(right);
                tuner1!.SendCmd(set);
                Thread.Sleep(1000);
                tuner1!.SelectDisplayPage();
                TabPageExpertLinears_Init();
            }
        }

        private void RadioButtonBankB_CheckedChanged(object sender, EventArgs e)
        {
            if (radioButtonBankB.Checked)
            {
                radioButtonBankA.Checked = false;
            }
            else
            {
                radioButtonBankA.Checked = true;
            }
        }

        private void ButtonSPEAnt_Click(object sender, EventArgs e)
        {
            // Seems we must be on display page to change antennas
            tuner1!.SelectDisplayPage();
            buttonSPEAnt.Text = "Ant " + tuner1.GetAntenna();
            tuner1!.SetAntenna(-1); // it's just a toggle for now
        }

        private void CheckedListBoxWalk1_ItemCheck(object sender, ItemCheckEventArgs e)
        {
            try
            {
                string itemText = ((CheckedListBox)sender).Items[e.Index].ToString();
                if (e.NewValue == CheckState.Checked)
                {

                }
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }


        }

        private void CheckedListBoxWalk1_Validated(object sender, EventArgs e)
        {
            FrequenciesToWalk();
            //FreqWalkSetFreq(0);
        }

        private void ButtonOperate_Click(object sender, EventArgs e)
        {
            try
            {
                //tuner1!.SelectDisplayPage();
                PowerSelect(myRig!.FrequencyA, myRig.ModeA, true, false);
                tuner1!.Operate(!tuner1.IsOperate);
            }

            catch (Exception ex)
            {
                ShowCustomMessageBox("Tuner power on error: " + ex.Message + "\n" + ex.StackTrace);
            }
            Cursor = Cursors.Default;

        }

        private void ButtonPowerLevel_Click(object sender, EventArgs e)
        {
            tuner1!.SendCmd(0x0b);
        }

        private void CheckBoxAmp1_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp1.Checked;
        }

        private void TextBoxPower1From_TextChanged(object sender, EventArgs e)
        {

        }

        private void TunePowerLimitCheck(TextBox textBox)
        {
            if (textBox.Text.Length > 0)
            {
                try
                {
                    if (Int32.TryParse(textBox.Text, out int power))
                    {
                        if (comboBoxTunerModel.Text.Contains(EXPERTLINEARS) && power > 40) textBox.Text = "40";
                    }
                }
                catch { }
            }
        }

        private void NumericUpDownFLRigBeforeWalk_ValueChanged(object sender, EventArgs e)
        {

        }

        private void CheckBoxAmp2_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp2.Checked;

        }

        private void CheckBoxAmp3_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp3.Checked;

        }

        private void CheckBoxAmp4_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp4.Checked;

        }

        private void CheckBoxAmp5_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp5.Checked;

        }

        private void CheckBoxAmp6_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp6.Checked;

        }

        private void CheckBoxAmp7_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp7.Checked;

        }

        private void CheckBoxAmp8_CheckedChanged(object sender, EventArgs e)
        {
            buttonOperate.Enabled = checkBoxAmp8.Checked;

        }

        private void TimerSWR_Tick(object sender, EventArgs e)
        {
            SWRUpdate();
        }
        private void TextBoxTune1Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune1Power);
        }

        private void TextBoxTune2Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune2Power);
        }

        private void TextBoxTune3Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune3Power);
        }

        private void TextBoxTune4Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune4Power);
        }

        private void TextBoxTune5Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune5Power);
        }

        private void TextBoxTune6Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune6Power);
        }

        private void TextBoxTune7Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune7Power);
        }

        private void TextBoxTune8Power_TextChanged(object sender, EventArgs e)
        {
            TunePowerLimitCheck(textBoxTune8Power);
        }
    }

    public class ComboBoxItem
    {
        public string? Text { get; set; }
        public object? MyGUID { get; set; }

        public override string? ToString()
        {
            return Text;
        }

        public override bool Equals(object obj)
        {
            if (obj is ComboBoxItem other)
            {
                if (ReferenceEquals(this, other)) return true;
                if (this.MyGUID != null && other.MyGUID != null)
                {
                    return this.MyGUID.Equals(other.MyGUID);
                }
            }
            return false;
        }

        public static bool operator ==(ComboBoxItem obj1, ComboBoxItem obj2)
        {
            if (ReferenceEquals(obj1, obj2)) return true;
            if (obj1 is null || obj2 is null) return false;
            return obj1.Equals(obj2);
        }

        public static bool operator !=(ComboBoxItem obj1, ComboBoxItem obj2)
        {
            return !(obj1 == obj2);
        }

        public override int GetHashCode()
        {
            return base.GetHashCode();
        }
    }

    [Serializable]
    public class AmpAutoTunerUtilException : System.Exception
    {
        private readonly Exception Inner;
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AmpAutoTunerUtilException() : base() { }
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AmpAutoTunerUtilException(string message) : base(message)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            //MessageBox.Show(message+"\n"+ Inner.Message +"\n" + this.StackTrace);
        }
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        public AmpAutoTunerUtilException(string message, System.Exception inner) : base(message, inner)
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        {
            if (string.IsNullOrEmpty(message))
            {
                throw new ArgumentException($"'{nameof(message)}' cannot be null or empty.", nameof(message));
            }

            this.Inner = inner ?? throw new ArgumentNullException(nameof(inner));
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
#pragma warning disable CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
        protected AmpAutoTunerUtilException(System.Runtime.Serialization.SerializationInfo info,
#pragma warning restore CS8618 // Non-nullable field must contain a non-null value when exiting constructor. Consider adding the 'required' modifier or declaring as nullable.
            System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
    }
}
