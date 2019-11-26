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
    public class Audio: IDisposable
    {
        public int DeviceNumber { get; set; }
        public float MyFrequency { get; set; }
        public float Volume { get; set; }
        private int SampleRate { get; set; }
        private WaveOutEvent driverOut;
        public List<DirectSoundDeviceInfo> DeviceInfo { get; }

        readonly SignalGenerator sg;
        private string errMsg = null;

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
#pragma warning disable CA1031 // Do not catch general exception types
            catch (Exception ex)
#pragma warning restore CA1031 // Do not catch general exception types
            {
                errMsg = ex.Message;
                //throw new AmpAutoTunerUtilException("Audio initialization failed",ex);
                //throw myEx;
            }
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Audio StartStopSineWave failed");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
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
#pragma warning disable CA1303 // Do not pass literals as localized parameters
                throw new Exception("Audio StartStopSineWave failed");
#pragma warning restore CA1303 // Do not pass literals as localized parameters
            }
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    driverOut.Dispose();
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~Audio()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            GC.SuppressFinalize(this);
        }
        #endregion
    }
}
