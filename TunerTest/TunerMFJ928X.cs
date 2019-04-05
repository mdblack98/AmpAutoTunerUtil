using AmpAutoTunerUtility;

using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TunerTest
{
    public class TunerMFJ928X : Tuner
    {
        //enum TunerStateEnum { UNKNOWN, SLEEP, AWAKE, TUNING, TUNEDONE, ERR}
        //TunerStateEnum TunerState = TunerStateEnum.UNKNOWN;
        private SerialPort SerialPortTuner = null;
        public double FwdPwr { get; set; }
        public double RefPwr { get; set; }
        public double Swr { get; set; }
        
        bool Tuning = false;

        public TunerMFJ928X(string model, string comport, string baud)
        {
            this.model = model;
            this.comport = comport;
            this.baud = baud;

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
                SetText("Error opening Tuner...\n" + ex.Message);
                return;
            }
            //SetText("Tuner opened on " + comport + "\n");
            //SerialPortTuner.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);


            byte[] buffer = new byte[4096];
            Action kickoffRead = null;
            SerialPortTuner.BaseStream.ReadTimeout = 100;
            kickoffRead = delegate
            {
                SerialPortTuner.BaseStream.BeginRead(buffer, 0, buffer.Length, delegate (IAsyncResult ar)
                {
                    try
                    {
                        int actualLength = SerialPortTuner.BaseStream.EndRead(ar);
                        byte[] received = new byte[actualLength];
                        Buffer.BlockCopy(buffer, 0, received, 0, actualLength);
                        raiseAppSerialDataEvent(received);
                    }
                    catch (InvalidOperationException ex)
                    {
                        handleAppSerialError(ex);
                        return;
                    }
                    kickoffRead();
                }, null);
            };
            kickoffRead();

        }

        //public string  GetText()
        //{
        //    string s = msg;
        //    msg = "";
        //    return s;
        //}

        public void SetText(string v)
        {
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
            SetText(MyTime() + "Auto Status: "+dumphex(data)+"\n");
            CMD_GetAutoStatus(data);
            SetText(MyTime() + "Auto Status: " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", Swr) + "\n");

        }

        private void MsgFE(byte[] received)
        {
            byte[] data = new byte[7];
            int i = 0;
            for (; i < received.Length - 1 && i < 7; ++i)
            {
                data[i] = received[i + 1];
            }
            for (; i < 7; ++i)
            {
                data[i] = (byte)SerialPortTuner.ReadByte();
            }
            //Thread.Sleep(200);
            byte datum = data[1];
            switch (datum)
            {
                // FE-06-01-80-00-79-FD -- tune failure
                case 0x06:
                    Tuning = false;
                    switch (data[3])
                    {
                        case 0x00:
                            SetText(MyTime() + "Started by Tune cmd\n");
                            break;
                        case 0x01:
                            SetText(MyTime() + "Started by > SWR");
                            break;
                        case 0x02:
                            SetText(MyTime() + "Started by StickyTune");
                            break;
                        case 0x80:
                            SetText(MyTime() + "Increase Power\n");
                            break;
                        case 0x81:
                            SetText(MyTime() + "Decrease Power\n");
                            break;
                        case 0x82:
                            SetText(MyTime() + "Overload\n");
                            break;
                        default:

                            break;
                    }
                    break;
                case 0x21:
                    switch (data[2])
                    {
                        case 0x06:
                            string status = "off\n";
                            if (data[3] == 0x01) status = "on\n";
                            SetText(MyTime() + "Amp " + status);
                            break;
                    }
                    break;
                default:
                    SetText(MyTime() + "CMD unknown=" + dumphex(data)+"\n");
                    break;
            {

            }
            }
            SetText(MyTime() + "CMD packet: " +dumphex(data)+"\n");
        }

        private void raiseAppSerialDataEvent(byte[] received)
        {
            //SetText(MyTime() + " "+dumphex(received)+"\n");
            bool abort = false;
            for(int i=0;i<received.Length;++i)
            {
                byte datum = received[i];
                switch(datum)
                {
                    case 0xfa: SetText(MyTime()+"Awake\n");break;
                    case 0xfb: SetText(MyTime() + "Sleeping\n"); break;
                    case 0xfe: MsgFE(received);i += 5; break;
                    case 0xff: MsgFF(received); i += 5; break;
                    default: SetText(datum.ToString("X")+" - Unknown\n");abort = true; break;
                }
                if (abort) break;
            }
        }

        private void handleAppSerialError(Exception ex)
        {
            SetText(MyTime() + "Serial err: " + ex.Message+"\n"+ex.StackTrace);
        }

        public override void Close()
        {
            if (SerialPortTuner != null)
            {
                SerialPortTuner.Close();
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
                Swr = (fwd + rev) / fwd;
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
                    if (Swr > 0 && Swr <= 1.5) response = 'T';
                    else if (Swr > 0 && Swr <= 3.0) response = 'M';
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
                        Swr = BCD5ToInt(b1, 2) / 10.0;
                        if (Swr > 0 && Swr <= 1.5) response = 'T';
                        else if (Swr > 0 && Swr <= 3.0) response = 'M';
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
            try
            {
                SerialPortTuner.ReadTimeout = 2000;  // do we need longer for tuning?
                byte[] wakeup = { 0x00 };
                SerialPortTuner.Write(wakeup, 0, wakeup.Length);
                Thread.Sleep(3); // Manual says sleep 3ms before sending cmd
                //Flush();
                byte checksum = (byte)((1024 - cmd[2] - cmd[3] - cmd[4] - cmd[5]) & 0xff);
                cmd[6] = checksum;
                SerialPortTuner.Write(cmd, 0, cmd.Length);
                SetText(MyTime() + "SendCMD:"+dumphex(cmd)+"\n");
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
            byte[] cmdampoff = { 0xfe, 0xfe, 0x21, 0x16, 0x00, bsecs, 0x00, 0xfd };
            SendCmd(cmdampoff, ref response);
        }

        override public void CMD_Amp(byte on)
        {
            byte[] response = new byte[8];
            byte[] cmdampoff = { 0xfe, 0xfe, 0x21, 0x06, on, 0x00, 0x00, 0xfd };
            SendCmd(cmdampoff, ref response);
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
        }

        public override void Tune()
        {
            SetText(MyTime()+"Set autostatus time to 2 seconds\n");
            CMD_SetAutoStatus(2);
            byte[] response = new byte[8];
            SetText(MyTime() + "Turn amp off\n");
            CMD_Amp(0);
            SetText(MyTime() + "If amp not off return\n");
            if (CMD_Amp()) return;
            SetText(MyTime() + "Start tune1, Tuning="+Tuning+"\n");
            CMD_Tune();
            SetText(MyTime() + "Start tune2, Tuning=" + Tuning + "\n");
            while (Tuning)
            {
                SetText(MyTime() + "wait for Tuning\n");
                Thread.Sleep(1000);
            }
            SetText(MyTime() + "Tuned " + FwdPwr + "/" + RefPwr + "/" + string.Format("{0:0.00}", Swr));
            SetText(MyTime() + "Turn amp on\n");
            CMD_Amp(1);
            //byte[] cmdampoff = { 0xfe, 0xfe, 0x21, 0x06, 0x00, 0x00, 0x00, 0xfd };
            //SendCmd(cmdampoff, ref response);
            return;
            byte[] cmdampquery = { 0xfe, 0xfe, 0x11, 0x06, 0x00, 0x00, 0x00, 0xfd };
            SendCmd(cmdampquery, ref response);
            return;
            if (response[4] != 0x00)
            {
                MessageBox.Show("Tuner did not bypass amp");
            }

            byte[] semimode = { 0xfe, 0xfe, 0x21, 0x14, 0x01, 0x00, 0x00, 0xfd };
            //SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            //SerialPortTuner.Read(response, 0, 1);
            SendCmd(semimode, ref response);
            // Start tuning
            byte[] tune = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SendCmd(cmdampquery, ref response);
            if (response[4] != 0x01)
            {
                MessageBox.Show("Tuner did not enable amp");
            }
            //SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            //SerialPortTuner.Read(response, 0, 1);

            //Flush();  // just drop any pending stuff on the floor
            SendCmd(tune, ref response);
        }

        private string dumphex(byte[] data)
        {
            return(BitConverter.ToString(data));
        }
    }
}
