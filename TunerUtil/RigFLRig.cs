// Version 20240412a
using System;
using System.CodeDom;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Security.Permissions;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
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
        private long transactionNumber = 0;
        public double frequencyA = 0;
        public double frequencyB = 0;
        public string modeA = "CW";
        public string modeAKeep = "CW";
        public string modeB = "CW";
        public string modeBKeep = "CW";
        public bool ptt;
        public int power;
        public bool transceive;
        public double swr=0;
        //private volatile object rigLock = new object();
        private Semaphore semaphore = new Semaphore(1,1);
        private volatile bool locked = false;
        int lastLineLock = 0;
        int lastLineUnLock = 0;
        int maxPower = 20;

        private int LineNumber(int stack = 2)
        {
            StackFrame CallStack = new StackFrame(stack, true);
            return CallStack.GetFileLineNumber();
        }

        private void Lock()
        {
            bool gotIt = semaphore.WaitOne(5000); // if we have to wait 5 seconds something is wrong
            //if (!gotIt && semaphore.CurrentCount != 1)
            if (!gotIt)
            {
//                MessageBox.Show("Semaphore 5sec timeout!! remaining=" + semaphore.CurrentCount + "...last Wait was from line#" + lastLineLock);
                MessageBox.Show("Semaphore 5sec timeout!! remaining=" + "...last Wait was from " + lastLineLock);
            }
            lastLineLock = LineNumber();
        }
        private void UnLock()
        {
            /*
            if (semaphore.CurrentCount == 0)
            
                DebugAddMsg(DebugEnum.WARN, "UnLock when not locked...last lock was " + lastLineLock);
                return;
            }
            if (semaphore.CurrentCount != 0)
            {
                int i = 1;
               //MessageBox.Show("Semaphore count != 1!!");
            }
            */
            lastLineUnLock = LineNumber();
            semaphore.Release(1);
        }
        public override bool Open()
        {
            model = "Unknown";
            int port = 12345;
            if (rigStream != null) { rigStream.Close(); rigStream.Dispose(); }
            if (rigClient != null) { rigClient.Close(); rigClient.Dispose(); }

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
            //FLRigGetVFO();
            //model = FLRigGetModel();
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
            int n = 0;
            //while ((xcvr = FLRigGetXcvr()) == null && --n > 0)
            while ((xcvr = FLRigGetXcvr()) == null)
            {
                Thread.Sleep(1000);
                DebugAddMsg(DebugEnum.LOG, "Waiting for FLRig " + ++n + "\n");
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
            string xcvr = "Rig??";
            try
            {
                //if (!checkBoxRig.Checked) return null;
                if (rigClient == null || rigStream == null) { Open(); }
                if (rigClient == null || rigStream == null)
                {
                    return "Unknown error during Open";
                }
                //Lock();
                string? xml2 = FLRigXML("rig.get_xcvr", null);
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
            }
            catch (Exception)
            {
                return "Unknown";
            }
            finally
            {
                //UnLock();
            }
            return xcvr;
        }
        private string FLRigXML(string cmd, string ?value)
        {
            //Debug(DebugEnum.LOG, "FLRig cmd=" + cmd + " value=" + value +"\n");
            string xmlHeader = "POST / RPC2 HTTP / 1.1\n";
            xmlHeader += "User - Agent: XMLRPC++ 0.8\n";
            xmlHeader += "Host: 127.0.0.1:12345\n";
            xmlHeader += "Content-type: text/xml\n";
            string xmlContent = "<?xml version=\"1.0\"?>\n<?clientid=\"AmpAutoTunerUtil("+ ++transactionNumber+"\")?>\n";
            if (transactionNumber >= 999999) transactionNumber = 0;
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

        public double FLRigGetSWR()
        {
            //double swr = 9;
            Lock();
            string xml = FLRigXML("rig.get_SWR", null);
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
                UnLock();
                return 0;
            }
            data = new Byte[4096];
            rigStream.ReadTimeout = 1000;
            //int power = 0;
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
                        SWR = Double.Parse(s);
                    }
                }
                catch (Exception)
                {

                }
            }
            catch (Exception)
            {

            }
            UnLock();
            return swr;
        }
        public override void Poll()
        {
            int n = 0;
            if (myThread is null)
            {
                MessageBox.Show("myThread=null in " + System.Reflection.MethodBase.GetCurrentMethod().Name);
                return;
            }
            model = FLRigGetModel();
            while (myThread.IsAlive == true)
            {
                FLRigGetVFO();
                frequencyA = FLRigGetFrequency('A');
                frequencyB = FLRigGetFrequency('B');
                ptt = FLRigGetPTT();
                //if (ptt)
                SWR = FLRigGetSWR();
                var rigPower = GetPower();
                if (rigPower > maxPower)
                {
                    DebugAddMsg(DebugEnum.LOG, "Power limited to " + maxPower + ".  See Power tab Max");
                    SetPower(maxPower);
                }
                if (++n % 2 == 0)  // do every other one
                {
                     modeA = FLRigGetMode('A');
                    if (modeA.Length == 0)
                         DebugAddMsg(DebugEnum.ERR, "FlRigGetMode A failing length==0\n");
                     modeB = FLRigGetMode('B');
                    if (modeB.Length == 0)
                         DebugAddMsg(DebugEnum.ERR, "FlRigGetMode B failing length==0\n");
                }
                Thread.Sleep(500);
            }
            Thread.Sleep(500);
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
                Lock();
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
            UnLock();
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
                Lock();
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
            UnLock();
        }
        private char FLRigGetVFO()
        {
            bool retry = false;
            int retryCount = 0;
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigGetVFO");
                return 'A';
            }
            do
            {
                try
                {
                    Lock();
                    string xml = FLRigXML("rig.get_AB", null);
                    Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                    rigStream.Write(data, 0, data.Length);
                    Byte[] data2 = new byte[4096];
                    rigStream.ReadTimeout = 1000;
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
                    //Thread.Sleep(2000);
                    retry = false;
                    //if (rigStream != null) rigStream.Close();
                    //FLRigConnect();
                }
                finally
                {
                    UnLock();
                }
            } while (retry);
            //UnLock();

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
            //Lock();
            mode = FLRigGetMode(vfo);
            if (mode.Length == 0)
                DebugAddMsg(DebugEnum.ERR, "GetMode length==0\n");
            if (vfo == 'A') modeA = mode;
            else modeB = mode;
            //UnLock();
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
            Lock();
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
                UnLock();
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
            UnLock();
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

        public override double SWR
        {
            get
            {
                return swr;
            }
            set
            {
                this.swr = value;
            }
        }
        private double FLRigGetFrequency(char vfo)
        {  
            double frequency = vfo == 'A'? frequencyA : frequencyB;
            string xml = FLRigXML("rig.get_vfo" + vfo, null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                Lock();
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
                        Thread.Sleep(2000);
                        //UnLock();
                        return 0;
                    }
                    else
                    {
                        DebugAddMsg(DebugEnum.ERR, "FLRig unexpected error:\n" + ex.Message + "\n");
                    }
                    //UnLock();
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
                        DebugMsg.DebugAddMsg(DebugEnum.ERR, "FLRigGetFrequency Exception#1\n" + ex.Message + "\n" + responseData);
                    }
                }
                catch (Exception ex)
                {
                    DebugMsg.DebugAddMsg(DebugEnum.ERR, "FLRigGetFrequency Exception#2\n" + ex.Message + "\n" + responseData);
                }
            }
            finally
            {
                UnLock();
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
                Lock();
                var myparam = "<params><param><value><double>" + frequency + "</double></value></param></params";
                string xml = FLRigXML("rig.set_vfo" + vfo, myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetFrequency response != 200 OK\n" + responseData + "\n");
                }
                if (vfo == 'A') frequencyA = frequency;
                else frequencyB = frequency;
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetVFO error: " + ex.Message + "\n" + ex.StackTrace);
                //Thread.Sleep(2000);
            }
            finally
            {
                UnLock();
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
        public override int SetPower(int pow)
        {
            if (rigStream is null)
            {
                MessageBox.Show("rigstream is null in FLRigSetPower");
                return 0;
            }
            try
            {
                Lock();
                var myparam = "<params><param><value><i4>" + pow + "</i4></value></param></params";
                string xml = FLRigXML("rig.set_power", myparam);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                rigStream.Write(data, 0, data.Length);
                Byte[] data2 = new byte[4096];
                Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                string responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                if (!responseData.Contains("200 OK"))
                {
                    DebugAddMsg(DebugEnum.ERR, "FLRigSetPower response != 200 OK\n" + responseData + "\n");
                }
                power = pow;
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "SetPower error: " + ex.Message + "\n" + ex.StackTrace);
                Thread.Sleep(2000);
            }
            UnLock();
            return power;
        }
        public override int Power
        {
            get
            {
                return power;
            }
            set
            {
                SetPower(value);
                power = value;
            }
        }

        private string ?FLRigGetModel()
        {
            string model = "?";
            try
            {
                Lock();
                string xml = FLRigXML("rig.get_info", null);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                try
                {
                    if (rigStream == null)
                    {
                        //UnLock();
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
                    //UnLock();
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
            }
            finally
            {
                UnLock();
            }
            return model;
        }
        private string FLRigGetMode(char vfo)
        {
            string mode = "?";
            try
            {
                Lock();
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
                    //UnLock();
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
                    if (mode != modeAKeep)
                    {
                        DebugAddMsg(DebugEnum.VERBOSE, "FLRigGetMode(" + vfo + ") modeA =" + modeA);
                        modeAKeep = mode;
                    }
                }
                else
                {

                    if (mode != modeBKeep)
                    {
                        DebugAddMsg(DebugEnum.VERBOSE, "FLRigGetMode(" + vfo + ") modeB =" + modeB);
                        modeBKeep = mode;
                    }
                }
            }
            finally
            {
                UnLock();
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
            Lock();
            try
            {
                //if (vfo == 'A' & mode == modeA) { UnLock(); return; }
                //if (vfo == 'B' && mode == modeB) { UnLock(); return; }
                DebugAddMsg(DebugEnum.VERBOSE, "FLRigSetVFO " + vfo + " to " + mode + "\n");
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
            UnLock();

        }
        public override string ModeA 
        {
            get { return modeA; }
            set 
            {
                FLRigSetMode('A', value);
                modeA = FLRigGetMode('A');
                if (modeA.Length == 0)
                        DebugAddMsg(DebugEnum.ERR, "FlRigGetMode A failed length==0\n");
            } 
        }
        public override string ModeB 
        { 
            get { return modeB; }
            set 
            {
                FLRigSetMode('B', value);
                modeB = FLRigGetMode('B');
                if (modeB.Length == 0)
                    DebugAddMsg(DebugEnum.ERR, "FlRigGetMode B failed length==0\n");
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

        public override int GetPower()
        {
            int power = 0;
            try
            {
                Lock();
                string xml = FLRigXML("rig.get_power", null);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                try
                {
                    if (rigStream == null)
                    {
                        //UnLock();  taken care of in finally
                        return 0;
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
                    //UnLock();  taken care of in finally.
                    return 0;
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
                            int offset1 = responseData.IndexOf("<i4>", StringComparison.InvariantCulture) + "<i4>".Length;
                            int offset2 = responseData.IndexOf("</i4>", StringComparison.InvariantCulture);
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
            }
            finally
            {
                UnLock();
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
                Lock();
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
            finally
            {
                UnLock();
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
            // GC.SuppressFinalize(rigLock);
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
                Lock();
                int pttFlag = 0;
                if (ptt == true) pttFlag = 1;
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
            finally
            {
                UnLock();
            }
        }
        public override void SetPTT(bool ptt)
        {
            FLRigSetPTT(ptt);
            this.ptt = ptt;
        }

        private bool FLRigGetPTT()
        {
            try
            {
                Lock();
                if (rigStream == null) return false;
                var xml = FLRigXML("rig.get_ptt", null);
                Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
                try
                {
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
                    //UnLock();
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
            }
            finally
            {
                UnLock();
            }
            return ptt;
        }
        public override bool GetPTT()
        {
            return ptt;
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
            /*
            if (!model.Equals("IC-7300")) return;
            try
            {
                //bool transceiveFlag = false;
                //if (transceiveFlag == true) transceiveFlag = true;
                Lock();
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
            finally
            {
                UnLock();
            }
            */

        }
        #endregion
    }
}
