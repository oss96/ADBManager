using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADBManager
{
    public class Manager
    {
        private List<AndroidDevice> adbDevices = new List<AndroidDevice>();
        private List<string> fastbootDevices = new List<string>();
        private delegate void RefreshCallback();
        private delegate void ChangeLastStatusCallBack(string label);
        private delegate void AddDataGridRowCallback_Fastboot();
        private delegate void RemoveDataGridRowCallback_Fastboot();
        private readonly Fastboot fastboot = new Fastboot();
        private DeviceMonitor monitor;
        private readonly MainForm mainForm;
        public event EventHandler<InstallEventArgs> InstallFailed;
        private ADB adb;
        private bool copyFinished;
        private Log log;


        public Manager()
        {
            this.mainForm = new MainForm(this);
            adb = new ADB(this);
        }

        internal void Run()
        {
            log = new Log();
            CreateLog("----- New Session ------");
            Thread startADB = new Thread(unused => ManageADB(true));
            startADB.Start();
            Thread startFastboot = new Thread(unused => ManageFastboot(true));
            startFastboot.Start();
            Thread startMonitor = new Thread(unused => ManageDeviceMonitor(true));
            startMonitor.Start();
            Application.Run(this.mainForm);
        }

        private void ManageDeviceMonitor(bool startMonitor)
        {
            while (true)
            {
                if (startMonitor)
                {
                    try
                    {
                        monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
                        monitor.DeviceConnected += OnDeviceConnected;
                        monitor.DeviceDisconnected += OnDeviceDisconnected;
                        monitor.Start();
                        break;
                    }
                    catch (System.Net.Sockets.SocketException se)
                    {
                        mainForm.SetLastStatus($"{se.Message}. Trying again...");
                        continue;
                    }
                }
                else
                {
                    if (monitor != null && monitor.IsRunning)
                    {
                        monitor = null;
                    }
                    break;
                }
            }
        }
        private void ManageFastboot(bool v)
        {
            while (true)
            {
                if (v)
                {
                    try
                    {
                        fastboot.FastbootDeviceConnected += FastbootDevice_Connected;
                        fastboot.FastbootDeviceDisconnected += FastbootDevice_Disconnected;
                        fastboot.StartWatch();
                        break;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else
                {
                    if (fastboot != null)
                    {
                        if (fastboot.startWatch != null && fastboot.startWatch.IsAlive)
                        {
                            fastboot.KillWatch();
                        }
                        fastboot.Dispose();
                    }
                    break;
                }
            }
        }
        private void ManageADB(bool v)
        {
            while (true)
            {
                if (v)
                {
                    try
                    {
                        AdbServerStatus adbServerStatus = new AdbServerStatus();
                        if (!adbServerStatus.IsRunning)
                        {
                            ADB.SendADBCommand("start-server");
                        }
                        mainForm.SetLastStatus($"connected to Port {AdbClient.AdbServerPort}");
                        break;
                    }
                    catch (Exception)
                    {
                        continue;
                    }
                }
                else
                {
                    AdbServerStatus adbServerStatus = new AdbServerStatus();
                    if (adbServerStatus.IsRunning)
                    {
                        ADB.SendADBCommand("kill-server");
                    }
                    break;
                }
            }
        }
        private void UnmountDevice(DeviceData device)
        {
            adb.SendShellCommand("rm -r /data/dalvik-cache", device.Serial);
            Thread.Sleep(100);
            adb.SendShellCommand("rm -r sdcard/TWRP", device.Serial);
            Thread.Sleep(100);
            adb.SendShellCommand("umount system", device.Serial);
            Thread.Sleep(100);
        }
        private void FastbootDevicesToRecovery()
        {
            foreach (string fastbootDevice in fastbootDevices)
            {
                fastboot.BootImage(fastbootDevice, "InstallationFiles\\recovery.img");
            }
        }
        private void ADBDevicesToFastboot(List<AndroidDevice> toFastbootDevices)
        {
            foreach (AndroidDevice device in toFastbootDevices)
            {
                ADB.RebootDevice("bootloader", device.GetDevice());
            }
        }
        private void WaitForFastbootLoad(List<string> installDevices)
        {
            bool allInFastboot = false;

            while (!allInFastboot)
            {
                fastbootDevices = GetFastbootDevices();
                List<string> notBooted = installDevices.Except(fastbootDevices).ToList();
                if (notBooted.Count == 0)
                {
                    allInFastboot = true;
                }
            }
        }
        internal void StartLog()
        {
            Thread startLog = new Thread(unused => log.ShowDialog());
            startLog.Start();
        }
        private void WaitForADBDevices(List<string> toRecoveryDevices)
        {
            bool allInRecovery = false;
            while (!allInRecovery)
            {
                List<AndroidDevice> androidDevices = ADB.GetConnectedDevices();
                List<string> androidDeviceSerialNrs = new List<string>();
                foreach (AndroidDevice androidDevice in androidDevices)
                {
                    androidDeviceSerialNrs.Add(androidDevice.Serial);
                }
                List<string> notBooted = toRecoveryDevices.Except(androidDeviceSerialNrs).ToList();
                if (notBooted.Count == 0 && androidDeviceSerialNrs.Count != 0)
                {
                    allInRecovery = true;
                }
            }
        }
        internal List<AndroidDevice> GetADBDevices()
        {
            adbDevices = ADB.GetConnectedDevices();
            return adbDevices;
        }
        internal List<string> GetFastbootDevices()
        {
            fastbootDevices = fastboot.GetDevices();
            fastbootDevices = fastbootDevices.Distinct().ToList();
            return fastbootDevices;
        }
        internal void FinishedCopying()
        {
            copyFinished = true;
        }
        private List<AndroidDevice> BringDevicesToRecovery()
        {
            mainForm.SetMarqueeProgressBar("Geräte werden vorbereitet, bitte warten...");
            List<AndroidDevice> installDevices = GetOnlineAndRecoveryADBDevices();
            List<string> adbDeviceSerialNrs = new List<string>();
            foreach (AndroidDevice androidDevice in installDevices)
            {
                adbDeviceSerialNrs.Add(androidDevice.Serial);
            }
            if (adbDeviceSerialNrs.Count > 0)
            {
                ADBDevicesToFastboot(installDevices);
                WaitForFastbootLoad(adbDeviceSerialNrs);
            }

            fastbootDevices = GetFastbootDevices();
            //if the devices all are in Fastboot mode
            if (adbDeviceSerialNrs.Count == 0 && fastbootDevices.Count != 0)
            {
                adbDeviceSerialNrs = fastbootDevices;
            }
            if (fastbootDevices.Count > 0)
            {
                FastbootDevicesToRecovery();
                WaitForADBDevices(adbDeviceSerialNrs);
            }
            installDevices = GetRecoveryADBDevices();
            return installDevices;
        }
        private List<AndroidDevice> GetRecoveryADBDevices()
        {
            this.adbDevices = ADB.GetConnectedDevices();
            List<AndroidDevice> result = new List<AndroidDevice>();
            foreach (AndroidDevice device in this.adbDevices)
            {
                if (device.State == DeviceState.Recovery)
                {
                    result.Add(device);
                }
            }
            return result;
        }
        private List<AndroidDevice> GetOnlineAndRecoveryADBDevices()
        {
            this.adbDevices = ADB.GetConnectedDevices();
            List<AndroidDevice> result = new List<AndroidDevice>();
            foreach (AndroidDevice device in this.adbDevices)
            {
                if (device.State == DeviceState.Online || device.State == DeviceState.Recovery)
                {
                    result.Add(device);
                }
            }
            return result;
        }
        private void WaitUntilAuthorized(string serial)
        {
            //Wait until authorized, then refresh devices
            List<string> androidDevices = new List<string>();
            while (!androidDevices.Contains(serial))
            {
                androidDevices = ADB.GetConnectedDeviceSerials();
            }
            mainForm.RefreshDevices();
        }
        internal void FormClosing()
        {
            ManageDeviceMonitor(false);
            ManageADB(false);
            ManageFastboot(false);
            Environment.Exit(Environment.ExitCode);
        }
        private void OnDeviceConnected(object sender, DeviceDataEventArgs e)
        {
            //when the start position is Fastboot, the devices get duplicated in Recovery mode
            AndroidDevice device = new AndroidDevice();
            List<AndroidDevice> devices = ADB.GetConnectedDevices();
            foreach (AndroidDevice item in devices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    device = item;
                    device.IMEI = ADB.GetIMEI(item.GetDevice());
                    adbDevices.Add(device);
                    mainForm.AddADBDevice(device);
                    break;
                }
            }
            if (e.Device.State == DeviceState.Unknown)
            {

            }
            else if (e.Device.State == DeviceState.Unauthorized)
            {
                MessageBox.Show("Bitte USB-Debugging auf alle Geräte erlauben!");
                Task.Run(() => WaitUntilAuthorized(e.Device.Serial));
            }
            else if (e.Device.State == DeviceState.Offline)
            {
            }
            CreateLog($"Device Connected: {e.Device.Serial}");
            CreateLog($"Device State: {e.Device.State}");
        }
        private void OnDeviceDisconnected(object sender, DeviceDataEventArgs e)
        {
            string s = "";
            AndroidDevice device = new AndroidDevice(e.Device.Model, e.Device.Name, e.Device.Product, e.Device.Serial, e.Device.State, e.Device.TransportId, ADB.GetIMEI(e.Device));
            foreach (AndroidDevice item in adbDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    if (item.IMEI != "not available")
                    {
                        s = item.Model + ": " + item.IMEI;
                    }
                    else
                    {
                        s = item.Model + ": " + item.Serial;
                    }
                }
            }
            mainForm.RemoveADBDevice(device);
        }
        private void FastbootDevice_Connected(object source, FastbootDeviceEventArgs args)
        {
            mainForm.AddDataGridRow_Fastboot(args.Device);
        }
        private void FastbootDevice_Disconnected(object source, FastbootDeviceEventArgs args)
        {
            mainForm.RemoveDataGridRow_Fastboot(args.Device);
        }
        protected virtual void OnInstallFailed(string message)
        {
            InstallFailed?.Invoke(this, new InstallEventArgs() { Message = message });
        }
        internal void SetContinuousProgressBar(string progressBarLabel, int progress)
        {
            mainForm.SetContinuousProgressBar(progressBarLabel, progress);
        }
        internal void SetMarqueeProgressBar(string progressBarLabel)
        {
            mainForm.SetMarqueeProgressBar(progressBarLabel);
        }
        internal void CreateLog(string txt)
        {
            Debug.Print(txt);
            Thread writeLog = new Thread(unused => log.newLine(txt));
            writeLog.Start();
            try
            {
                using (StreamWriter streamWriter = new StreamWriter("Log.txt", true))
                {
                    if (!File.Exists("Log.txt"))
                    {
                        File.Create("Log.txt");
                    }
                    streamWriter.WriteLine($"{DateTime.Now}: {txt}");
                    streamWriter.Close();
                }
            }
            catch (Exception)
            {
            }
        }
        /// <summary>
        /// this Log will not be written on disk, because of the size of the file
        /// </summary>
        /// <param name="txt"></param>
        /// <param name="verbose"></param>
        internal void CreateLog(string txt, bool verbose)
        {
            Debug.Print(txt);
            Thread writeLog = new Thread(unused => log.newLine(txt));
            writeLog.Start();
        }
        internal void StartURL(string url)
        {
            Process.Start(url.ToString());
        }
        internal void RestartADB()
        {
            AdbServerStatus adbServerStatus = new AdbServerStatus();
            while (!adbServerStatus.IsRunning)
            {
                ManageADB(true);
                adbServerStatus = new AdbServerStatus();
            }
        }
    }
}