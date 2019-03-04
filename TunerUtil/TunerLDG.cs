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
    class TunerLDG
    {
        private SerialPort SerialPortTuner = null;

        public TunerLDG(string model, string comport, string baud)
        {
            if (model.Equals("LDG"))
            {
                baud = "38400";
            }
            else if (model.Equals("MFJ-928"))
            {
                baud = "4800";
            }
            if (comport.Length==0 || baud.Length==0)
            {
                return;
            }
            //try
            //{
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
            //}
            //catch (Exception ex)
            //{
            //    MessageBox.Show(ex.Message);
            //}
        }

        public void Close()
        {
            if (SerialPortTuner != null)
            {
                SerialPortTuner.Close();
                SerialPortTuner.Dispose();
                SerialPortTuner = null;
            }
        }

        public string GetSerialPortTuner()
        {
            if (SerialPortTuner == null)
            {
                return (string)null;
            }
            return SerialPortTuner.PortName;
        }

        public char Tune()
        {
            // LDG Reponse to T command
            // T < 1.5
            // M 1.5-3.1
            // F Failed
            char response = 'X';
            byte[] buf = new byte[19];
            //int n = 0;
            try
            {
                // Need leading space to wake up the tuner
                while(SerialPortTuner.BytesToRead > 0)
                {
                    SerialPortTuner.ReadChar();
                }
                SerialPortTuner.Write("  ");
                // Documentation doesn't mention you need to wait a bit after the wakeup char
                Thread.Sleep(50);
                SerialPortTuner.Write("T");
                Thread.Sleep(100);
                //n = SerialPortTuner.Read(buf, 0, buf.Length);
                response = (char)SerialPortTuner.ReadChar();
                Thread.Sleep(200);
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
                response = '?';
            }
            return response;
        }
    }
}
