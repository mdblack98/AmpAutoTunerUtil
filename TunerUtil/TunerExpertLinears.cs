﻿using Microsoft.VisualBasic.Logging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.IO.Ports;
using System.Linq;
using System.Runtime.Remoting.Channels;
using System.Security.Cryptography;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using System.Windows.Media.Animation;

namespace AmpAutoTunerUtility
{
    class TunerExpertLinears : Tuner
    {
        private SerialPort ?SerialPortTuner = null;
        private bool runThread = false;
        char response = 'X';
        private string swr1 = "?";
        private string swr2 = "?";
        private string power = "?";
        //public char bank = "?";
        private string temp1 = "?";
        private string antenna = "?";
        private string bandstr = "?";
        public bool tuning = false;
        //Byte[] responseOld = new byte[512];
        public TunerExpertLinears(string model, string comport, string baud, out string ?errmsg)
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
                ReadTimeout = 5000,
                WriteTimeout = 500
            };
            SerialPortTuner.Open();
            Thread.Sleep(500);
            //Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            //Byte[] response = new Byte[512];
            //SerialPortTuner.Write(cmd, 0, 6);
            myThread = new Thread(new ThreadStart(this.ThreadTask))
            {
                IsBackground = true
            };
            myThread.Start();
            Thread.Sleep(1000);
            //isOn = GetStatus();
            if (isOn == false)
            {
                isOn = isOn;
            }
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
            SerialPortTuner!.Write(cmdDisplay, 0, 6);
            SerialLock.ReleaseMutex();
            Thread.Sleep(200);
        }

        public override void SelectManualTunePage()
        {
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SelectManualTunePage tuner not open?");
                return;
            }
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
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SelectManualTunePage tuner not open?");
                return;
            }
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
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SendCmd tuner not open?");
                return;
            }
            try
            {
                Byte[] cmdBuf = { 0x55, 0x55, 0x55, 0x01, cmd, cmd };
                SerialLock.WaitOne(2000);


                SerialPortTuner.Write(cmdBuf, 0, 6);
                Thread.Sleep(150);
                SerialLock.ReleaseMutex();
            }
            catch (Exception)
            {
                SerialLock.ReleaseMutex();
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.WARN, "Can't send tuner cmd????");
                return;
            }
        }

        readonly char[] lookup = { ' ', '!', '"', '#','$', '%', '&', '\\', '(', ')', '*', '+', ',', '-','.', '/','0','1','2','3','4','5','6','7','8','9',':',';','<','=','>','?','@','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','Z','[','\\','^','_','?','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z','{','|','}','?' };
        private Screen screenLast = Screen.Unknown;
        //public override bool GetStatus2(screen myScreen)
        public override bool GetStatus2(Screen ?myScreen = Screen.Unknown)
        {
            if (myScreen is null)
            {
                MessageBox.Show("GetStatus2 myScreen=null!");
                return false; 
            }

            if (SerialPortTuner is null)
            {
                MessageBox.Show("GetStatus2 SerialPortTuner=null!");
                return false; 
            }


            try
            {
                int loop = 10;
                try
                {
                    SerialLock.WaitOne(2000);
                }
                catch (Exception)
                {
                    return false;
                }
                if (screenLast != myScreen)
                {
                    if (myScreen == Screen.Unknown) MessageBox.Show("Where are we?\n");
                    if (myScreen == Screen.Antenna) SelectAntennaPage();
                    else if (myScreen == Screen.ManualTune) SelectManualTunePage();
                    else SelectDisplayPage();
                    screenLast = (Screen)myScreen;
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
                        if (myByte == 0 && --loop == 0) { SerialLock.ReleaseMutex(); return false; }
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
                response[3] = (byte)SerialPortTuner.ReadByte(); // should be 6a
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
                            SerialLock.ReleaseMutex();
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
                if (antennas is null)
                {
                    MessageBox.Show("tuner1.antennas=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                    SerialLock.ReleaseMutex();
                    return false;
                }
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
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus error: " + ex.Message + "\n" + ex.StackTrace);
                    SerialLock.ReleaseMutex();
                    return false;
                }
            SerialLock.ReleaseMutex();
            return true;
        }
        public override bool GetStatus()
        {
            try
            {
                if (SerialPortTuner == null)
                    return false;
                if (freqWalkIsRunning == true) return false;
                try
                {
                    SerialLock.WaitOne(2000);
                }
                catch (Exception)
                {
                    return false;
                }
            writeagain:
                if (SerialPortTuner == null) { SerialLock.ReleaseMutex(); return false; }
                SerialPortTuner.DiscardInBuffer();
                Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x90, 0x90 };
                Byte[] response = new Byte[128];
                //SerialPortTuner.Write(cmd, 0, 6);
                int myByte;
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
            //int zeroCount = 0;
            readagain:
                var watch = System.Diagnostics.Stopwatch.StartNew();
                while (myByte != 0xaa)
                {
                    try
                    {
                        myByte = (byte)SerialPortTuner.ReadByte();
                        if (myByte == 0 && watch.ElapsedMilliseconds > 1000)
                        {
                            watch.Restart();
                            SerialLock.ReleaseMutex();
                            return false;
                        }
                        if (myByte == -1)
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
                response[0] = (byte)myByte;
                response[1] = (byte)SerialPortTuner.ReadByte();
                if (response[1] != 0xaa)
                { //SerialLock.ReleaseMutex(); 
                    goto writeagain;
                }
                response[2] = (byte)SerialPortTuner.ReadByte();
                if (response[2] != 0xaa)
                { //SerialLock.ReleaseMutex(); 
                    goto writeagain;
                }
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
                // wrong packet?
                if (response[3] != 0x43) goto readagain;
                for (int i = 0; i < 67; ++i)
                {
                    sum += response[i + 4];
                }
                long chkByte0 = sum % 256;
                long chkByte1 = sum / 256;
                long resByte0 = response[71];
                long resByte1 = response[72];
                var sresponse = System.Text.Encoding.Default.GetString(response, 0, n - 5);
                if (chkByte0 != resByte0)
                {
                    SerialLock.ReleaseMutex();
                    return false;
                }
                if (chkByte1 != resByte1)
                {
                    SerialLock.ReleaseMutex();
                    return false;
                }

                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
                // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
                // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
                string[] mytokens = sresponse.Split(',');
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens.Length + ":" + sresponse + "\n");
                try
                {
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
                        model = "SPE " + mytokens[1];
                        bank = mytokens[4][0];
                        power = mytokens[10];
                        swr1 = mytokens[11];
                        SetSWR(Double.Parse(swr1));
                        swr2 = mytokens[12];
                        isOperate = mytokens[2] == "S"?false:true;
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
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "MSG: " + ex.Message + "\n" + ex.StackTrace);
                    SerialLock.ReleaseMutex();
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus: " + ex.Message + "\n" + ex.StackTrace);
                SerialLock.ReleaseMutex();
                return false;
            }
            SerialLock.ReleaseMutex();
            return true;
        }

        void PacketStatus()
        {

        }

        readonly Thread ?myThread;
        private void ThreadTask()
        {
            runThread = true;
            while (runThread)
            {
                try
                {
                    Thread.Sleep(1000);
                    if (!freqWalkIsRunning)
                        isOn = GetStatus();
                    //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears thread got status\n");
                }
                catch (Exception ex)
                {
                    string path = System.IO.Path.GetTempPath() + "AmpAutoTunerUtility.log";
                    File.AppendAllText(path, ex.Message + "\n" + ex.StackTrace + "\n" + ex.Source + "\n" + ex.TargetSite + "\n" + ex.InnerException + "\n");
                }
            }
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears thread stopped\n");
        }

        public override string GetSWRString()
        {
            return "SWR ATU/ANT" + swr1 + "/" + swr2;
        }
        public override string ?GetSerialPortTuner()
        {
            if (SerialPortTuner == null)
            {
                return null;
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
            if (antenna.Equals("?")) return 0;
            return int.Parse(antenna.Substring(0,1));
        }
        public override void SetAntenna(int antennaNumberRequested, bool tuneIsRunning = false)
        {
            //return;
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SetAntenna SerialPortTuner=null!");
                return;
            }
            try
            {
                //if (antennaNumberRequested != GetAntenna())
                //if (antennaNumberRequested != freqWalkAntenna)
                //{
                //    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "antennaNumberRequested " + antennaNumberRequested + " != freqWalkAntenna " + freqWalkAntenna)
                //    return;
                //}
                if (antennaNumberRequested == 0)
                    return;
                if (!antenna.Equals(antennaNumberRequested.ToString()))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.TRACE, "SetAntenna " + antennaNumberRequested + " getting amp status\n");
                    if (!antenna.Equals("?")) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "antenna=" + int.Parse(antenna.Substring(0, 1)) + ", antennaNumberRequest=" + antennaNumberRequested + "\n");
                    Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x04, 0x04 };
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Setting antenna to other antenna\n");
                    SerialPortTuner.Write(cmd, 0, 6);
                    antenna = antennaNumberRequested.ToString();
                }
                //Thread.Sleep(500);
                //while (GetStatus()) ;
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
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SerialPortTuner=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }
            SerialLock.WaitOne();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x09, 0x09 };
            Byte[] cmdMsg = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            SerialPortTuner.DiscardInBuffer();
            SerialPortTuner.Write(cmdMsg, 0, 6);
            //Byte[] response;
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
                SerialLock.ReleaseMutex();
                return;
            }
            while (tuning == true)
            {
                GetStatus2(Tuner.Screen.Antenna);
                Thread.Sleep(100);
            }
            //SelectDisplayPage();
            SerialLock.ReleaseMutex();

        }

        public override void Poll()
        {
            //return; //MDB
            GetStatus();
            //this.SelectAntennaPage();
            GetStatus2(Screen.Antenna);
        }
