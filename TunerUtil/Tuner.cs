using System;
using System.Security.Policy;
using static AmpAutoTunerUtility.DebugMsg;

namespace AmpAutoTunerUtility
{
    public abstract class Tuner : IDisposable
    {
        private bool _disposed = false;
        public static readonly string[] DebugEnumText = { "LOG", "ERR", "WRN", "TRC", "VER" };

        //public enum TunerState
        //{
        //    Unknown,
        //    NeedsTuning,
        //    Tuned
        //}
        //public TunerState State { get; set; }
        public double SWR { get; set; }
        protected private string model = null;
        protected private string comport = null;
        protected private string baud = null;
        protected private DebugEnum DebugLevel = DebugEnum.WARN;
        protected private double Inductance { get; set; } // pF
        protected private double Capacitance { get; set; } // uH
        public int AntennaNumber { get; set; }
        public bool TuneFull { get; set; }
        public string[,] antennas;
        public int[,] tuneFrequencies;
        public ulong cIndex;
        public ulong lIndex;
        public bool isOn = false;
        public Tuner()
        {
            model = null;
            comport = null;
            baud = null;
            //AntennaNumber = 1;
        }

        ~Tuner()
        {
            Dispose(false);
        }

        public string GetComPort() { return comport; }
        //public Tuner(string model, string comport, string baud)
        //{
        //    this.model = model;
        //    this.comport = comport;
        //    this.baud = baud;
        //}

        public void Dispose() 
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // release managed resources
            }
            _disposed = true;
        }
        
        public virtual void SetDebugLevel(DebugEnum level)
        {
            DebugLevel = level;
        }
        public virtual double GetInductance()
        {
            return Inductance;
        }
        public virtual void SetInductance(double value)
        {
            Inductance = value;
        }
        public virtual double GetCapacitance()
        {
            return Capacitance;
        }
        public virtual void SetCapacitance(double value)
        {
            Capacitance = value;
        }

        public virtual string GetModel()
        {
            return model;
        }

        public virtual void Poll()
        {
            // no action by default
        }


        public virtual string GetSWRString()
        {
            return "SWR.XX";
        }

        public virtual string GetPower()
        {
            return "POW";
        }


        public virtual string GetSerialPortTuner()
        {
            return this.comport;
        }

        public virtual void Tune()
        {
            //return '!';
        }

        public virtual void Close()
        {

        }

        public virtual char ReadResponse()
        {
            return '?';
        }

        public virtual void CMDAmp(byte onStatus)
        {
            return;
        }

        public virtual void SetTuningMode(int mode)
        {
            // mode == 0 is auto
            // mode == 1 is semiauto
            // mode == 2 is manual 
        }
        public virtual bool GetAmpStatus()
        {
            return true;
        }
        public virtual void SetAmp(bool onStatus)
        {
            // nothing to do
        }

        public virtual  void Save()
        {
            // depends if the tuner can save settings
        }

        public virtual void SetCapacitance(int v)
        {
            throw new NotImplementedException();
        }

        public virtual void SetInductance(decimal v)
        {
            throw new NotImplementedException();
        }

        public virtual int GetAntenna()
        {
            throw new NotImplementedException();
        }
        public virtual void SetAntenna(int antennaNumber, bool tuneIsRunning = false)
        {
            throw new NotImplementedException();
        }

        public virtual void GetAntennaData(int bandNumber)
        {
            throw new NotImplementedException();
        }

        public virtual void SelectAntennaPage()
        {
            throw new NotImplementedException();
        }

        public virtual void SelectDisplayPage()
        {
            throw new NotImplementedException();
        }
        public virtual void SelectManualTunePage()
        {
            throw new NotImplementedException();
        }
        public enum Screen { Unknown, Home, Tune, Antenna };
        public virtual bool GetStatus()
        {
            throw new NotImplementedException();
        }
        public virtual bool GetStatus2(Screen myScreen)
        {
            throw new NotImplementedException();
        }
        public virtual void SendCmd(byte cmd)
        {
            throw new NotImplementedException();
        }
        public virtual bool On()
        {
            throw new NotImplementedException();
        }
        public virtual bool Off()
        {
            throw new NotImplementedException();
        }
    }
}
