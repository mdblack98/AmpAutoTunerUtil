using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;

namespace AmpAutoTunerUtility
{
    class RigFLRig : Rig
    {
        readonly string rig = "FLRig";
        TcpClient rigClient;
        NetworkStream rigStream;
        string errorMessage = "None";

        public override void Close()
        {
            throw new NotImplementedException();
        }

        public override double GetFrequency(char vfo)
        {
            throw new NotImplementedException();
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

        public override bool Open()
        {
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
                //    richTextBoxRig.AppendText(MyTime() + "FLRig connected\n");
                //}
            }
            catch (Exception ex)
            {
                //checkBoxRig.Checked = false;
                if (ex.Message.Contains("actively refused"))
                {
                    errorMessage = "FLRig not responding...";
                    //richTextBoxRig.AppendText(MyTime() + "FLRig not responding...\n");
                }
                else
                {
                    errorMessage = "FLRig unexpected error:\n" + ex.Message;
                    //richTextBoxRig.AppendText(MyTime() + "FLRig unexpected error:\n" + ex.Message + "\n");
                }
                return false;
            }
            return true;
        }

        public override void SetFrequency(double frequency)
        {
            throw new NotImplementedException();
        }

        public override void SetMode(char vfo, string mode)
        {
            throw new NotImplementedException();
        }

        // FLRig unique functions below here

        private bool FLRigWait()
        {
            /*
            string xcvr = "";
            while ((xcvr = FLRigGetXcvr()) == null && formClosing == false)
            {
                Thread.Sleep(500);
            }
            if (formClosing == true)
            {
                richTextBoxRig.AppendText(MyTime() + "Aborting FLRigWait\n");
                return false;
            }
            richTextBoxRig.AppendText(MyTime() + "Rig is " + xcvr + "\n");
            return true;
            */
            return false;
        }
    }
}
