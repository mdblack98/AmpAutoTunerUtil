using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO.Ports;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace AmpAutoTunerUtility
{
    class TunerLDG : Tuner
    {
        private SerialPort SerialPortTuner = null;
        char response = 'X';

        public TunerLDG(string model, string comport, string baud)
        {
            this.comport = comport;
            this.model = model;
#pragma warning disable CA1307 // Specify StringComparison
            if (model.Equals("LDG"))
#pragma warning restore CA1307 // Specify StringComparison
            {
                baud = "38400";
            }
#pragma warning disable CA1307 // Specify StringComparison
            else if (model.Equals("MFJ-928"))
#pragma warning restore CA1307 // Specify StringComparison
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
                    BaudRate = Int32.Parse(baud, CultureInfo.InvariantCulture),
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
        public override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SerialPortTuner.Dispose();
            }
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

        public override char ReadResponse()
        {
            return response;
        }

        public override string GetPower()
        {
            // Can't get power from the LDG tuner
            // So will have to use the rig power level instead elsewhere
            return null;
        }
        public override void Tune()
        {
            // LDG Reponse to T command
            // T < 1.5
            // M 1.5-3.1
            // F Failed
            //byte[] buf = new byte[19];
            try
            {
                // Need leading space to wake up the tuner
                while(SerialPortTuner.BytesToRead > 0)
                {
                    SerialPortTuner.ReadChar();
                }
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                SerialPortTuner.Write("  ");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                              // Documentation doesn't mention you need to wait a bit after the wakeup char
                Thread.Sleep(50);
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                SerialPortTuner.Write("T");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
                Thread.Sleep(100);
                response = (char)SerialPortTuner.ReadChar();
                Thread.Sleep(200);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                MessageBox.Show("Tuner Util:" + ex.Message);
                response = '?';
            }
        }

        public override sealed void Dispose()
        {
        }
    }
}
