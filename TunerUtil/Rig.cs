using System.Collections.Generic;

namespace AmpAutoTunerUtility
{
    public abstract class Rig
    {
        protected string model = null;

        public Rig()
        {
            model = null;
        }

        public abstract bool Open();

        public abstract string GetRig();

        public abstract void Close();

        public abstract double GetFrequency(char vfo);

        public abstract void SetFrequency(double frequency);

        public abstract string GetMode(char vfo);

        public abstract void SetMode(char vfo, string mode);

        public abstract List<string> GetModes();
    }
}
