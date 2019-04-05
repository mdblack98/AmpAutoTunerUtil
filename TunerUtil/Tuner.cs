using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpAutoTunerUtility
{
    public class Tuner
    {
        protected string model = null;
        protected string comport = null;
        protected string baud = null;
        protected ConcurrentQueue<string> msg = new ConcurrentQueue<string>();
        protected int Inductance { get; set;} // pF
        protected int Capacitance { get; set; } // uH

        public Tuner()
        {
            model = null;
            comport = null;
            baud = null;
        }

        //public Tuner(string model, string comport, string baud)
        //{
        //    this.model = model;
        //    this.comport = comport;
        //    this.baud = baud;
        //}

        public virtual int GetInductance()
        {
            return Inductance;
        }
        public virtual int SetInductance(int value)
        {
            Inductance = value;
            return Inductance;
        }
        public virtual int GetCapacitance()
        {
            return Capacitance;
        }
        public virtual int SetCapacitance(int value)
        {
            Capacitance = value;
            return Inductance;
        }

        public virtual string GetModel()
        {
            return model;
        }

        public virtual void Poll()
        {
            // no action by default
        }

        public virtual string GetText()
        {
            if (msg.TryDequeue(out string mymsg))
            {
                return mymsg;
            }
            else return "";
        }

        public virtual string GetSWR()
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

        public virtual void CMD_Amp(byte on)
        {
            return;
        }
    }
}
