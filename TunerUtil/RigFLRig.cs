// Version 20240412a
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using System.Xml.Linq;
using static AmpAutoTunerUtility.DebugMsg;


namespace AmpAutoTunerUtility
{
    class RigFLRig : Rig, IDisposable
    {
        readonly string rig = "FLRig";
        TcpClient ?rigClient;
        NetworkStream ?rigStream;
        public string errorMessage = "None";
        private Thread ?myThread;
        private string ?model;
        private char vfo = '?';
        public double frequencyA = 0;
        public double frequencyB = 0;
        public string modeA = "CW";
        public string modeAKeep = "CW";
        public string modeB = "CW";
        public string modeBKeep = "CW";
        public bool ptt;
        public int power;
        public bool transceive;
        public override bool Open()
        {
            model = "Unknown";
            int port = 12345;
            try
            {
                rigClient = new TcpClient("127.0.0.1", port);
                rigStream = rigClient.GetStream();
                rigStream.ReadTimeout = 500;
                if (FLRigWait() == false)
                {
                    return false;
                }
                //else
                //{
                //    richTextBoxRig.AppendText("FLRig connected\n");
                //}
            }
            catch (Exception ex)
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    errorMessage = "FLRig not responding...";
                    //richTextBoxRig.AppendText("FLRig not responding...\n");
                }
                else
                {
                    errorMessage = "FLRig unexpected error:\n" + ex.Message;
                    //richTextBoxRig.AppendText("FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return false;
            }
            // Prime a few things
            FLRigGetVFO();
            model = FLRigGetModel();
            myThread = new Thread(new ThreadStart(Poll))
            {
                IsBackground = true
            };
            myThread.Start();
            return true;
        }

