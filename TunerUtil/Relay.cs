using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using static FTD2XX_NET.FTDI;

namespace TunerUtil
{
    class Relay
    {
        private FTDI ftdi = new FTDI();
        private bool disposed = true;
        List<string> comList = new List<string>();
        readonly uint devcount = 0;
        string comPort = "";
        int relayNum = 0;

        public Relay()
        {
            disposed = false;
            ftdi.SetBaudRate(9600);
            ftdi.GetNumberOfDevices(ref devcount);
            if (devcount == 0)
            {
                return;
            }
            FT_DEVICE_INFO_NODE[] nodes = new FT_DEVICE_INFO_NODE[devcount];
            FT_STATUS status = ftdi.GetDeviceList(nodes);
            uint index = 0;
           foreach (FT_DEVICE_INFO_NODE node in nodes)
            {
                if (node.Description.Contains("FT245R"))
                {
                    ftdi.OpenByIndex(index);
                    ftdi.GetCOMPort(out string comport);
                    ftdi.Close();
                    comList.Add(comport);
                    ++index;
                }
            }
        }

        public void Open(string comPortNew)
        {
            uint index = (uint)comList.IndexOf(comPortNew);
            ftdi.OpenByIndex(index);
            ftdi.SetBitMode(0xff, 0x01);
            comPort = comPortNew;
            relayNum = (int)index + 1; // index is 0-based, our relayNum is 1-based for the GUI
        }

        public int RelayNumber()
        {
            return relayNum;
        }

        public List<string> ComList()
        {
            return comList;
        }

        public uint DevCount()
        {
            return devcount;
        }
        /*
        public Relay(string comPort, string baud)
        {
            FT_DEVICE_INFO_NODE[] nodes = new FT_DEVICE_INFO_NODE[devcount];
            FT_STATUS status = ftdi.GetDeviceList(nodes);
            ftdi.OpenByIndex(0);
            ftdi.SetBitMode(0xff, 0x01);
            ftdi.GetCOMPort(out string comport);
            if (comport.Length == 0) comport = "Not detected";
            //richTextBox1.AppendText("COM Port: " + comport + "\n" + nodes[0].Description + "\n");
            //Init();
        }
        */

        ~Relay()
        {
            if (ftdi != null && ftdi.IsOpen)
                ftdi.Close();
        }

        public void Close()
        {
            if (ftdi != null)
                ftdi.Close();
        }

        public bool IsOpen()
        {
            if (ftdi == null) return false;
            return ftdi.IsOpen;
        }

        public void Dispose()
        {
            this.Dispose(true);
            GC.SuppressFinalize(this);
        }

        public void Dispose(bool disposing)
        {
            if (this.disposed)
            {
                return;
            }
            if (disposing)
            {
                return;
            }
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
            Thread.Sleep(2);
        }
    }
}
