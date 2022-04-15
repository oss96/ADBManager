using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace ADBManager
{

    class Fastboot
    {
        readonly ProcessStartInfo processStartInfo = new ProcessStartInfo
        {
            FileName = "files/fastboot.exe",
            Arguments = "",
            CreateNoWindow = true,
            RedirectStandardOutput = true,
            UseShellExecute = false
        };
        private List<string> devices = new List<string>();
        private readonly List<string> oldDevices = new List<string>();
        private bool runWatch;
        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceConnected;
        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceDisconnected;
        internal Thread startWatch;


        public Fastboot()
        {

        }


        protected virtual void OnFastbootDeviceConnected(string device)
        {
            FastbootDeviceConnected?.Invoke(this, new FastbootDeviceEventArgs() { Device = device });
        }
        protected virtual void OnFastbootDeviceDisconnected(string device)
        {
            FastbootDeviceDisconnected?.Invoke(this, new FastbootDeviceEventArgs() { Device = device });
        }
        internal List<string> GetDevices()
        {
            Process proc = new Process();
            proc.Exited += Proc_Exited;
            proc.StartInfo = processStartInfo;
            proc.StartInfo.Arguments = " devices";
            proc.Start();
            string s = "";
            while (!proc.StandardOutput.EndOfStream)
            {
                s += proc.StandardOutput.ReadLine();
            }
            devices = s.Split(new string[] { "\tfastboot" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            proc.WaitForExit();
            if (proc.HasExited)
            {
            }
            return devices;
        }
        internal void RebootDevices()
        {
            foreach (string device in GetDevices())
            {
                Process proc = new Process();
                proc.Exited += Proc_Exited;
                proc.StartInfo = processStartInfo;
                proc.StartInfo.Arguments = " reboot";
                proc.Start();
            }
        }
        internal void BootImage(string deviceSerial, string bootImgPath)
        {
            Process proc = new Process();
            proc.Exited += Proc_Exited;
            proc.StartInfo = processStartInfo;
            proc.StartInfo.Arguments = $" -s {deviceSerial} boot {bootImgPath}";
            proc.Start();
            proc.WaitForExit();
        }
        private void Proc_Exited(object sender, EventArgs e)
        {
            (sender as Process).Dispose();
        }
        public void StartWatch()
        {
            startWatch = new Thread(Start);
            runWatch = true;
            startWatch.Start();
        }
        protected void Start()
        {
            string newDevice;
            while (runWatch)
            {
                oldDevices.Clear();
                oldDevices.AddRange(devices);
                devices = GetDevices();
                if (devices.Count > oldDevices.Count)
                {
                    foreach (var item in devices)
                    {
                        if (!oldDevices.Contains(item))
                        {
                            newDevice = item;
                            OnFastbootDeviceConnected(newDevice);
                        }
                    }
                }
                else if (devices.Count < oldDevices.Count)
                {
                    foreach (var item in oldDevices)
                    {
                        if (!devices.Contains(item))
                        {
                            newDevice = item;
                            OnFastbootDeviceDisconnected(newDevice);
                        }
                    }
                }
                Thread.Sleep(1000);
            }
        }
        internal void Dispose()
        {
            Thread stopWatchThread = new Thread(unused => KillWatch());
            stopWatchThread.Start();
        }
        internal void KillWatch()
        {
            runWatch = false;
            if (startWatch != null)
            {
                startWatch.Abort();
                startWatch = null;
            }
            this.Dispose();
        }
    }
}