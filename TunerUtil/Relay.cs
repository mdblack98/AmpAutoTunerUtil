using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Threading;
using static FTD2XX_NET.FTDI;

namespace AmpAutoTunerUtility
{
    class Relay
    {
        private FTDI ftdi = new FTDI();
        readonly List<string> comList = new List<string>();
        readonly List<uint> comIndex = new List<uint>();
        readonly uint devcount = 0;
        string comPort = "";
        int relayNum = 0;
        string serialNumber = "";
        readonly List<string> serialNums = new List<string>();
        public string errMsg = null;

        public Relay()
        {
            try
            {
                ftdi.SetBaudRate(9600);
                ftdi.GetNumberOfDevices(ref devcount);
                if (devcount == 0)
                {
                    errMsg = "No devices found";
                    return;
                }
                FT_DEVICE_INFO_NODE[] nodes = new FT_DEVICE_INFO_NODE[devcount];
                FT_STATUS status = ftdi.GetDeviceList(nodes);
                uint index = 0;
                uint nRelays = 0;
                foreach (FT_DEVICE_INFO_NODE node in nodes)
                {
                    if (node.Description.Contains("FT245R"))
                    {
                        nRelays++;
                        ftdi.OpenByIndex(index);
                        //Nothing unique in the EEPROM to show 4 or 8 channel
                        //FT232R_EEPROM_STRUCTURE ee232r = new FT232R_EEPROM_STRUCTURE();
                        //ftdi.ReadFT232REEPROM(ee232r);
                        ftdi.GetCOMPort(out string comport);
                        Close();
                        comList.Add(comport);
                        comIndex.Add(index);
                        serialNums.Add(node.SerialNumber);
                    }
                    ++index;
                }
                devcount = nRelays;
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = ex.Message;
            }
        }

        public void Open(bool close = true)
        {
            Open(comPort, close);
        }

        public void Open(string comPortNew, bool close = true)
        {
            errMsg = null;
            if (ftdi == null)
            {
                ftdi = new FTDI();
            }
            try
            {
                int index = comList.IndexOf(comPortNew);
                if (index < 0)
                {
                    index = -1;
                    return;
                }
                ftdi.OpenByIndex(comIndex[index]);
                ftdi.SetBitMode(0xff, 0x01);
                comPort = comPortNew;
                relayNum = (int)index + 1; // index is 0-based, our relayNum is 1-based for the GUI
                serialNumber = serialNums[index];
                if (close)
                {
                    ftdi.Close();
                    ftdi = null;
                }
                //AllOff();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay Open failed\n"+ex.Message;
            }
        }


        public string SerialNumber()
        {
            return serialNumber;
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
            Close();
        }

        public void Close()
        {
            // Put some delays in here as the 8-channel relay was turning on some relays
            // I had 4-channel as Relay1 and 8-channel as Relay2
            // The delays seem to have cured that problem
            if (ftdi != null && ftdi.IsOpen)
            {
                AllOff();
                ftdi.Close();
                Thread.Sleep(200);
            }
            ftdi = null;
            Thread.Sleep(200);
        }

        public void AllOff()
        {
            //Open();
            errMsg = null;
            try
            {
                Set(1, 0); // Turn off all relays
                Set(2, 0);
                Set(3, 0);
                Set(4, 0);
                Set(5, 0);
                Set(6, 0);
                Set(7, 0);
                Set(8, 0);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay AllOff failed\n"+ex.Message;
            }
            //ftdi.Close();
        }

        public bool IsOpen()
        {
            if (ftdi == null) return false;
            return ftdi.IsOpen;
        }

        public bool Status(int nRelay)
        {
            Open();
            errMsg = null;
            try
            {
                byte bitModes = 0;
                ftdi.GetPinStates(ref bitModes);
                if ((bitModes & (1 << (nRelay - 1))) != 0)
                {
                    Close();
                    return true;
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay Status failed\n"+ex.Message;
            }
            Close();
            return false;
        }

        public byte Status()
        {
            bool local = false;
            if (ftdi!=null && !ftdi.IsOpen) // then we'll open and close the device inside here
            {
                Open();
                local = true;
            }
            errMsg = null;
            byte bitModes = 0;
            try
            {
                ftdi.GetPinStates(ref bitModes);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay Status failed\n"+ex.Message;
            }
            if (local)
            {
                Close();
            }
            return bitModes;
        }

        public void Init()
        {
            Open();
            errMsg = null;
            try
            {
                byte bitModes = 0;
                ftdi.GetPinStates(ref bitModes);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay Init failed\n"+ex.Message;
            }
           Close();
        }

        public void Set(int nRelay, byte status)
        {
            if (ftdi == null)
            {
                Open(false);
            }
            errMsg = null;
            try
            {
                // Get status
                byte[] data = { 0xff, 0xff, 0x00 };
                uint nWritten = 0;
                byte flags;
                byte bitModes = 0x00;

                ftdi.GetPinStates(ref bitModes);

                if (status != 0)
                {
                    flags = (byte)(bitModes | (1u << (nRelay - 1)));
                }
                else
                {
                    flags = (byte)(bitModes & (~(1u << (nRelay - 1))));
                }
                data[2] = flags;
                ftdi.Write(data, data.Length, ref nWritten);
                if (nWritten == 0)
                {
                    Close();
                    errMsg = "Unable to write to relay...disconnected?";
                    return;
                    //throw new Exception("Unable to write to relay...disconnected?");
                }
                Thread.Sleep(100);
                byte bitModes2 = 0x00;
                ftdi.GetPinStates(ref bitModes2);
                if (status != 0) // check we set it
                {
                    if ((bitModes2 & (1u << (nRelay - 1))) == 0)
                    {
                        errMsg = "Relay did not get set!";
                        Close();
                        return;
                    }
                }
                //Status();
            }
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = "Relay Set failed\n"+ex.Message;
            }
        }
    }
}
