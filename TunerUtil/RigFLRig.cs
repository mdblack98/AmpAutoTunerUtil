﻿using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Windows.Forms;
using static AmpAutoTunerUtility.DebugMsg;

namespace AmpAutoTunerUtility
{
    class RigFLRig : Rig, IDisposable
    {
        readonly string rig = "FLRig";
        TcpClient rigClient;
        NetworkStream rigStream;
        public string errorMessage = "None";
        private Thread myThread;
        private string model;
        private char vfo;
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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
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
            myThread = new Thread(new ThreadStart(Poll));
            myThread.IsBackground = true;
            myThread.Start();
            return true;
        }
        public override void Close()
        {
            rigStream.Close();
            rigClient.Close();
        }
        private bool FLRigWait()
        {
            String xcvr;
            int n = 16;
            while ((xcvr = FLRigGetXcvr()) == null && --n > 0)
            {
                Thread.Sleep(1000);
                DebugAddMsg(DebugEnum.LOG, "Waiting for FLRigv " + n + "\n");
                Application.DoEvents();
            }
            if (xcvr == null)
            {
                DebugAddMsg(DebugEnum.ERR, "No transceiver?  Aborting FLRigWait\n");
                return false;
            }
            //if (formClosing == true)
            //{
            //    return false;
            //}
            DebugAddMsg(DebugEnum.LOG, "Rig is " + xcvr + "\n");
            return true;
        }
        private bool FLRigSend(string xml, out string value)
        {
            value = "";
            if (rigStream == null)
            {
                return false;
            }
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                rigStream.Write(data, 0, data.Length);
                if (xml.Contains("rig.set") || xml.Contains("rig.cat_string"))
                {
                    int saveTimeout = rigStream.ReadTimeout;
                    // Just read the response and ignore it for now
                    rigStream.ReadTimeout = 2000;
                    byte[] data2 = new byte[4096];
                    Int32 bytes = rigStream.Read(data2, 0, data2.Length);
                    String responseData = Encoding.ASCII.GetString(data2, 0, bytes);
                    if (!responseData.Contains("200 OK"))
                    {
                        DebugAddMsg(DebugEnum.ERR, "FLRig error: unknown response=" + responseData + "\n");
                        //MyMessageBox("Unknown response from FLRig\n" + responseData);
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "FLRig error:\n" + ex.Message + "\n");
                if (rigClient != null)
                {
                    rigStream.Close();
                    rigClient.Close();
                    rigStream = null;
                    rigClient = null;
                }
                //checkBoxRig.Checked = false;
                //DisconnectFLRig();
                return false;
            }
            return true;
        }

        private bool FLRigSend(string xml)
        {
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
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
                return false;
            }
            return true;
        }
        private string FLRigGetXcvr()
        {
            string xcvr = null;
            //if (!checkBoxRig.Checked) return null;
            if (rigClient == null || rigStream == null) { Open(); }
            string xml2 = FLRigXML("rig.get_xcvr", null);
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
                    FrequencyA = FrequencyB = 0;
                    ModeA = ModeB = "?";
                }
            }
            catch (Exception ex)
            {
                DebugAddMsg(DebugEnum.ERR, "Rig not responding\n" + ex.Message + "\n");
                FrequencyA = FrequencyB = 0;
                ModeA = ModeB = "?";
            }
            rigStream.ReadTimeout = timeoutSave;
            return xcvr;
        }
        private string FLRigXML(string cmd, string value)
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
            while (myThread.IsAlive == true) 
            {
                FLRigGetVFO();
                //FLRigGetFrequency('A');
                //FLRigGetFrequency('B');
                Thread.Sleep(1000);
            }
        }


        private void FLRigSetVFO(char newvfo)
        {
            var xml = FLRigXML("rig.set_vfoAB", "" + newvfo);
            if (FLRigSend(xml) == false)
            { // Abort if FLRig is giving an error
                DebugAddMsg(DebugEnum.ERR, "FLRigSend got an error??\n");
            }
            vfo = newvfo;
        }

        private char FLRigGetVFO()
        {
            string xml = FLRigXML("rig.get_AB", null);
            if (FLRigSend(xml, out string value))
                return value[0];
            return '?';
        }
        public override char VFO
        {
            get 
            {
                return vfo; 
            }
            set 
            { 
                vfo = value; 
            }
        }
        public override double GetFrequency(char vfo)
        {
            if (vfo == 'A')
                return FrequencyA;
            else if (vfo == 'B')
                return FrequencyB;
            else if (this.VFO == 'A')
                return FrequencyA;
            else
                return FrequencyB;
            // do we need VFOC ever for FLRig?
        }

        public override string GetMode(char vfo)
        {
            throw new NotImplementedException();
        }

        public override string GetRig()
        {
            return rig;
        }

        // Return a list of all available modes
        public override List<string> GetModes()
        {
            throw new NotImplementedException();
        }


        public override void SetFrequency(char vfo, double frequency)
        {
            throw new NotImplementedException();
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

        public override string Model
        {
            get
            {
                return this.model;
            }
            set
            {
                if (value != null)
                    this.model = value;
                else
                    this.model = "Unknown";
            }
        }

        private double FLRigGetFrequency(char vfo)
        {
            double frequency = 0;
            string xml = FLRigXML("rig.get_vfo" + 'A', null);
            Byte[] data = System.Text.Encoding.ASCII.GetBytes(xml);
            try
            {
                if (rigStream == null) return 0.0;
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
                        frequency = Double.Parse(freqString, CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception ex)
                {

                }
            }
            catch (Exception ex)
            {

            }
            return frequency;
        }

        private void FLRigSetFrequency(char vfo, double frequency)
        {
            frequency = 0;
            string xml = FLRigXML("rig.get_vfo" + 'A', null);
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
                return;
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
                        frequency = Double.Parse(freqString, CultureInfo.InvariantCulture);
                    }
                }
                catch (Exception ex)
                {

                }
            }
            catch (Exception ex)
            {

            }

        }
        public override double FrequencyA
        {
            get
            {
                return this.FrequencyA;
            }
            set 
            {
                this.FrequencyA = value;
                FLRigSetFrequency('A', this.FrequencyA);
            }
        }
        public override double FrequencyB { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string ModeA { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override string ModeB { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }
        public override bool PTT { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    rigStream.Close();
                    rigStream.Dispose();
                    rigClient.Dispose();
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
        #endregion
    }
}
