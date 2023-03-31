using FTD2XX_NET;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading;
using static FTD2XX_NET.FTDI;

namespace AmpAutoTunerUtility
{
    class Relay
    {
        public FTDI ftdi = new FTDI();
        readonly List<string> comList = new List<string>();
        readonly List<uint> comIndex = new List<uint>();
        readonly uint devcount = 0;
        string comPort = "";
        int relayNum = 0;
        string serialNumber = "";
        readonly List<string> serialNums = new List<string>();
        public string errMsg = null;
        public bool relayError;
        private byte ampRelay = 1; // 1-based number of the relay used for the amplifier on/off

        public byte AmpRelay { get => ampRelay; set => ampRelay = value; }

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
                        //Close();
                        comList.Add(comport);
                        comIndex.Add(index);
                        serialNums.Add(node.SerialNumber);
                    }
                    ++index;
                }
                devcount = nRelays;
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
            }
        }

        public void Open(bool close = false)
        {
            Open(comPort, close);
        }

        public void Open(string comPortNew, bool close = false)
        {
            errMsg = null;
            if (ftdi == null)
            {
                ftdi = new FTDI();
            }
            else
            {
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI close#1\n");
                ftdi.Close();
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
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI close#2\n");
                    ftdi.Close();
                    ftdi = null;
                }
                AllOff();
            }
            catch (Exception ex)
            {
                errMsg = "Relay Open failed\n" + ex.Message + "\n";
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, errMsg);
                return;
            }
            // this msg doesn't display...why??
            //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, "Relay#"+relayNum+" opened\n");
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
            if (ftdi != null)
            {
                //AllOff();
                //DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI close#3\n");
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
                //Set(1, 0); // Turn off all relays
                //Set(2, 0);
                Set(3, 0);
                Set(4, 0);
                Set(5, 0);
                Set(6, 0);
                Set(7, 0);
                Set(8, 0);
            }
            catch (Exception ex)
            {
                errMsg = "Relay AllOff failed\n" + ex.Message;
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
            Monitor.Enter(ftdi);
            //Open();
            errMsg = null;
            try
            {
                byte bitModes = 0;
                ftdi.GetPinStates(ref bitModes);
                if ((bitModes & (1 << (nRelay - 1))) != 0)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI close#4\n");
                    Close();
                    Monitor.Exit(ftdi);
                    return true;
                }
            }
            catch (Exception ex)
            {
                errMsg = "Relay Status failed\n" + ex.Message;
            }
            Monitor.Exit(ftdi);
            return false;
        }

        public byte Status()
        {
            relayError = false;
            if (ftdi == null)
            {
                //Open(false);
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI is null??\n");
                relayError = true;
                return 0x0;
            }
            Monitor.Enter(ftdi);
            bool local = false;
            if (ftdi != null && !ftdi.IsOpen) // then we'll open and close the device inside here
            {
                Open();
                local = true;
            }
            errMsg = null;
            byte bitModes = 0x00;
            try
            {
                FTD2XX_NET.FTDI.FT_STATUS status = ftdi.GetPinStates(ref bitModes);
                if (status != FT_STATUS.FT_OK)
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "FTDI status != FT_STATUS_.FT_OK, " + status + " != " + FT_STATUS.FT_OK + "\n");
                    errMsg = "Oops!!";
                    ftdi.CyclePort();
                    ftdi.Close();
                    ftdi = null;
                    Monitor.Exit(ftdi);
                    relayError = true;
                    return 0x0;
                }
            }
            catch (Exception ex)
            {
                errMsg = "Relay Status failed\n" + ex.Message;
            }
            if (local)
            {
                Close();
            }
            if (ftdi != null)
            Monitor.Exit(ftdi);
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
            catch (Exception ex)
            {
                errMsg = "Relay Init failed\n" + ex.Message;
            }
            Close();
        }

        public void Set(int nRelay, byte status, [CallerMemberName] string name = "",
                   [CallerLineNumber] int line = -1)
        {
            DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "Relay Set(" + nRelay + ","+status+") called from " + name + "@line(" + line + ")\n");
            if (ftdi == null)
            {
                Open(false);
            }
            if (nRelay == 0) return;
            if (ftdi == null)
            {
                return;
            }
            Monitor.Enter(ftdi);
            errMsg = null;
            try
            {
                // Get status
                byte[] data = { 0xff, 0xff, 0x00 };
                uint nWritten = 0;
                byte flags;
                byte bitModes = 0x00;
                byte bitModesSave;

                ftdi.GetPinStates(ref bitModes);
                bitModesSave = bitModes;
                // we need to mask the antenna bit if being requested
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "nRelay=" + nRelay + " AmpRelay=" + this.AmpRelay);
                if (nRelay == this.AmpRelay)
                {
                    // just set the amp bit 
                    int tmp1 = bitModes;
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "tmp1 #1=" + tmp1);
                    tmp1 ^= (-status ^ bitModes) & (0x1 << (nRelay - 1));
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "tmp1 #2=" + tmp1);
                    bitModes = (byte)tmp1;
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "bitModes " + bitModes);
                    data[0] = data[1] = 0xff;
                    data[0] &= bitModes;
                    data[1] &= bitModes;
                    data[2] = bitModes;
                    ftdi.Write(data, data.Length, ref nWritten);
                    return;
                }
                else
                {
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Turn off all antenna bits");

                    // turn off all antenna bits by masking amp bit only
                    // because we will re-enable them if setting them
                    bitModes = (byte)(bitModes & (byte)(0x1 << (this.ampRelay - 1)));
                    data[0] = data[1] = 0xff;
                    data[0] &= bitModes;
                    data[1] &= bitModes;
                    data[2] = bitModes;
                    if ((bitModes & nRelay) != (bitModesSave & nRelay))
                    { 
                        ftdi.Write(data, data.Length, ref nWritten);
                        Thread.Sleep(500);
                    }
                    byte newBitModes = 0xff;
                    ftdi.GetPinStates(ref newBitModes);
                    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Bits after zeroed=" + newBitModes);

                }
                if (status != 0)
                {
                    flags = (byte)(bitModes | (1u << (nRelay - 1)));
                }
                else
                {
                    //flags = (byte)(bitModes | (~(1u << (nRelay - 1))));
                    flags = bitModes;
                }
                //if (flags == bitModesSave)
                //{
                //    DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.VERBOSE, "No relay change needed bitModes=x" + flags.ToString("X2"));
                //    return;
                //}

                // Ensure all antenna bits are off -- masking off 1+2 for now for amp relay -- need Relay page to have bit exclusions
                //data[0] = data[1] = data[2] = (byte)(bitModesSave & 0x3);
                //ftdi.Write(data, data.Length, ref nWritten);

                data[0] = data[1] = 0xff;
                data[0] &= flags;
                data[1] &= flags;
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Relay #" + nRelay + " set=" + status + " bits=" + bitModes + " newBits=" + flags + "\n");
                data[2] = flags;
                //if (bitModes != bitModesSave) 
                {

                    ftdi.Write(data, data.Length, ref nWritten);
                    if (nWritten == 0)
                    {
                        Close();
                        errMsg = "Unable to write to relay...disconnected?";
                        DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, errMsg);
                        Monitor.Exit(ftdi);
                        return;
                        //throw new Exception("Unable to write to relay...disconnected?");
                    }
                    Thread.Sleep(200);
                }
                byte bitModes2 = 0x00;
                ftdi.GetPinStates(ref bitModes2);
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.LOG, "Relay #" + nRelay + " statusbits=" + bitModes2 + "\n");
                if (status != 0) // check we set it
                {
                    if ((bitModes2 & (1u << (nRelay - 1))) == 0)
                    {
                        errMsg = "Relay did not get set!";
                        DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, errMsg);
                        Close();
                        Monitor.Exit(ftdi);
                        return;
                    }
                }
                //Status();
            }
            catch (Exception ex)
            {
                errMsg = "Relay Set failed\n" + ex.Message;
                DebugMsg.DebugAddMsg(DebugMsg.DebugEnum.ERR, errMsg);
            }
            Monitor.Exit(ftdi);
        }
    }
}
