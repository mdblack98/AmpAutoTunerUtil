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
        
        bool Tuning = false;
        Thread SerialThread;

        public TunerMFJ928(string model, string comport, string baud)
        {
            this.model = model;
            this.comport = comport;
            this.baud = baud;
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
                SetText(DebugEnum.DEBUG_ERR, "Error opening Tuner...\n" + ex.Message);
                return;
            }
            SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "Tuner opened on " + comport + "\n");
            //SerialPortTuner.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);


            byte[] buffer = new byte[4096];
            byte[] buffer1 = new byte[1];
            Action kickoffRead = null;
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            Queue<byte> myQueue = new Queue<byte>();
            kickoffRead = delegate
            {

                SerialPortTuner.BaseStream.BeginRead(buffer, 0, buffer.Length, delegate (IAsyncResult ar)
                {
                    try
                    {
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
                            raiseAppSerialDataEvent(cmd);
                        }
                    }
                    catch (InvalidOperationException ex)
                    {
                        handleAppSerialError(ex);
                        return;
                    }
                    kickoffRead();
                }, null);
            };
            //*/
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            kickoffRead();
            //SerialPortTuner.ReadTimeout = 10;
            //SerialThread = new Thread(new ThreadStart(SerialThreadProc));
            //SerialThread.Start();
            /*
            kickoffRead = delegate
            {
                byte stx = 0;
                //SerialPortTuner.BaseStream.BeginRead(buffer1, 0, buffer1.Length, delegate (IAsyncResult ar)
                bool flag = true;
                while (flag)
                {
                    try
                    {
                        while(SerialPortTuner.BytesToRead == 0)
                        {
                            Application.DoEvents();
                            Thread.Sleep(5);
                        }
                        stx = (byte)SerialPortTuner.ReadByte();
                        flag = false;
                    }
                    catch (Exception ex)
                    {
                        MessageBox.Show(ex.StackTrace);
                        //QueryContinueDragEventArgs;
                    }
                }
                {
                    try
                    {
                        //int actualLength = SerialPortTuner.BaseStream.EndRead(ar);
                        //byte[] received = new byte[actualLength];
                        byte[] received;
                        //Buffer.BlockCopy(buffer1, 0, received, 0, actualLength);
                        if (stx == 0xfe)
                        {
                            SerialPortTuner.BaseStream.Read(buffer, 0, 7);
                            received = new byte[8];
                            received[0] = 0xfe;
                            Buffer.BlockCopy(buffer, 0, received, 1, 7);
                            SetText(DebugEnum.DEBUG_ERR, "Serial data: " + dumphex(received) + "\n");
                        }
                        else if (stx == 0xff)
                        {
                            int actualLength2 = 0;
                            SerialPortTuner.Read(buffer, 0, 5);
                            if (buffer.Length != 5)
                            {
                                SetText(DebugEnum.DEBUG_ERR, "Expected length=5, got length=" + actualLength2 +"\n");
                                SetText(DebugEnum.DEBUG_ERR, "Serial data: " + dumphex(buffer) + "\n");
                            }
                            else
                            {
                                received = new byte[6];
                                received[0] = 0xff;
                                Buffer.BlockCopy(buffer, 0, received, 1, 5);
                                SetText(DebugEnum.DEBUG_ERR, "Serial data: " + dumphex(received) + "\n");
                            }
                        }
                        else if (stx == 0xfb || stx == 0xfa || stx == 0xfd)
                        {
                            SetText(DebugEnum.DEBUG_ERR, "Serial data: " + stx.ToString("X") + "\n");
                        }
                        else
                        {
                            SetText(DebugEnum.DEBUG_ERR, "Serial data unknown: " + stx.ToString("X") + "\n");
                        }
                        //raiseAppSerialDataEvent(received);
                    }
                    catch (InvalidOperationException ex)
                    {
                        handleAppSerialError(ex);
                        //return;
                    }
                    kickoffRead();
                }//, null);
            };
            kickoffRead();
            */
        }

        byte[] GetCmd(Queue<byte> q)
        {
            byte[] cmd;
            int nbytes = 0;
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
                        SetText(DebugEnum.DEBUG_ERR,"Unknown " + b +"\n");
                        cmd = new byte[1];
                        cmd[0] = q.Dequeue();
                        return null;
                }
            }
            return null;
        }
        private void SerialThreadProc()
        {
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            while (SerialPortTuner.IsOpen)
            {
                try
                {
                    byte[] buffer = new byte[4096];
                    byte[] received = null;
                    byte stx = (byte)SerialPortTuner.BaseStream.ReadByte();
                    switch (stx) {
                        case 0xfa:
                        case 0xfb:
                        case 0xfd:
                            SetText(DebugEnum.DEBUG_ERR, "Serial data: " + stx.ToString("X") + "\n");
                            break;
                        case 0xff:
                            received = new byte[6];
                            received[0] = stx;
                            int nbytes = 1;
                            do
                            {
                                int n = SerialPortTuner.BaseStream.Read(received, nbytes, 5);
                                nbytes += n;
                            } while (nbytes != 6);
                            if (nbytes != 6)
                            {
                                SetText(DebugEnum.DEBUG_ERR, "Expected length=5, got length=" + nbytes + "\n");
                                SetText(DebugEnum.DEBUG_ERR, "Serial data: " + dumphex(received) + "\n");
                            }
                            else
                            {
                                SetText(DebugEnum.DEBUG_VERBOSE, "Serial data OK: " + dumphex(received) + "\n");
                            }
                            break;
                        default:
                            SetText(DebugEnum.DEBUG_ERR, "Unknown stx=" + stx.ToString("X")+"\n");
                            break;
                    }
                }
                catch (Exception ex)
                {
                    Thread.Sleep(10);
                }
            }
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetInductance(int value)
        {
            byte[] bcdValue = IntToBCD5((uint)value, 2);
            byte[] response = null;
            byte[] cmdSetInductance = { 0xfe, 0xfe, 0x21, 0x01, bcdValue[0], bcdValue[1], 0x00, 0xfd };
            SendCmd(cmdSetInductance, ref response);
        }

        // If value == 1 or -1 will increment or decrement 
        public override void SetCapacitance(int value)
        {
            SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "SetCapacitance " + value + "\n");
            switch(value)
            {
                case 1:
                    byte[] response1 = null;
                    byte[] cmdSetCapacitanceInc = { 0xfe, 0xfe, 0x02, 0x00, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceInc, ref response1);
                    break;
                case -1:
                    byte[] response2 = null;
                    byte[] cmdSetCapacitanceDec = { 0xfe, 0xfe, 0x02, 0x01, 0x00, 0x01, 0x00, 0xfd };
                    SendCmd(cmdSetCapacitanceDec, ref response2);
                    break;
                default:
                    byte[] bcdValue = IntToBCD5((uint)value, 2);
                    byte[] response3 = null;
                    byte[] cmdSetCapacitance = { 0xfe, 0xfe, 0x21, 0x00, bcdValue[1], bcdValue[0], 0x00, 0xfd };
                    SendCmd(cmdSetCapacitance, ref response3);
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

        public void SetText(DebugEnum debug, string v)
        {
            if (debug <= DebugLevel || Tuning) 
                msg.Enqueue(v);
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
            SetText(Tuner.DebugEnum.DEBUG_VERBOSE,MyTime() + "Auto Status: "+dumphex(data)+"\n");
            CMD_GetAutoStatus(data);
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Auto Status: " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", SWR) + "\n");

        }

        private void MsgFE(byte[] received)
        {
            if (received.Length != 8)
            {
                MessageBox.Show("oops!!  MsgFe != 8");
                return;
            }
            byte[] data = new byte[7];
            int i = 0;
            for (; i < received.Length - 1 && i < 7; ++i)
            {
                data[i] = received[i + 1];
            }
            //for (; i < 7; ++i)
            //{
            //    data[i] = (byte)SerialPortTuner.ReadByte();
            //}
            SetText(DebugEnum.DEBUG_VERBOSE, "MsgFE: " + dumphex(data) + "\n");
            //Thread.Sleep(200);
            byte datum = data[1];
            byte[] b2 = new byte[2];
            switch (datum)
            {
                // FE-06-01-80-00-79-FD -- tune failure
                case 0x06:
                    Tuning = false;
                    TunerState = TunerStateEnum.TUNEDONE;
                    switch (data[3])
                    {
                        case 0x00:
                            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Started by Tune cmd\n");
                            break;
                        case 0x01:
                            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Started by > SWR");
                            break;
                        case 0x02:
                            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Started by StickyTune");
                            break;
                        case 0x80:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "Error: Increase Power\n");
                            SWR = 0;
                            break;
                        case 0x81:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "Error: Decrease Power\n");
                            SWR = 0;
                            break;
                        case 0x82:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "Error: Overload\n");
                            SWR = 0;
                            break;
                        default:

                            break;
                    }
                    break;
                case 0x10:
                    switch (data[2])
                    {
                        case 0x41:
                            b2[0] = data[4];
                            b2[1] = data[3];
                            Capacitance = (int)BCD5ToInt(b2, 2);
                            break;
                        case 0x42:
                            b2[0] = data[4];
                            b2[1] = data[3];
                            Inductance = (int)BCD5ToInt(b2, 2);
                            break;
                        default:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "CMD unknown=" + dumphex(data) + "\n");
                            break;
                    }
                    break;
                case 0x11:
                    switch(data[2])
                    {
                        case 0x06:
                            string status = "off\n";
                            if (data[3] == 0x01) status = "on\n";
                            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "CMD unknown=" + dumphex(data) + "\n");
                            break;
                    }
                    break;
                case 0x21:
                    switch (data[2])
                    {
                        case 0x06:
                            string status = "off\n";
                            if (data[3] == 0x01) status = "on\n";
                            SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "Amp " + status);
                            break;
                        default:
                            SetText(DebugEnum.DEBUG_ERR, MyTime() + "CMD unknown=" + dumphex(data) + "\n");
                            break;
                    }
                    break;
                default:
                    SetText(DebugEnum.DEBUG_ERR,MyTime() + "CMD unknown=" + dumphex(data) + "\n");
                    break;
            }
            SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "CMD packet: " + dumphex(data) + "\n");
        }

        // Poll common elements
        public override void Poll()
        {
            if (SerialPortTuner == null) return;
            byte[] response = new byte[8];
            byte[] cmdGetCapacitance = { 0xfe, 0xfe, 0x10, 0x41, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdGetCapacitance, ref response);
            byte[] cmdGetInductance = { 0xfe, 0xfe, 0x10, 0x42, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdGetInductance, ref response);
        }

        private void raiseAppSerialDataEvent(byte[] received)
        {
            SetText(DebugEnum.DEBUG_ERR, MyTime() + " "+dumphex(received)+"\n");
            //return;
            byte datum = received[0];
            switch(datum)
            {
                case 0xfa:
                    SetText(DebugEnum.DEBUG_VERBOSE, MyTime()+"Awake\n");
                    break;
                case 0xfb:
                    SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "Sleeping\n");
                    break;
                case 0xfe:
                    MsgFE(received);
                    break;
                case 0xff:
                    MsgFF(received);
                    break;
                default:
                    SetText(DebugEnum.DEBUG_ERR, datum.ToString("X")+" - Error unknown event in " + dumphex(received)+"\n");
                    break;
            }
            Application.DoEvents();
        }

        private void handleAppSerialError(Exception ex)
        {
            SetText(DebugEnum.DEBUG_ERR, MyTime() + "Serial err: " + ex.Message+"\n"+ex.StackTrace);
        }

        public override void Close()
        {
            if (SerialPortTuner != null)
            {
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

        private void Flush()
        {
            while (SerialPortTuner.BytesToRead > 0)
            {
                SerialPortTuner.ReadChar();
            }
        }

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
            byte[] b2 = new byte[2];
            b2[0] = data[1];
            b2[1] = data[0];
            double fwd = BCD5ToInt(b2, 2) / 10.0;
            b2[0] = data[3];
            b2[1] = data[2];
            double rev = BCD5ToInt(b2, 2) / 10.0;
            FwdPwr = fwd;
            RefPwr = rev;
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
            char response = 'X';
            bool GotSWR = false;
            int nwait = 0;
            while(TunerState != TunerStateEnum.TUNEDONE && nwait++ < 20)
            {
                SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Tune wait " + ++nn+"\n");
                Thread.Sleep(500);
            }
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Tuning done SWR=\n" + string.Format("{0:0.00}", SWR));
            if (SWR > 0 && SWR < 1.5) response = 'T';
            else if (SWR > 0 && SWR < 3) response = 'M';
            else response = 'F';
            return response;
            while (true && !GotSWR)
            {
                ++nn;
                if (nn > 10) return response;
                byte datum = (byte)SerialPortTuner.ReadByte();
                while (datum != 0xff && datum != 0xfe && datum != 0xfa && datum != 0xfb)
                {
                    datum = (byte)SerialPortTuner.ReadByte();
                }
                if (datum == 0xfa)
                {
                    //SetText("0xfa Awake\n");
                }
                else if (datum == 0xfb)
                {
                    //SetText("0xfb Sleep\n");
                    return 'S';
                }
                else if (datum == 0xff)
                {
                    byte[] data = new byte[5];
                    SerialPortTuner.Read(data, 0, 5);
                    CMD_GetAutoStatus(data);
                    response = 'A';
                    if (SWR > 0 && SWR <= 1.5) response = 'T';
                    else if (SWR > 0 && SWR <= 3.0) response = 'M';
                    //if (response != '?') GotSWR = true;
                    //return response;
                }
                else if (datum == 0xfe) // after we tune we wait for a 0xfe packet
                {
                    byte[] data = new byte[7];
                    SerialPortTuner.Read(data, 0, 7);
                    if (data[1] == 0x06 && data[2] == 0x00) // then it's the tuning answer
                    {
                        byte[] b1 = new byte[2];
                        b1[0] = data[4];
                        b1[1] = data[3];
                        SWR = BCD5ToInt(b1, 2) / 10.0;
                        if (SWR > 0 && SWR <= 1.5) response = 'T';
                        else if (SWR > 0 && SWR <= 3.0) response = 'M';
                        // turn amp control back on
                        byte[] cmdamp = { 0xfe, 0xfe, 0x21, 0x06, 0x01, 0x00, 0x00, 0xfd };
                        byte[] cmdbuf = new byte[8];
                        SendCmd(cmdamp, ref cmdbuf);
                        Thread.Sleep(10);
                        return response;
                    }
                }
                else
                {
                    int n = SerialPortTuner.BytesToRead;
                    byte[] data = new byte[n];
                    SerialPortTuner.Read(data, 0, n);

                    //SetText("Unknown: ");
                    //dumphex(data);
                    //SetText("\n");
                }
            }
            return '?';
        }

        private void SendCmd(byte[] cmd, ref byte[] response)
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
                SetText(DebugEnum.DEBUG_VERBOSE, MyTime() + "SendCMD:"+dumphex(cmd)+"\n");
            }
            catch (TimeoutException ex)
            {
                MessageBox.Show("Timeout sending cmd\n" + ex.StackTrace);
            }
        }

        public void CMD_SetAutoStatus(int seconds)
        {
            byte[] response = new byte[8];
            byte bsecs = (byte)(seconds << 4);
            byte[] cmdampoff = { 0xfe, 0xfe, 0x21, 0x16, 0x00, 0x02, 0x00, 0xfd };
            SendCmd(cmdampoff, ref response);
            Thread.Sleep(500);
        }

        override public void CMD_Amp(byte on)
        {
            byte[] response = new byte[8];
            byte[] cmdamp = { 0xfe, 0xfe, 0x21, 0x06, on, 0x00, 0x00, 0xfd };
            SendCmd(cmdamp, ref response);
        }

        public bool CMD_Amp()
        {
            byte[] response = new byte[8];
            byte[] cmdampoff = { 0xfe, 0xfe, 0x11, 0x06, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdampoff, ref response);
            return response[4] == 0x01;
        }

        public void CMD_Tune()
        {
            byte[] response = new byte[8];
            Tuning = true;
            byte[] tune = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(tune, ref response);
            TunerState = TunerStateEnum.TUNING;
        }

        public override void Tune()
        {
            byte[] response = new byte[8];
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Turn amp off\n");
            CMD_Amp(0);
            if (CMD_Amp())
            {
                SetText(DebugEnum.DEBUG_ERR, MyTime() + "Amp not off, returning!!\n");
                return;
            }
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Start tune1, Tuning="+Tuning+"\n");
            CMD_Tune();
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Start tune2, Tuning=" + Tuning + "\n");
            int n = 0;
            while (Tuning && n++ < 100)
            {
                SetText(DebugEnum.DEBUG_TRACE, MyTime() + "wait for Tuning " + n + "/100\n");
                Application.DoEvents();
                Thread.Sleep(300);
            }
            Thread.Sleep(500);
            SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Tuned " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", SWR));
            //SetText(DebugEnum.DEBUG_TRACE, MyTime() + "Turn amp on\n");
            //CMD_Amp(1);
            return;
        }

        private string dumphex(byte[] data)
        {
            return(BitConverter.ToString(data));
        }
    }
}