        public override void Close()
        {
            rigStream?.Close();
            rigClient?.Close();
        }
        private bool FLRigWait()
        {
            String ?xcvr;
            int n = 16;
            while ((xcvr = FLRigGetXcvr()) == null && --n > 0)
            {
                Thread.Sleep(1000);
                //DebugAddMsg(DebugEnum.LOG, "Waiting for FLRig " + n + "\n");
                Application.DoEvents();
            }
            if (xcvr == null)
            {
                //DebugAddMsg(DebugEnum.ERR, "No transceiver?  Aborting FLRigWait\n");
                return false;
            }
            //if (formClosing == true)
            //{
            //    return false;
            //}
            DebugAddMsg(DebugEnum.LOG, "Rig is " + xcvr + "\n");
            return true;
        }
        private string ?FLRigGetXcvr()
        {
            try
            {
                Monitor.Enter(12345);
            }
            catch(Exception)
            {
                return "Unknown";
            }
            string ?xcvr = null;
            //if (!checkBoxRig.Checked) return null;
            if (rigClient == null || rigStream == null) { Open(); }
            if (rigClient == null || rigStream == null)
            {
                return "Unknown error during Open"; 
            }
            string ?xml2 = FLRigXML("rig.get_xcvr", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml2);
            try
            {
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "FLRigGetXcvr error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //tabPage.SelectedTab = tabPageDebug;
                return null;
            }
            data = new Byte[4096];
            int timeoutSave = rigStream.ReadTimeout;
            rigStream.ReadTimeout = 2000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        xcvr = responseData.Substring(offset1, offset2 - offset1);
                        if (xcvr.Length == 0) xcvr = null;
                    }
                    else
                    {
                        //labelFreq.Text = "?";
                        DebugAddMsg(DebugEnum.ERR, responseData + "\n");
                        //tabPage.SelectedTab = tabPageDebug;
                    }
                }
                catch (Exception)
                {
                    DebugAddMsg(DebugEnum.ERR, "Error parsing freq from answer:\n" + responseData + "\n");
                    frequencyA = frequencyB = 0;
                    ModeA = ModeB = "?";
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "Rig not responding\n" + ex.Message + "\n");
                frequencyA = frequencyB = 0;
                ModeA = ModeB = "?";
            }
            rigStream.ReadTimeout = timeoutSave;
            return xcvr;
        }
        private string FLRigXML(string cmd, string ?value)
        {
            //Debug(DebugEnum.LOG, "FLRig cmd=" + cmd + " value=" + value +"\n");
            string xmlHeader = "POST / RPC2 HTTP / 1.1\n";
            xmlHeader += "User - Agent: XMLRPC++ 0.8\n";
            xmlHeader += "Host: 127.0.0.1:12345\n";
            xmlHeader += "Content-type: text/xml\n";
            string xmlContent = "<?xml version=\"1.0\"?>\n<?clientid=\"AmpAutoTunerUtil\"?>\n";
            xmlContent += "<methodCall><methodName>";
            xmlContent += cmd;
            xmlContent += "</methodName>\n";
            if (value != null && value.Length > 0)
            {
                xmlContent += value;
            }
            xmlContent += "</methodCall>\n";
            xmlHeader += "Content-length: " + xmlContent.Length + "\n\n";
            string xml = xmlHeader + xmlContent;
            return xml;
        }

        public override void Poll()
        {
            int n = 0;
            if (myThread is null)
            {
                MessageBox.Show("myThread=null in " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }
            while (myThread.IsAlive == true)
            {
                FLRigGetVFO();
                frequencyA = FLRigGetFrequency('A');
                frequencyB = FLRigGetFrequency('B');
                ptt = FLRigGetPTT();
                power = FLRigGetPower();
                if (++n % 2 == 0)  // do every other one
                {
                    do
                    {
                        modeA = FLRigGetMode('A');
                        if (modeA.Length == 0)
                            DebugAddMsg(DebugEnum.ERR, "FlRigGetMode A length==0\n");
                    } while (modeA.Length == 0);
                    do
                    { 
                        modeB = FLRigGetMode('B');
                        if (modeB.Length == 0)
                            DebugAddMsg(DebugEnum.ERR, "FlRigGetMode B length==0\n");
                    } while (modeB.Length == 0);
                }
                Thread.Sleep(500);
            }
        }

        public override void SendCommand(int command)
        {
            if (command == 0) return;
            if (rigStream is null)
            {
                MessageBox.Show("Error in SendCommand rigstream is null");
                return;
            }
            try
            {
                Monitor.Enter(12345);
                var myparam = "<params><param><value><i4>" + command + "</i4></value></param></params";
                string xml = FLRigXML("rig.cmd", myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig rig.cmd response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetVFO error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        private void FLRigSetVFO(char vfo)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetVFO");
                return;
            }
            try
            {
                Monitor.Enter(12345);
                var myparam = "<params><param><value>" + vfo + "</value></param></params";
                string xml = FLRigXML("rig.set_AB", myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetVFO response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetVFO error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        private char FLRigGetVFO()
        {
            bool retry = true;
            int retryCount = 0;
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigGetVFO");
                return 'A';
            }
            Monitor.Enter(12345);
            do
            {
                try
                {
                    string xml = FLRigXML("rig.get_AB", null);
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                    rigStream.Write(data, 0, data.Length);
                    Byte[] data2 = new byte[4096];
                    rigStream.ReadTimeout = 5000;
                    Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                    string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                    if (responseData.Contains("<value>B"))
                    {
                        vfo = 'B';
                    }
                    else
                    {
                        vfo = 'A';
                    }
                    retry = false; // got here so we're good
                }
                catch (Exception)
                {
                    DebugAddMsg(DebugEnum.ERR, "GetActiveVFO error #" + ++retryCount + "\n");
                    Thread.Sleep(2000);
                    //if (rigStream != null) rigStream.Close();
                    //FLRigConnect();
                }
            } while (retry);
            return vfo;
        }
        public override char VFO
        {
            get
            {
                return vfo;
            }
            set
            {
                FLRigSetVFO(value);
                vfo = value;
            }
        }
        public override double GetFrequency(char vfo)
        {
            if (vfo == 'A')
                return frequencyA;
            else
                return frequencyB;
            // do we need VFOC ever for FLRig?
        }

        //public override void SetFrequency(double frequency)
        //{
        //    FLRigSetFrequency(this.VFO, frequency);
        //}
        public override string GetMode(char vfo)
        {
            string mode;
            do
            {
                mode = FLRigGetMode(vfo);
                if (mode.Length == 0)
                    DebugAddMsg(DebugEnum.ERR, "GetMode length==0\n");
            } while(mode.Length == 0);
            if (vfo == 'A') modeA = mode;
            else modeB = mode;
            return mode;
        }

        public override string GetRig()
        {
            return rig;
        }

        // Return a list of all available modes
        public override List<string> ?GetModes()
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in GetModes");
                return null;
            }
            Monitor.Enter(12345);
            rigStream.Flush();
            var xml = FLRigXML("rig.get_modes", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return null;
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return null;
            }
            data = new Byte[4096];
            List<string> modes = new List<string>();
            rigStream.ReadTimeout = 5000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    char[] sep = { '<', '>' };
                    if (responseData.Contains("<value>")) // then we have some info
                    {
                        string[] tokens = responseData.Split(sep);
                        for(int i=0; i<tokens.Length; i++)
                        {
                            if (tokens[i].Equals("value"))
                            {
                                if (tokens[i+1].Length > 0) modes.Add(tokens[i+1]);
                            }
                        }
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            return modes;
        }

        public override void SetMode(char vfo, string mode)
        {
            /*
            string myparam = "<params><param><value>" + mode + "</value></param></params>";
            var xml = FLRigXML("rig.set_modeA", myparam);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                Debug(DebugEnum.ERR, "FLRig set_modeA got an error??\n");
                return;
            }
            */
        }

        // FLRig unique functions below here

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        public override string ?Model
        {
            get
            {
                return this.model;
            }
        }

        private double FLRigGetFrequency(char vfo)
        {
            Monitor.Enter(12345);
            double frequency = vfo == 'A'? frequencyA : frequencyB;
            string xml = FLRigXML("rig.get_vfo" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return 0.0;
                rigStream.Flush();
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return 0.0;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 5000;
            String responseData = "no response";
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        string freqString = responseData.Substring(offset1, offset2 - offset1);
                        frequency = Double.Parse(freqString, CultureInfo.InvariantCulture);
                        if (frequency < 1000) DebugMsg.DebugAddMsg(DebugEnum.ERR, "Frequency==0\n" + responseData);
                        //DebugMsg.DebugAddMsg(DebugEnum.ERR, "Frequency==0\n" + responseData);
                    }
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugEnum.ERR, "FLRigGetFrequency Exception#1\n"+ex.Message+"\n"+responseData);
                }
            }
            catch (Exception ex)
            {
                DebugMsg.DebugAddMsg(DebugEnum.ERR, "FLRigGetFrequency Exception#2\n"+ex.Message+"\n" + responseData);
            }
            return frequency;
        }

        private void FLRigSetFrequency(char vfo, double frequency)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetFrequency");
                return;
            }
            try
            {
                Monitor.Enter(12345);
                var myparam = "<params><param><value><double>" + frequency + "</double></value></param></params";
                string xml = FLRigXML("rig.set_vfo" + vfo, myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetVFOA/B response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetVFO error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        public override double FrequencyA
        {
            get
            {
                return frequencyA;
            }
            set
            {
                FLRigSetFrequency('A', value);
                frequencyA = value;
            }
        }
        public override double FrequencyB
        {
            get
            {
                return frequencyB;
            }
            set
            {
                FLRigSetFrequency('B', value);
                frequencyB = value;
            }
        }

        private  string ?FLRigGetModel()
        {
            Monitor.Enter(12345);
            string xml = FLRigXML("rig.get_info", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null)
                {
                    return "";
                }
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return "";
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 5000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        string info = responseData.Substring(offset1, offset2 - offset1);
                        string[] tokens = info.Split(new char[] { '\n' });
                        model = tokens[0].Substring(2);
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            return model;
        }
        private string FLRigGetMode(char vfo)
        {
            Monitor.Enter(12345);
            string xml = FLRigXML("rig.get_mode" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return "";
                rigStream.Flush();
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return "";
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 5000;
            string mode = "?";
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        mode = responseData.Substring(offset1, offset2 - offset1);
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            if (vfo == 'A')
            {
                //modeA = mode;
                if (modeA != modeAKeep)
                {
                    DebugAddMsg(DebugEnum.VERBOSE, "FLRigGetMode(" + vfo + ") modeA =" + modeA);
                    modeAKeep = modeA;
                }
            }
            else
            {

                //modeB = mode;
                if (modeB != modeBKeep) {
                    DebugAddMsg(DebugEnum.VERBOSE, "FLRigGetMode(" + vfo + ") modeB =" + modeB);
                    modeBKeep = modeB;
                }
            }
            return mode;
        }

    

        private void FLRigSetMode(char vfo, string mode)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetMode");
                return;
            }
            Monitor.Enter(12345);
            try
            {
                if (vfo == 'A' & mode == modeA) return;
                if (vfo == 'B' && mode == modeB) return;
                var myparam = "<params><param><value>" + mode + "</value></param></params";
                string xml = FLRigXML("rig.set_mode" + vfo, myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetVFO response != 200 OK\n" + responseData + "\n");
                }
                else
                {
                    if (vfo == 'A') modeA = mode;
                    else modeB = mode;
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetVFO error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        public override string ModeA 
        {
            get { return modeA; }
            set 
            {
                FLRigSetMode('A', value);
                do
                {
                    modeA = FLRigGetMode('A');
                    if (modeA.Length == 0)
                        DebugAddMsg(DebugEnum.ERR, "FlRigGetMode A length==0\n");
                } while (modeA.Length == 0);
            } 
        }
        public override string ModeB 
        { 
            get { return modeB; }
            set 
            {
                FLRigSetMode('B', value);
                do
                {
                    modeB = FLRigGetMode('B');
                    if (modeB.Length == 0)
                        DebugAddMsg(DebugEnum.ERR, "FlRigGetMode B length==0\n");
                } while (modeB.Length == 0);
            }
        }
        public override bool PTT
        {
            get
            {
                return ptt;
            }
            set
            {
                FLRigSetPTT(value);
                ptt = value;
            }
        }

        private int FLRigGetPower()
        {
            Monitor.Enter(12345);
            string xml = FLRigXML("rig.get_power", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return 0;
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return 0;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 5000;
            int power = 0;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        var s = responseData.Substring(offset1, offset2 - offset1);
                        power = int.Parse(s);
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            return power;
        }
        void FLRigSetPower(int value)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetPower");
                return;
            }
            try
            {
                Monitor.Enter(12345);
                var myparam = "<params><param><value><i4>" + value + "</i4></value></param></params";
                string xml = FLRigXML("rig.set_power", myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetPTT response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetPTT error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        public override int Power
        {
            get { return power;  }
            set 
            {
                FLRigSetPower(value);
            }
        }

        public override bool Transceive 
        { 
            get { return transceive; }
            set {
                SetTransceive(value);
                transceive = value;
            } 
        }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    rigStream!.Close();
                    rigStream!.Dispose();
                    rigClient!.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~RigFLRig()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }

        public override void SetFrequency(double frequency)
        {
            SetFrequency(this.VFO, frequency);
        }

        public override void SetFrequency(char vfo, double frequency)
        {
            if (vfo == 'A' || vfo == 'B')
            {
                FLRigSetFrequency(vfo, frequency);
            }
            else
            { 
                FLRigSetFrequency(this.VFO, frequency);
            }
        }

        private void FLRigSetPTT(bool ptt)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetPTT");
                return;
            }
            try
            {
                int pttFlag = 0;
                if (ptt == true) pttFlag = 1;
                Monitor.Enter(12345);
                var myparam = "<params><param><value><i4>" + pttFlag + "</i4></value></param></params";
                string xml = FLRigXML("rig.set_ptt", myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetPTT response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetPTT error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        public override void SetPTT(bool ptt)
        {
            FLRigSetPTT(ptt);
            this.ptt = ptt;
        }

        private bool FLRigGetPTT()
        {
            Monitor.Enter(12345);
            var xml = FLRigXML("rig.get_ptt", null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return false;
                rigStream.Write(data, 0, data.Length);
            }
            catch (Exception ex)
            {
                if (rigStream == null || ex.Message.Contains("Unable to write"))
                {
                    DebugAddMsg(DebugEnum.ERR, "Error...Did FLRig shut down?\n");
                }
                else
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return false;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 5000;
            try
            {
                Int32 bytes = rigStream.Read(data, 0, data.Length);
                String responseData = Encoding.ASCII.GetString(data, 0, bytes);
                //richTextBoxRig.AppendText(responseData + "\n");
                try
                {
                    if (responseData.Contains("<value>")) // then we have a frequency
                    {
                        int offset1 = responseData.IndexOf("<value>", StringComparison.InvariantCulture) + "<value>".Length;
                        int offset2 = responseData.IndexOf("</value>", StringComparison.InvariantCulture);
                        string freqString = responseData.Substring(offset1, offset2 - offset1);
                        ptt = Boolean.Parse(freqString);
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            return ptt;
        }
        public override bool GetPTT()
        {
            return FLRigGetPTT();
        }

        public override string GetModel()
        {
            string ?model = "unknown";
            model = FLRigGetModel();
            if (model == null) { model = "unknown"; }
            return model;
        }
        public override void SetTransceive(bool transceive)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in SetTransceive");
                return;
            }
            // Implemented for Expert Linear amplifier
            // Using transceive to sync frequency
            // We turn if off when walking and back on when walk stops
            if (model is null)
            {
                MessageBox.Show("model=null at " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }
            if (!model.Equals("IC-7300")) return;
            try
            {
                //bool transceiveFlag = false;
                //if (transceiveFlag == true) transceiveFlag = true;
                Monitor.Enter(12345);
                string xml;
                if (transceive) 
                { 
                    xml = FLRigXML("rig.cat_string", "<params><param><value>xfe xfe x94 xe0 x1a x05 x00 x73 x01 xfd</value></param></params");
                }
                else
                {
                    xml = FLRigXML("rig.cat_string", "<params><param><value>xfe xfe x94 xe0 x1a x05 x00 x73 x00 xfd</value></param></params");
                }
                Thread.Sleep(500);

                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetTransceive response != 200 OK\n" + responseData + "\n");
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetTransceive error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
        }
        #endregion
    }
}
