using AmpAutoTunerUtility;

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmpAutoTunerUtility
{
    public class TunerMFJ928 : Tuner
    {
        enum TunerStateEnum { UNKNOWN, SLEEP, AWAKE, TUNING, TUNEDONE, ERR}
        TunerStateEnum TunerState = TunerStateEnum.UNKNOWN;
        private SerialPort SerialPortTuner = null;
        public double FwdPwr { get; set; }
        public double RefPwr { get; set; }
        public double SWR { get; set; }
        //public new int Inductance { get; set; }
        //public int Capacitance { get; set; }
        
        volatile bool Tuning = false;

        public TunerMFJ928(string model, string comport, string baud, out string error)
        {
            this.model = model;
            this.comport = comport;
            this.baud = baud;
            error = null;
            SWR = 0;
            if (model.Equals("MFJ-928"))
            {
                baud = "4800";
            }
            if (comport.Length == 0 || baud.Length == 0)
            {
                return;
            }
            SerialPortTuner = new SerialPort
            {
                PortName = comport,
                BaudRate = Int32.Parse(baud),
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
                SetText(DebugEnum.ERR, MyTime() + "Error opening Tuner...\n" + ex.Message);
                error = "Error opening Tuner...\n" + ex.Message;
                return;
            }
            SetText(DebugEnum.LOG, MyTime() + "Tuner opened on " + comport + "\n");
            //SerialPortTuner.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);


            byte[] buffer = new byte[4096];
            byte[] buffer1 = new byte[1];
            Action kickoffRead = null;
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            Queue<byte> myQueue = new Queue<byte>();
            kickoffRead = delegate
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
                            if (cmd[0]==0xfe && cmd.Length!=8)
                            {
                                continue;
                            }
                            RaiseAppSerialDataEvent(cmd);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        HandleAppSerialError(ex);
                        return;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.Message + "\n", ex.StackTrace);
                    }
                    kickoffRead();
                }, null);
            };
            //*/
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            kickoffRead();
            CMD_Amp(1);
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
                        SetText(DebugEnum.ERR, MyTime() + "Unknown " + b +"\n");
                        cmd = new byte[1];
                        cmd[0] = q.Dequeue();
                        return null;
                }
            }
            return null;
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetInductance(int value)
        {
            byte[] bcdValue = IntToBCD5((uint)value, 2);
            byte[] cmdSetInductance = { 0xfe, 0xfe, 0x21, 0x01, bcdValue[0], bcdValue[1], 0x00, 0xfd };
            SendCmd(cmdSetInductance);
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetCapacitance(int value)
        {
            SetText(DebugEnum.VERBOSE, MyTime() + "SetCapacitance " + value + "\n");
            switch(value)
            {
                case 1:
                    byte[] cmdSetCapacitanceInc = { 0xfe, 0xfe, 0x02, 0x00, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceInc);
                    break;
                case -1:
                    byte[] cmdSetCapacitanceDec = { 0xfe, 0xfe, 0x02, 0x01, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceDec);
                    break;
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
            //SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "Capacitance = " + this.Capacitance + "\n");
            //SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "Inductance = " + this.Inductance+"\n");
            return "FwdPwr " +FwdPwr.ToString()+"W";
        }

        public override string GetSWR()
        {
            return "SWR " + string.Format("{0:0.00}", SWR);
        }

        public void SetText(DebugEnum debugLevel, string v)
        {
            if (debugLevel <= DebugLevel || debugLevel == Tuner.DebugEnum.LOG || Tuning)
            {
                DebugMsg myMsg = new DebugMsg
                {
                    Text = v,
                    Level = debugLevel
                };
                msgQueue.Enqueue(myMsg);
            }
        }

        private string MyTime()
        {
            string time = DateTime.Now.ToString("HH:mm:ss.fff") + ": ";
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
            SetText(Tuner.DebugEnum.VERBOSE,MyTime() + "Auto Status: "+Dumphex(data)+"\n");
            CMD_GetAutoStatus(data);
            SetText(DebugEnum.TRACE, MyTime() + "Auto Status: " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", SWR) + "\n");

        }

        private void MsgFE(byte[] received)
        {
            if (received.Length != 8)
            {
                MessageBox.Show("oops!!  MsgFe != 8");
                return;
            }
            //SetText(DebugEnum.VERBOSE, "MsgFE: " + dumphex(data) + "\n");           //Thread.Sleep(200);
            byte cmd = received[2];
            byte scmd = received[3];
            byte[] b2 = new byte[3];
            switch (cmd)
            {
                // FE-FE-06-01-80-00-79-FD -- tune failure
                case 0x06:
                    SetText(DebugEnum.TRACE, MyTime() + "case 0x06 observed\n");
                    switch (scmd)
                    {
                        case 0x00:
                            SetText(DebugEnum.TRACE, MyTime() + "Started by Tune cmd\n");
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            byte[] b1 = new byte[2];
                            b1[1] = received[4];
                            b1[0] = received[5];
                            SWR = BCD5ToInt(b1, 2)/10.0;
                            if (SWR < 2.0) MemoryWriteCurrentFreq();
                            break;
                        case 0x01:
                            SetText(DebugEnum.TRACE, MyTime() + "Started by > SWR\n");
                            break;
                        case 0x02:
                            SetText(DebugEnum.TRACE, MyTime() + "Started by StickyTune\n");
                            break;
                        case 0x80:
                            SetText(DebugEnum.ERR, MyTime() + "Error: Increase Power\n");
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            SWR = 99;
                            break;
                        case 0x81:
                            SetText(DebugEnum.ERR, MyTime() + "Error: Decrease Power\n");
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            SWR = 99;
                            break;
                        case 0x82:
                            SetText(DebugEnum.ERR, MyTime() + "Error: Overload\n");
                            Tuning = false;
                            TunerState = TunerStateEnum.TUNEDONE;
                            SWR = 99;
                            break;
                        default:
                            SetText(DebugEnum.ERR, MyTime() + "Error: tune=" + received[3].ToString("X"));
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
                            Inductance = (int)BCD5ToInt(b2, 2);
                            break;
                        default:
                            SetText(DebugEnum.ERR, MyTime() + "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                case 0x11:
                    switch(received[3])
                    {
                        case 0x06:
                            string status = "off\n";
                            if (received[4] == 0x01) status = "on\n";
                            SetText(DebugEnum.TRACE, MyTime() + "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.ERR, MyTime() + "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                case 0x21:
                    switch (received[3])
                    {
                        case 0x06:
                            string status = "off\n";
                            if (received[4] == 0x01) status = "on\n";
                            SetText(DebugEnum.VERBOSE, MyTime() + "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.ERR, MyTime() + "CMD unknown=" + Dumphex(received) + "\n");
                            break;
                    }
                    break;
                default:
                    SetText(DebugEnum.ERR,MyTime() + "CMD unknown=" + Dumphex(received) + "\n");
                    break;
            }
            SetText(DebugEnum.VERBOSE, MyTime() + "CMD packet: " + Dumphex(received) + "\n");
        }

        // Poll common elements
        public override void Poll()
        {
            if (SerialPortTuner == null) return;
            byte[] cmdGetCapacitance = { 0xfe, 0xfe, 0x10, 0x41, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdGetCapacitance);
            byte[] cmdGetInductance = { 0xfe, 0xfe, 0x10, 0x42, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdGetInductance);
        }

        private void RaiseAppSerialDataEvent(byte[] received)
        {
            SetText(DebugEnum.VERBOSE, MyTime() + " "+Dumphex(received)+"\n");
            //return;
            byte datum = received[0];
            switch(datum)
            {
                case 0xfa:
                    SetText(DebugEnum.VERBOSE, MyTime()+"Awake\n");
                    break;
                case 0xfb:
                    SetText(DebugEnum.VERBOSE, MyTime() + "Sleeping\n");
                    break;
                case 0xfe:
                    MsgFE(received);
                    break;
                case 0xff:
                    MsgFF(received);
                    break;
                default:
                    SetText(DebugEnum.ERR, MyTime() + datum.ToString("X")+" - Error unknown event in " + Dumphex(received)+"\n");
                    break;
            }
            Application.DoEvents();
        }

        private void HandleAppSerialError(Exception ex)
        {
            SetText(DebugEnum.ERR, MyTime() + "Serial err: " + ex.Message+"\n"+ex.StackTrace);
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

        public uint BCD5ToInt(byte[] bcd, int len)
        {
            uint outInt = 0;

            for (int i = 0; i < len; i++)
            {
                int mul = (int)Math.Pow(10, (i * 2));
                outInt += (uint)(((bcd[i] & 0xF)) * mul);
                mul = (int)Math.Pow(10, (i * 2) + 1);
                outInt += (uint)(((bcd[i] >> 4)) * mul);
            }

            return outInt;
        }
        public byte[] IntToBCD5(uint numericvalue, int bytesize = 5)
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
            SetText(DebugEnum.VERBOSE, MyTime() + "AutoStatus=" + data + "\n");
            byte[] b2 = new byte[2];
            b2[0] = data[1];
            b2[1] = data[0];
            double fwd = BCD5ToInt(b2, 2) / 10.0;
            b2[0] = data[3];
            b2[1] = data[2];
            double rev = BCD5ToInt(b2, 2) / 10.0;
            FwdPwr = fwd;
            RefPwr = rev;
            SetText(DebugEnum.VERBOSE, MyTime() + "AutoStatus=" + data + ", Fwd="+fwd+","+ "Ref="+RefPwr+"\n");

            if (fwd > 0)
            {
                SWR = (1+Math.Sqrt(rev/fwd)) / (1-Math.Sqrt(rev/fwd));
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
                SetText(DebugEnum.TRACE, MyTime() + "Tune wait " + ++nn+"\n");
                Thread.Sleep(500);
            }
            SetText(DebugEnum.TRACE, MyTime() + "Tuning done SWR=" + string.Format("{0:0.00}", SWR)+"\n");
            if (SWR > 0 && SWR <= 2) response = 'T';
            else if (SWR > 0 && SWR < 3) response = 'M';
            else response = 'F';
            return response;
        }

        private void SendCmd(byte[] cmd)
        {
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
                SetText(DebugEnum.VERBOSE, MyTime() + "SendCMD:"+Dumphex(cmd)+"\n");
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show("Timeout sending cmd\n" + ex.StackTrace);
            }
        }

        override public void CMD_Amp(byte on)
        {
            byte[] cmdamp = { 0xfe, 0xfe, 0x21, 0x06, on, 0x00, 0x00, 0xfd };
            SendCmd(cmdamp);
        }

        public void CMD_Amp()
        {
            byte[] cmdampoff = { 0xfe, 0xfe, 0x11, 0x06, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdampoff);
            //return response[4] == 0x01;
        }

        public void CMD_Tune()
        {
            SetText(DebugEnum.TRACE, MyTime() + "CMD_Tune()\n");
            Tuning = true;
            byte[] tune = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(tune);
            TunerState = TunerStateEnum.TUNING;
        }

        public override void Tune()
        {
            //byte[] response = new byte[8];
            //SetText(DebugEnum.TRACE, MyTime() + "Turn amp off\n");
            //CMD_Amp(0);
            //if (CMD_Amp())
            //{
            //    SetText(DebugEnum.ERR, MyTime() + "Amp not off, returning!!\n");
            //    return;
            //}
            SetText(DebugEnum.TRACE, MyTime() + "Start tune1, Tuning="+Tuning+"\n");
            CMD_Tune();
            SetText(DebugEnum.TRACE, MyTime() + "Start tune2, Tuning=" + Tuning + "\n");
            int n = 0;
            while (Tuning && n++ < 60)
            {
                SetText(DebugEnum.TRACE, MyTime() + "wait for Tuning " + n + "/30\n");
                Application.DoEvents();
                Thread.Sleep(1000);
                SetText(DebugEnum.TRACE, MyTime() + "Tuning status=" + Tuning + "\n");
            }
            //Thread.Sleep(500);
            SetText(DebugEnum.TRACE, MyTime() + "Tuned " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", SWR)+"\n");
            //SetText(DebugEnum.TRACE, MyTime() + "Turn amp on\n");
            //CMD_Amp(1);
            return;
        }

        private void MemoryWriteCurrentFreq()
        {
            byte[] writeMemory = { 0xfe, 0xfe, 0x30, 0x00, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(writeMemory);
        }
        private string Dumphex(byte[] data)
        {
            return(BitConverter.ToString(data));
        }

        public override bool GetAmpStatus()
        {
            SetText(DebugEnum.TRACE, MyTime() + "GetAmpStatus()\n");
            byte[] cmd = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(cmd);
            return false;
        }

        public override void SetTuningMode(int mode)
        {

        }

        public override void SetAmp(bool on)
        {
        }
    }
}
