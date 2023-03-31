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

        Thread myThread;
        private void ThreadTask()
        {
            //Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x80, 0x80 };
            Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x90, 0x90 };
            Byte[] response = new Byte[128];
            Thread.Sleep(1000);
            SerialPortTuner.Write(cmd, 0, 6);
            runThread = true;
            while (runThread)
            {
                Byte myByte;
                myByte = 0x00;
                try
                {
                    SerialPortTuner.Write(cmd, 0, 6);
                }
                catch (Exception)
                {
                    return;
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
                            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "timeout");
                    }
                response[0] = myByte;
                response[1] = (byte)SerialPortTuner.ReadByte();
                if (response[1] != 0xaa) continue;
                response[2] = (byte)SerialPortTuner.ReadByte();
                if (response[2] != 0xaa) continue;
                response[3] = (byte)SerialPortTuner.ReadByte(); // should be the length
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "bytes=" + response[3]);
                for (int i = 0; i < response[3]; ++i)
                {
                    response[i + 4] = (byte)SerialPortTuner.ReadByte();
                }
                var sresponse = System.Text.Encoding.Default.GetString(response);
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, sresponse);
                // "ªªªC,13K,S,R,A,1,10,1a,0r,L,00\00, 0.00, 0.00, 0.0, 0.0,081,000,000,N,N,
                // SYNC,ID,Operate(S/O),Rx/Tx,Bank,Input,Band,TXAnt and ATU, RxAnt,PwrLevel,PwrOut,SWRATU,SWRANT,VPA, IPA, TempUpper, TempLower,
                string[] mytokens = sresponse.Split(',');
                if (mytokens.Length > 15)
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
                        char c = mytokens[20][0];
                        if (c != '\0')
                        {
                            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, mytokens[20]);
                            Application.DoEvents();
                        }
                    }
                    Application.DoEvents();
                }
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
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "SetAntenna " + antennaNumberRequested);
            try
            {
                int tmp;
                if (int.TryParse(antenna, out tmp) == false) 
                    Thread.Sleep(2000);
                if (int.Parse(antenna.Substring(0,1)) == antennaNumberRequested)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Antenna already set to " + antennaNumberRequested);
                    return;
                }
                Byte[] cmd = { 0x55, 0x55, 0x55, 0x01, 0x04, 0x04 };
                Byte[] response = new Byte[128];
                SerialPortTuner.Write(cmd, 0, 6);
                Thread.Sleep(1000);
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tuner error: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace);
            }
        }
    }
}
