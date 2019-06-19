using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace ADBManager
{

    class Fastboot
    {
        private readonly Process proc = new Process
        {
            StartInfo = new ProcessStartInfo
            {
                FileName = "files/fastboot.exe",
                Arguments = "",
                CreateNoWindow = true,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        public Process Proc => proc;
        private List<string> devices = new List<string>();
        private readonly List<string> oldDevices = new List<string>();
        private bool runWatch;
        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceConnected;
        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceDisconnected;
        Thread startWatch;

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
            Proc.StartInfo.Arguments = " devices";
            Proc.Start();
            string s = "";
            while (!Proc.StandardOutput.EndOfStream)
            {
                s += Proc.StandardOutput.ReadLine();
            }
            devices = s.Split(new string[] { "\tfastboot" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            return devices;
        }
        internal void RebootDevices()
        {
            foreach (string device in GetDevices())
            {
                Proc.StartInfo.Arguments = " reboot";
                Proc.Start();
            }
        }
        internal void BootImage(List<string> devices, string bootImgPath)
        {
            foreach (string device in devices)
            {
                Proc.StartInfo.Arguments = $" -s {device} boot {bootImgPath}";
                Proc.Start();
                Proc.WaitForExit();
            }
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
                GetDevices();
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
                Thread.Sleep(500);
            }
        }
        internal void Dispose()
        {
            Thread stopWatchThread = new Thread(unused => KillWatch());
            stopWatchThread.Start();
        }
        protected void KillWatch()
        {
            runWatch = false;
            if (startWatch != null)
            {
                startWatch.Abort();
            }
        }
    }
}