#pragma warning disable IDE0052 // Remove unread private members
        private Dictionary <int, double> ?lDict;
        private Dictionary<int, double> ?cDict;
#pragma warning restore IDE0052 // Remove unread private members

        private void InitArrays()
        {
            return;
            //cDict?.Clear();
            //lDict?.Clear();
            try
            {
                cDict = new Dictionary<int, double>
                {
                    { 150010, 0 },
                    { 150012, 2.5 },
                    { 100015, 5 },
                    { 150017, 7.5 },
                    { 101110, 10 },
                    { 151112, 12.5 },
                    { 101115, 15 },
                    { 151117, 17.5 },
                    { 101210, 20 },
                    { 151212, 22.5 },
                    { 101215, 25 },
                    { 151217, 27.5 },
                    { 101310, 30 },
                    { 151312, 32.5 },
                    { 101315, 35 },
                    { 151317, 37.5 },
                    { 101410, 40 },
                    { 151412, 42.5 },
                    { 101415, 45 },
                    { 151417, 47.5 },
                    { 101510, 50 },
                    { 151512, 52.5 },
                    { 101515, 55 },
                    { 151517, 57.5 },
                    { 101610, 60 },
                    { 151612, 62.5 },
                    { 101615, 65 },
                    { 151617, 67.5 },
                    { 101710, 70 },
                    { 151712, 72.5 },
                    { 101715, 75 },
                    { 151717, 77.5 },
                    { 101810, 80 },
                    { 151812, 82.5 },
                    { 101815, 85 },
                    { 151817, 87.5 },
                    { 101910, 90 },
                    { 151912, 92.5 },
                    { 101915, 95 },
                    { 151917, 97.5 },
                    { 101010, 100 },
                    { 151012, 102.5 },
                    { 101015, 105 },
                    { 151017, 107.5 },
                    { 101110, 110 },
                    { 151112, 112.5 },
                    { 101115, 115 },
                    { 151117, 117.5 },
                    { 101210, 120 },
                    { 151212, 122.5 },
                    { 101215, 125 },
                    { 151217, 127.5 },
                    { 101310, 130 },
                    { 151312, 132.5 },
                    { 101315, 135 },
                    { 151317, 137.5 },
                    { 101410, 140 },
                    { 151412, 142.5 },
                    { 101415, 145 },
                    { 151417, 147.5 },
                    { 101510, 150 },
                    { 151512, 152.5 },
                    { 101515, 155 },
                    { 151517, 157.5 },
                    { 101614, 164 },
                    { 151616, 166.5 },
                    { 101619, 169 },
                    { 151711, 171.5 },
                    { 101714, 174 },
                    { 151716, 176.5 },
                    { 101719, 179 },
                    { 151811, 181.5 },
                    { 101814, 184 },
                    { 151816, 186.5 },
                    { 101819, 189 },
                    { 151911, 191.5 },
                    { 101914, 194 },
                    { 151916, 196.5 },
                    { 101919, 199 },
                    { 151011, 201.5 },
                    { 101014, 204 },
                    { 151016, 206.5 },
                    { 101019, 209 },
                    { 151111, 211.5 },
                    { 101114, 214 },
                    { 151116, 216.5 },
                    { 101119, 219 },
                    { 151211, 221.5 },
                    { 101214, 224 },
                    { 151216, 226.5 },
                    { 101219, 229 },
                    { 151311, 231.5 },
                    { 101314, 234 },
                    { 151316, 236.5 },
                    { 101319, 239 },
                    { 151411, 241.5 },
                    { 101414, 244 },
                    { 151416, 246.5 },
                    { 101419, 249 },
                    { 151511, 251.5 },
                    { 101514, 254 },
                    { 151516, 256.5 },
                    { 101519, 259 },
                    { 151611, 261.5 },
                    { 101614, 264 },
                    { 151616, 266.5 },
                    { 101619, 269 },
                    { 151711, 271.5 },
                    { 101714, 274 },
                    { 151716, 276.5 },
                    { 101719, 279 },
                    { 151811, 281.5 },
                    { 101814, 284 },
                    { 151816, 286.5 },
                    { 101819, 289 },
                    { 151911, 291.5 },
                    { 101914, 294 },
                    { 151916, 296.5 },
                    { 101919, 299 },
                    { 151011, 301.5 },
                    { 101014, 304 },
                    { 151016, 306.5 },
                    { 101019, 309 },
                    { 151111, 311.5 },
                    { 101114, 314 },
                    { 151116, 316.5 },
                    { 101119, 319 },
                    { 151211, 321.5 },
                    { 101218, 328 },
                    { 151310, 330.5 },
                    { 101313, 333 },
                    { 151315, 335.5 },
                    { 101318, 338 },
                    { 151410, 340.5 },
                    { 101413, 343 },
                    { 151415, 345.5 },
                    { 101418, 348 },
                    { 151510, 350.5 },
                    { 101513, 353 },
                    { 151515, 355.5 },
                    { 101518, 358 },
                    { 151610, 360.5 },
                    { 101613, 363 },
                    { 151615, 365.5 },
                    { 101618, 368 },
                    { 151710, 370.5 },
                    { 101713, 373 },
                    { 151715, 375.5 },
                    { 101718, 378 },
                    { 151810, 380.5 },
                    { 101813, 383 },
                    { 151815, 385.5 },
                    { 101818, 388 },
                    { 151910, 390.5 },
                    { 101913, 393 },
                    { 151915, 395.5 },
                    { 101918, 398 },
                    { 151010, 400.5 },
                    { 101013, 403 },
                    { 151015, 405.5 },
                    { 101018, 408 },
                    { 151110, 410.5 },
                    { 101113, 413 },
                    { 151115, 415.5 },
                    { 101118, 418 },
                    { 151210, 420.5 },
                    { 101213, 423 },
                    { 151215, 425.5 },
                    { 101218, 428 },
                    { 151310, 430.5 },
                    { 101313, 433 },
                    { 151315, 435.5 },
                    { 101318, 438 },
                    { 151410, 440.5 },
                    { 101413, 443 },
                    { 151415, 445.5 },
                    { 101418, 448 },
                    { 151510, 450.5 },
                    { 101513, 453 },
                    { 151515, 455.5 },
                    { 101518, 458 },
                    { 151610, 460.5 },
                    { 101613, 463 },
                    { 151615, 465.5 },
                    { 101618, 468 },
                    { 151710, 470.5 },
                    { 101713, 473 },
                    { 151715, 475.5 },
                    { 101718, 478 },
                    { 151810, 480.5 },
                    { 101813, 483 },
                    { 151815, 485.5 },
                    { 101912, 492 },
                    { 151914, 494.5 },
                    { 101917, 497 },
                    { 151919, 499.5 },
                    { 101012, 502 },
                    { 151014, 504.5 },
                    { 101017, 507 },
                    { 151019, 509.5 },
                    { 101112, 512 },
                    { 151114, 514.5 },
                    { 101117, 517 },
                    { 151119, 519.5 },
                    { 101212, 522 },
                    { 151214, 524.5 },
                    { 101217, 527 },
                    { 151219, 529.5 },
                    { 101312, 532 },
                    { 151314, 534.5 },
                    { 101317, 537 },
                    { 151319, 539.5 },
                    { 101412, 542 },
                    { 151414, 544.5 },
                    { 101417, 547 },
                    { 151419, 549.5 },
                    { 101512, 552 },
                    { 151514, 554.5 },
                    { 101517, 557 },
                    { 151519, 559.5 },
                    { 101612, 562 },
                    { 151614, 564.5 },
                    { 101617, 567 },
                    { 151619, 569.5 },
                    { 101712, 572 },
                    { 151714, 574.5 },
                    { 101717, 577 },
                    { 151719, 579.5 },
                    { 101812, 582 },
                    { 151814, 584.5 },
                    { 101817, 587 },
                    { 151819, 589.5 },
                    { 101912, 592 },
                    { 151914, 594.5 },
                    { 101917, 597 },
                    { 151919, 599.5 },
                    { 101012, 602 },
                    { 151014, 604.5 },
                    { 101017, 607 },
                    { 151019, 609.5 },
                    { 101112, 612 },
                    { 151114, 614.5 },
                    { 101117, 617 },
                    { 151119, 619.5 },
                    { 101212, 622 },
                    { 151214, 624.5 },
                    { 101217, 627 },
                    { 151219, 629.5 },
                    { 101312, 632 },
                    { 151314, 634.5 },
                    { 101317, 637 },
                    { 151319, 639.5 },
                    { 101412, 642 },
                    { 151414, 644.5 },
                    { 101417, 647 },
                    { 151419, 649.5 },
                    { 101610, 660 },
                    { 151612, 662.5 },
                    { 101615, 665 },
                    { 151617, 667.5 },
                    { 101710, 670 },
                    { 151712, 672.5 },
                    { 101715, 675 },
                    { 151717, 677.5 },
                    { 101810, 680 },
                    { 151812, 682.5 },
                    { 101815, 685 },
                    { 151817, 687.5 },
                    { 101910, 690 },
                    { 151912, 692.5 },
                    { 101915, 695 },
                    { 151917, 697.5 },
                    { 101010, 700 },
                    { 151012, 702.5 },
                    { 101015, 705 },
                    { 151017, 707.5 },
                    { 101110, 710 },
                    { 151112, 712.5 },
                    { 101115, 715 },
                    { 151117, 717.5 },
                    { 101210, 720 },
                    { 151212, 722.5 },
                    { 101215, 725 },
                    { 151217, 727.5 },
                    { 101310, 730 },
                    { 151312, 732.5 },
                    { 101315, 735 },
                    { 151317, 737.5 },
                    { 101410, 740 },
                    { 151412, 742.5 },
                    { 101415, 745 },
                    { 151417, 747.5 },
                    { 101510, 750 },
                    { 151512, 752.5 },
                    { 101515, 755 },
                    { 151517, 757.5 },
                    { 101610, 760 },
                    { 151612, 762.5 },
                    { 101615, 765 },
                    { 151617, 767.5 },
                    { 101710, 770 },
                    { 151712, 772.5 },
                    { 101715, 775 },
                    { 151717, 777.5 },
                    { 101810, 780 },
                    { 151812, 782.5 },
                    { 101815, 785 },
                    { 151817, 787.5 },
                    { 101910, 790 },
                    { 151912, 792.5 },
                    { 101915, 795 },
                    { 151917, 797.5 },
                    { 101010, 800 },
                    { 151012, 802.5 },
                    { 101015, 805 },
                    { 151017, 807.5 },
                    { 101110, 810 },
                    { 151112, 812.5 },
                    { 101115, 815 },
                    { 151117, 817.5 },
                    { 101214, 824 },
                    { 151216, 826.5 },
                    { 101219, 829 },
                    { 151311, 831.5 },
                    { 101314, 834 },
                    { 151316, 836.5 },
                    { 101319, 839 },
                    { 151411, 841.5 },
                    { 101414, 844 },
                    { 151416, 846.5 },
                    { 101419, 849 },
                    { 151511, 851.5 },
                    { 101514, 854 },
                    { 151516, 856.5 },
                    { 101519, 859 },
                    { 151611, 861.5 },
                    { 101614, 864 },
                    { 151616, 866.5 },
                    { 101619, 869 },
                    { 151711, 871.5 },
                    { 101714, 874 },
                    { 151716, 876.5 },
                    { 101719, 879 },
                    { 151811, 881.5 },
                    { 101814, 884 },
                    { 151816, 886.5 },
                    { 101819, 889 },
                    { 151911, 891.5 },
                    { 101914, 894 },
                    { 151916, 896.5 },
                    { 101919, 899 },
                    { 151011, 901.5 },
                    { 101014, 904 },
                    { 151016, 906.5 },
                    { 101019, 909 },
                    { 151111, 911.5 },
                    { 101114, 914 },
                    { 151116, 916.5 },
                    { 101119, 919 },
                    { 151211, 921.5 },
                    { 101214, 924 },
                    { 151216, 926.5 },
                    { 101219, 929 },
                    { 151311, 931.5 },
                    { 101314, 934 },
                    { 151316, 936.5 },
                    { 101319, 939 },
                    { 151411, 941.5 },
                    { 101414, 944 },
                    { 151416, 946.5 },
                    { 101419, 949 },
                    { 151511, 951.5 },
                    { 101514, 954 },
                    { 151516, 956.5 },
                    { 101519, 959 },
                    { 151611, 961.5 },
                    { 101614, 964 },
                    { 151616, 966.5 },
                    { 101619, 969 },
                    { 151711, 971.5 },
                    { 101714, 974 },
                    { 151716, 976.5 },
                    { 101719, 979 },
                    { 151811, 981.5 },
                    { 101818, 988 },
                    { 151910, 990.5 },
                    { 101913, 993 },
                    { 151915, 995.5 },
                    { 101918, 998 },
                    { 151010, 1000.5 },
                    { 101013, 1003 },
                    { 151015, 1005.5 },
                    { 101018, 1008 },
                    { 151110, 1010.5 },
                    { 101113, 1013 },
                    { 151115, 1015.5 },
                    { 101118, 1018 },
                    { 151210, 1020.5 },
                    { 101213, 1023 },
                    { 151215, 1025.5 },
                    { 101218, 1028 },
                    { 151310, 1030.5 },
                    { 101313, 1033 },
                    { 151315, 1035.5 },
                    { 101318, 1038 },
                    { 151410, 1040.5 },
                    { 101413, 1043 },
                    { 151415, 1045.5 },
                    { 101418, 1048 },
                    { 151510, 1050.5 },
                    { 101513, 1053 },
                    { 151515, 1055.5 },
                    { 101518, 1058 },
                    { 151610, 1060.5 },
                    { 101613, 1063 },
                    { 151615, 1065.5 },
                    { 101618, 1068 },
                    { 151710, 1070.5 },
                    { 101713, 1073 },
                    { 151715, 1075.5 },
                    { 101718, 1078 },
                    { 151810, 1080.5 },
                    { 101813, 1083 },
                    { 151815, 1085.5 },
                    { 101818, 1088 },
                    { 151910, 1090.5 },
                    { 101913, 1093 },
                    { 151915, 1095.5 },
                    { 101918, 1098 },
                    { 151010, 1100.5 },
                    { 101013, 1103 },
                    { 151015, 1105.5 },
                    { 101018, 1108 },
                    { 151110, 1110.5 },
                    { 101113, 1113 },
                    { 151115, 1115.5 },
                    { 101118, 1118 },
                    { 151210, 1120.5 },
                    { 101213, 1123 },
                    { 151215, 1125.5 },
                    { 101218, 1128 },
                    { 151310, 1130.5 },
                    { 101313, 1133 },
                    { 151315, 1135.5 },
                    { 101318, 1138 },
                    { 151410, 1140.5 },
                    { 101413, 1143 },
                    { 151415, 1145.5 },
                    { 101512, 1152 },
                    { 151514, 1154.5 },
                    { 101517, 1157 },
                    { 151519, 1159.5 },
                    { 101612, 1162 },
                    { 151614, 1164.5 },
                    { 101617, 1167 },
                    { 151619, 1169.5 },
                    { 101712, 1172 },
                    { 151714, 1174.5 },
                    { 101717, 1177 },
                    { 151719, 1179.5 },
                    { 101812, 1182 },
                    { 151814, 1184.5 },
                    { 101817, 1187 },
                    { 151819, 1189.5 },
                    { 101912, 1192 },
                    { 151914, 1194.5 },
                    { 101917, 1197 },
                    { 151919, 1199.5 },
                    { 101012, 1202 },
                    { 151014, 1204.5 },
                    { 101017, 1207 },
                    { 151019, 1209.5 },
                    { 101112, 1212 },
                    { 151114, 1214.5 },
                    { 101117, 1217 },
                    { 151119, 1219.5 },
                    { 101212, 1222 },
                    { 151214, 1224.5 },
                    { 101217, 1227 },
                    { 151219, 1229.5 },
                    { 101312, 1232 },
                    { 151314, 1234.5 },
                    { 101317, 1237 },
                    { 151319, 1239.5 },
                    { 101412, 1242 },
                    { 151414, 1244.5 },
                    { 101417, 1247 },
                    { 151419, 1249.5 },
                    { 101512, 1252 },
                    { 151514, 1254.5 },
                    { 101517, 1257 },
                    { 151519, 1259.5 },
                    { 101612, 1262 },
                    { 151614, 1264.5 },
                    { 101617, 1267 },
                    { 151619, 1269.5 },
                    { 101712, 1272 },
                    { 151714, 1274.5 },
                    { 101717, 1277 },
                    { 151719, 1279.5 },
                    { 101812, 1282 },
                    { 151814, 1284.5 },
                    { 101817, 1287 },
                    { 151819, 1289.5 },
                    { 101912, 1292 },
                    { 151914, 1294.5 },
                    { 101917, 1297 },
                    { 151919, 1299.5 },
                    { 101012, 1302 },
                    { 151014, 1304.5 },
                    { 101017, 1307 },
                    { 151019, 1309.5 },
                    { 101210, 1320 },
                    { 151212, 1322.5 },
                    { 101215, 1325 },
                    { 151217, 1327.5 },
                    { 101310, 1330 },
                    { 151312, 1332.5 },
                    { 101315, 1335 },
                    { 151317, 1337.5 },
                    { 101410, 1340 },
                    { 151412, 1342.5 },
                    { 101415, 1345 },
                    { 151417, 1347.5 },
                    { 101510, 1350 },
                    { 151512, 1352.5 },
                    { 101515, 1355 },
                    { 151517, 1357.5 },
                    { 101610, 1360 },
                    { 151612, 1362.5 },
                    { 101615, 1365 },
                    { 151617, 1367.5 },
                    { 101710, 1370 },
                    { 151712, 1372.5 },
                    { 101715, 1375 },
                    { 151717, 1377.5 },
                    { 101810, 1380 },
                    { 151812, 1382.5 },
                    { 101815, 1385 },
                    { 151817, 1387.5 },
                    { 101910, 1390 },
                    { 151912, 1392.5 },
                    { 101915, 1395 },
                    { 151917, 1397.5 },
                    { 101010, 1400 },
                    { 151012, 1402.5 },
                    { 101015, 1405 },
                    { 151017, 1407.5 },
                    { 101110, 1410 },
                    { 151112, 1412.5 },
                    { 101115, 1415 },
                    { 151117, 1417.5 },
                    { 101210, 1420 },
                    { 151212, 1422.5 },
                    { 101215, 1425 },
                    { 151217, 1427.5 },
                    { 101310, 1430 },
                    { 151312, 1432.5 },
                    { 101315, 1435 },
                    { 151317, 1437.5 },
                    { 101410, 1440 },
                    { 151412, 1442.5 },
                    { 101415, 1445 },
                    { 151417, 1447.5 },
                    { 101510, 1450 },
                    { 151512, 1452.5 },
                    { 101515, 1455 },
                    { 151517, 1457.5 },
                    { 101610, 1460 },
                    { 151612, 1462.5 },
                    { 101615, 1465 },
                    { 151617, 1467.5 },
                    { 101710, 1470 },
                    { 151712, 1472.5 },
                    { 101715, 1475 },
                    { 151717, 1477.5 },
                    { 101814, 1484 },
                    { 151816, 1486.5 },
                    { 101819, 1489 },
                    { 151911, 1491.5 },
                    { 101914, 1494 },
                    { 151916, 1496.5 },
                    { 101919, 1499 },
                    { 151011, 1501.5 },
                    { 101014, 1504 },
                    { 151016, 1506.5 },
                    { 101019, 1509 },
                    { 151111, 1511.5 },
                    { 101114, 1514 },
                    { 151116, 1516.5 },
                    { 101119, 1519 },
                    { 151211, 1521.5 },
                    { 101214, 1524 },
                    { 151216, 1526.5 },
                    { 101219, 1529 },
                    { 151311, 1531.5 },
                    { 101314, 1534 },
                    { 151316, 1536.5 },
                    { 101319, 1539 },
                    { 151411, 1541.5 },
                    { 101414, 1544 },
                    { 151416, 1546.5 },
                    { 101419, 1549 },
                    { 151511, 1551.5 },
                    { 101514, 1554 },
                    { 151516, 1556.5 },
                    { 101519, 1559 },
                    { 151611, 1561.5 },
                    { 101614, 1564 },
                    { 151616, 1566.5 },
                    { 101619, 1569 },
                    { 151711, 1571.5 },
                    { 101714, 1574 },
                    { 151716, 1576.5 },
                    { 101719, 1579 },
                    { 151811, 1581.5 },
                    { 101814, 1584 },
                    { 151816, 1586.5 },
                    { 101819, 1589 },
                    { 151911, 1591.5 },
                    { 101914, 1594 },
                    { 151916, 1596.5 },
                    { 101919, 1599 },
                    { 151011, 1601.5 },
                    { 101014, 1604 },
                    { 151016, 1606.5 },
                    { 101019, 1609 },
                    { 151111, 1611.5 },
                    { 101114, 1614 },
                    { 151116, 1616.5 },
                    { 101119, 1619 },
                    { 151211, 1621.5 },
                    { 101214, 1624 },
                    { 151216, 1626.5 },
                    { 101219, 1629 },
                    { 151311, 1631.5 },
                    { 101314, 1634 },
                    { 151316, 1636.5 },
                    { 101319, 1639 },
                    { 151411, 1641.5 },
                    { 101418, 1648 },
                    { 151510, 1650.5 },
                    { 101513, 1653 },
                    { 151515, 1655.5 },
                    { 101518, 1658 },
                    { 151610, 1660.5 },
                    { 101613, 1663 },
                    { 151615, 1665.5 },
                    { 101618, 1668 },
                    { 151710, 1670.5 },
                    { 101713, 1673 },
                    { 151715, 1675.5 },
                    { 101718, 1678 },
                    { 151810, 1680.5 },
                    { 101813, 1683 },
                    { 151815, 1685.5 },
                    { 101818, 1688 },
                    { 151910, 1690.5 },
                    { 101913, 1693 },
                    { 151915, 1695.5 },
                    { 101918, 1698 },
                    { 151010, 1700.5 },
                    { 101013, 1703 },
                    { 151015, 1705.5 },
                    { 101018, 1708 },
                    { 151110, 1710.5 },
                    { 101113, 1713 },
                    { 151115, 1715.5 },
                    { 101118, 1718 },
                    { 151210, 1720.5 },
                    { 101213, 1723 },
                    { 151215, 1725.5 },
                    { 101218, 1728 },
                    { 151310, 1730.5 },
                    { 101313, 1733 },
                    { 151315, 1735.5 },
                    { 101318, 1738 },
                    { 151410, 1740.5 },
                    { 101413, 1743 },
                    { 151415, 1745.5 },
                    { 101418, 1748 },
                    { 151510, 1750.5 },
                    { 101513, 1753 },
                    { 151515, 1755.5 },
                    { 101518, 1758 },
                    { 151610, 1760.5 },
                    { 101613, 1763 },
                    { 151615, 1765.5 },
                    { 101618, 1768 },
                    { 151710, 1770.5 },
                    { 101713, 1773 },
                    { 151715, 1775.5 },
                    { 101718, 1778 },
                    { 151810, 1780.5 },
                    { 101813, 1783 },
                    { 151815, 1785.5 },
                    { 101818, 1788 },
                    { 151910, 1790.5 },
                    { 101913, 1793 },
                    { 151915, 1795.5 },
                    { 101918, 1798 },
                    { 151010, 1800.5 },
                    { 101013, 1803 },
                    { 151015, 1805.5 },
                    { 101112, 1812 },
                    { 151114, 1814.5 },
                    { 101117, 1817 },
                    { 151119, 1819.5 },
                    { 101212, 1822 },
                    { 151214, 1824.5 },
                    { 101217, 1827 },
                    { 151219, 1829.5 },
                    { 101312, 1832 },
                    { 151314, 1834.5 },
                    { 101317, 1837 },
                    { 151319, 1839.5 },
                    { 101412, 1842 },
                    { 151414, 1844.5 },
                    { 101417, 1847 },
                    { 151419, 1849.5 },
                    { 101512, 1852 },
                    { 151514, 1854.5 },
                    { 101517, 1857 },
                    { 151519, 1859.5 },
                    { 101612, 1862 },
                    { 151614, 1864.5 },
                    { 101617, 1867 },
                    { 151619, 1869.5 },
                    { 101712, 1872 },
                    { 151714, 1874.5 },
                    { 101717, 1877 },
                    { 151719, 1879.5 },
                    { 101812, 1882 },
                    { 151814, 1884.5 },
                    { 101817, 1887 },
                    { 151819, 1889.5 },
                    { 101912, 1892 },
                    { 151914, 1894.5 },
                    { 101917, 1897 },
                    { 151919, 1899.5 },
                    { 101012, 1902 },
                    { 151014, 1904.5 },
                    { 101017, 1907 },
                    { 151019, 1909.5 },
                    { 101112, 1912 },
                    { 151114, 1914.5 },
                    { 101117, 1917 },
                    { 151119, 1919.5 },
                    { 101212, 1922 },
                    { 151214, 1924.5 },
                    { 101217, 1927 },
                    { 151219, 1929.5 },
                    { 101312, 1932 },
                    { 151314, 1934.5 },
                    { 101317, 1937 },
                    { 151319, 1939.5 },
                    { 101412, 1942 },
                    { 151414, 1944.5 },
                    { 101417, 1947 },
                    { 151419, 1949.5 },
                    { 101512, 1952 },
                    { 151514, 1954.5 },
                    { 101517, 1957 },
                    { 151519, 1959.5 },
                    { 101612, 1962 },
                    { 151614, 1964.5 },
                    { 101617, 1967 },
                    { 151619, 1969.5 },
                    { 101810, 1980 },
                    { 151812, 1982.5 },
                    { 101815, 1985 },
                    { 151817, 1987.5 },
                    { 101910, 1990 },
                    { 151912, 1992.5 },
                    { 101915, 1995 },
                    { 151917, 1997.5 },
                    { 101010, 2000 },
                    { 151012, 2002.5 },
                    { 101015, 2005 },
                    { 151017, 2007.5 },
                    { 101110, 2010 },
                    { 151112, 2012.5 },
                    { 101115, 2015 },
                    { 151117, 2017.5 },
                    { 101210, 2020 },
                    { 151212, 2022.5 },
                    { 101215, 2025 },
                    { 151217, 2027.5 },
                    { 101310, 2030 },
                    { 151312, 2032.5 },
                    { 101315, 2035 },
                    { 151317, 2037.5 },
                    { 101410, 2040 },
                    { 151412, 2042.5 },
                    { 101415, 2045 },
                    { 151417, 2047.5 },
                    { 101510, 2050 },
                    { 151512, 2052.5 },
                    { 101515, 2055 },
                    { 151517, 2057.5 },
                    { 101610, 2060 },
                    { 151612, 2062.5 },
                    { 101615, 2065 },
                    { 151617, 2067.5 },
                    { 101710, 2070 },
                    { 151712, 2072.5 },
                    { 101715, 2075 },
                    { 151717, 2077.5 },
                    { 101810, 2080 },
                    { 151812, 2082.5 },
                    { 101815, 2085 },
                    { 151817, 2087.5 },
                    { 101910, 2090 },
                    { 151912, 2092.5 },
                    { 101915, 2095 },
                    { 151917, 2097.5 },
                    { 101010, 2100 },
                    { 151012, 2102.5 },
                    { 101015, 2105 },
                    { 151017, 2107.5 },
                    { 101110, 2110 },
                    { 151112, 2112.5 },
                    { 101115, 2115 },
                    { 151117, 2117.5 },
                    { 101210, 2120 },
                    { 151212, 2122.5 },
                    { 101215, 2125 },
                    { 151217, 2127.5 },
                    { 101310, 2130 },
                    { 151312, 2132.5 },
                    { 101315, 2135 },
                    { 151317, 2137.5 },
                    { 101414, 2144 },
                    { 151416, 2146.5 },
                    { 101419, 2149 },
                    { 151511, 2151.5 },
                    { 101514, 2154 },
                    { 151516, 2156.5 },
                    { 101519, 2159 },
                    { 151611, 2161.5 },
                    { 101614, 2164 },
                    { 151616, 2166.5 },
                    { 101619, 2169 },
                    { 151711, 2171.5 },
                    { 101714, 2174 },
                    { 151716, 2176.5 },
                    { 101719, 2179 },
                    { 151811, 2181.5 },
                    { 101814, 2184 },
                    { 151816, 2186.5 },
                    { 101819, 2189 },
                    { 151911, 2191.5 },
                    { 101914, 2194 },
                    { 151916, 2196.5 },
                    { 101919, 2199 },
                    { 151011, 2201.5 },
                    { 101014, 2204 },
                    { 151016, 2206.5 },
                    { 101019, 2209 },
                    { 151111, 2211.5 },
                    { 101114, 2214 },
                    { 151116, 2216.5 },
                    { 101119, 2219 },
                    { 151211, 2221.5 },
                    { 101214, 2224 },
                    { 151216, 2226.5 },
                    { 101219, 2229 },
                    { 151311, 2231.5 },
                    { 101314, 2234 },
                    { 151316, 2236.5 },
                    { 101319, 2239 },
                    { 151411, 2241.5 },
                    { 101414, 2244 },
                    { 151416, 2246.5 },
                    { 101419, 2249 },
                    { 151511, 2251.5 },
                    { 101514, 2254 },
                    { 151516, 2256.5 },
                    { 101519, 2259 },
                    { 151611, 2261.5 },
                    { 101614, 2264 },
                    { 151616, 2266.5 },
                    { 101619, 2269 },
                    { 151711, 2271.5 },
                    { 101714, 2274 },
                    { 151716, 2276.5 },
                    { 101719, 2279 },
                    { 151811, 2281.5 },
                    { 101814, 2284 },
                    { 151816, 2286.5 },
                    { 101819, 2289 },
                    { 151911, 2291.5 },
                    { 101914, 2294 },
                    { 151916, 2296.5 },
                    { 101919, 2299 },
                    { 151011, 2301.5 },
                    { 101018, 2308 },
                    { 151110, 2310.5 },
                    { 101113, 2313 },
                    { 151115, 2315.5 },
                    { 101118, 2318 },
                    { 151210, 2320.5 },
                    { 101213, 2323 },
                    { 151215, 2325.5 },
                    { 101218, 2328 },
                    { 151310, 2330.5 },
                    { 101313, 2333 },
                    { 151315, 2335.5 },
                    { 101318, 2338 },
                    { 151410, 2340.5 },
                    { 101413, 2343 },
                    { 151415, 2345.5 },
                    { 101418, 2348 },
                    { 151510, 2350.5 },
                    { 101513, 2353 },
                    { 151515, 2355.5 },
                    { 101518, 2358 },
                    { 151610, 2360.5 },
                    { 101613, 2363 },
                    { 151615, 2365.5 },
                    { 101618, 2368 },
                    { 151710, 2370.5 },
                    { 101713, 2373 },
                    { 151715, 2375.5 },
                    { 101718, 2378 },
                    { 151810, 2380.5 },
                    { 101813, 2383 },
                    { 151815, 2385.5 },
                    { 101818, 2388 },
                    { 151910, 2390.5 },
                    { 101913, 2393 },
                    { 151915, 2395.5 },
                    { 101918, 2398 },
                    { 151010, 2400.5 },
                    { 101013, 2403 },
                    { 151015, 2405.5 },
                    { 101018, 2408 },
                    { 151110, 2410.5 },
                    { 101113, 2413 },
                    { 151115, 2415.5 },
                    { 101118, 2418 },
                    { 151210, 2420.5 },
                    { 101213, 2423 },
                    { 151215, 2425.5 },
                    { 101218, 2428 },
                    { 151310, 2430.5 },
                    { 101313, 2433 },
                    { 151315, 2435.5 },
                    { 101318, 2438 },
                    { 151410, 2440.5 },
                    { 101413, 2443 },
                    { 151415, 2445.5 },
                    { 101418, 2448 },
                    { 151510, 2450.5 },
                    { 101513, 2453 },
                    { 151515, 2455.5 },
                    { 101518, 2458 },
                    { 151610, 2460.5 },
                    { 101613, 2463 },
                    { 151615, 2465.5 },
                    { 101712, 2472 },
                    { 151714, 2474.5 },
                    { 101717, 2477 },
                    { 151719, 2479.5 },
                    { 101812, 2482 },
                    { 151814, 2484.5 },
                    { 101817, 2487 },
                    { 151819, 2489.5 },
                    { 101912, 2492 },
                    { 151914, 2494.5 },
                    { 101917, 2497 },
                    { 151919, 2499.5 },
                    { 101012, 2502 },
                    { 151014, 2504.5 },
                    { 101017, 2507 },
                    { 151019, 2509.5 },
                    { 101112, 2512 },
                    { 151114, 2514.5 },
                    { 101117, 2517 },
                    { 151119, 2519.5 },
                    { 101212, 2522 },
                    { 151214, 2524.5 },
                    { 101217, 2527 },
                    { 151219, 2529.5 },
                    { 101312, 2532 },
                    { 151314, 2534.5 },
                    { 101317, 2537 },
                    { 151319, 2539.5 },
                    { 101412, 2542 },
                    { 151414, 2544.5 },
                    { 101417, 2547 },
                    { 151419, 2549.5 },
                    { 101512, 2552 },
                    { 151514, 2554.5 },
                    { 101517, 2557 },
                    { 151519, 2559.5 },
                    { 101612, 2562 },
                    { 151614, 2564.5 },
                    { 101617, 2567 },
                    { 151619, 2569.5 },
                    { 101712, 2572 },
                    { 151714, 2574.5 },
                    { 101717, 2577 },
                    { 151719, 2579.5 },
                    { 101812, 2582 },
                    { 151814, 2584.5 },
                    { 101817, 2587 },
                    { 151819, 2589.5 },
                    { 101912, 2592 },
                    { 151914, 2594.5 },
                    { 101917, 2597 },
                    { 151919, 2599.5 },
                    { 101012, 2602 },
                    { 151014, 2604.5 },
                    { 101017, 2607 },
                    { 151019, 2609.5 },
                    { 101112, 2612 },
                    { 151114, 2614.5 },
                    { 101117, 2617 },
                    { 151119, 2619.5 },
                    { 101212, 2622 },
                    { 151214, 2624.5 },
                    { 101217, 2627 },
                    { 151219, 2629.5 }
                };

                lDict = new Dictionary<int, double>
                {
                    { 100010, 0 },
                    { 101015, 0.05 },
                    { 101110, 0.1 },
                    { 101115, 0.15 },
                    { 101210, 0.2 },
                    { 101215, 0.25 },
                    { 101310, 0.3 },
                    { 101315, 0.35 },
                    { 101410, 0.4 },
                    { 101415, 0.45 },
                    { 101510, 0.5 },
                    { 101515, 0.55 },
                    { 101610, 0.6 },
                    { 101615, 0.65 },
                    { 101710, 0.7 },
                    { 101715, 0.75 },
                    { 101810, 0.8 },
                    { 101815, 0.85 },
                    { 101910, 0.9 },
                    { 101915, 0.95 },
                    { 111010, 1 },
                    { 111015, 1.05 },
                    { 111110, 1.1 },
                    { 111115, 1.15 },
                    { 111210, 1.2 },
                    { 111215, 1.25 },
                    { 111310, 1.3 },
                    { 111315, 1.35 },
                    { 111410, 1.4 },
                    { 111415, 1.45 },
                    { 111510, 1.5 },
                    { 111515, 1.55 },
                    { 111610, 1.6 },
                    { 111615, 1.65 },
                    { 111710, 1.7 },
                    { 111715, 1.75 },
                    { 111810, 1.8 },
                    { 111815, 1.85 },
                    { 111910, 1.9 },
                    { 111915, 1.95 },
                    { 121010, 2 },
                    { 121015, 2.05 },
                    { 121110, 2.1 },
                    { 121115, 2.15 },
                    { 121210, 2.2 },
                    { 121215, 2.25 },
                    { 121310, 2.3 },
                    { 121315, 2.35 },
                    { 121410, 2.4 },
                    { 121415, 2.45 },
                    { 121510, 2.5 },
                    { 121515, 2.55 },
                    { 121610, 2.6 },
                    { 121615, 2.65 },
                    { 121710, 2.7 },
                    { 121715, 2.75 },
                    { 121810, 2.8 },
                    { 121815, 2.85 },
                    { 121910, 2.9 },
                    { 121915, 2.95 },
                    { 131010, 3 },
                    { 131015, 3.05 },
                    { 131110, 3.1 },
                    { 131115, 3.15 },
                    { 131210, 3.2 },
                    { 131215, 3.25 },
                    { 131310, 3.3 },
                    { 131315, 3.35 },
                    { 131410, 3.4 },
                    { 131415, 3.45 },
                    { 131510, 3.5 },
                    { 131515, 3.55 },
                    { 131610, 3.6 },
                    { 131615, 3.65 },
                    { 131710, 3.7 },
                    { 131715, 3.75 },
                    { 131810, 3.8 },
                    { 131815, 3.85 },
                    { 131910, 3.9 },
                    { 131915, 3.95 },
                    { 141010, 4 },
                    { 141015, 4.05 },
                    { 141110, 4.1 },
                    { 141115, 4.15 },
                    { 141210, 4.2 },
                    { 141215, 4.25 },
                    { 141310, 4.3 },
                    { 141315, 4.35 },
                    { 141410, 4.4 },
                    { 141415, 4.45 },
                    { 141510, 4.5 },
                    { 141515, 4.55 },
                    { 141610, 4.6 },
                    { 141615, 4.65 },
                    { 141710, 4.7 },
                    { 141715, 4.75 },
                    { 141810, 4.8 },
                    { 141815, 4.85 },
                    { 141910, 4.9 },
                    { 141915, 4.95 },
                    { 151010, 5 },
                    { 151015, 5.05 },
                    { 151110, 5.1 },
                    { 151115, 5.15 },
                    { 151210, 5.2 },
                    { 151215, 5.25 },
                    { 151310, 5.3 },
                    { 151315, 5.35 },
                    { 151410, 5.4 },
                    { 151415, 5.45 },
                    { 151510, 5.5 },
                    { 151515, 5.55 },
                    { 151610, 5.6 },
                    { 151615, 5.65 },
                    { 151710, 5.7 },
                    { 151715, 5.75 },
                    { 151810, 5.8 },
                    { 151815, 5.85 },
                    { 151910, 5.9 },
                    { 151915, 5.95 },
                    { 161010, 6 },
                    { 161015, 6.05 },
                    { 161110, 6.1 },
                    { 161115, 6.15 },
                    { 161210, 6.2 },
                    { 161215, 6.25 },
                    { 161310, 6.3 },
                    { 161315, 6.35 }
                };
            }
            catch (Exception e)
            {
                MessageBox.Show("InitArray error\n" + e.Message + "\n" + e.StackTrace);
            }
        }
        public override bool On()
        {
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SerialPortTuner=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
            SerialLock.WaitOne();
            SerialPortTuner.DtrEnable = false;
            SerialPortTuner.RtsEnable = true;
            Thread.Sleep(1000);
            SerialPortTuner.DtrEnable = true;
            SerialPortTuner.RtsEnable = false;
            isOn = true;
            SerialLock.ReleaseMutex();
            return true;
        }
        public override bool Off()
        {
            SendCmd(0x0a);
            isOn = false;
            return true;
        }

        public override void Operate(bool on)
        {
            SendCmd(0x0d);
        }
        public override double GetInductance()
        {
            bool ok = GetStatus2(Tuner.Screen.ManualTune);
            if (ok == false)
            {
                //do
                //{
                    //SelectManualTunePage();
                    ok = GetStatus2(Tuner.Screen.ManualTune);
                //} while (!ok);
            }
            return Inductance;
        }
        public override double GetCapacitance()
        {
            bool ok = GetStatus2(Tuner.Screen.ManualTune);
            if (ok == false)
            {
                //do
                //{
                    //SelectManualTunePage();
                    ok = GetStatus2(Tuner.Screen.ManualTune);
                //} while( !ok );
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
