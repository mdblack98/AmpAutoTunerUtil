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
        private SerialPort SerialPortTuner = null;
        public double FwdPwr { get; set; }
        public double RefPwr { get; set; }
        public double Swr { get; set; }

        public TunerMFJ928(string model, string comport, string baud)
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
            SerialPortTuner.Open();
            SerialPortTuner.DataReceived += new SerialDataReceivedEventHandler(port_DataReceived);
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

        private void LDGAutoStatus(byte[] data)
        {
            byte[] b2 = new byte[2];
            b2[0] = data[0];
            b2[1] = data[1];
            double fwd = BCD5ToInt(b2, 2) / 10.0;
            b2[0] = data[2];
            b2[1] = data[3];
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
                    LDGAutoStatus(data);
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
                    if (data[1] == 0x06 && data[2]==0x00) // then it's the tuning answer
                    {
                        byte[] b1 = new byte[2];
                        b1[0] = data[4];
                        b1[1] = data[3];
                        Swr = BCD5ToInt(b1, 2)/10.0;
                        if (Swr > 0 && Swr <= 1.5) response = 'T';
                        else if (Swr > 0 && Swr <= 3.0) response = 'M';
                        // turn amp control back on
                        byte[] cmdamp = { 0xfe, 0xfe, 0x21, 0x06, 0x01, 0x00, 0x00, 0xfd };
                        SendCmd(cmdamp);
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

        private void SendCmd(byte[] cmd)
        {
            byte checksum = (byte)((1024 - cmd[2] - cmd[3] - cmd[4] - cmd[5]) & 0xff);
            cmd[6] = checksum;
            SerialPortTuner.Write(cmd, 0, cmd.Length);
        }

        public override void Tune()
        {
            byte[] response = new byte[8];

            Flush();

            byte[] wakeup = { 0x00 };
            SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            SerialPortTuner.Read(response, 0, 1);
            //Thread.Sleep(3); // manual says 3ms

            byte[] cmdampoff = { 0xfe, 0xfe, 0x21, 0x06, 0x00, 0x00, 0x00, 0xfd };
            //SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            //SerialPortTuner.Read(response, 0, 1);
            SendCmd(cmdampoff);
            SerialPortTuner.Read(response, 0, response.Length);

            byte[] semimode = { 0xfe, 0xfe, 0x21, 0x14, 0x01, 0x00, 0x00, 0xfd };
            //SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            //SerialPortTuner.Read(response, 0, 1);
            SendCmd(semimode);
            SerialPortTuner.Read(response, 0, response.Length);

            byte[] tune = { 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            SerialPortTuner.Read(response, 0, response.Length);
            //SerialPortTuner.Write(wakeup, 0, wakeup.Length);
            //SerialPortTuner.Read(response, 0, 1);

            //Flush();  // just drop any pending stuff on the floor
            SendCmd(tune);
        }

        private void port_DataReceived(object sender, SerialDataReceivedEventArgs e)
        {
            //port.ReadTimeout = 100;
            Thread.Sleep(100);
            byte[] data = new byte[SerialPortTuner.BytesToRead];
            SerialPortTuner.Read(data, 0, SerialPortTuner.BytesToRead);
            dumphex(data);
            //SetTextRig("\n");
        }

        private void dumphex(byte[] data)
        {
            //SetTextRig(BitConverter.ToString(data));
        }
    }
}
