using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;

namespace AmpAutoTunerUtility
{
    class TunerExpertLinears : Tuner
    {
        private SerialPort SerialPortTuner = null;
        private bool runThread = false;
        char response = 'X';
        private string swr1 = "?";
        private string swr2 = "?";
        private string power = "?";
        private string temp1 = "?";
        private string antenna = "?";
        private string bandstr = "?";
        public bool tuning = false;
        //Byte[] responseOld = new byte[512];
        public TunerExpertLinears(string model, string comport, string baud, out string errmsg)
        {
            InitArrays();
            antennas = new string[12, 2];
            errmsg = null;
            this.comport = comport;
            this.model = model;
            if (!baud.Equals("115200"))
                baud = "115200"; // baud rate is fixed
            if (comport.Length == 0 || baud.Length == 0)
            {
                MessageBox.Show("com port(" + comport + ") or baud(" + baud + ") is empty");
                return;
            }
            //try
            //{
            SerialPortTuner = new SerialPort
            {
                PortName = comport,
                BaudRate = Int32.Parse(baud, CultureInfo.InvariantCulture),
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 1000,
                WriteTimeout = 500
            };
            SerialPortTuner.Open();
            Thread.Sleep(500);
            myThread = new Thread(new ThreadStart(this.ThreadTask))
            {
                IsBackground = true
            };
            myThread.Start();
            isOn = GetStatus();
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
            tuneFrequencies = new int[,]
                {
                { 24, 20, 1785 },
                { 29, 20, 3470 },
                { 20, 25, 5013 },
                { 19, 25, 6963 },
                {  3, 50, 10075 },
                { 12, 50, 13975 },
                {  3, 50, 18075 },
                { 14, 50, 20975 },
                { 3, 73, 24891 },
                { 18, 100, 28050 },
                { 18, 250, 49875 }
                    // no 4M -- not tunable
                };
        }
        //public override void Dispose(bool disposing)
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SerialPortTuner?.Dispose();
            }
        }
        public override void Close()
        {
            //myThread.Abort();
            runThread = false;
            Thread.Sleep(500);
            //while (myThread.IsAlive) Thread.Sleep(50);

            if (SerialPortTuner != null)
            {
                SerialPortTuner.Close();
                SerialPortTuner.Dispose();
                SerialPortTuner = null;
            }
        }
        static readonly Mutex SerialLock = new Mutex(false, "AmpSerial");

        public override void SelectDisplayPage()
        {
            Byte[] cmdDisplay = { 0x55, 0x55, 0x55, 0x01, 0x0c, 0x0c };
            SerialLock.WaitOne();
            SerialPortTuner.Write(cmdDisplay, 0, 6);
            Thread.Sleep(200);
        }

        public override void SelectManualTunePage()
        {
            // display, set, forward, set, when done display again
            Byte[] cmdSet = { 0x55, 0x55, 0x55, 0x01, 0x11, 0x11 };
            Byte[] cmdRight = { 0x55, 0x55, 0x55, 0x01, 0x10, 0x10 };
            SerialLock.WaitOne();
            // Have to go to home display page and display again
            SelectDisplayPage();
            //SelectDisplayPage();
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialLock.ReleaseMutex();
        }
        public override void SelectAntennaPage()
        {
            // display, set, forward, set, when done display again
            Byte[] cmdSet = { 0x55, 0x55, 0x55, 0x01, 0x11, 0x11 };
            Byte[] cmdRight = { 0x55, 0x55, 0x55, 0x01, 0x10, 0x10 };
            SerialLock.WaitOne();
            // Have to go to home display page and display again
            SelectDisplayPage();
            SelectDisplayPage();
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialLock.ReleaseMutex();
        }
        public override void SendCmd(byte cmd)
        {
            Byte[] cmdBuf = { 0x55, 0x55, 0x55, 0x01, cmd, cmd };
            SerialLock.WaitOne();
            SerialPortTuner.Write(cmdBuf, 0, 6);
            Thread.Sleep(150);
            SerialLock.ReleaseMutex();
        }

        readonly char[] lookup = { ' ', '!', '"', '#','$', '%', '&', '\\', '(', ')', '*', '+', ',', '-','.', '/','0','1','2','3','4','5','6','7','8','9',':',';','<','=','>','?','@','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','Z','[','\\','^','_','?','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z','{','|','}','?' };
        private Screen screenLast = Screen.Unknown;
        //public override bool GetStatus2(screen myScreen)
        public override bool GetStatus2(Screen myScreen = Screen.Unknown)
        {

            SerialLock.WaitOne();
            if (screenLast != myScreen)
            {
                if (myScreen == Screen.Unknown) MessageBox.Show("Where are we?\n");
                if (myScreen == Screen.Antenna) SelectAntennaPage();
                else if (myScreen == Screen.ManualTune) SelectManualTunePage();
                else SelectDisplayPage();
                screenLast = myScreen;
            }
            SerialPortTuner.DiscardInBuffer();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            Byte[] response = new Byte[512];
            SerialPortTuner.Write(cmd, 0, 6);
            Byte myByte;
            for (int i = 0; i < response.Length; ++i) response[i] = 0;
            myByte = 0x00;
            try
            {
                SerialPortTuner.Write(cmd, 0, 6);
            }
            catch (Exception)
            {
                SerialLock.ReleaseMutex();
                return false;
            }
            while (myByte != 0xaa)
                try
                {
                    myByte = (byte)SerialPortTuner.ReadByte();
                    //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Got " + String.Format("{0:X}", myByte));
                }
                catch (Exception ex)
                {
                    if (ex.HResult != -2146233083)
                        DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "timeout\n");
                    SerialLock.ReleaseMutex();
                    return false;
                }
            response[0] = myByte;
            response[1] = (byte)SerialPortTuner.ReadByte();
            if (response[1] != 0xaa) { SerialLock.ReleaseMutex(); return false; }
            response[2] = (byte)SerialPortTuner.ReadByte();
            if (response[2] != 0xaa) { SerialLock.ReleaseMutex(); return false; }
            response[3] = (byte)SerialPortTuner.ReadByte(); // shouold be 6a
            int n = 4;
            int count = 0;
            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "bytes=" + response[3]);
            for (int i = 0; i < 371 - 4; ++i)
            {
                try
                {
                    response[i + 4] = (byte)SerialPortTuner.ReadByte();
                    ++count;
                }
                catch (TimeoutException)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus serial timeout\n");
                    SerialLock.ReleaseMutex();
                    return false;
                }
                ++n;
            }
            long sum = 0;
            for (int i = 7; i < 369; ++i)
            {
                sum += response[i];
            }
            long chkByte0 = sum % 256;
            long chkByte1 = sum / 256;
            long resByte0 = response[369];
            long resByte1 = response[370];
            if (chkByte0 != resByte0) { SerialLock.ReleaseMutex(); return false; }
            if (chkByte1 != resByte1) { SerialLock.ReleaseMutex(); return false; }
            // check for M in MANUAL
            if (myScreen == Screen.Antenna && response[17] != 0x33) 
            { 
                SerialLock.ReleaseMutex(); 
                return false; 
            }
            else if (myScreen == Screen.ManualTune && response[17] != 0x2d) 
            { 
                SerialLock.ReleaseMutex(); 
                return false; 
            }
            byte ledStatus = response[8];
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "LED status: " + ledStatus.ToString("X2") + "\n");
            if (myScreen == Screen.ManualTune)
            {
                try
                {
                    while (response[160] != 0x0e)
                    {
                        return false;
                    };
                    string value = lookup[(int)response[156]].ToString();
                    value += lookup[(int)response[157]].ToString();
                    value += lookup[(int)response[158]].ToString();
                    value += lookup[(int)response[159]].ToString();
                    value += lookup[(int)response[160]].ToString();
                    value += lookup[(int)response[161]].ToString();
                    Capacitance = Double.Parse(value.Trim());
                    value = lookup[(int)response[116]].ToString();
                    value += lookup[(int)response[117]].ToString();
                    value += lookup[(int)response[118]].ToString();
                    value += lookup[(int)response[119]].ToString();
                    value += lookup[(int)response[120]].ToString();
                    value += lookup[(int)response[121]].ToString();
                    Inductance = Double.Parse(value.Trim());
                    //cIndex = (ulong)((response[161] << 16) | (response[158] << 8) | response[159]);
                    //lIndex = (ulong)((response[118] << 16) | (response[120] << 8) | response[121]);
                    string lohi = lookup[(int)response[166]].ToString();
                }
                catch (Exception ex) 
                {
                    MessageBox.Show("Error: " + ex.Message + "\n" + ex.StackTrace);
                }
            }
            /*
            for (int i = 0; i < 369; ++i)
            {
                if (responseOld[0] == 0xaa && responseOld[i] != response[i])
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Byte[" + i + "] old=" + responseOld[i].ToString("X2") + "new=" + response[i].ToString("X2") + "\n");
                }
            }
            responseOld = response;
            */
            tuning = response[8] == 0xb8;
            int indexBytes = 56;
            int[] bandLookup = { 0, 4, 8, 1, 5, 9, 2, 6, 10, 3, 7, 11 };
            if (myScreen == Screen.Antenna)
            {
                for (int i = 0; i < 12; ++i)
                {
                    int i2 = bandLookup[i];
                    if (response[indexBytes] == 46) antennas[i2, 0] = "NO";
                    else antennas[i2, 0] = (response[indexBytes] - 16).ToString();
                    if (response[indexBytes + 3] == 46) antennas[i2, 1] = "NO";
                    else antennas[i2, 1] = (response[indexBytes + 3] - 16).ToString();
                    indexBytes += 13;
                    if (i == 2 || i == 5 || i == 8) indexBytes++;
                }
            }
            SerialLock.ReleaseMutex();
            return true;
        }
        public override bool GetStatus()
        {
            if (SerialPortTuner == null) return false;
            SerialLock.WaitOne();
            SerialPortTuner.DiscardInBuffer();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x90, 0x90 };
            Byte[] response = new Byte[128];
            //SerialPortTuner.Write(cmd, 0, 6);
            Byte myByte;
            for (int i = 0; i < response.Length; ++i) response[i] = 0;
            myByte = 0x00;
            try
            {
                SerialPortTuner.Write(cmd, 0, 6);
            }
            catch (Exception)
            {
                SerialLock.ReleaseMutex();
                return false;
            }
            while (myByte != 0xaa)
            {
                try
                {
                    myByte = (byte)SerialPortTuner.ReadByte();
                    if (myByte == 0)
                    {
                        SerialLock.ReleaseMutex();
                        return false;
                    }
                    //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Got " + String.Format("{0:X}", myByte));
                }
                catch (Exception ex)
                {
                    if (ex.HResult != -2146233083)
                        DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "timeout\n");
                    SerialLock.ReleaseMutex();
                    return false;
                }
            }
            response[0] = myByte;
            response[1] = (byte)SerialPortTuner.ReadByte();
            if (response[1] != 0xaa) { SerialLock.ReleaseMutex(); return false; }
            response[2] = (byte)SerialPortTuner.ReadByte();
            if (response[2] != 0xaa) { SerialLock.ReleaseMutex(); return false; }
            response[3] = (byte)SerialPortTuner.ReadByte(); // should be the length
            int n = 4;
            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "bytes=" + response[3]);
            for (int i = 0; i < response[3] + 5; ++i)
            {
                try
                {
                    response[i + 4] = (byte)SerialPortTuner.ReadByte();
                }
                catch (TimeoutException)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus serial timeout\n");
                    SerialLock.ReleaseMutex();
                    return false;
                }
                ++n;
            }
            long sum = 0;
            for (int i = 0; i < 67; ++i)
            {
                sum += response[i + 4];
            }
            long chkByte0 = sum % 256;
            long chkByte1 = sum / 256;
            long resByte0 = response[71];
            long resByte1 = response[72];
            var sresponse = System.Text.Encoding.Default.GetString(response, 0, n - 5);
            if (chkByte0 != resByte0) { SerialLock.ReleaseMutex(); return false; }
            if (chkByte1 != resByte1) { SerialLock.ReleaseMutex(); return false; }

            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
            // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
            // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
            string[] mytokens = sresponse.Split(',');
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens.Length + ":" + sresponse + "\n");
            if (mytokens.Length > 20)
            {
                var newBand = mytokens[6];
                var newAntenna = mytokens[7];
                if (!bandstr.Equals(newBand))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed band " + bandstr + " to " + newBand + "\n");
                    Application.DoEvents();
                    bandstr = newBand;
                    band = int.Parse(bandstr);
                }
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "antenna=" + antenna + ", newAntenna=" + newAntenna + "\n");

                if (!antenna.Equals(newAntenna))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed antenna " + antenna + " to " + newAntenna + "\n");
                    Application.DoEvents();
                    antenna = newAntenna;
                }
                AntennaNumber = int.Parse(antenna.Substring(0, 1));
                model = mytokens[1];
                power = mytokens[10];
                swr1 = mytokens[11];
                SetSWR(Double.Parse(swr1));
                swr2 = mytokens[12];
                if (temp1.Equals("?")) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears connected\n");
                temp1 = mytokens[15];
                if (mytokens.Length >= 21)
                {
                    response[0] = (byte)mytokens[18][0];
                    response[1] = 0;
                    if (mytokens[18].Length > 0 && !mytokens[18].Equals("N"))
                    {
                        DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "MSG: " + mytokens[18] + "\n");
                    }
                }
                Application.DoEvents();
            }
            SerialLock.ReleaseMutex();
            return true;
        }

        readonly Thread myThread;
        private void ThreadTask()
        {
            runThread = true;
            while (runThread)
            {
                while (GetStatus() && runThread)
                {
                    //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears thread got status\n");
                    Thread.Sleep(1000);
                };
            }
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears thread stopped\n");
        }

        public override string GetSWRString()
        {
            return "SWR ATU/ANT" + swr1 + "/" + swr2;
        }
        public override string GetSerialPortTuner()
        {
            if (SerialPortTuner == null)
            {
                return (string)null;
            }
            return SerialPortTuner.PortName;
        }

        public override char ReadResponse()
        {
            return response;
        }

        public override string GetPower()
        {
            // Can't get power from the LDG tuner
            // So will have to use the rig power level instead elsewhere
            return "Pwr/Temp " + power + "/" + temp1 + "C";
        }
        public override int GetAntenna()
        {
            return int.Parse(antenna);
        }
        public override void SetAntenna(int antennaNumberRequested, bool tuneIsRunning = false)
        {
            try
            {
                //if (antennaNumberRequested != freqWalkAntenna)
                //{
                //    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "antennaNumberRequested " + antennaNumberRequested + " != freqWalkAntenna " + freqWalkAntenna)
                //    return;
                //}
                Thread.Sleep(1000);
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.TRACE, "SetAntenna " + antennaNumberRequested + " getting amp status\n");
                while (GetStatus() == false) ;
                if (int.TryParse(antenna.Substring(0, 1), out int tmp) == false)
                    Thread.Sleep(2000);
                if (int.Parse(antenna.Substring(0, 1)) == antennaNumberRequested)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Antenna already set to " + antennaNumberRequested + "\n");
                    return;
                }
                if (!antenna.Equals("?")) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "antenna=" + int.Parse(antenna.Substring(0, 1)) + ", antennaNumberRequest=" + antennaNumberRequested + "\n");
                Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x04, 0x04 };
                Byte[] response = new Byte[128];
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Setting antenna to other antenna\n");
                SerialPortTuner.Write(cmd, 0, 6);
                Thread.Sleep(500);
                while (GetStatus()) ;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tuner error: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace);
            }
        }

        public override void SetTuningMode(int mode)
        {
            if (mode == 2)
            {
                SelectAntennaPage();
            }
        }
        public override void Tune()
        {
            //SelectAntennaPage();

            SerialLock.WaitOne();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x09, 0x09 };
            Byte[] cmdMsg = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            SerialPortTuner.DiscardInBuffer();
            SerialPortTuner.Write(cmdMsg, 0, 6);
            Byte[] response;
            SerialPortTuner.Write(cmd, 0, 6);
            int loopCount = 0;
            while (tuning == false && ++loopCount < 20)
            {
                GetStatus2(Tuner.Screen.Antenna);
                Thread.Sleep(100);
            }
            if (tuning == false)
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "Tuning did not start!!\n");
                return;
            }
            while (tuning == true)
            {
                GetStatus2(Tuner.Screen.Antenna);
                Thread.Sleep(100);
            }
            //SelectDisplayPage();
        }

        public override void Poll()
        {
            return; //MDB
            while (!GetStatus()) ;
            //this.SelectAntennaPage();
            while (!GetStatus2(Screen.Antenna)) ;
        }
        private Dictionary <int, double> lDict;
        private Dictionary<int, double> cDict;

        private void InitArrays()
        {
            return;
            try
            {
                lDict = new Dictionary<int, double>();
                cDict = new Dictionary<int, double>();
                cDict.Add(150010, 0);
                cDict.Add(150012, 2.5);
                cDict.Add(100015, 5);
                cDict.Add(150017, 7.5);
                cDict.Add(101110, 10);
                cDict.Add(151112, 12.5);
                cDict.Add(101115, 15);
                cDict.Add(151117, 17.5);
                cDict.Add(101210, 20);
                cDict.Add(151212, 22.5);
                cDict.Add(101215, 25);
                cDict.Add(151217, 27.5);
                cDict.Add(101310, 30);
                cDict.Add(151312, 32.5);
                cDict.Add(101315, 35);
                cDict.Add(151317, 37.5);
                cDict.Add(101410, 40);
                cDict.Add(151412, 42.5);
                cDict.Add(101415, 45);
                cDict.Add(151417, 47.5);
                cDict.Add(101510, 50);
                cDict.Add(151512, 52.5);
                cDict.Add(101515, 55);
                cDict.Add(151517, 57.5);
                cDict.Add(101610, 60);
                cDict.Add(151612, 62.5);
                cDict.Add(101615, 65);
                cDict.Add(151617, 67.5);
                cDict.Add(101710, 70);
                cDict.Add(151712, 72.5);
                cDict.Add(101715, 75);
                cDict.Add(151717, 77.5);
                cDict.Add(101810, 80);
                cDict.Add(151812, 82.5);
                cDict.Add(101815, 85);
                cDict.Add(151817, 87.5);
                cDict.Add(101910, 90);
                cDict.Add(151912, 92.5);
                cDict.Add(101915, 95);
                cDict.Add(151917, 97.5);
                cDict.Add(101010, 100);
                cDict.Add(151012, 102.5);
                cDict.Add(101015, 105);
                cDict.Add(151017, 107.5);
                cDict.Add(101110, 110);
                cDict.Add(151112, 112.5);
                cDict.Add(101115, 115);
                cDict.Add(151117, 117.5);
                cDict.Add(101210, 120);
                cDict.Add(151212, 122.5);
                cDict.Add(101215, 125);
                cDict.Add(151217, 127.5);
                cDict.Add(101310, 130);
                cDict.Add(151312, 132.5);
                cDict.Add(101315, 135);
                cDict.Add(151317, 137.5);
                cDict.Add(101410, 140);
                cDict.Add(151412, 142.5);
                cDict.Add(101415, 145);
                cDict.Add(151417, 147.5);
                cDict.Add(101510, 150);
                cDict.Add(151512, 152.5);
                cDict.Add(101515, 155);
                cDict.Add(151517, 157.5);
                cDict.Add(101614, 164);
                cDict.Add(151616, 166.5);
                cDict.Add(101619, 169);
                cDict.Add(151711, 171.5);
                cDict.Add(101714, 174);
                cDict.Add(151716, 176.5);
                cDict.Add(101719, 179);
                cDict.Add(151811, 181.5);
                cDict.Add(101814, 184);
                cDict.Add(151816, 186.5);
                cDict.Add(101819, 189);
                cDict.Add(151911, 191.5);
                cDict.Add(101914, 194);
                cDict.Add(151916, 196.5);
                cDict.Add(101919, 199);
                cDict.Add(151011, 201.5);
                cDict.Add(101014, 204);
                cDict.Add(151016, 206.5);
                cDict.Add(101019, 209);
                cDict.Add(151111, 211.5);
                cDict.Add(101114, 214);
                cDict.Add(151116, 216.5);
                cDict.Add(101119, 219);
                cDict.Add(151211, 221.5);
                cDict.Add(101214, 224);
                cDict.Add(151216, 226.5);
                cDict.Add(101219, 229);
                cDict.Add(151311, 231.5);
                cDict.Add(101314, 234);
                cDict.Add(151316, 236.5);
                cDict.Add(101319, 239);
                cDict.Add(151411, 241.5);
                cDict.Add(101414, 244);
                cDict.Add(151416, 246.5);
                cDict.Add(101419, 249);
                cDict.Add(151511, 251.5);
                cDict.Add(101514, 254);
                cDict.Add(151516, 256.5);
                cDict.Add(101519, 259);
                cDict.Add(151611, 261.5);
                cDict.Add(101614, 264);
                cDict.Add(151616, 266.5);
                cDict.Add(101619, 269);
                cDict.Add(151711, 271.5);
                cDict.Add(101714, 274);
                cDict.Add(151716, 276.5);
                cDict.Add(101719, 279);
                cDict.Add(151811, 281.5);
                cDict.Add(101814, 284);
                cDict.Add(151816, 286.5);
                cDict.Add(101819, 289);
                cDict.Add(151911, 291.5);
                cDict.Add(101914, 294);
                cDict.Add(151916, 296.5);
                cDict.Add(101919, 299);
                cDict.Add(151011, 301.5);
                cDict.Add(101014, 304);
                cDict.Add(151016, 306.5);
                cDict.Add(101019, 309);
                cDict.Add(151111, 311.5);
                cDict.Add(101114, 314);
                cDict.Add(151116, 316.5);
                cDict.Add(101119, 319);
                cDict.Add(151211, 321.5);
                cDict.Add(101218, 328);
                cDict.Add(151310, 330.5);
                cDict.Add(101313, 333);
                cDict.Add(151315, 335.5);
                cDict.Add(101318, 338);
                cDict.Add(151410, 340.5);
                cDict.Add(101413, 343);
                cDict.Add(151415, 345.5);
                cDict.Add(101418, 348);
                cDict.Add(151510, 350.5);
                cDict.Add(101513, 353);
                cDict.Add(151515, 355.5);
                cDict.Add(101518, 358);
                cDict.Add(151610, 360.5);
                cDict.Add(101613, 363);
                cDict.Add(151615, 365.5);
                cDict.Add(101618, 368);
                cDict.Add(151710, 370.5);
                cDict.Add(101713, 373);
                cDict.Add(151715, 375.5);
                cDict.Add(101718, 378);
                cDict.Add(151810, 380.5);
                cDict.Add(101813, 383);
                cDict.Add(151815, 385.5);
                cDict.Add(101818, 388);
                cDict.Add(151910, 390.5);
                cDict.Add(101913, 393);
                cDict.Add(151915, 395.5);
                cDict.Add(101918, 398);
                cDict.Add(151010, 400.5);
                cDict.Add(101013, 403);
                cDict.Add(151015, 405.5);
                cDict.Add(101018, 408);
                cDict.Add(151110, 410.5);
                cDict.Add(101113, 413);
                cDict.Add(151115, 415.5);
                cDict.Add(101118, 418);
                cDict.Add(151210, 420.5);
                cDict.Add(101213, 423);
                cDict.Add(151215, 425.5);
                cDict.Add(101218, 428);
                cDict.Add(151310, 430.5);
                cDict.Add(101313, 433);
                cDict.Add(151315, 435.5);
                cDict.Add(101318, 438);
                cDict.Add(151410, 440.5);
                cDict.Add(101413, 443);
                cDict.Add(151415, 445.5);
                cDict.Add(101418, 448);
                cDict.Add(151510, 450.5);
                cDict.Add(101513, 453);
                cDict.Add(151515, 455.5);
                cDict.Add(101518, 458);
                cDict.Add(151610, 460.5);
                cDict.Add(101613, 463);
                cDict.Add(151615, 465.5);
                cDict.Add(101618, 468);
                cDict.Add(151710, 470.5);
                cDict.Add(101713, 473);
                cDict.Add(151715, 475.5);
                cDict.Add(101718, 478);
                cDict.Add(151810, 480.5);
                cDict.Add(101813, 483);
                cDict.Add(151815, 485.5);
                cDict.Add(101912, 492);
                cDict.Add(151914, 494.5);
                cDict.Add(101917, 497);
                cDict.Add(151919, 499.5);
                cDict.Add(101012, 502);
                cDict.Add(151014, 504.5);
                cDict.Add(101017, 507);
                cDict.Add(151019, 509.5);
                cDict.Add(101112, 512);
                cDict.Add(151114, 514.5);
                cDict.Add(101117, 517);
                cDict.Add(151119, 519.5);
                cDict.Add(101212, 522);
                cDict.Add(151214, 524.5);
                cDict.Add(101217, 527);
                cDict.Add(151219, 529.5);
                cDict.Add(101312, 532);
                cDict.Add(151314, 534.5);
                cDict.Add(101317, 537);
                cDict.Add(151319, 539.5);
                cDict.Add(101412, 542);
                cDict.Add(151414, 544.5);
                cDict.Add(101417, 547);
                cDict.Add(151419, 549.5);
                cDict.Add(101512, 552);
                cDict.Add(151514, 554.5);
                cDict.Add(101517, 557);
                cDict.Add(151519, 559.5);
                cDict.Add(101612, 562);
                cDict.Add(151614, 564.5);
                cDict.Add(101617, 567);
                cDict.Add(151619, 569.5);
                cDict.Add(101712, 572);
                cDict.Add(151714, 574.5);
                cDict.Add(101717, 577);
                cDict.Add(151719, 579.5);
                cDict.Add(101812, 582);
                cDict.Add(151814, 584.5);
                cDict.Add(101817, 587);
                cDict.Add(151819, 589.5);
                cDict.Add(101912, 592);
                cDict.Add(151914, 594.5);
                cDict.Add(101917, 597);
                cDict.Add(151919, 599.5);
                cDict.Add(101012, 602);
                cDict.Add(151014, 604.5);
                cDict.Add(101017, 607);
                cDict.Add(151019, 609.5);
                cDict.Add(101112, 612);
                cDict.Add(151114, 614.5);
                cDict.Add(101117, 617);
                cDict.Add(151119, 619.5);
                cDict.Add(101212, 622);
                cDict.Add(151214, 624.5);
                cDict.Add(101217, 627);
                cDict.Add(151219, 629.5);
                cDict.Add(101312, 632);
                cDict.Add(151314, 634.5);
                cDict.Add(101317, 637);
                cDict.Add(151319, 639.5);
                cDict.Add(101412, 642);
                cDict.Add(151414, 644.5);
                cDict.Add(101417, 647);
                cDict.Add(151419, 649.5);
                cDict.Add(101610, 660);
                cDict.Add(151612, 662.5);
                cDict.Add(101615, 665);
                cDict.Add(151617, 667.5);
                cDict.Add(101710, 670);
                cDict.Add(151712, 672.5);
                cDict.Add(101715, 675);
                cDict.Add(151717, 677.5);
                cDict.Add(101810, 680);
                cDict.Add(151812, 682.5);
                cDict.Add(101815, 685);
                cDict.Add(151817, 687.5);
                cDict.Add(101910, 690);
                cDict.Add(151912, 692.5);
                cDict.Add(101915, 695);
                cDict.Add(151917, 697.5);
                cDict.Add(101010, 700);
                cDict.Add(151012, 702.5);
                cDict.Add(101015, 705);
                cDict.Add(151017, 707.5);
                cDict.Add(101110, 710);
                cDict.Add(151112, 712.5);
                cDict.Add(101115, 715);
                cDict.Add(151117, 717.5);
                cDict.Add(101210, 720);
                cDict.Add(151212, 722.5);
                cDict.Add(101215, 725);
                cDict.Add(151217, 727.5);
                cDict.Add(101310, 730);
                cDict.Add(151312, 732.5);
                cDict.Add(101315, 735);
                cDict.Add(151317, 737.5);
                cDict.Add(101410, 740);
                cDict.Add(151412, 742.5);
                cDict.Add(101415, 745);
                cDict.Add(151417, 747.5);
                cDict.Add(101510, 750);
                cDict.Add(151512, 752.5);
                cDict.Add(101515, 755);
                cDict.Add(151517, 757.5);
                cDict.Add(101610, 760);
                cDict.Add(151612, 762.5);
                cDict.Add(101615, 765);
                cDict.Add(151617, 767.5);
                cDict.Add(101710, 770);
                cDict.Add(151712, 772.5);
                cDict.Add(101715, 775);
                cDict.Add(151717, 777.5);
                cDict.Add(101810, 780);
                cDict.Add(151812, 782.5);
                cDict.Add(101815, 785);
                cDict.Add(151817, 787.5);
                cDict.Add(101910, 790);
                cDict.Add(151912, 792.5);
                cDict.Add(101915, 795);
                cDict.Add(151917, 797.5);
                cDict.Add(101010, 800);
                cDict.Add(151012, 802.5);
                cDict.Add(101015, 805);
                cDict.Add(151017, 807.5);
                cDict.Add(101110, 810);
                cDict.Add(151112, 812.5);
                cDict.Add(101115, 815);
                cDict.Add(151117, 817.5);
                cDict.Add(101214, 824);
                cDict.Add(151216, 826.5);
                cDict.Add(101219, 829);
                cDict.Add(151311, 831.5);
                cDict.Add(101314, 834);
                cDict.Add(151316, 836.5);
                cDict.Add(101319, 839);
                cDict.Add(151411, 841.5);
                cDict.Add(101414, 844);
                cDict.Add(151416, 846.5);
                cDict.Add(101419, 849);
                cDict.Add(151511, 851.5);
                cDict.Add(101514, 854);
                cDict.Add(151516, 856.5);
                cDict.Add(101519, 859);
                cDict.Add(151611, 861.5);
                cDict.Add(101614, 864);
                cDict.Add(151616, 866.5);
                cDict.Add(101619, 869);
                cDict.Add(151711, 871.5);
                cDict.Add(101714, 874);
                cDict.Add(151716, 876.5);
                cDict.Add(101719, 879);
                cDict.Add(151811, 881.5);
                cDict.Add(101814, 884);
                cDict.Add(151816, 886.5);
                cDict.Add(101819, 889);
                cDict.Add(151911, 891.5);
                cDict.Add(101914, 894);
                cDict.Add(151916, 896.5);
                cDict.Add(101919, 899);
                cDict.Add(151011, 901.5);
                cDict.Add(101014, 904);
                cDict.Add(151016, 906.5);
                cDict.Add(101019, 909);
                cDict.Add(151111, 911.5);
                cDict.Add(101114, 914);
                cDict.Add(151116, 916.5);
                cDict.Add(101119, 919);
                cDict.Add(151211, 921.5);
                cDict.Add(101214, 924);
                cDict.Add(151216, 926.5);
                cDict.Add(101219, 929);
                cDict.Add(151311, 931.5);
                cDict.Add(101314, 934);
                cDict.Add(151316, 936.5);
                cDict.Add(101319, 939);
                cDict.Add(151411, 941.5);
                cDict.Add(101414, 944);
                cDict.Add(151416, 946.5);
                cDict.Add(101419, 949);
                cDict.Add(151511, 951.5);
                cDict.Add(101514, 954);
                cDict.Add(151516, 956.5);
                cDict.Add(101519, 959);
                cDict.Add(151611, 961.5);
                cDict.Add(101614, 964);
                cDict.Add(151616, 966.5);
                cDict.Add(101619, 969);
                cDict.Add(151711, 971.5);
                cDict.Add(101714, 974);
                cDict.Add(151716, 976.5);
                cDict.Add(101719, 979);
                cDict.Add(151811, 981.5);
                cDict.Add(101818, 988);
                cDict.Add(151910, 990.5);
                cDict.Add(101913, 993);
                cDict.Add(151915, 995.5);
                cDict.Add(101918, 998);
                cDict.Add(151010, 1000.5);
                cDict.Add(101013, 1003);
                cDict.Add(151015, 1005.5);
                cDict.Add(101018, 1008);
                cDict.Add(151110, 1010.5);
                cDict.Add(101113, 1013);
                cDict.Add(151115, 1015.5);
                cDict.Add(101118, 1018);
                cDict.Add(151210, 1020.5);
                cDict.Add(101213, 1023);
                cDict.Add(151215, 1025.5);
                cDict.Add(101218, 1028);
                cDict.Add(151310, 1030.5);
                cDict.Add(101313, 1033);
                cDict.Add(151315, 1035.5);
                cDict.Add(101318, 1038);
                cDict.Add(151410, 1040.5);
                cDict.Add(101413, 1043);
                cDict.Add(151415, 1045.5);
                cDict.Add(101418, 1048);
                cDict.Add(151510, 1050.5);
                cDict.Add(101513, 1053);
                cDict.Add(151515, 1055.5);
                cDict.Add(101518, 1058);
                cDict.Add(151610, 1060.5);
                cDict.Add(101613, 1063);
                cDict.Add(151615, 1065.5);
                cDict.Add(101618, 1068);
                cDict.Add(151710, 1070.5);
                cDict.Add(101713, 1073);
                cDict.Add(151715, 1075.5);
                cDict.Add(101718, 1078);
                cDict.Add(151810, 1080.5);
                cDict.Add(101813, 1083);
                cDict.Add(151815, 1085.5);
                cDict.Add(101818, 1088);
                cDict.Add(151910, 1090.5);
                cDict.Add(101913, 1093);
                cDict.Add(151915, 1095.5);
                cDict.Add(101918, 1098);
                cDict.Add(151010, 1100.5);
                cDict.Add(101013, 1103);
                cDict.Add(151015, 1105.5);
                cDict.Add(101018, 1108);
                cDict.Add(151110, 1110.5);
                cDict.Add(101113, 1113);
                cDict.Add(151115, 1115.5);
                cDict.Add(101118, 1118);
                cDict.Add(151210, 1120.5);
                cDict.Add(101213, 1123);
                cDict.Add(151215, 1125.5);
                cDict.Add(101218, 1128);
                cDict.Add(151310, 1130.5);
                cDict.Add(101313, 1133);
                cDict.Add(151315, 1135.5);
                cDict.Add(101318, 1138);
                cDict.Add(151410, 1140.5);
                cDict.Add(101413, 1143);
                cDict.Add(151415, 1145.5);
                cDict.Add(101512, 1152);
                cDict.Add(151514, 1154.5);
                cDict.Add(101517, 1157);
                cDict.Add(151519, 1159.5);
                cDict.Add(101612, 1162);
                cDict.Add(151614, 1164.5);
                cDict.Add(101617, 1167);
                cDict.Add(151619, 1169.5);
                cDict.Add(101712, 1172);
                cDict.Add(151714, 1174.5);
                cDict.Add(101717, 1177);
                cDict.Add(151719, 1179.5);
                cDict.Add(101812, 1182);
                cDict.Add(151814, 1184.5);
                cDict.Add(101817, 1187);
                cDict.Add(151819, 1189.5);
                cDict.Add(101912, 1192);
                cDict.Add(151914, 1194.5);
                cDict.Add(101917, 1197);
                cDict.Add(151919, 1199.5);
                cDict.Add(101012, 1202);
                cDict.Add(151014, 1204.5);
                cDict.Add(101017, 1207);
                cDict.Add(151019, 1209.5);
                cDict.Add(101112, 1212);
                cDict.Add(151114, 1214.5);
                cDict.Add(101117, 1217);
                cDict.Add(151119, 1219.5);
                cDict.Add(101212, 1222);
                cDict.Add(151214, 1224.5);
                cDict.Add(101217, 1227);
                cDict.Add(151219, 1229.5);
                cDict.Add(101312, 1232);
                cDict.Add(151314, 1234.5);
                cDict.Add(101317, 1237);
                cDict.Add(151319, 1239.5);
                cDict.Add(101412, 1242);
                cDict.Add(151414, 1244.5);
                cDict.Add(101417, 1247);
                cDict.Add(151419, 1249.5);
                cDict.Add(101512, 1252);
                cDict.Add(151514, 1254.5);
                cDict.Add(101517, 1257);
                cDict.Add(151519, 1259.5);
                cDict.Add(101612, 1262);
                cDict.Add(151614, 1264.5);
                cDict.Add(101617, 1267);
                cDict.Add(151619, 1269.5);
                cDict.Add(101712, 1272);
                cDict.Add(151714, 1274.5);
                cDict.Add(101717, 1277);
                cDict.Add(151719, 1279.5);
                cDict.Add(101812, 1282);
                cDict.Add(151814, 1284.5);
                cDict.Add(101817, 1287);
                cDict.Add(151819, 1289.5);
                cDict.Add(101912, 1292);
                cDict.Add(151914, 1294.5);
                cDict.Add(101917, 1297);
                cDict.Add(151919, 1299.5);
                cDict.Add(101012, 1302);
                cDict.Add(151014, 1304.5);
                cDict.Add(101017, 1307);
                cDict.Add(151019, 1309.5);
                cDict.Add(101210, 1320);
                cDict.Add(151212, 1322.5);
                cDict.Add(101215, 1325);
                cDict.Add(151217, 1327.5);
                cDict.Add(101310, 1330);
                cDict.Add(151312, 1332.5);
                cDict.Add(101315, 1335);
                cDict.Add(151317, 1337.5);
                cDict.Add(101410, 1340);
                cDict.Add(151412, 1342.5);
                cDict.Add(101415, 1345);
                cDict.Add(151417, 1347.5);
                cDict.Add(101510, 1350);
                cDict.Add(151512, 1352.5);
                cDict.Add(101515, 1355);
                cDict.Add(151517, 1357.5);
                cDict.Add(101610, 1360);
                cDict.Add(151612, 1362.5);
                cDict.Add(101615, 1365);
                cDict.Add(151617, 1367.5);
                cDict.Add(101710, 1370);
                cDict.Add(151712, 1372.5);
                cDict.Add(101715, 1375);
                cDict.Add(151717, 1377.5);
                cDict.Add(101810, 1380);
                cDict.Add(151812, 1382.5);
                cDict.Add(101815, 1385);
                cDict.Add(151817, 1387.5);
                cDict.Add(101910, 1390);
                cDict.Add(151912, 1392.5);
                cDict.Add(101915, 1395);
                cDict.Add(151917, 1397.5);
                cDict.Add(101010, 1400);
                cDict.Add(151012, 1402.5);
                cDict.Add(101015, 1405);
                cDict.Add(151017, 1407.5);
                cDict.Add(101110, 1410);
                cDict.Add(151112, 1412.5);
                cDict.Add(101115, 1415);
                cDict.Add(151117, 1417.5);
                cDict.Add(101210, 1420);
                cDict.Add(151212, 1422.5);
                cDict.Add(101215, 1425);
                cDict.Add(151217, 1427.5);
                cDict.Add(101310, 1430);
                cDict.Add(151312, 1432.5);
                cDict.Add(101315, 1435);
                cDict.Add(151317, 1437.5);
                cDict.Add(101410, 1440);
                cDict.Add(151412, 1442.5);
                cDict.Add(101415, 1445);
                cDict.Add(151417, 1447.5);
                cDict.Add(101510, 1450);
                cDict.Add(151512, 1452.5);
                cDict.Add(101515, 1455);
                cDict.Add(151517, 1457.5);
                cDict.Add(101610, 1460);
                cDict.Add(151612, 1462.5);
                cDict.Add(101615, 1465);
                cDict.Add(151617, 1467.5);
                cDict.Add(101710, 1470);
                cDict.Add(151712, 1472.5);
                cDict.Add(101715, 1475);
                cDict.Add(151717, 1477.5);
                cDict.Add(101814, 1484);
                cDict.Add(151816, 1486.5);
                cDict.Add(101819, 1489);
                cDict.Add(151911, 1491.5);
                cDict.Add(101914, 1494);
                cDict.Add(151916, 1496.5);
                cDict.Add(101919, 1499);
                cDict.Add(151011, 1501.5);
                cDict.Add(101014, 1504);
                cDict.Add(151016, 1506.5);
                cDict.Add(101019, 1509);
                cDict.Add(151111, 1511.5);
                cDict.Add(101114, 1514);
                cDict.Add(151116, 1516.5);
                cDict.Add(101119, 1519);
                cDict.Add(151211, 1521.5);
                cDict.Add(101214, 1524);
                cDict.Add(151216, 1526.5);
                cDict.Add(101219, 1529);
                cDict.Add(151311, 1531.5);
                cDict.Add(101314, 1534);
                cDict.Add(151316, 1536.5);
                cDict.Add(101319, 1539);
                cDict.Add(151411, 1541.5);
                cDict.Add(101414, 1544);
                cDict.Add(151416, 1546.5);
                cDict.Add(101419, 1549);
                cDict.Add(151511, 1551.5);
                cDict.Add(101514, 1554);
                cDict.Add(151516, 1556.5);
                cDict.Add(101519, 1559);
                cDict.Add(151611, 1561.5);
                cDict.Add(101614, 1564);
                cDict.Add(151616, 1566.5);
                cDict.Add(101619, 1569);
                cDict.Add(151711, 1571.5);
                cDict.Add(101714, 1574);
                cDict.Add(151716, 1576.5);
                cDict.Add(101719, 1579);
                cDict.Add(151811, 1581.5);
                cDict.Add(101814, 1584);
                cDict.Add(151816, 1586.5);
                cDict.Add(101819, 1589);
                cDict.Add(151911, 1591.5);
                cDict.Add(101914, 1594);
                cDict.Add(151916, 1596.5);
                cDict.Add(101919, 1599);
                cDict.Add(151011, 1601.5);
                cDict.Add(101014, 1604);
                cDict.Add(151016, 1606.5);
                cDict.Add(101019, 1609);
                cDict.Add(151111, 1611.5);
                cDict.Add(101114, 1614);
                cDict.Add(151116, 1616.5);
                cDict.Add(101119, 1619);
                cDict.Add(151211, 1621.5);
                cDict.Add(101214, 1624);
                cDict.Add(151216, 1626.5);
                cDict.Add(101219, 1629);
                cDict.Add(151311, 1631.5);
                cDict.Add(101314, 1634);
                cDict.Add(151316, 1636.5);
                cDict.Add(101319, 1639);
                cDict.Add(151411, 1641.5);
                cDict.Add(101418, 1648);
                cDict.Add(151510, 1650.5);
                cDict.Add(101513, 1653);
                cDict.Add(151515, 1655.5);
                cDict.Add(101518, 1658);
                cDict.Add(151610, 1660.5);
                cDict.Add(101613, 1663);
                cDict.Add(151615, 1665.5);
                cDict.Add(101618, 1668);
                cDict.Add(151710, 1670.5);
                cDict.Add(101713, 1673);
                cDict.Add(151715, 1675.5);
                cDict.Add(101718, 1678);
                cDict.Add(151810, 1680.5);
                cDict.Add(101813, 1683);
                cDict.Add(151815, 1685.5);
                cDict.Add(101818, 1688);
                cDict.Add(151910, 1690.5);
                cDict.Add(101913, 1693);
                cDict.Add(151915, 1695.5);
                cDict.Add(101918, 1698);
                cDict.Add(151010, 1700.5);
                cDict.Add(101013, 1703);
                cDict.Add(151015, 1705.5);
                cDict.Add(101018, 1708);
                cDict.Add(151110, 1710.5);
                cDict.Add(101113, 1713);
                cDict.Add(151115, 1715.5);
                cDict.Add(101118, 1718);
                cDict.Add(151210, 1720.5);
                cDict.Add(101213, 1723);
                cDict.Add(151215, 1725.5);
                cDict.Add(101218, 1728);
                cDict.Add(151310, 1730.5);
                cDict.Add(101313, 1733);
                cDict.Add(151315, 1735.5);
                cDict.Add(101318, 1738);
                cDict.Add(151410, 1740.5);
                cDict.Add(101413, 1743);
                cDict.Add(151415, 1745.5);
                cDict.Add(101418, 1748);
                cDict.Add(151510, 1750.5);
                cDict.Add(101513, 1753);
                cDict.Add(151515, 1755.5);
                cDict.Add(101518, 1758);
                cDict.Add(151610, 1760.5);
                cDict.Add(101613, 1763);
                cDict.Add(151615, 1765.5);
                cDict.Add(101618, 1768);
                cDict.Add(151710, 1770.5);
                cDict.Add(101713, 1773);
                cDict.Add(151715, 1775.5);
                cDict.Add(101718, 1778);
                cDict.Add(151810, 1780.5);
                cDict.Add(101813, 1783);
                cDict.Add(151815, 1785.5);
                cDict.Add(101818, 1788);
                cDict.Add(151910, 1790.5);
                cDict.Add(101913, 1793);
                cDict.Add(151915, 1795.5);
                cDict.Add(101918, 1798);
                cDict.Add(151010, 1800.5);
                cDict.Add(101013, 1803);
                cDict.Add(151015, 1805.5);
                cDict.Add(101112, 1812);
                cDict.Add(151114, 1814.5);
                cDict.Add(101117, 1817);
                cDict.Add(151119, 1819.5);
                cDict.Add(101212, 1822);
                cDict.Add(151214, 1824.5);
                cDict.Add(101217, 1827);
                cDict.Add(151219, 1829.5);
                cDict.Add(101312, 1832);
                cDict.Add(151314, 1834.5);
                cDict.Add(101317, 1837);
                cDict.Add(151319, 1839.5);
                cDict.Add(101412, 1842);
                cDict.Add(151414, 1844.5);
                cDict.Add(101417, 1847);
                cDict.Add(151419, 1849.5);
                cDict.Add(101512, 1852);
                cDict.Add(151514, 1854.5);
                cDict.Add(101517, 1857);
                cDict.Add(151519, 1859.5);
                cDict.Add(101612, 1862);
                cDict.Add(151614, 1864.5);
                cDict.Add(101617, 1867);
                cDict.Add(151619, 1869.5);
                cDict.Add(101712, 1872);
                cDict.Add(151714, 1874.5);
                cDict.Add(101717, 1877);
                cDict.Add(151719, 1879.5);
                cDict.Add(101812, 1882);
                cDict.Add(151814, 1884.5);
                cDict.Add(101817, 1887);
                cDict.Add(151819, 1889.5);
                cDict.Add(101912, 1892);
                cDict.Add(151914, 1894.5);
                cDict.Add(101917, 1897);
                cDict.Add(151919, 1899.5);
                cDict.Add(101012, 1902);
                cDict.Add(151014, 1904.5);
                cDict.Add(101017, 1907);
                cDict.Add(151019, 1909.5);
                cDict.Add(101112, 1912);
                cDict.Add(151114, 1914.5);
                cDict.Add(101117, 1917);
                cDict.Add(151119, 1919.5);
                cDict.Add(101212, 1922);
                cDict.Add(151214, 1924.5);
                cDict.Add(101217, 1927);
                cDict.Add(151219, 1929.5);
                cDict.Add(101312, 1932);
                cDict.Add(151314, 1934.5);
                cDict.Add(101317, 1937);
                cDict.Add(151319, 1939.5);
                cDict.Add(101412, 1942);
                cDict.Add(151414, 1944.5);
                cDict.Add(101417, 1947);
                cDict.Add(151419, 1949.5);
                cDict.Add(101512, 1952);
                cDict.Add(151514, 1954.5);
                cDict.Add(101517, 1957);
                cDict.Add(151519, 1959.5);
                cDict.Add(101612, 1962);
                cDict.Add(151614, 1964.5);
                cDict.Add(101617, 1967);
                cDict.Add(151619, 1969.5);
                cDict.Add(101810, 1980);
                cDict.Add(151812, 1982.5);
                cDict.Add(101815, 1985);
                cDict.Add(151817, 1987.5);
                cDict.Add(101910, 1990);
                cDict.Add(151912, 1992.5);
                cDict.Add(101915, 1995);
                cDict.Add(151917, 1997.5);
                cDict.Add(101010, 2000);
                cDict.Add(151012, 2002.5);
                cDict.Add(101015, 2005);
                cDict.Add(151017, 2007.5);
                cDict.Add(101110, 2010);
                cDict.Add(151112, 2012.5);
                cDict.Add(101115, 2015);
                cDict.Add(151117, 2017.5);
                cDict.Add(101210, 2020);
                cDict.Add(151212, 2022.5);
                cDict.Add(101215, 2025);
                cDict.Add(151217, 2027.5);
                cDict.Add(101310, 2030);
                cDict.Add(151312, 2032.5);
                cDict.Add(101315, 2035);
                cDict.Add(151317, 2037.5);
                cDict.Add(101410, 2040);
                cDict.Add(151412, 2042.5);
                cDict.Add(101415, 2045);
                cDict.Add(151417, 2047.5);
                cDict.Add(101510, 2050);
                cDict.Add(151512, 2052.5);
                cDict.Add(101515, 2055);
                cDict.Add(151517, 2057.5);
                cDict.Add(101610, 2060);
                cDict.Add(151612, 2062.5);
                cDict.Add(101615, 2065);
                cDict.Add(151617, 2067.5);
                cDict.Add(101710, 2070);
                cDict.Add(151712, 2072.5);
                cDict.Add(101715, 2075);
                cDict.Add(151717, 2077.5);
                cDict.Add(101810, 2080);
                cDict.Add(151812, 2082.5);
                cDict.Add(101815, 2085);
                cDict.Add(151817, 2087.5);
                cDict.Add(101910, 2090);
                cDict.Add(151912, 2092.5);
                cDict.Add(101915, 2095);
                cDict.Add(151917, 2097.5);
                cDict.Add(101010, 2100);
                cDict.Add(151012, 2102.5);
                cDict.Add(101015, 2105);
                cDict.Add(151017, 2107.5);
                cDict.Add(101110, 2110);
                cDict.Add(151112, 2112.5);
                cDict.Add(101115, 2115);
                cDict.Add(151117, 2117.5);
                cDict.Add(101210, 2120);
                cDict.Add(151212, 2122.5);
                cDict.Add(101215, 2125);
                cDict.Add(151217, 2127.5);
                cDict.Add(101310, 2130);
                cDict.Add(151312, 2132.5);
                cDict.Add(101315, 2135);
                cDict.Add(151317, 2137.5);
                cDict.Add(101414, 2144);
                cDict.Add(151416, 2146.5);
                cDict.Add(101419, 2149);
                cDict.Add(151511, 2151.5);
                cDict.Add(101514, 2154);
                cDict.Add(151516, 2156.5);
                cDict.Add(101519, 2159);
                cDict.Add(151611, 2161.5);
                cDict.Add(101614, 2164);
                cDict.Add(151616, 2166.5);
                cDict.Add(101619, 2169);
                cDict.Add(151711, 2171.5);
                cDict.Add(101714, 2174);
                cDict.Add(151716, 2176.5);
                cDict.Add(101719, 2179);
                cDict.Add(151811, 2181.5);
                cDict.Add(101814, 2184);
                cDict.Add(151816, 2186.5);
                cDict.Add(101819, 2189);
                cDict.Add(151911, 2191.5);
                cDict.Add(101914, 2194);
                cDict.Add(151916, 2196.5);
                cDict.Add(101919, 2199);
                cDict.Add(151011, 2201.5);
                cDict.Add(101014, 2204);
                cDict.Add(151016, 2206.5);
                cDict.Add(101019, 2209);
                cDict.Add(151111, 2211.5);
                cDict.Add(101114, 2214);
                cDict.Add(151116, 2216.5);
                cDict.Add(101119, 2219);
                cDict.Add(151211, 2221.5);
                cDict.Add(101214, 2224);
                cDict.Add(151216, 2226.5);
                cDict.Add(101219, 2229);
                cDict.Add(151311, 2231.5);
                cDict.Add(101314, 2234);
                cDict.Add(151316, 2236.5);
                cDict.Add(101319, 2239);
                cDict.Add(151411, 2241.5);
                cDict.Add(101414, 2244);
                cDict.Add(151416, 2246.5);
                cDict.Add(101419, 2249);
                cDict.Add(151511, 2251.5);
                cDict.Add(101514, 2254);
                cDict.Add(151516, 2256.5);
                cDict.Add(101519, 2259);
                cDict.Add(151611, 2261.5);
                cDict.Add(101614, 2264);
                cDict.Add(151616, 2266.5);
                cDict.Add(101619, 2269);
                cDict.Add(151711, 2271.5);
                cDict.Add(101714, 2274);
                cDict.Add(151716, 2276.5);
                cDict.Add(101719, 2279);
                cDict.Add(151811, 2281.5);
                cDict.Add(101814, 2284);
                cDict.Add(151816, 2286.5);
                cDict.Add(101819, 2289);
                cDict.Add(151911, 2291.5);
                cDict.Add(101914, 2294);
                cDict.Add(151916, 2296.5);
                cDict.Add(101919, 2299);
                cDict.Add(151011, 2301.5);
                cDict.Add(101018, 2308);
                cDict.Add(151110, 2310.5);
                cDict.Add(101113, 2313);
                cDict.Add(151115, 2315.5);
                cDict.Add(101118, 2318);
                cDict.Add(151210, 2320.5);
                cDict.Add(101213, 2323);
                cDict.Add(151215, 2325.5);
                cDict.Add(101218, 2328);
                cDict.Add(151310, 2330.5);
                cDict.Add(101313, 2333);
                cDict.Add(151315, 2335.5);
                cDict.Add(101318, 2338);
                cDict.Add(151410, 2340.5);
                cDict.Add(101413, 2343);
                cDict.Add(151415, 2345.5);
                cDict.Add(101418, 2348);
                cDict.Add(151510, 2350.5);
                cDict.Add(101513, 2353);
                cDict.Add(151515, 2355.5);
                cDict.Add(101518, 2358);
                cDict.Add(151610, 2360.5);
                cDict.Add(101613, 2363);
                cDict.Add(151615, 2365.5);
                cDict.Add(101618, 2368);
                cDict.Add(151710, 2370.5);
                cDict.Add(101713, 2373);
                cDict.Add(151715, 2375.5);
                cDict.Add(101718, 2378);
                cDict.Add(151810, 2380.5);
                cDict.Add(101813, 2383);
                cDict.Add(151815, 2385.5);
                cDict.Add(101818, 2388);
                cDict.Add(151910, 2390.5);
                cDict.Add(101913, 2393);
                cDict.Add(151915, 2395.5);
                cDict.Add(101918, 2398);
                cDict.Add(151010, 2400.5);
                cDict.Add(101013, 2403);
                cDict.Add(151015, 2405.5);
                cDict.Add(101018, 2408);
                cDict.Add(151110, 2410.5);
                cDict.Add(101113, 2413);
                cDict.Add(151115, 2415.5);
                cDict.Add(101118, 2418);
                cDict.Add(151210, 2420.5);
                cDict.Add(101213, 2423);
                cDict.Add(151215, 2425.5);
                cDict.Add(101218, 2428);
                cDict.Add(151310, 2430.5);
                cDict.Add(101313, 2433);
                cDict.Add(151315, 2435.5);
                cDict.Add(101318, 2438);
                cDict.Add(151410, 2440.5);
                cDict.Add(101413, 2443);
                cDict.Add(151415, 2445.5);
                cDict.Add(101418, 2448);
                cDict.Add(151510, 2450.5);
                cDict.Add(101513, 2453);
                cDict.Add(151515, 2455.5);
                cDict.Add(101518, 2458);
                cDict.Add(151610, 2460.5);
                cDict.Add(101613, 2463);
                cDict.Add(151615, 2465.5);
                cDict.Add(101712, 2472);
                cDict.Add(151714, 2474.5);
                cDict.Add(101717, 2477);
                cDict.Add(151719, 2479.5);
                cDict.Add(101812, 2482);
                cDict.Add(151814, 2484.5);
                cDict.Add(101817, 2487);
                cDict.Add(151819, 2489.5);
                cDict.Add(101912, 2492);
                cDict.Add(151914, 2494.5);
                cDict.Add(101917, 2497);
                cDict.Add(151919, 2499.5);
                cDict.Add(101012, 2502);
                cDict.Add(151014, 2504.5);
                cDict.Add(101017, 2507);
                cDict.Add(151019, 2509.5);
                cDict.Add(101112, 2512);
                cDict.Add(151114, 2514.5);
                cDict.Add(101117, 2517);
                cDict.Add(151119, 2519.5);
                cDict.Add(101212, 2522);
                cDict.Add(151214, 2524.5);
                cDict.Add(101217, 2527);
                cDict.Add(151219, 2529.5);
                cDict.Add(101312, 2532);
                cDict.Add(151314, 2534.5);
                cDict.Add(101317, 2537);
                cDict.Add(151319, 2539.5);
                cDict.Add(101412, 2542);
                cDict.Add(151414, 2544.5);
                cDict.Add(101417, 2547);
                cDict.Add(151419, 2549.5);
                cDict.Add(101512, 2552);
                cDict.Add(151514, 2554.5);
                cDict.Add(101517, 2557);
                cDict.Add(151519, 2559.5);
                cDict.Add(101612, 2562);
                cDict.Add(151614, 2564.5);
                cDict.Add(101617, 2567);
                cDict.Add(151619, 2569.5);
                cDict.Add(101712, 2572);
                cDict.Add(151714, 2574.5);
                cDict.Add(101717, 2577);
                cDict.Add(151719, 2579.5);
                cDict.Add(101812, 2582);
                cDict.Add(151814, 2584.5);
                cDict.Add(101817, 2587);
                cDict.Add(151819, 2589.5);
                cDict.Add(101912, 2592);
                cDict.Add(151914, 2594.5);
                cDict.Add(101917, 2597);
                cDict.Add(151919, 2599.5);
                cDict.Add(101012, 2602);
                cDict.Add(151014, 2604.5);
                cDict.Add(101017, 2607);
                cDict.Add(151019, 2609.5);
                cDict.Add(101112, 2612);
                cDict.Add(151114, 2614.5);
                cDict.Add(101117, 2617);
                cDict.Add(151119, 2619.5);
                cDict.Add(101212, 2622);
                cDict.Add(151214, 2624.5);
                cDict.Add(101217, 2627);
                cDict.Add(151219, 2629.5);

                lDict.Add(100010, 0);
                lDict.Add(101015, 0.05);
                lDict.Add(101110, 0.1);
                lDict.Add(101115, 0.15);
                lDict.Add(101210, 0.2);
                lDict.Add(101215, 0.25);
                lDict.Add(101310, 0.3);
                lDict.Add(101315, 0.35);
                lDict.Add(101410, 0.4);
                lDict.Add(101415, 0.45);
                lDict.Add(101510, 0.5);
                lDict.Add(101515, 0.55);
                lDict.Add(101610, 0.6);
                lDict.Add(101615, 0.65);
                lDict.Add(101710, 0.7);
                lDict.Add(101715, 0.75);
                lDict.Add(101810, 0.8);
                lDict.Add(101815, 0.85);
                lDict.Add(101910, 0.9);
                lDict.Add(101915, 0.95);
                lDict.Add(111010, 1);
                lDict.Add(111015, 1.05);
                lDict.Add(111110, 1.1);
                lDict.Add(111115, 1.15);
                lDict.Add(111210, 1.2);
                lDict.Add(111215, 1.25);
                lDict.Add(111310, 1.3);
                lDict.Add(111315, 1.35);
                lDict.Add(111410, 1.4);
                lDict.Add(111415, 1.45);
                lDict.Add(111510, 1.5);
                lDict.Add(111515, 1.55);
                lDict.Add(111610, 1.6);
                lDict.Add(111615, 1.65);
                lDict.Add(111710, 1.7);
                lDict.Add(111715, 1.75);
                lDict.Add(111810, 1.8);
                lDict.Add(111815, 1.85);
                lDict.Add(111910, 1.9);
                lDict.Add(111915, 1.95);
                lDict.Add(121010, 2);
                lDict.Add(121015, 2.05);
                lDict.Add(121110, 2.1);
                lDict.Add(121115, 2.15);
                lDict.Add(121210, 2.2);
                lDict.Add(121215, 2.25);
                lDict.Add(121310, 2.3);
                lDict.Add(121315, 2.35);
                lDict.Add(121410, 2.4);
                lDict.Add(121415, 2.45);
                lDict.Add(121510, 2.5);
                lDict.Add(121515, 2.55);
                lDict.Add(121610, 2.6);
                lDict.Add(121615, 2.65);
                lDict.Add(121710, 2.7);
                lDict.Add(121715, 2.75);
                lDict.Add(121810, 2.8);
                lDict.Add(121815, 2.85);
                lDict.Add(121910, 2.9);
                lDict.Add(121915, 2.95);
                lDict.Add(131010, 3);
                lDict.Add(131015, 3.05);
                lDict.Add(131110, 3.1);
                lDict.Add(131115, 3.15);
                lDict.Add(131210, 3.2);
                lDict.Add(131215, 3.25);
                lDict.Add(131310, 3.3);
                lDict.Add(131315, 3.35);
                lDict.Add(131410, 3.4);
                lDict.Add(131415, 3.45);
                lDict.Add(131510, 3.5);
                lDict.Add(131515, 3.55);
                lDict.Add(131610, 3.6);
                lDict.Add(131615, 3.65);
                lDict.Add(131710, 3.7);
                lDict.Add(131715, 3.75);
                lDict.Add(131810, 3.8);
                lDict.Add(131815, 3.85);
                lDict.Add(131910, 3.9);
                lDict.Add(131915, 3.95);
                lDict.Add(141010, 4);
                lDict.Add(141015, 4.05);
                lDict.Add(141110, 4.1);
                lDict.Add(141115, 4.15);
                lDict.Add(141210, 4.2);
                lDict.Add(141215, 4.25);
                lDict.Add(141310, 4.3);
                lDict.Add(141315, 4.35);
                lDict.Add(141410, 4.4);
                lDict.Add(141415, 4.45);
                lDict.Add(141510, 4.5);
                lDict.Add(141515, 4.55);
                lDict.Add(141610, 4.6);
                lDict.Add(141615, 4.65);
                lDict.Add(141710, 4.7);
                lDict.Add(141715, 4.75);
                lDict.Add(141810, 4.8);
                lDict.Add(141815, 4.85);
                lDict.Add(141910, 4.9);
                lDict.Add(141915, 4.95);
                lDict.Add(151010, 5);
                lDict.Add(151015, 5.05);
                lDict.Add(151110, 5.1);
                lDict.Add(151115, 5.15);
                lDict.Add(151210, 5.2);
                lDict.Add(151215, 5.25);
                lDict.Add(151310, 5.3);
                lDict.Add(151315, 5.35);
                lDict.Add(151410, 5.4);
                lDict.Add(151415, 5.45);
                lDict.Add(151510, 5.5);
                lDict.Add(151515, 5.55);
                lDict.Add(151610, 5.6);
                lDict.Add(151615, 5.65);
                lDict.Add(151710, 5.7);
                lDict.Add(151715, 5.75);
                lDict.Add(151810, 5.8);
                lDict.Add(151815, 5.85);
                lDict.Add(151910, 5.9);
                lDict.Add(151915, 5.95);
                lDict.Add(161010, 6);
                lDict.Add(161015, 6.05);
                lDict.Add(161110, 6.1);
                lDict.Add(161115, 6.15);
                lDict.Add(161210, 6.2);
                lDict.Add(161215, 6.25);
                lDict.Add(161310, 6.3);
                lDict.Add(161315, 6.35);
            }
            catch (Exception e)
            {
                MessageBox.Show("InitArray error\n" + e.Message + "\n" + e.StackTrace);
            }
        }
        public override bool On()
        {
            SerialPortTuner.DtrEnable = false;
            SerialPortTuner.RtsEnable = true;
            Thread.Sleep(1000);
            SerialPortTuner.DtrEnable = true;
            SerialPortTuner.RtsEnable = false;
            return true;
        }
        public override bool Off()
        {
            SendCmd(0x0a);
            return true;
        }

        public override double GetInductance()
        {
            bool ok = GetStatus2(Tuner.Screen.ManualTune);
            if (ok == false)
            {
                do
                {
                    //SelectManualTunePage();
                    ok = GetStatus2(Tuner.Screen.ManualTune);
                } while (!ok);
            }
            return Inductance;
        }
        public override double GetCapacitance()
        {
            bool ok = GetStatus2(Tuner.Screen.ManualTune);
            if (ok == false)
            {
                do
                {
                    //SelectManualTunePage();
                    ok = GetStatus2(Tuner.Screen.ManualTune);
                } while( !ok );
            }
            return Capacitance;
        }
        public override void SetInductance(double value)
        {
            byte cmdDown = 0x05; // L Down
            byte cmdUp = 0x06; // L Up
            if (value > 6.35) value = 6.35;
            var l = GetInductance();
            while (l < value)
            {
                SendCmd(cmdUp); // step L up 
                l = GetInductance();
            }
            while( l > value)
            {
                SendCmd(cmdDown); // step L down
                l = GetInductance();
            }
        }
        public override void SetCapacitance(double value)
        {
            byte cmdDown = 0x07; // C Down
            byte cmdUp = 0x08; // C Up
            if (value > 2629.5) value = 2629.5;
            var c = GetCapacitance();
            while (c < value)
            {
                SendCmd(cmdUp); // step C up 
                c = GetCapacitance();
            }
            while (c > value)
            {
                SendCmd(cmdDown); // step C down
                c = GetCapacitance();
            }
        }
    }
}
