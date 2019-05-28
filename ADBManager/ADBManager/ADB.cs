using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;

namespace ADBManager
{
    public class ADB
    {
        private readonly MainForm mainForm;

        public ADB(MainForm mainForm)
        {
            this.mainForm = mainForm;
        }

        internal static List<AndroidDevice> GetConnectedDevice()
        {
            //Get the first entry from the list of connected Android devices
            List<AndroidDevice> androidDevices = new List<AndroidDevice>();
            try
            {
                AdbServer adb = new AdbServer();
                var result = adb.StartServer(@"files/adb.exe", restartServerIfNewer: false);

                AdbServerStatus adbServerStatus = adb.GetStatus();

                var devices = AdbClient.Instance.GetDevices();

                foreach (var item in devices)
                {
                    androidDevices.Add(new AndroidDevice(item.Model, item.Name, item.Product, item.Serial, item.State, item.TransportId));
                }
            }
            catch
            {

            }
            return androidDevices;
        }
        /// <summary>
        /// Reboots device
        /// </summary>
        /// <param name="device"></param>
        internal static void RebootDevice(List<DeviceData> devices)
        {
            foreach (DeviceData device in devices)
            {
                AdbClient.Instance.Reboot(device);
            }
        }
        /// <summary>
        /// Reboots assigned devices
        /// modes are: recovery, bootloader, fastboot
        /// </summary>
        /// <param name="device"></param>
        internal static void RebootDevice(string mode, List<DeviceData> devices)
        {
            foreach (DeviceData device in devices)
            {
                AdbClient.Instance.Reboot(mode, device);
            }
        }
        internal static void SendShellCommand(string command, DeviceData device)
        {
            OutputReceiver outputReceiver = new OutputReceiver();
            AdbClient.Instance.ExecuteRemoteCommand(command, device, outputReceiver);
        }
        internal void InstallAPK(string path, List<DeviceData> devices, string appName)
        {
            string packageName = GetPackageName(path);
            foreach (DeviceData device in devices)
            {
                mainForm.SetLastStatus($"Installing {appName} on {device.Model}");
                Stream apkfile = new MemoryStream(File.ReadAllBytes(path));
                AdbClient.Instance.Install(device, apkfile);
                mainForm.SetLastStatus($"finished installing {appName} on {device.Model}");
            }
        }
        internal void UninstallAPP(string packageName, List<DeviceData> devices)
        {
            //SharpAdbClient.DeviceData
            //devices[0].UninstallPackage();
            PackageManager packageManager = new PackageManager(devices[0], true);
            Dictionary<string, string> packages = packageManager.Packages;
            AppList appList = new AppList(packages);
            appList.ShowDialog();

            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/aapt2.exe",
                    //Arguments = $"dump badging {}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            //get App Icon

        }
        internal string GetPackageName(string apkPath)
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/aapt2.exe",
                    Arguments = $"dump packagename {apkPath}",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            string result = "";
            while (!proc.StandardOutput.EndOfStream)
            {
                result = proc.StandardOutput.ReadLine();
            }
            if (result == "")
            {
                mainForm.SetLastStatus($"Error while installing \"{apkPath}\"");
            }
            return result;
        }
        internal string GetAppName(string apkPath)
        {
            string appName = "";
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/aapt2.exe",
                    Arguments = $"dump badging {apkPath}",
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    RedirectStandardOutput = true
                }
            };
            proc.Start();
            string output = "";
            Thread readOutput = new Thread(() => { output = proc.StandardOutput.ReadToEnd(); });
            readOutput.Start();
            readOutput.Join();
            if (output.Contains("application-label:"))
            {
                output = output.Substring(
                    output.IndexOf("application-label:"), output.Length - output.IndexOf("application-label:")).Remove(0, 19);
                appName = output.Substring(0, output.IndexOf("'\r\n"));
            }
            return appName;
        }
    }
    class OutputReceiver : IShellOutputReceiver
    {
        public bool ParsesErrors { get; set; }

        public void AddOutput(string line)
        {
        }

        public void Flush()
        {
        }
    }
}
