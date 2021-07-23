using System;
using System.Collections.Generic;
using System.Net.Sockets;

namespace AmpAutoTunerUtility
{
    class RigFLRig : Rig, IDisposable
    {
        readonly string rig = "FLRig";
        TcpClient rigClient;
        NetworkStream rigStream;
        public string errorMessage = "None";

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
                //    richTextBoxRig.AppendText("FLRig connected\n");
                //}
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                //checkBoxRig.Checked = false;
#pragma warning disable CA1307 // Specify StringComparison
                if (ex.Message.Contains("actively refused"))
#pragma warning restore CA1307 // Specify StringComparison
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

        private static bool FLRigWait()
        {
            /*
            string xcvr = "";
            while ((xcvr = FLRigGetXcvr()) == null && formClosing == false)
            {
                Thread.Sleep(500);
            }
            if (formClosing == true)
            {
                richTextBoxRig.AppendText("Aborting FLRigWait\n");
                return false;
            }
            richTextBoxRig.AppendText("Rig is " + xcvr + "\n");
            return true;
            */
            return false;
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

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
