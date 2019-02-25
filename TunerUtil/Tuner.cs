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
    class Tuner
    {
        private SerialPort SerialPortTuner = null;
        public Tuner(string model, string comport, string baud)
        {
            if (comport.Length==0 || baud.Length==0)
            {
                return;
            }
            try
            {
                SerialPortTuner = new SerialPort
                {
                    PortName = comport,
                    BaudRate = Int32.Parse(baud),
                    Parity = Parity.None,
                    DataBits = 8,
                    StopBits = StopBits.One,
                    Handshake = Handshake.None,
                    ReadTimeout = 500,
                    WriteTimeout = 500
                };
                SerialPortTuner.Open();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message);
            }
        }

        public char Tune()
        {
            // LDG Reponse to T command
            // T < 1.5
            // M 1.5-3.1
            // F Failed
            char response = '?';
            try
            {
                // Need leading space to wake up the tuner
                SerialPortTuner.Write(" T");
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
