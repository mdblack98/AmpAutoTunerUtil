using Microsoft.VisualBasic.Logging;
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
using static AmpAutoTunerUtility.DebugMsg;

namespace AmpAutoTunerUtility
{
    class TunerExpertLinears : Tuner
    {
        private SerialPort SerialPortTuner = null;
        private bool runThread = false;
        readonly char response = 'X';
        public double SWR1 = 0;
        public double SWR2 = 0;
        private string swr1 = "?";
        private string swr2 = "?";
        private string powerLevel = "?";
        private string power = "?";
        //public char bank = "?";
        private string temp1 = "?";
        private string antenna = "?";
        private string bandstr = "?";
        public bool tuning = false;
        //Byte[] responseOld = new byte[512];
        const double swrCutoff = 1.35;
        private byte ledStatus = 0;
        public TunerExpertLinears(string model, string comport, string baud, out string errmsg)
        {
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
            //if (isOn == false)
            //{
            //    isOn = true;
            //}
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
                { 12, 25, 6963 },
                {  3, 50, 10075 },
                {  9, 50, 13975 },
                {  3, 50, 18075 },
                { 11, 50, 20975 },
                {  3, 73, 24891 },
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
        //static readonly Mutex SerialLock = new Mutex(false, "AmpSerial");

        public override void SelectDisplayPage()
        {
            Byte[] cmdDisplay = [0x55, 0x55, 0x55, 0x01, 0x0c, 0x0c];
            //Monitor.Enter("ExpertLinear");
            SerialPortTuner!.Write(cmdDisplay, 0, 6);
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
            byte[] cmdSet = [0x55, 0x55, 0x55, 0x01, 0x11, 0x11];
            Byte[] cmdRight = [0x55, 0x55, 0x55, 0x01, 0x10, 0x10];
            //Monitor.Enter("ExpertLinear");
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
        }
        public override void SelectAntennaPage()
        {
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SelectManualTunePage tuner not open?");
                return;
            }
            // display, set, forward, set, when done display again
            Byte[] cmdSet = [0x55, 0x55, 0x55, 0x01, 0x11, 0x11];
            Byte[] cmdRight = [0x55, 0x55, 0x55, 0x01, 0x10, 0x10];
            //Monitor.Enter("ExpertLinear");
            // Have to go to home display page and display again
            SelectDisplayPage();
            SelectDisplayPage();
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
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
                Byte[] cmdBuf = [0x55, 0x55, 0x55, 0x01, cmd, cmd];
                //Monitor.Enter("ExpertLinear");
                SerialPortTuner.Write(cmdBuf, 0, 6);
                Thread.Sleep(150);
            }
            catch (Exception)
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.WARN, "Can't send tuner cmd????");
                return;
            }
        }

        readonly char[] lookup = [' ', '!', '"', '#','$', '%', '&', '\\', '(', ')', '*', '+', ',', '-','.', '/','0','1','2','3','4','5','6','7','8','9',':',';','<','=','>','?','@','A','B','C','D','E','F','G','H','I','J','K','L','M','N','O','P','Q','R','S','T','U','V','W','Z','[','\\','^','_','?','a','b','c','d','e','f','g','h','i','j','k','l','m','n','o','p','q','r','s','t','u','v','w','x','y','z','{','|','}','?'];
        private Screen screenLast = Screen.Unknown;
        private object gotTuner = "gotTuner";

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

            //Monitor.Enter("ExpertLinear");


            try
            {
                int loop = 10;
                if (screenLast != myScreen)
                {
                    if (myScreen == Screen.Unknown) MessageBox.Show("Where are we?\n");
                    if (myScreen == Screen.Antenna) SelectAntennaPage();
                    else if (myScreen == Screen.ManualTune) SelectManualTunePage();
                    else SelectDisplayPage();
                    screenLast = (Screen)myScreen;
                }
                Thread.Sleep(1000);
                SerialPortTuner.DiscardInBuffer();
                Byte[] cmd = [0x55, 0x55, 0x55, 0x01, 0x80, 0x80];
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
                    return false;
                }
                while (myByte != 0xaa)
                    try
                    {
                        myByte = (byte)SerialPortTuner.ReadByte();
                        if (myByte == 0 && --loop == 0) { return false; }
                        //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Got " + String.Format("{0:X}", myByte));
                    }
                    catch (Exception ex)
                    {
                        if (ex.HResult != -2146233083)
                            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "timeout\n");
                        return false;
                    }
                response[0] = myByte;
                response[1] = (byte)SerialPortTuner.ReadByte();
                if (response[1] != 0xaa) { return false; }
                response[2] = (byte)SerialPortTuner.ReadByte();
                if (response[2] != 0xaa) { return false; }
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
                if (chkByte0 != resByte0) { return false; }
                if (chkByte1 != resByte1) { return false; }
                // check for M in MANUAL
                if (myScreen == Screen.Antenna && response[17] != 0x33)
                {
                    return true;
                }
                else if (myScreen == Screen.ManualTune && response[17] != 0x2d)
                {
                    return true;
                }
                ledStatus = response[8];
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "LED status: " + ledStatus.ToString("X2") + "\n");
                if (myScreen == Screen.ManualTune)
                {
                    try
                    {
                        while (response[160] != 0x0e)
                        {
                            return true;
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
                        value = lookup[(int)response[198]].ToString();
                        value += lookup[(int)response[199]].ToString();
                        value += lookup[(int)response[200]].ToString();
                        value += lookup[(int)response[201]].ToString();
                        value += lookup[(int)response[202]].ToString();
                        SWRAnt = 0;
                        Double.TryParse(value, out SWRAnt);
                        value = lookup[(int)response[318]].ToString();
                        value += lookup[(int)response[319]].ToString();
                        value += lookup[(int)response[320]].ToString();
                        value += lookup[(int)response[321]].ToString();
                        value += lookup[(int)response[322]].ToString();
                        SWRATU = 0;
                        Double.TryParse(value.Trim(), out SWRATU);
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
                int[] bandLookup = [0, 4, 8, 1, 5, 9, 2, 6, 10, 3, 7, 11];
                if (antennas is null)
                {
                    MessageBox.Show("tuner1.antennas=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
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
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus error: " + ex.Message + "\n" + ex.StackTrace);
                    return true;
                }
            return true;
        }
        public override bool GetStatus()
        {
            try
            {
                if (SerialPortTuner == null)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "SerialPortTuner==null");
                    return false;
                }
                //if (freqWalkIsRunning == true || !isOn)
                //if (!isOn)
                //{
                //    return false;
                //    //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "freqWalkIsRunning " + freqWalkIsRunning + ", isOn=" + isOn);;
                //}
                /*
                try
                {
                    Monitor.Enter("ExpertLinear");
                }
                catch (Exception)
                {
                    return false;
                }
                */
                int repeat = 1;
                //Monitor.Enter("ExpertLinear");
            writeagain:
                if (SerialPortTuner == null) { return false; }
                SerialPortTuner.DiscardInBuffer();
                Byte[] cmd = [0x55, 0x55, 0x55, 0x01, 0x90, 0x90];
                Byte[] response = new Byte[128];
                //SerialPortTuner.Write(cmd, 0, 6);
                int myByte;
                for (int i = 0; i < response.Length; ++i) response[i] = 0;
                myByte = 0x00;
                try
                {
                    SerialPortTuner.Write(cmd, 0, 6);
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, ex.Message + "\n" + ex.StackTrace);
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
                            if (IsOn)
                                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "Elapsed expired\n");
                            if (repeat>0)
                            {
                                --repeat;
                                goto writeagain;
                            }
                            IsOn = false;
                            return false;
                        }
                        if (myByte == -1)
                        {
                            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "myByte == -1\n");
                            return false;
                        }
                        //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Got " + String.Format("{0:X}", myByte));
                    }
                    catch (Exception ex)
                    {
                        //if (ex.HResult != -2146233083)
                        if (IsOn) // only show timeout if tuner is on
                            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, ex.Message+ex.StackTrace);
                        IsOn = false;
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
                        return true;
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
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "chkByte0\n");
                    return true; // return true since we are still connected
                }
                if (chkByte1 != resByte1)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "chkByte1\n");
                    return true; // return true since we are still connected
                }

                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
                // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
                // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
                string[] mytokens = sresponse.Split(',');
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens.Length + ":" + sresponse + "\n");
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
                        powerLevel = mytokens[9];
                        power = mytokens[10];
                        swr1 = mytokens[11];
                        if (!swr1.Equals("0.00)"))
                            SWRATU = Convert.ToDouble(swr1); // ATU SWR
                        SetSWR(Double.Parse(swr1));
                        swr2 = mytokens[12];
                        if (!swr2.Equals("0.00"))
                            SWRAnt = Convert.ToDouble(swr2);
                        IsOperate = mytokens[2] != "S";
                        if (temp1.Equals("?")) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears connected\n");
                        temp1 = mytokens[15];
                        if (mytokens.Length >= 21)
                        {
                            response[0] = (byte)mytokens[18][0];
                            response[1] = 0;
                            if (mytokens[18].Length > 0 && !mytokens[18].Equals("N"))
                            {
                                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "MSG: " + mytokens[18] + "\n");
                            }
                        }
                        Application.DoEvents();
                    }
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "MSG: " + ex.Message + "\n" + ex.StackTrace);
                    return false;
                }
            }
            catch (Exception ex)
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus: " + ex.Message + "\n" + ex.StackTrace);
                return true;
            }
            IsOn = true;
            return true;
        }

        readonly Thread myThread;
        private void ThreadTask()
        {
            runThread = true;
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "Expert Linears thread started\n");
            while (runThread)
            {
                try
                {
                    Thread.Sleep(1000);
                    //if (!freqWalkIsRunning)
                    Monitor.Enter(gotTuner);
                    if (!poweringDown)
                        IsOn = GetStatus();
                    Monitor.Exit(gotTuner);
                    if (IsOn==false)
                    {
                        continue;
                    }
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
        public override string GetSerialPortTuner()
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

        public override string GetPowerLevel()
        {
            string tmp;
            if (powerLevel == "H") tmp = "Max";
            else if (powerLevel == "M") tmp = "Mid";
            else if (powerLevel == "L") tmp = "Low";
            else tmp = "???";
            return tmp;
        }
        public override void SetPowerLevel(string value)
        {
            if (powerLevel == value) return;
            string current = GetPowerLevel();
            if ((current == "Max" || current == "Low") && value == "Mid")
            {
                SendCmd(0x0b);
            }
            else if (current == "Max" && value == "Low")
            {
                SendCmd(0x0b);
                SendCmd(0x0b);
            }
            else if (current == "Low" && value == "Max")
            {
                SendCmd(0x0b);
                SendCmd(0x0b);
            }
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
                    Byte[] cmd = [0x55, 0x55, 0x55, 0x01, 0x04, 0x04];
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
            //Monitor.Enter("ExpertLinear");
            Byte[] cmd = [0x55, 0x55, 0x55, 0x01, 0x09, 0x09];
            Byte[] cmdMsg = [0x55, 0x55, 0x55, 0x01, 0x80, 0x80];
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
            //return; //MDB
            GetStatus();
            //this.SelectAntennaPage();
            GetStatus2(Screen.Antenna);
        }
        //private Dictionary <int, double> ?lDict;
        //private Dictionary<int, double> ?cDict;

        public override bool On()
        {
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SerialPortTuner=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return false;
            }
            //Monitor.Enter("ExpertLinear");
            SerialPortTuner.DtrEnable = false;
            SerialPortTuner.RtsEnable = true;
            Thread.Sleep(1000);
            SerialPortTuner.DtrEnable = true;
            SerialPortTuner.RtsEnable = false;
            IsOn = true;
            return true;
        }
        public override bool Off()
        {
            IsOn = false;
            SendCmd(0x0a);
            return true;
        }

        public override void Operate(bool on)
        {
            if (on && !this.IsOperate)
                SendCmd(0x0d);
            if (!on && this.IsOperate)
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
                    GetStatus2(Tuner.Screen.ManualTune);
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
                    GetStatus2(Tuner.Screen.ManualTune);
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
        void FindMinSWR_L()
        {
            GetStatus2(Tuner.Screen.ManualTune);
            //GetStatus();
            double l = GetInductance();
            var SWRMin = SWRATU;
            if (SWRMin == 0) return;
            byte cmdDownL = 0x05; // L Down
            byte cmdUpL = 0x06; // L Up
            double minSWR_L = l;
            bool loop;
            do {
                // Step L up
                loop = false;
                Application.DoEvents();
                SendCmd(cmdUpL);
                SendCmd(cmdUpL);
                Thread.Sleep(100);
                Application.DoEvents();
                //GetStatus();
                GetStatus2(Tuner.Screen.ManualTune);
                if (SWRATU < SWRMin)
                {
                    loop = true;
                    SWRMin = GetSWR();
                    minSWR_L = GetInductance();
                }
                l = GetInductance();
            } while (loop && SWRMin > swrCutoff && l > 0 && l < 269);

            // Move to last best one
            SetInductance(minSWR_L);
            //GetStatus();
            GetStatus2(Tuner.Screen.ManualTune);
            SWRMin = SWRATU;
            do
            {
                // Step L down
                SendCmd(cmdDownL);
                SendCmd(cmdDownL);
                Application.DoEvents();
                Thread.Sleep(200);
                Application.DoEvents();
                //GetStatus();
                GetStatus2(Tuner.Screen.ManualTune);
                loop = false;
                double swr = SWRATU;
                if (swr < SWRMin)
                {
                    loop = true;
                    SWRMin = swr;
                    minSWR_L = GetInductance();
                    Application.DoEvents();
                }
                l = GetInductance();
                Application.DoEvents();
            } while (loop && SWRMin > swrCutoff && l > 0 && l < 269);

            // Move to last best one
            SetInductance(minSWR_L);
            GetStatus2(Tuner.Screen.ManualTune);
        }
        void FindMinSWR_C()
        {
            GetStatus2(Tuner.Screen.ManualTune);
            GetStatus();
            double c = GetCapacitance();
            var SWRMin = SWRATU;
            if (SWRMin == 0) return;
            byte cmdDownC = 0x07; // C Down
            byte cmdUpC = 0x08; // C Up
            double minSWR_C = c;
            bool loop;
            do {
                // Step C up
                Application.DoEvents();
                SendCmd(cmdUpC);
                SendCmd(cmdUpC);
                Application.DoEvents();
                Thread.Sleep(200);
                Application.DoEvents();
                //GetStatus();
                GetStatus2(Tuner.Screen.ManualTune);
                double swr = SWRATU;
                loop = false;
                if (swr < SWRMin)
                {
                    Application.DoEvents();
                    loop = true;
                    SWRMin = swr;
                    minSWR_C = GetCapacitance();
                }
                Thread.Sleep(1000);
            } while (loop && SWRMin > swrCutoff && minSWR_C <= 2629.5);

            // Move to last best one
            SetCapacitance(minSWR_C);
            Application.DoEvents();
            //GetStatus();
            GetStatus2(Tuner.Screen.ManualTune);
            SWRMin = SWRATU;
            do
            {
                Application.DoEvents();
                loop = false;
                // Step C down
                SendCmd(cmdDownC);
                SendCmd(cmdDownC);
                Application.DoEvents();
                Thread.Sleep(200);
                Application.DoEvents();
                //GetStatus();
                GetStatus2(Tuner.Screen.ManualTune);
                double swr = SWRATU;
                if (swr < SWRMin)
                {
                    loop = true;
                    SWRMin = swr;
                    minSWR_C = GetCapacitance();
                    Application.DoEvents();
                }
            } while (loop && SWRMin > swrCutoff && minSWR_C > 0);

            // Move to last best one
            SetCapacitance(minSWR_C);
            GetStatus2(Tuner.Screen.ManualTune);
        }

        private void TuneCurrentBandWithATU()
        {
            Form1.myRig.ModeA = "FM";
            Form1.myRig.ModeB = "FM";
            var nFreqs = tuneFrequencies[band, 0];
            var step = tuneFrequencies[band, 1];
            var freq = tuneFrequencies[band, 2];
            // build freq table
            int[] freqTable = new int[nFreqs];
            for (int i = 0; i < nFreqs; i++)
            {
                freqTable[i] = (freq + (step * i)) * 1000;
            }
            Form1.myRig.SetPTT(true);
            foreach (int thisFreq in freqTable)
            {
                int freq2Tune = thisFreq;
                if (freq2Tune == 0) continue;
                Form1.myRig.SetFrequency('A', freq2Tune);
                Form1.myRig.SetFrequency('B', freq2Tune);
                SendCmd(0x09);
                Thread.Sleep(100);
                Application.DoEvents();
                int loop = 300;
                do
                {
                    Application.DoEvents();
                    GetStatus2();
                    Thread.Sleep(100);
                } while (--loop>0 && (ledStatus & 0x40) == 0);
            }
            Form1.myRig.SetPTT(false);
            DebugMsg.DebugAddMsg(DebugEnum.LOG, "Tuning done");
        }
        public override void TuneCurrentBand(bool ATU)
        {
            Monitor.Enter(gotTuner);
            if (ATU)
            {
                TuneCurrentBandWithATU();
                Monitor.Exit(gotTuner);
                return;
            }
            try
            {
                byte cmdTune = 0x09;
                GetStatus();
                GetStatus2(Tuner.Screen.ManualTune);
                double l = GetInductance();
                double c = GetCapacitance();
                DebugMsg.DebugAddMsg(DebugEnum.LOG, "L=" + l.ToString("0.0") + "C=" + c.ToString("0,0"));
                Form1.myRig.ModeA = "FM";
                Form1.myRig.ModeB = "FM";

                // Ready for finding min SWR test
                double SWRStart;
                // need the band
                if (tuneFrequencies is null)
                {
                    MessageBox.Show("tuner problem is null ", System.Reflection.MethodBase.GetCurrentMethod().Name);
                    Monitor.Exit(gotTuner);
                    return;
                }
                var nFreqs = tuneFrequencies[band, 0];
                var step = tuneFrequencies[band, 1];
                var freq = tuneFrequencies[band, 2];
                // build freq table
                int[] freqTable = new int[nFreqs];
                for (int i = 0; i < nFreqs; i++)
                {
                    freqTable[i] = (freq + (step * i)) * 1000;
                }
                Form1.myRig.SetFrequency('A', freqTable[0]);
                Form1.myRig.SetFrequency('B', freqTable[0]);
                SelectManualTunePage();
                Form1.myRig.SetPTT(true);
                Application.DoEvents();
                Thread.Sleep(500);
                Application.DoEvents();
                GetStatus2(Tuner.Screen.ManualTune);
                var lastL = GetInductance();
                var lastC = GetCapacitance();
                bool firstFlag = true;
                // loop freq table
                int freq2Tune;
                bool saveIt = false;
                foreach (int thisFreq in freqTable)
                {
                    freq2Tune = thisFreq;
                    if (freq2Tune == 0) continue;
                    Form1.myRig.SetFrequency('A', freq2Tune);
                    Form1.myRig.SetFrequency('B', freq2Tune);
                    Application.DoEvents();
                    double swr;
                    GetStatus2(Tuner.Screen.ManualTune);
                    SWRStart = SWRATU;
                    Application.DoEvents();
                    if (!firstFlag)
                    {
                        Thread.Sleep(500);
                        GetStatus();
                        swr = SWRStart = SWRATU;
                        if (swr > swrCutoff)
                        {
                            SetCapacitance(lastC);
                            SetInductance(lastL);
                            saveIt = true;
                            Application.DoEvents();
                        }
                    }
                    firstFlag = false;
                    do
                    {
                        if (SWRStart < swrCutoff)
                        {
                            Application.DoEvents();
                            //DebugMsg.DebugAddMsg(DebugEnum.LOG, "Have " + freq2Tune + " SWR " + SWRStart);
                            break;
                        }
                        FindMinSWR_L();
                        FindMinSWR_C();
                        var logmsg = "Test " + Form1.frequencyHz + "\t" + SWRStart + "\t" + GetInductance().ToString("{.0}") + "\t" + GetCapacitance().ToString("{.0}") + "\n";
                        DebugMsg.DebugAddMsg(DebugEnum.LOG, logmsg);
                        Application.DoEvents();
                        GetStatus2(Tuner.Screen.ManualTune);
                        swr = SWRATU;
                    } while (swr < SWRStart && swr > swrCutoff);
                    lastC = GetCapacitance();
                    lastL = GetInductance();
                    if (lastC == 0 || lastL == 0)
                    {
                        lastC = 0;
                    }
                    if (saveIt)
                    {
                        Application.DoEvents();
                        SendCmd(cmdTune);  // remember our tuning values
                        Application.DoEvents();
                        Thread.Sleep(200);
                        Application.DoEvents();

                    }
                    DebugMsg.DebugAddMsg(DebugEnum.LOG, "Freq " + freq2Tune + " SWR " + SWRStart.ToString("0.00"));
                }
                Form1.myRig.SetPTT(false);

            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message + ex.StackTrace);
            }
            Monitor.Exit(gotTuner);
        }
    }
}
