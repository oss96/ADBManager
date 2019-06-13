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
                CreateNoWindow = false,
                RedirectStandardOutput = true,
                UseShellExecute = false
            }
        };
        public Process Proc => proc;
        private List<string> devices = new List<string>();

        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceConnected;
        public event EventHandler<FastbootDeviceEventArgs> FastbootDeviceDisconnected;


        public Fastboot()
        {

        }

        protected virtual void OnFastbootDeviceConnected(string device)
        {
            StartFastbootWatch();
            FastbootDeviceConnected?.Invoke(this, new FastbootDeviceEventArgs() { Device = device });
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
        internal void RebootDevices(List<string> devices)
        {
            foreach (string device in devices)
            {
                Proc.StartInfo.Arguments = $" -s {device} reboot";
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
        public void StartFastbootWatch()
        {
            List<string> oldDevices = new List<string>();
            string newDevice = string.Empty;
            oldDevices.AddRange(devices);
            while (true)
            {
                if (GetDevices().Count != 0)
                {
                    //Trigger Event
                    if (devices.Count != 0)
                    {
                        IEnumerable<string> device = devices.Intersect(oldDevices);
                        foreach (var item in devices)
                        {
                            if (!oldDevices.Contains(item))
                            {
                                newDevice = item;
                            }
                        }
                        OnFastbootDeviceConnected(newDevice);
                    }
                }
                else
                {
                    continue;
                }
            }
        }
    }
}
