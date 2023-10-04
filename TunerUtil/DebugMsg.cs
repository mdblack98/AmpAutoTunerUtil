using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AmpAutoTunerUtility
{
    public class DebugMsg
    {
        public static bool clockIsZulu;
        public static readonly string[] DebugEnumText = { "LOG", "ERR", "WRN", "TRC", "VER" };

        protected static private ConcurrentQueue<DebugMsg> msgQueue = new ConcurrentQueue<DebugMsg>();


        public enum DebugEnum
        {
            LOG, // force logging of message
            ERR, // fatal or action needed
            WARN, // non-fatal things
            TRACE, // important things to trace
            VERBOSE // lots of output
        }
        public static string MyTime()
        {
            string time;
            if (clockIsZulu)
                time = DateTime.UtcNow.ToString("HH:mm:ss.fffZ", CultureInfo.InvariantCulture) + ": ";
            else
                time = DateTime.Now.ToString("HH:mm:ss.fff", CultureInfo.InvariantCulture) + ": ";
            return time;
        }

        public string ?Text { get; set; }
        public DebugEnum Level { get; set; }
        public static void DebugAddMsg(DebugEnum level, string msg)
        {
            if (msg == null) return;
            if (msg[msg.Length - 1] != '\n') msg += "\n";
            DebugMsg msgItem = new DebugMsg
            {
                Text = MyTime()+" " +msg,
                Level = level
            };
            DebugAddMsg(msgItem);
        }

        //private static string MyTime()
        //{
        //    throw new NotImplementedException();
        //}

        public static  void DebugAddMsg(DebugMsg msg)
        {
            msgQueue.Enqueue(msg);
        }
        public static DebugMsg ?DebugGetMsg()
        {
            if (msgQueue.TryDequeue(out DebugMsg mymsg))
            {
                // Add the debug level to the message
                string stmp = DebugEnumText[(int)mymsg.Level] + ":" + mymsg.Text;
                mymsg.Text = stmp;
                return mymsg;
            }
            else return null;
        }
    }
}
