using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpAutoTunerUtility
{
    public class DebugMsg
    {
        protected static private ConcurrentQueue<DebugMsg> msgQueue = new ConcurrentQueue<DebugMsg>();
        public enum DebugEnum
        {
            ERR, // fatal or action needed
            WARN, // non-fatal things
            TRACE, // important things to trace
            VERBOSE, // lots of output
            LOG // force logging of message
        }
        public string Text { get; set; }
            public DebugEnum Level { get; set; }
        public static void DebugAddMsg(DebugEnum level, string msg)
        {
            DebugMsg msgItem = new DebugMsg
            {
                Text = Form1.MyTime()+" " +msg,
                Level = level
            };
            DebugAddMsg(msgItem);
        }

        private static string MyTime()
        {
            throw new NotImplementedException();
        }

        public static void DebugAddMsg(DebugMsg msg)
        {
            msgQueue.Enqueue(msg);
        }
        public static DebugMsg DebugGetMsg()
        {
            if (msgQueue.TryDequeue(out DebugMsg mymsg))
            {
                // Add the debug level to the message
                string stmp = Tuner.DebugEnumText[(int)mymsg.Level] + ":" + mymsg.Text;
                mymsg.Text = stmp;
                return mymsg;
            }
            else return null;
        }
    }
}
