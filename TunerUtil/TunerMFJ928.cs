using AmpAutoTunerUtility;

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Windows.Forms;
using static AmpAutoTunerUtility.DebugMsg;

namespace AmpAutoTunerUtility
{
    public sealed class TunerMFJ928 : Tuner
    {
        enum TunerStateEnum { UNKNOWN, SLEEP, AWAKE, TUNING, TUNEDONE, ERR}
        TunerStateEnum TunerState = TunerStateEnum.UNKNOWN;
        private SerialPort SerialPortTuner = null;
        public double FwdPwr { get; set; }
        public double RefPwr { get; set; }
        //public new int Inductance { get; set; }
        //public int Capacitance { get; set; }
        
        volatile bool Tuning = false;
        volatile bool dataReceived = false;

        public TunerMFJ928(string model, string comport, string baud, out string error)
        {
            this.model = model;
            this.comport = comport;
            this.baud = baud;
            error = null;
            SetSWR(0);

            baud = "4800"; // baud rate is fixed
            if (comport == null || baud == null || comport.Length == 0 || baud.Length == 0)
            {
                MessageBox.Show("com port(" + comport + ") or baud(" + baud + ") is empty");
                return;
            }
            SerialPortTuner = new SerialPort
            {
                PortName = comport,
                BaudRate = Int32.Parse(baud, CultureInfo.InvariantCulture),
                Parity = Parity.None,
                DataBits = 8,
                StopBits = StopBits.One,
                Handshake = Handshake.None,
                ReadTimeout = 30000,
                WriteTimeout = 500
            };
            try
            {
                SerialPortTuner.Open();
            }
            catch (Exception ex)
            {
                Application.DoEvents();
                SetText(DebugEnum.ERR, "Error opening Tuner...\n" + ex.Message);
                error = "Error opening Tuner...\n" + ex.Message;
                throw;
            }

            byte[] buffer = new byte[4096];
            byte[] buffer1 = new byte[1];
            Action kickoffRead = null;
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            Queue<byte> myQueue = new Queue<byte>();
            GetAntenna();
            kickoffRead = delegate
            {
                //while (true)
                {
                    if (!SerialPortTuner.BaseStream.CanRead) return;
                    SerialPortTuner.BaseStream.BeginRead(buffer, 0, buffer.Length, delegate (IAsyncResult ar)
                    {
                        try
                        {
                            if (!SerialPortTuner.IsOpen) return;
                            int actualLength = SerialPortTuner.BaseStream.EndRead(ar);
                            byte[] received = new byte[actualLength];
                            Buffer.BlockCopy(buffer, 0, received, 0, actualLength);
                            foreach (byte b in received) myQueue.Enqueue(b);
                            byte[] cmd;
                            while ((cmd = GetCmd(myQueue)) != null)
                            {
                                if (cmd[0] == 0xfe && cmd.Length != 8)
                                {
                                    continue;
                                }
                                RaiseAppSerialDataEvent(cmd);
                                dataReceived = true;
                            }
                        }
                        catch (InvalidOperationException ex)
                        {
                            HandleAppSerialError(ex);
                            return;
                        }
                        catch (Exception)
                        {
                            //MessageBox.Show(ex.Message + "\n", ex.StackTrace);
                            //throw;
                        }
                    kickoffRead();
                    }, null);
                }
            };
            //*/
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            kickoffRead();
            CMDAmp(1);
            GetAmpStatus();
            Thread.Sleep(1000);
            if (!dataReceived)
            {
                DebugAddMsg(DebugEnum.ERR, "Tuner not talking??\n");
            }
        }

