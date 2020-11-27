using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using static AmpAutoTunerUtility.DebugMsg;

namespace AmpAutoTunerUtility
{
    public abstract class Tuner: IDisposable
    {
        private bool _disposed = false;
        public static readonly string[] DebugEnumText = { "ERR", "WRN", "TRC", "VER", "LOG" };

        public double SWR { get; set; }
        protected private string model = null;
        protected private string comport = null;
        protected private string baud = null;
        protected private DebugEnum DebugLevel = DebugEnum.WARN;
        protected private int Inductance { get; set; } // pF
        protected private int Capacitance { get; set; } // uH

        public Tuner()
        {
            model = null;
            comport = null;
            baud = null;
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
        public virtual int GetInductance()
        {
            return Inductance;
        }
        public virtual void SetInductance(int value)
        {
            Inductance = value;
        }
        public virtual int GetCapacitance()
        {
            return Capacitance;
        }
        public virtual void SetCapacitance(int value)
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

        //public virtual int GetInductance()
        //{
            // returns pF;
        //    return -1;
        //}

        //public virtual int GetCapacitance()
        //{
        //    // returns uH
        //    return -1;
        //}

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
        }
        public virtual bool GetAmpStatus()
        {
            return true;
        }
        public virtual void SetAmp(bool onStatus)
        {
            // nothing to do
        }

    }
}
