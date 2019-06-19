﻿using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Threading;
using System.Linq;
using System.Windows.Forms;

namespace ADBManager
{
    public class ADB
    {
        private readonly MainForm mainForm;
        private List<string> unInstallList = new List<string>();


        public ADB()
        {
        }
        public ADB(MainForm mainForm)
        {
            this.mainForm = mainForm;
        }


        internal static List<AndroidDevice> GetConnectedDevice()
        {
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
            try
            {
                StartServer();
                string packageName = GetPackageName(path);
                foreach (DeviceData device in devices)
                {
                    mainForm.SetLastStatus($"Installing {appName} on {device.Model}");
                    Process process = new Process
                    {
                        StartInfo = new ProcessStartInfo
                        {
                            FileName = @"files\adb.exe",
                            Arguments = $"-s {device.Serial} install -r {path}",
                            CreateNoWindow = true
                        }
                    };
                    process.Start();


                    mainForm.SetLastStatus($"finished installing {appName} on {device.Model}");
                }
            }
            catch (PackageInstallationException pie)
            {
                MessageBox.Show(pie.Message, "Error while installing");
                mainForm.SetLastStatus($"Error while installing {appName}");
            }
        }
        internal void UninstallAPP(List<DeviceData> devices)
        {
            List<PackageManager> packageManagers = new List<PackageManager>();
            List<Dictionary<string, string>> packages = new List<Dictionary<string, string>>();
            foreach (DeviceData device in devices)
            {
                PackageManager tmp_packageManager = new PackageManager(device, true);
                packageManagers.Add(tmp_packageManager);
                packages.Add(new Dictionary<string, string>(tmp_packageManager.Packages));
            }
            using (AppList appList = new AppList(packages, this))
            {
                appList.ShowDialog();
            }
            foreach (var packageManager in packageManagers)
            {
                if (unInstallList.Count != 0)
                    foreach (string item in unInstallList)
                    {
                        try
                        {
                            packageManager.UninstallPackage(item);
                        }
                        catch (Exception)
                        {

                            throw;
                        }
                    }
            }
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
                    Arguments = $"dump badging \"{ apkPath }\"",
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
        internal string GetAPKVersion(string apkPath)
        {
            string appName = "";
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/aapt2.exe",
                    Arguments = $"dump badging \"{ apkPath }\"",
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
            if (output.Contains("versionName="))
            {
                output = output.Substring(
                    output.IndexOf("versionName="), output.Length - output.IndexOf("versionName=")).Remove(0, 12);
                appName = output.Substring(0, output.IndexOf("platformBuildVersionName"));
                appName = appName.Replace("'", "").Trim();
            }
            return appName;
        }
        internal void StartServer()
        {
            AdbServerStatus adb = new AdbServerStatus();
            if (adb.IsRunning)
            {
                AdbClient.Instance.KillAdb();
            }
            AdbServer.Instance.StartServer(@"files\adb.exe", true);
        }
        internal void SetUninstallList(List<string> inputUninstallList) => unInstallList = inputUninstallList;
        internal List<TreeNode> GetDeviceDirectory(string path, DeviceData device)
        {
            List<TreeNode> treeNodes = new List<TreeNode>();
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s {device.Serial} shell ls",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            };
            if (path == "")
            {
                proc.Start();
                proc.WaitForExit();
                string s = "";
                while (!proc.StandardOutput.EndOfStream)
                {
                    s += proc.StandardOutput.ReadLine();
                }
                s = s.Substring(s.LastIndexOf("Permission denied"), s.Length - s.LastIndexOf("Permission denied")).Remove(0, 17);
                List<string> directories = s.Split(new string[] { " " }, StringSplitOptions.RemoveEmptyEntries).ToList();
                foreach (string directory in directories)
                {
                    treeNodes.Add(new TreeNode(directory));
                }
            }
            return treeNodes;
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
