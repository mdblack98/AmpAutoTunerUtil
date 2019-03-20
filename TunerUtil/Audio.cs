using NAudio.Wave;
using NAudio.Wave.SampleProviders;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Management;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AmpAutoTunerUtility
{
    public class Audio
    {
        public int DeviceNumber { get; set; }
        public float MyFrequency { get; set; }
        public float Volume { get; set; }
        private int SampleRate { get; set; }
        private WaveOutEvent driverOut;
        public List<DirectSoundDeviceInfo> DeviceInfo { get; }
        SignalGenerator sg;
        public string errMsg = null;

        public Audio()
        {
            try
            {
                SampleRate = 8000;
                DeviceNumber = 0;
                MyFrequency = 1500;
                Volume = .95f;
                DeviceInfo = new List<DirectSoundDeviceInfo>();
                EnumerateAudioOutputDevices();
                sg = new SignalGenerator(SampleRate, 2)
                {
                    Frequency = MyFrequency,
                    Gain = Volume,
                    Type = SignalGeneratorType.Sin
                };
                driverOut = new WaveOutEvent
                {
                    DeviceNumber = DeviceNumber
                };
                driverOut.Init(sg);
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
                //throw new AmpAutoTunerUtilException("Audio initialization failed",ex);
                //throw myEx;
            }
        }

        ~Audio()
        {

        }


        public void StartStopSineWave()
        {
            errMsg = null;
            try
            {
                if (driverOut.PlaybackState == PlaybackState.Playing)
                {
                    driverOut.Stop();
                }
                else
                {
                    driverOut.Dispose();
                    driverOut = new WaveOutEvent
                    {
                        DeviceNumber = DeviceNumber
                    };
                    driverOut.Init(sg);
                    driverOut.Play();
                }
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
                throw new Exception("Audio StartStopSineWave failed");
            }
        }

        private void EnumerateAudioOutputDevices()
        {
            errMsg = null;
            try
            {
                int waveOutDevices = WaveOut.DeviceCount;
                int n = 0;
                foreach (var dev in DirectSoundOut.Devices)
                {
                    if (n != 0) // skip the 1st Primary Sound Driver
                    {
                        DeviceInfo.Add(dev);
                    }
                    ++n;
                }
            }
            catch (Exception ex)
            {
                errMsg = ex.Message;
                throw new Exception("Audio StartStopSineWave failed");
            }
        }
    }
}
