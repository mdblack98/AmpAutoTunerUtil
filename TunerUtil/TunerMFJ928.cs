using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TunerUtil
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

        private void ReadResponse()
        {
            // Have to handle each response
            byte[] data = new byte[4096];
            int i = 0;
            while (SerialPortTuner.BytesToRead < 6)
            {
                Thread.Sleep(100);
            }
            while (SerialPortTuner.BytesToRead > 0)
            {
                data[i] = (byte)SerialPortTuner.ReadByte();
                if (data[i] == 0xfa) i = 0; // what does 0xff mean?
                else
                {
                    if (data[i] == 0xff)
                    {
                        byte[] b2 = new byte[2];
                        SerialPortTuner.Read(b2, 0, 2);
                        uint fwd = BCD5ToInt(b2, 2);
                        SerialPortTuner.Read(b2, 0, 2);
                        uint rev = BCD5ToInt(b2, 2);
                        SerialPortTuner.Read(b2, 0, 1); // just ignore the 0x1c at the end for now
                        FwdPwr = fwd;
                        RefPwr = rev;
                        if (fwd > 0) Swr = (fwd + rev) / fwd;
                        else Swr = 0;
                        return;
                    }
                    else
                    {
                        if (i > 0)
                        {
                            MessageBox.Show("Why are we here? bytes=" + i);
                        }
                    }
                    ++i;
                }
            }
        }

        public override char Tune()
        {
            char response = 'F';
            //byte[] data = { 0x00, 0xfe, 0xfe, 0x06, 0x01, 0x00, 0x00, 0xf9, 0xfd };
            byte[] data = { 0x06, 0x01, 0x00, 0x00, 0xf9};
            Flush();
            SerialPortTuner.Write(data, 0, data.Length);
            ReadResponse();
            if (Swr > 0 && Swr <= 1.5) response = 'T';
            else if (Swr > 0 && Swr <= 3.0) response = 'M';
            return response;
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