        byte[] GetCmd(Queue<byte> q)
        {
            byte[] cmd;
            int nbytes;
            foreach (byte b in q)
            {
                switch(b)
                {
                    case 0xfa: // awake
                    case 0xfb: // sleep
                        cmd = new byte[1];
                        cmd[0] = q.Dequeue();
                        return cmd;
                    case 0xfe:
                        nbytes = 8;
                        if (q.Count >= nbytes)
                        {
                            cmd = new byte[nbytes];
                            for (int i = 0; i < nbytes; ++i)
                            {
                                cmd[i] = q.Dequeue();
                            }
                            return cmd;
                        }
                        return null;
                    case 0xff: // autostatus
                        nbytes = 6;
                        if (q.Count >= nbytes)
                        {
                            cmd = new byte[nbytes];
                            for (int i = 0; i < nbytes; ++i)
                            {
                                cmd[i] = q.Dequeue();
                            }
                            return cmd;
                        }
                        return null;
                    default:
                        SetText(DebugEnum.ERR, "Unknown " + b +"\n");
                        cmd = new byte[1];
                        cmd[0] = q.Dequeue();
                        return null;
                }
            }
            return null;
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetInductance(double value)
        {
            byte[] bcdValue = IntToBCD5((uint)value, 2);
            byte[] cmdSetInductance = { 0xfe, 0xfe, 0x21, 0x01, bcdValue[0], bcdValue[1], 0x00, 0xfd };
            SendCmd(cmdSetInductance);
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetCapacitance(int value)
        {
            SetText(DebugEnum.VERBOSE, "SetCapacitance " + value + "\n");
            switch(value)
            {
                /*
                case 1:
                    byte[] cmdSetCapacitanceInc = { 0xfe, 0xfe, 0x02, 0x00, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceInc);
                    break;
                case -1:
                    byte[] cmdSetCapacitanceDec = { 0xfe, 0xfe, 0x02, 0x01, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceDec);
                    break;
                */
                default:
                    byte[] bcdValue = IntToBCD5((uint)value, 2);
                    byte[] cmdSetCapacitance = { 0xfe, 0xfe, 0x21, 0x00, bcdValue[1], bcdValue[0], 0x00, 0xfd };
                    SendCmd(cmdSetCapacitance);
                    break;
            }
        }

        public override string GetPower()
        {
            Poll();
            //SetText(DebugEnum.DEBUG_VERBOSE, "Capacitance = " + this.Capacitance + "\n");
            //SetText(DebugEnum.DEBUG_VERBOSE, "Inductance = " + this.Inductance+"\n");
            return "FwdPwr " +FwdPwr.ToString(CultureInfo.InvariantCulture) +"W";
        }

        public override string GetSWRString()
        {
            return "SWR " + string.Format(CultureInfo.InvariantCulture,"{0:0.00}", GetSWR());
        }

        public void SetText(DebugEnum debugLevel, string v)
        {
            if (debugLevel <= DebugLevel || debugLevel == DebugEnum.LOG || Tuning)
            {
                DebugMsg myMsg = new DebugMsg
                {
                    Text = MyTime() + " " + v,
                    Level = debugLevel
                };
                //msgQueue.Enqueue(myMsg);
                DebugAddMsg(myMsg);
            }
        }

        private static string MyTime()
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + ": ";
            return time;
        }

        private void MsgFF(byte[] received)
        {
            byte[] data = new byte[5];
            int i = 0;
            for (; i < received.Length - 1 && i < data.Length; ++i)
            {
                data[i] = received[i + 1];
            }
            for (; i < 5; ++i)
            {
                data[i] = (byte)SerialPortTuner.ReadByte();
            }
            //Thread.Sleep(200);
            //SetText(DebugEnum.VERBOSE,"Auto Status: "+Dumphex(data)+"\n");
            CMD_GetAutoStatus(data);
            //SetText(DebugEnum.TRACE, "Auto Status: " + FwdPwr + "/" + RefPwr + "/" + string.Format(CultureInfo.InvariantCulture,"{0:0.00}", SWR) + "\n");

        }

        private void MsgFE(byte[] received)
        {
            if (received.Length != 8)
            {
                //MessageBox.Show("oops!!  MsgFe != 8");
                return;
            }
            //SetText(DebugEnum.VERBOSE, "MsgFE: " + dumphex(data) + "\n");           //Thread.Sleep(200);
            byte cmd = received[2];
            byte scmd = received[3];
            byte statusbyte = received[4];
            byte[] b2 = new byte[4];
            switch (cmd)
            {
                // FE-FE-06-01-80-00-79-FD -- tune failure
                case 0x06:
                    //SetText(DebugEnum.TRACE, "case 0x06 observed\n");
                    switch (scmd)
                    {
                        case 0x00:
                            //SetText(DebugEnum.TRACE, "Started by Tune cmd\n");
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            byte[] b1 = new byte[2];
                            b1[1] = received[4];
                            b1[0] = received[5];
                            SetSWR(BCD5ToInt(b1, 2)/10.0);
                            if (GetSWR() < 2.0) MemoryWriteCurrentFreq();
                            break;
                        case 0x01:
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            switch (statusbyte)
                            {
                                case 0x00: SetText(DebugEnum.ERR, "Start by Tune Command\n");break;
                                case 0x01: SetText(DebugEnum.ERR, "Start by SWR > AutoTuneSWR\n"); break;
                                case 0x02: SetText(DebugEnum.ERR, "Start by StickyTune\n"); break;
                                case 0x80: SetText(DebugEnum.ERR, "QRO Increase Power\n"); break;
                                case 0x81: SetText(DebugEnum.ERR, "QRP Decrease\n"); break;
                                case 0x82: SetText(DebugEnum.ERR, "QRT Overload\n"); break;
                                default: SetText(DebugEnum.ERR, "Unknown status" + b2 + "\n"); break;
                                
                            }
                            //SetText(DebugEnum.ERR, "Tuning failed\n");
                            break;
                        default:
                            //SetText(DebugEnum.ERR, "Error: tune???=" + received[3].ToString("X", CultureInfo.InvariantCulture));
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            break;
                    }
                    break;
                case 0x10:
                    switch (received[3])
                    {
                        case 0x41:
                            b2[0] = received[5];
                            b2[1] = received[4];
                            Capacitance = (int)BCD5ToInt(b2, 2);
                            break;
                        case 0x42:
                            b2[0] = received[5];
                            b2[1] = received[4];
                            Inductance = (int)BCD5ToInt(b2, 2)/100.0;
                            break;
                        default:
                            SetText(DebugEnum.ERR, "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                case 0x11:
                    switch(received[3])
                    {
                        case 0x03:
                            if (received[4] == 0x00) AntennaNumber = 1;
                            else AntennaNumber = 2;
                            break;
                        case 0x06:
                            string status = "off\n";
                            if (received[4] == 0x01) status = "on\n";
                            SetText(DebugEnum.TRACE, "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.ERR, "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                case 0x21:
                    switch (received[3])
                    {
                        case 0x03:
                            AntennaNumber = received[4] == 0 ? 1 : 2;
                            break;
                        case 0x06:
                            string status = "off\n";
                            if (received[4] == 0x01) status = "on\n";
                            SetText(DebugEnum.VERBOSE, "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.ERR, "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                default:
                    SetText(DebugEnum.ERR,"CMD unknown=" + Dumphex(received) + "\n");
                    break;
            }
            //SetText(DebugEnum.VERBOSE, "CMD packet: " + Dumphex(received) + "\n");
        }

        // Poll common elements
        public override void Poll()
        {
            return;
            /*if (SerialPortTuner != null)
            {
                if (!Tuning)
                {
                    byte[] cmdGetCapacitance = { 0xfe, 0xfe, 0x10, 0x41, 0x00, 0x00, 0x00, 0xfd };
                    SendCmd(cmdGetCapacitance);
                    byte[] cmdGetInductance = { 0xfe, 0xfe, 0x10, 0x42, 0x00, 0x00, 0x00, 0xfd };
                    SendCmd(cmdGetInductance);
                }
            }
            */
        }

        private void RaiseAppSerialDataEvent(byte[] received)
        {
            //SetText(DebugEnum.VERBOSE, Dumphex(received)+"\n");
            //return;
            byte datum = received[0];
            switch(datum)
            {
                case 0xfa:
                    //SetText(DebugEnum.VERBOSE, "Awake\n");
                    break;
                case 0xfb:
                    //SetText(DebugEnum.VERBOSE, "Sleeping\n");
                    break;
                case 0xfe:
                    MsgFE(received);
                    break;
                case 0xff:
                    MsgFF(received);
                    break;
                default:
                    SetText(DebugEnum.ERR,  datum.ToString("X", CultureInfo.InvariantCulture) +" - Error unknown event in " + Dumphex(received)+"\n");
                    break;
            }
            //Application.DoEvents();
        }

        private void HandleAppSerialError(Exception ex)
        {
            SetText(DebugEnum.ERR, "Serial err: " + ex.Message+"\n"+ex.StackTrace);
        }

        public override void Close()
        {
            if (SerialPortTuner != null)
            {
                SerialPortTuner.BaseStream.Close();
                SerialPortTuner.Close();
                //SerialThread.Abort();
                Thread.Sleep(500); // let thread detect closure
                SerialPortTuner.Dispose();
                SerialPortTuner = null;
            }
        }

        public override string GetSerialPortTuner()
        {
            if (SerialPortTuner == null)
            {
                return (string)null;
            }
            return SerialPortTuner.PortName;
        }

        /*
        private void Flush()
        {
            while (SerialPortTuner.BytesToRead > 0)
            {
                SerialPortTuner.ReadChar();
            }
        }
        */

        public static uint BCD5ToInt(byte[] bcd, int len)
        {
            uint outInt = 0;
            if (bcd == null) return 0;
            for (int i = 0; i < len; i++)
            {
                int mul = (int)Math.Pow(10, (i * 2));
                outInt += (uint)(((bcd[i] & 0xF)) * mul);
                mul = (int)Math.Pow(10, (i * 2) + 1);
                outInt += (uint)(((bcd[i] >> 4)) * mul);
            }

            return outInt;
        }
        public static byte[] IntToBCD5(uint numericvalue, int bytesize = 5)
        {
            byte[] bcd = new byte[bytesize];
            for (int byteNo = 0; byteNo < bytesize; ++byteNo)
                bcd[byteNo] = 0;
            for (int digit = 0; digit < bytesize * 2; ++digit)
            {
                uint hexpart = numericvalue % 10;
                bcd[digit / 2] |= (byte)(hexpart << ((digit % 2) * 4));
                numericvalue /= 10;
            }
            return bcd;
        }

        private void CMD_GetAutoStatus(byte[] data)
        {
            //SetText(DebugEnum.VERBOSE, "AutoStatus=" + data + "\n");
            byte[] b2 = new byte[2];
            b2[0] = data[1];
            b2[1] = data[0];
            double fwd = BCD5ToInt(b2, 2) / 10.0;
            b2[0] = data[3];
            b2[1] = data[2];
            double rev = BCD5ToInt(b2, 2) / 10.0;
            FwdPwr = fwd;
            RefPwr = rev;
            //SetText(DebugEnum.VERBOSE, "AutoStatus= Fwd="+fwd+","+ "Ref="+RefPwr+"\n");

            if (fwd > 0)
            {
                SetSWR((1+Math.Sqrt(rev/fwd)) / (1-Math.Sqrt(rev/fwd)));
            }
            //else Swr = 0;
            return;
        }

        public override char ReadResponse()
        {
            // Have to handle each response
            //byte[] data = new byte[4096];
            //int i = 0;
            int nn = 0;
            char response;
            //bool GotSWR = false;
            int nwait = 0;
            while(TunerState != TunerStateEnum.TUNEDONE && nwait++ < 20)
            {
                SetText(DebugEnum.TRACE, "Tune wait " + ++nn+"\n");
                Thread.Sleep(500);
            }
            if (TunerState == TunerStateEnum.TUNEDONE) Thread.Sleep(2000);
            SetText(DebugEnum.TRACE, "Tuning done SWR=" + string.Format(CultureInfo.InvariantCulture,"{0:0.00}", GetSWR()) +"\n");
            if (GetSWR() > 0 && GetSWR() <= 2) response = 'T';
            else if (GetSWR() > 0 && GetSWR() < 3) response = 'M';
            else response = 'F';
            return response;
        }

        private void SendCmd(byte[] cmd)
        {
            if (SerialPortTuner == null) return;
            if (!SerialPortTuner.IsOpen) return;
            try
            {
                //SerialPortTuner.ReadTimeout = 2000;  // do we need longer for tuning?
                byte[] wakeup = { 0x00 };
                SerialPortTuner.Write(wakeup, 0, wakeup.Length);
                Thread.Sleep(3); // Manual says sleep 3ms before sending cmd
                //Flush();
                byte checksum = (byte)((1024 - cmd[2] - cmd[3] - cmd[4] - cmd[5]) & 0xff);
                cmd[6] = checksum;
                SerialPortTuner.Write(cmd, 0, cmd.Length);
                //SetText(DebugEnum.VERBOSE, "SendCMD:"+Dumphex(cmd)+"\n");
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show("Timeout sending cmd\n" + ex.StackTrace);
            }
        }

        override public void CMDAmp(byte on)
        {
            byte[] cmdamp = { 0xfe, 0xfe, 0x21, 0x06, on, 0x00, 0x00, 0xfd };
            SendCmd(cmdamp);
        }

        public void CMDAmp()
        {
            byte[] cmdampoff = { 0xfe, 0xfe, 0x11, 0x06, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdampoff);
            //return response[4] == 0x01;
        }

        public void CMDTune()
        {
            SetText(DebugEnum.TRACE, "CMD_Tune()\n");
            Tuning = true;
            byte[] tune = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(tune);
            TunerState = TunerStateEnum.TUNING;
        }

        public override void Tune()
        {
            //byte[] response = new byte[8];
            //SetText(DebugEnum.TRACE, "Turn amp off\n");
            //CMD_Amp(0);
            //if (CMD_Amp())
            //{
            //    SetText(DebugEnum.ERR, "Amp not off, returning!!\n");
            //    return;
            //}
            SetText(DebugEnum.TRACE, "Start tune1, Tuning="+Tuning+"\n");
            CMDTune();
            SetText(DebugEnum.TRACE, "Start tune2, Tuning=" + Tuning + "\n");
            int n = 0;
            while (Tuning && n++ < 60)
            {
                SetText(DebugEnum.LOG, "wait for Tuning " + n + "/60\n");
                Application.DoEvents();
                Thread.Sleep(1000);
                SetText(DebugEnum.TRACE, "Tuning status=" + Tuning + "\n");
            }
            //Thread.Sleep(500);
            SetText(DebugEnum.TRACE, "Tuned " + FwdPwr + "/" + RefPwr + "/" + string.Format(CultureInfo.InvariantCulture,"{0:0.00}", GetSWR()) +"\n");
            //SetText(DebugEnum.TRACE, "Turn amp on\n");
            //CMD_Amp(1);
            return;
        }

        private void MemoryWriteCurrentFreq()
        {
            byte[] writeMemory = { 0xfe, 0xfe, 0x30, 0x00, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(writeMemory);
        }
        private static string Dumphex(byte[] data)
        {
            return(BitConverter.ToString(data));
        }

        public override bool GetAmpStatus()
        {
            SetText(DebugEnum.TRACE, "GetAmpStatus()\n");
            byte[] cmd = { 0xfe, 0xfe, 0x06, 0x00, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(cmd);
            return false;
        }

        public override void SetTuningMode(int mode)
        {

        }

        public override void SetAmp(bool on)
        {
        }
        public override void SetCapacitance(double v)
        {
            SetCapacitance(v);
            /*
            SetText(DebugEnum.TRACE, "SaveCapacitance()\n");
            byte cap1 = (byte)(v >> 8);
            byte cap0 = (byte)(v & 0xff);
            byte[] cmd = { 0xfe, 0xfe, 0x21, 0x00, cap1, cap0, 0xf9, 0xfd };
            SendCmd(cmd);
            */
        }
        public override void SetInductance(decimal v)
        {
            SetText(DebugEnum.TRACE, "SaveInductance()\n");
            var b = IntToBCD5(Convert.ToUInt32(v * 100), 2);
            byte[] cmd = { 0xfe, 0xfe, 0x22, 0x01, b[1], b[0], 0xf9, 0xfd };
            SendCmd(cmd);
        }
        public override void Save()
        {
            MemoryWriteCurrentFreq();
        }

        public override int GetAntenna()
        {
            byte[] cmd = { 0xfe, 0xfe, 0x11, 0x03, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmd);
            Thread.Sleep(200);
            SetText(DebugEnum.TRACE, "GetAntenna()=" + AntennaNumber + "\n");
            return AntennaNumber;
        }
        public override void SetAntenna(int antennaNumberRequested, bool tuneIsRunning = false)
        {
            //SetText(DebugEnum.TRACE, "SetAntenna() requested=" + antennaNumberRequested + "\n");
            byte[] cmd = { 0xfe, 0xfe, 0x21, 0x03, (byte)(antennaNumberRequested - 1), 0x00, 0x00, 0xfd };
            SendCmd(cmd);
            Thread.Sleep(200);
            //SetText(DebugEnum.TRACE, "SetAntenna() result=" + AntennaNumber + "\n");
        }
        /*
        public override void Dispose(bool disposing)
        {
        }

        public override void Dispose()
        {
        }
        */
    }
}
