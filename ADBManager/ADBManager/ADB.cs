using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ADBManager
{
    public class ADB
    {
        internal Manager manager;
        private List<string> unInstallList = new List<string>();


        public ADB(Manager manager)
        {
            this.manager = manager;
        }

        #region Getters
        internal static List<AndroidDevice> GetConnectedDevices()
        {
            List<AndroidDevice> androidDevices = new List<AndroidDevice>();
            try
            {
                AdbServer adb = new AdbServer();
                var result = adb.StartServer(@"files/adb.exe", restartServerIfNewer: false);
                AdbClient adbClient = new AdbClient();
                var devices = adbClient.GetDevices();

                foreach (var item in devices)
                {
                    androidDevices.Add(new AndroidDevice(item.Model, item.Name, item.Product, item.Serial, item.State, item.TransportId, GetIMEI(item)));
                }
            }
            catch
            {
            }
            return androidDevices;
        }

        internal static List<string> GetConnectedDeviceSerials()
        {
            List<AndroidDevice> androidDevices = GetConnectedDevices();
            List<string> returnDevices = new List<string>();
            foreach (AndroidDevice androidDevice in androidDevices)
            {
                if (androidDevice.State == DeviceState.Online || androidDevice.State == DeviceState.Recovery)
                {
                    returnDevices.Add(androidDevice.Serial);
                }
            }
            return returnDevices;
        }

        internal List<TreeNode> GetDeviceDirectory(string path, DeviceData device)
        {
            SendShellCommand("mount system", device.Serial);
            List<TreeNode> treeNodes = new List<TreeNode>();
            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = @"files/adb.exe",
                    Arguments = $" -s {device.Serial} shell ls -1 {path}",
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                }
            })
            {
                proc.Start();
                proc.WaitForExit();
                List<string> directories = new List<string>();
                string line;
                while (!proc.StandardOutput.EndOfStream)
                {
                    line = proc.StandardOutput.ReadLine();
                    if (!string.IsNullOrEmpty(line))
                    {
                        directories.Add(line);
                    }
                }
                if (directories.Count == 0)
                    return treeNodes;
                if (directories.First() != path)
                    foreach (string directory in directories)
                    {
                        TreeNode node = new TreeNode(directory)
                        {
                            BackColor = Color.LightGray
                        };
                        treeNodes.Add(node);
                    }
                if (directories.Contains("adb: error: failed to get feature set: device 'shell' not found"))
                {
                    AdbClient adbClient = new AdbClient();
                    adbClient.KillAdb();
                    StartServer();
                    return GetDeviceDirectory(path, device);
                }
            }
            return treeNodes;
        }

        internal string GetAndroidVersion(string filePath)
        {
            string androidVersion = string.Empty;
            List<string> buildFileLines = File.ReadLines(filePath).ToList();
            for (int i = 0; i < buildFileLines.Count; i++)
            {
                if (buildFileLines[i].StartsWith("ro.build.version.release"))
                {
                    androidVersion = buildFileLines[i].Substring(buildFileLines[i].IndexOf("=") + 1);
                    manager.CreateLog("Android Version found: " + androidVersion);
                }
            }
            return androidVersion;
        }

        internal static string GetIMEI(DeviceData device)
        {
            string imei;
            imei = SendShellCommand(@"service call iphonesubinfo 1 | grep -o '[0-9a-f]\{8\} ' | tail -n+3 | while read a; do echo -n \\u${a:4:4}\\u${a:0:4}; done", device.Serial, true);
            if (imei == "/sbin/sh: service: not found" || imei.Contains("CANNOT LINK"))
            {
                imei = "not available";
            }
            return imei.Replace("\0", "");
        }

        internal string GetAppName(string apkPath)
        {
            string appName = "";
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/aapt2.exe",
                    Arguments = $"dump badging \"{apkPath}\"",
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
                    Arguments = $"dump badging \"{apkPath}\"",
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

        #endregion

        /// <summary>
        /// Reboots device
        /// </summary>
        /// <param name="device"></param>
        internal void RebootDevice(DeviceData device)
        {
            try
            {
                manager.CreateLog($"Rebooting {device.Serial}");
                    AdbClient adbClient = new AdbClient();
                adbClient.Reboot(device);
            }
            catch (Exception)
            {
            }
        }

        /// <summary>
        /// Reboots assigned devices
        /// modes are: recovery, bootloader, fastboot
        /// </summary>
        /// <param name="device"></param>
        internal static void RebootDevice(string mode, DeviceData device)
        {
                    AdbClient adbClient = new AdbClient();
            adbClient.Reboot(mode, device);
        }

        internal static string SendShellCommand(string command, string deviceSerial, bool _static)
        {
            string result = string.Empty;
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s {deviceSerial} shell {command}",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false

                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                result += proc.StandardOutput.ReadLine();
            }
            proc.Dispose();
            return result;
        }

        internal string SendShellCommand(string command, string deviceSerial)
        {
            string result = string.Empty;
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s {deviceSerial} shell {command}",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false

                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                result += proc.StandardOutput.ReadLine();
            }
            proc.Dispose();
            manager.CreateLog($"Shell command: {command} on {deviceSerial}", true);
            return result;
        }

        internal void MakeDirectory(string path, string deviceSerial)
        {
            string s = string.Empty;
            string folder = string.Empty;
            List<string> tmp = path.Split('/').ToList();
            folder = tmp.ElementAt(tmp.Count - 1);
            path = path.Remove(path.IndexOf(folder));
            while (!s.Contains("File exists"))
            {
                //Check if push was successful and Cancel installation if not
                SendShellCommand("mount system", deviceSerial);
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "files/adb.exe",
                        Arguments = $" -s {deviceSerial} shell mkdir {path}{folder}",
                        CreateNoWindow = true,
                        RedirectStandardOutput = true,
                        UseShellExecute = false
                    }
                };
                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    s += proc.StandardOutput.ReadLine();
                    manager.CreateLog("MakeDirectory: " + s);
                }
                proc.Dispose();
            }
        }

        static internal void StartServer()
        {
            AdbServerStatus adb = new AdbServerStatus();
            if (adb.IsRunning)
            {
                AdbClient adbClient = new AdbClient();
                adbClient.KillAdb();
            }
            AdbServer.Instance.StartServer(@"files\adb.exe", true);
        }

        static internal void StopServer()
        {
            AdbServerStatus adb = new AdbServerStatus();
            if (adb.IsRunning)
            {
                AdbClient adbClient = new AdbClient();
                adbClient.KillAdb();
            }
        }

        internal void PushFile(string source, string destination, string deviceSerial)
        {
            string fileName = string.Empty;
            List<string> tmp = source.Split(new string[] { "\\" }, StringSplitOptions.RemoveEmptyEntries).ToList();
            fileName = tmp.ElementAt(tmp.Count - 1);
            string s = string.Empty;
            string resultCode = SendShellCommand($"ls {destination}", deviceSerial);
            while (true)
            {
                if (s.Contains("No such file or directory"))
                {
                    SendShellCommand("mount system", deviceSerial);
                }
                resultCode = SendShellCommand($"ls {destination}", deviceSerial);
                if (resultCode.Contains(fileName) && !resultCode.Contains("No such file or directory") && resultCode != "")
                {
                    manager.CreateLog($"{fileName} pushed, moving on");
                    break;
                }
                //Check if push was successful and Cancel installation if not
                Process proc = new Process
                {
                    StartInfo = new ProcessStartInfo
                    {
                        FileName = "files/adb.exe",
                        Arguments = $" -s {deviceSerial} push {source} {destination}",
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        CreateNoWindow = true,
                        UseShellExecute = false
                    }
                };

                proc.Start();
                while (!proc.StandardOutput.EndOfStream)
                {
                    s = proc.StandardOutput.ReadLine();
                    manager.CreateLog("PushFile" + s, true);
                }
                proc.Dispose();
            }
        }

        internal void PullFile(string source, string destination, string deviceSerial)
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s {deviceSerial} pull {source} {destination}",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                string s = proc.StandardOutput.ReadLine();
            }
            proc.Dispose();
            proc.Close();
        }

        internal static string SendADBCommand(string command)
        {
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" {command}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            };
            proc.Start();

            string output;
            while (!proc.StandardOutput.EndOfStream)
            {
            }
            output = proc.StandardOutput.ReadToEnd();
            proc.Close();
            proc.Dispose();
            return output;
        }

        internal static string GetPackageName(string apkPath)
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
            proc.Dispose();
            return result;
        }

        internal void InstallAPK(string path, DeviceData device)
        {
            string s = string.Empty;
            string packagename = GetPackageName(path);
            Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s \"{device.Serial}\" install -r -d {path}",
                    CreateNoWindow = true,
                    RedirectStandardOutput = true,
                    UseShellExecute = false
                }
            };
            proc.Start();
            while (!proc.StandardOutput.EndOfStream)
            {
                s = proc.StandardOutput.ReadLine();
                manager.CreateLog($"Installing: {packagename} {s}");
            }
            proc.Dispose();
        }

        internal void PushFolder(string source, string destination, DeviceData device)
        {
            List<string> tmpDirectories = GetDirectories(Directory.GetCurrentDirectory().ToString() + "\\" + source);
            List<string> directories = new List<string>();

            if (tmpDirectories is null)
            {
                manager.CancelInstallation($"{source} ist nicht verfügbar.\nBitte kopieren Sie den Ordner \"{source}\" nach \"InstallationFiles\"", device.Serial, false);
                return;
            }


            //get the destination directories
            foreach (string directory in tmpDirectories)
            {
                directories.Add(directory.Substring(directory.IndexOf("SKMaps"), directory.Length - directory.IndexOf("SKMaps")));
            }
            List<string> tmpFiles = new List<string>();
            foreach (string tmpDirectory in tmpDirectories)
            {
                tmpFiles.AddRange(Directory.GetFiles(tmpDirectory).ToList());
            }
            if (directories.Count == 0)
            {
                tmpFiles = Directory.GetFiles(source).ToList();
            }
            else
            {
                manager.CreateLog("Creating directories...");
                manager.SetMarqueeProgressBar("Creating directories...");
                foreach (string directory in directories)
                {
                    CreateDirectory("sdcard/" + directory, device);
                }
            }
            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            })
            {
                int filesProcessed = 0;
                foreach (string file in tmpFiles)
                {
                    string rootFolder = source.Substring(source.LastIndexOf("\\"), source.Length - source.LastIndexOf("\\")).Replace("\\", "");
                    string path = file.Substring(file.IndexOf(rootFolder), file.Length - file.IndexOf(rootFolder)).Replace("\\", "/");
                    string fileName = file.Substring(file.LastIndexOf("\\"), file.Length - file.LastIndexOf("\\")).Replace("\\", "");
                    proc.StartInfo.Arguments = $" -s \"{ device.Serial}\" push \"{ file}\" \"{ destination}/{ path }\"";
                    proc.Start();
                    string output = string.Empty;
                    manager.CreateLog($"pushing {file}");
                    while (!proc.StandardOutput.EndOfStream)
                    {
                        output += proc.StandardOutput.ReadLine();
                        string s = output.After($"{destination}/{path}");
                    }
                    filesProcessed++;
                    int totalProgress = (int)((double)filesProcessed / tmpFiles.Count * 100);
                    manager.SetContinuousProgressBar($"Pushing {fileName}", totalProgress);
                }
                proc.Dispose();
            }
            manager.SetContinuousProgressBar("finished", 0);
            manager.FinishedCopying();
        }

        internal List<string> GetDirectories(string path)
        {
            List<string> allDirectories;
            try
            {
                List<string> directories = Directory.GetDirectories(path).ToList();
                allDirectories = Directory.GetDirectories(path).ToList();
                foreach (string directory in directories)
                {
                    allDirectories.AddRange(GetDirectories(directory));
                }
                return allDirectories;
            }
            catch (DirectoryNotFoundException)
            {
                return null;
            }

        }

        private void CreateDirectory(string inputDirectory, DeviceData device)
        {
            inputDirectory = inputDirectory.Replace("\\", "/");
            using (Process proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "files/adb.exe",
                    Arguments = $" -s \"{device.Serial}\" shell mkdir -p " + inputDirectory,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true,
                    UseShellExecute = false
                }
            })
            {
                while (true)
                {
                    //Create the directory
                    string folderName = inputDirectory.Substring(inputDirectory.LastIndexOf('/') + 1);
                    string resultCode = SendShellCommand($"ls -a {inputDirectory.Substring(0, inputDirectory.LastIndexOf('/'))}", device.Serial);
                    if (resultCode.Contains(folderName))
                    {
                        manager.CreateLog($"{inputDirectory} exists, moving on");
                        break;
                    }
                    proc.Start();
                    manager.CreateLog($"Creating {inputDirectory}");
                    manager.SetContinuousProgressBar($"Creating directory {inputDirectory}", 0);
                    while (!proc.StandardOutput.EndOfStream)
                    {
                    }
                }
                proc.Dispose();
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

        internal void SetUninstallList(List<string> inputUninstallList) => unInstallList = inputUninstallList;
    }


    class OutputReceiver : IShellOutputReceiver
    {
        /// <inheritdoc/>
        public bool ParsesErrors { get; set; }

        /// <inheritdoc/>
        public void AddOutput(string line)
        {
        }

        /// <inheritdoc/>
        public void Flush()
        {
        }
    }
}
