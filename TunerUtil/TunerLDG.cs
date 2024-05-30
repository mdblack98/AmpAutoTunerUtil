using System;
using System.Globalization;
using System.IO.Ports;
using System.Threading;
using System.Windows;
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
            if (!baud.Equals("38400"))
                baud = "38400"; // baud rate is fixed
            if (comport.Length == 0 || baud.Length == 0)
            {
                //MessageBox.Show("com port("+comport+") or baud("+baud+") is empty");
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
        //public override void Dispose(bool disposing)
        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                SerialPortTuner?.Dispose();
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
                return "Unknown";
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
            return "0";
        }
        public override void Tune()
        {
            // LDG Reponse to T command
            // T < 1.5
            // M 1.5-3.1
            // F Failed
            //byte[] buf = new byte[19];
            if (SerialPortTuner is null)
            {
                MessageBox.Show("SerialPortTuner=null in" + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }

            SerialPortTuner.ReadTimeout = 30000;
            try
            {
                // Need leading space to wake up the tuner
                while (SerialPortTuner.BytesToRead > 0)
                {
                    SerialPortTuner.ReadChar();
                }

                SerialPortTuner.Write("  ");
                // Documentation doesn't mention you need to wait a bit after the wakeup char
                Thread.Sleep(50);
                if (TuneFull)
                {
                    SerialPortTuner.Write("F");
                }
                else
                {
                    SerialPortTuner.Write("T");
                }

                Thread.Sleep(100);
                int loop = 3;
                char c;
                do
                {
                    --loop;
                    c = (char)SerialPortTuner.ReadChar();
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "LDG Response=0x" + String.Format("#{0:X}\n", c));
                } while (loop > 0 && c != 'T' && c != 'F' && c != 'M');
                if (loop < 0) DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "LDG timeout\n");
                response = c;
                Thread.Sleep(250);  // Manual say no commands within 200ms of last command
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tuner Util:" + ex.Message);
                response = '?';
            }
        }

        //public override int GetAntenna()
        //{
        /*
        SerialPortTuner.Write("A");
        Thread.Sleep(100);
        response = (char)SerialPortTuner.ReadChar();
        AntennaNumber = Convert.ToInt32(response);
        return AntennaNumber;
        */
        //}
        public override void SetAntenna(int antennaNumberRequested, bool tuneIsRunning=false)
        {
            if (SerialPortTuner == null) return;
            try
            {
                if (antennaNumberRequested == AntennaNumber) return;
                SerialPortTuner.Write(" ");
                Thread.Sleep(50);
                SerialPortTuner.Write("A");
                //Thread.Sleep(1000);
                SerialPortTuner.ReadTimeout = 1000;
                response = (char)SerialPortTuner.ReadChar();
                if (response == 0)
                {
                    MessageBox.Show("No response to antenna query");
                    return;
                }
                switch (response)
                {
                    case '1': AntennaNumber = 1; break;
                    case '2': AntennaNumber = 2; break;
                    default: MessageBox.Show("Unknown antenna=" + response); break;
                }
                //MessageBox.Show("Query#1 response=" + response + " AntennaNumber/antennaNumberRequested=" + AntennaNumber + "/" + antennaNumberRequested);
                if (antennaNumberRequested != AntennaNumber)
                {
                    SerialPortTuner.Write(" ");
                    Thread.Sleep(50);
                    SerialPortTuner.Write("A" + antennaNumberRequested);
                    response = (char)SerialPortTuner.ReadChar();
                    if (response == 0)
                    {
                        MessageBox.Show("No response to antenna query#2");
                        return;
                    }
                    switch (response)
                    {
                        case '1': AntennaNumber = 1; break;
                        case '2': AntennaNumber = 2; break;
                        default: MessageBox.Show("Unknown antenna=" + response); break;
                    }
                    //MessageBox.Show("Query#2 response=" + response + " AntennaNumber/antennaNumberRequested=" + AntennaNumber + "/" + antennaNumberRequested);
                }
            }
            catch (Exception ex)
            {
                MessageBox.Show("Tuner error: " + ex.Message + "\n" + ex.Source + "\n" + ex.StackTrace);
            }
        }
    }
}
