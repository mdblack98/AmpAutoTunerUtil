using Microsoft.VisualBasic.Logging;
using System;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
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
        private string band = "?";
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
                ReadTimeout = 1000,
                WriteTimeout = 500
            };
            SerialPortTuner.Open();
            Thread.Sleep(500);
            myThread = new Thread(new ThreadStart(this.ThreadTask));
            myThread.IsBackground = true;
            myThread.Start();

            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }
        //public override void Dispose(bool disposing)
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (SerialPortTuner != null) SerialPortTuner.Dispose();
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
        static Mutex GetStatusLock = new Mutex(false,"AmpSerial");

        private bool GetStatus2()
        {
            // display, set, forward, set, when done display again
            Byte[] cmdDisplay = { 0x55, 0x55, 0x55, 0x01, 0x0c, 0x0c };
            Byte[] cmdSet = { 0x55, 0x55, 0x55, 0x01, 0x11, 0x11 };
            Byte[] cmdRight = { 0x55, 0x55, 0x55, 0x01, 0x10, 0x10 };
            GetStatusLock.WaitOne();
            SerialPortTuner.Write(cmdDisplay, 0, 6);
            SerialPortTuner.Write(cmdDisplay, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdRight, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.Write(cmdSet, 0, 6);
            Thread.Sleep(200);
            SerialPortTuner.DiscardInBuffer();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            Byte[] response = new Byte[512];
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
                GetStatusLock.ReleaseMutex();
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
                    GetStatusLock.ReleaseMutex();
                    return false;
                }
            response[0] = myByte;
            response[1] = (byte)SerialPortTuner.ReadByte();
            if (response[1] != 0xaa) { GetStatusLock.ReleaseMutex(); return false; }
            response[2] = (byte)SerialPortTuner.ReadByte();
            if (response[2] != 0xaa) { GetStatusLock.ReleaseMutex(); return false; }
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
                    GetStatusLock.ReleaseMutex();
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
            if (chkByte0 != resByte0) { GetStatusLock.ReleaseMutex(); return false; }
            if (chkByte1 != resByte1) { GetStatusLock.ReleaseMutex(); return false; }
            int tunerStatus = response[8];
            int indexBytes = 56;
            int[] bandLookup = { 0, 4, 8, 1, 5, 9, 2, 6, 10, 3, 7, 11 };
            for (int i=0; i < 12;++i)
            {
                int i2 = bandLookup[i];
                if (response[indexBytes] == 46) antennas[i2, 0] = "NO";
                else antennas[i2,0] = (response[indexBytes] - 16).ToString();
                if (response[indexBytes+3] == 46) antennas[i2, 1] = "NO"; 
                else antennas[i2,1] = (response[indexBytes + 3] -  16).ToString();
                indexBytes += 13;
                if (i == 2 || i== 5 || i ==8) indexBytes++;
            }
            SerialPortTuner.Write(cmdDisplay, 0, 6);
            return true;
        }
        private bool GetStatus()
        {
            GetStatusLock.WaitOne();
            SerialPortTuner.DiscardInBuffer();
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x90, 0x90 };
            Byte[] response = new Byte[128];
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
                GetStatusLock.ReleaseMutex();
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
                    GetStatusLock.ReleaseMutex();                     
                    return false;
                }
            response[0] = myByte;
            response[1] = (byte)SerialPortTuner.ReadByte();
            if (response[1] != 0xaa) { GetStatusLock.ReleaseMutex(); return false; }
            response[2] = (byte)SerialPortTuner.ReadByte();
            if (response[2] != 0xaa) { GetStatusLock.ReleaseMutex(); return false; }
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
                    GetStatusLock.ReleaseMutex();
                    return false;
                }
                ++n;
            }
            long sum = 0;
            for (int i = 0; i < 67; ++i)
            {
                sum+= response[i + 4];
            }
            long chkByte0 = sum % 256;
            long chkByte1 = sum / 256;
            long resByte0 = response[71];
            long resByte1 = response[72];
            var sresponse = System.Text.Encoding.Default.GetString(response, 0, n-5);
            if (chkByte0 != resByte0) { GetStatusLock.ReleaseMutex(); return false; }
            if (chkByte1 != resByte1) { GetStatusLock.ReleaseMutex(); return false; }

            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
            // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
            // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
            string[] mytokens = sresponse.Split(',');
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens.Length + ":" + sresponse + "\n");
            if (mytokens.Length > 20)
            {
                var newBand = mytokens[6];
                var newAntenna = mytokens[7];
                if (!band.Equals(newBand))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed band " + band + " to " + newBand + "\n");
                    Application.DoEvents();
                    band = newBand;
                }
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "antenna=" + antenna + ", newAntenna=" + newAntenna + "\n");

                if (!antenna.Equals(newAntenna))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed antenna " + antenna + " to " + newAntenna + "\n");
                    Application.DoEvents();
                    antenna = newAntenna;
                }
                AntennaNumber = int.Parse(antenna.Substring(0, 1));
                power = mytokens[10];
                swr1 = mytokens[11];
                swr2 = mytokens[12];
                if (temp1.Equals("?")) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears connected\n");
                temp1 = mytokens[15];
                if (mytokens.Length >= 21)
                {
                    if (mytokens[20].Length > 0 && !mytokens[20].Equals("N\r"))
                    {
                        //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "MSG: " + mytokens[20] + "\n");
                    }
                }
                Application.DoEvents();
            }
            GetStatusLock.ReleaseMutex();
            return true;
        }

        Thread myThread;
        private void ThreadTask()
        {
            runThread = true;
            while (runThread)
            {
                while(GetStatus() && runThread)
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
                while(GetStatus()==false);
                int tmp;
                if (int.TryParse(antenna.Substring(0,1), out tmp) == false) 
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
        public override void Tune()
        {
            GetStatus2();
        }

        public override void Poll()
        {
            while(!GetStatus());
            while(!GetStatus2());
            for(int i=0;i<12;++i)
            {
                
            }
        }
    }
}
