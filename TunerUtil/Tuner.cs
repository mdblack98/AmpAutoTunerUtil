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

        public virtual string GetModel()
        {
            return model;
        }

        public virtual string GetText()
        {
            if (msg.TryDequeue(out string mymsg))
            {
                return mymsg;
            }
            else return "";
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

        public virtual void CMD_Amp(byte on)
        {
            return;
        }
    }
}
