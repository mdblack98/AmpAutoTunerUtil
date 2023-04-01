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
            while (myThread.IsAlive) Thread.Sleep(50);

            if (SerialPortTuner != null)
            {
                SerialPortTuner.Close();
                SerialPortTuner.Dispose();
                SerialPortTuner = null;
            }
        }
        static volatile bool GetStatusLock = false;

        private bool GetStatus()
        {
            if (GetStatusLock)
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "GetStatus busy...returning...");
                Application.DoEvents();
                Thread.Sleep(500);
                return false;
            }
            GetStatusLock = true;
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
                GetStatusLock = false; 
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
                    GetStatusLock = false;
                    return false;
                }
            response[0] = myByte;
            response[1] = (byte)SerialPortTuner.ReadByte();
            if (response[1] != 0xaa) { GetStatusLock = false; return false; }
            response[2] = (byte)SerialPortTuner.ReadByte();
            if (response[2] != 0xaa) { GetStatusLock = false; return false; }
            response[3] = (byte)SerialPortTuner.ReadByte(); // should be the length
            int n = 4;
            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "bytes=" + response[3]);
            for (int i = 0; i < response[3] + 5; ++i)
            {
                try
                {
                    response[i + 4] = (byte)SerialPortTuner.ReadByte();
                }
                catch (TimeoutException e)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "GetStatus serial timeout\n");
                    GetStatusLock = false;
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
            if (chkByte0 != resByte0) { GetStatusLock = false; return false; }
            if (chkByte1 != resByte1) { GetStatusLock = false; return false; }

            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
            // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
            // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
            string[] mytokens = sresponse.Split(',');
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens.Length + ":" + sresponse + "\n");
            if (mytokens.Length > 20)
            {
                if (!band.Equals(mytokens[6]))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed from band " + band + " to " + mytokens[6] + "\n");
                    Application.DoEvents();
                    band = mytokens[6];
                }
                if (!antenna.Equals(mytokens[7]))
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Expert Linears changed from antenna " + antenna + " to " + mytokens[7] + "\n");
                    Application.DoEvents();
                    antenna = mytokens[7];
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
                        Application.DoEvents();
                    }
                }
                Application.DoEvents();
            }
            GetStatusLock = false;
            return true;
        }

        Thread myThread;
        private void ThreadTask()
        {
            runThread = true;
            while (runThread)
            {
                //GetStatus();
                Thread.Sleep(1000);
            }
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
        public override void Tune()
        {
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
                Thread.Sleep(500);
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
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "Setting antenna to other antenna\n");
                SerialPortTuner.Write(cmd, 0, 6);
                Thread.Sleep(500);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tuner error: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace);
            }
        }
    }
}
