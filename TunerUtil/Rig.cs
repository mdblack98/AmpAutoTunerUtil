using System.Collections.Generic;
using System.Security.Permissions;
using System.Security.Policy;

namespace AmpAutoTunerUtility
{
    public abstract class Rig
    {
        //protected private string model = null;
        public abstract string Model { get; }
        public abstract char VFO { get; set; }
        public abstract double FrequencyA { get; set; }
        public abstract double FrequencyB { get; set; } 
        public abstract string ModeA { get; set; }
        public abstract string ModeB { get; set; }
        public abstract bool PTT { get; set; }
        public abstract int Power { get; set; }
        public abstract bool Transceive { get; set; }
        /// <summary>
        /// public abstract void SendCommand(byte[] command);
        /// </summary>
        /// <param name="command"></param>
        //public abstract void SendCommand(string command);
        public abstract void SendCommand(int command);
        public Rig()
        {
            //Model = "Unknown";
        }

        public abstract void Poll();
               
        public abstract bool Open();

        public abstract string GetRig();

        public abstract void Close();

        public abstract double GetFrequency(char vfo);

        public abstract void SetFrequency(double frequency);

        public abstract string GetMode(char vfo);

        public abstract void SetMode(char vfo, string mode);

        public abstract List<string> GetModes();

        public abstract void SetFrequency(char vfo, double frequency);
        public abstract void SetPTT(bool ptt);
        public abstract bool GetPTT();

        public abstract void SetTransceive(bool transceive);

        public abstract string GetModel();

    }
}
