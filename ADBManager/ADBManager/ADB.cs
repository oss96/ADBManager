using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;

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
        internal void InstallAPK(string path, List<DeviceData> devices)
        {
            string packageName = GetPackageName(path);
            foreach (DeviceData device in devices)
            {
                mainForm.SetLastStatus($"Installing apk on {device.Model}");
                /*Process proc = new Process
                {
                    StartInfo =
                    {
                        FileName = "files/adb.exe",
                        Arguments = $"-s {device.Serial} install {path}",
                        CreateNoWindow = false,
                        UseShellExecute = false,
                        ErrorDialog=true
                    }
                };
                proc.Start();
                proc.WaitForExit();*/
                Stream apkfile = new MemoryStream(File.ReadAllBytes(path));
                AdbClient.Instance.Install(device, apkfile);
                mainForm.SetLastStatus($"finished installing {packageName} on {device.Model}");
            }
        }
        private string GetPackageName(string apkPath)
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
