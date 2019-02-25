using FTD2XX_NET;
using System;
using System.IO.Ports;
using System.Threading;
using static FTD2XX_NET.FTDI;

namespace TunerUtil
{
    class Relay
    {
        private FTDI ftdi;

        public Relay(string comPort, string baud)
        {
            return;
            ftdi = new FTDI();
            ftdi.SetBaudRate(9600);
            uint devcount = 0;
            ftdi.GetNumberOfDevices(ref devcount);
            if (devcount == 0)
            {
                return;
            }
            FT_DEVICE_INFO_NODE[] nodes = new FT_DEVICE_INFO_NODE[devcount];
            FT_STATUS status = ftdi.GetDeviceList(nodes);
            ftdi.OpenByIndex(0);
            ftdi.SetBitMode(0xff, 0x01);
            ftdi.GetCOMPort(out string comport);
            if (comport.Length == 0) comport = "Not detected";
            //richTextBox1.AppendText("COM Port: " + comport + "\n" + nodes[0].Description + "\n");
            //Init();
        }

        ~Relay()
        {
            if (ftdi != null && ftdi.IsOpen) ftdi.Close();
        }

        public bool Status(int nRelay)
        {
            byte bitModes = 0;
            ftdi.GetPinStates(ref bitModes);
            if ((bitModes & (1<<(nRelay-1))) != 0)
            {
                return true;
            }
            return false;
        }

        public byte Status()
        {
            byte bitModes = 0;
            ftdi.GetPinStates(ref bitModes);
            return bitModes;
        }

        public void Init()
        {
            byte bitModes = 0;
            ftdi.GetPinStates(ref bitModes);

        }

        public void Set(int nRelay,byte status)
        {
            // Get status
            byte[] data = { 0xff, 0xff, 0x00 };
            uint nWritten = 0;
            byte flags;
            byte bitModes = 0;

            ftdi.GetPinStates(ref bitModes);

            if (status!=0)
            {
                flags = (byte)(bitModes | (1u << (nRelay - 1)));
            }
            else
            {
                flags = (byte)(bitModes & (~(1u << (nRelay - 1))));
            }
            data[2] = flags;
            ftdi.Write(data, data.Length, ref nWritten);
            Thread.Sleep(1);
        }
    }
}
