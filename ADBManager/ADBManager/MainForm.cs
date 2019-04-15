using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Linq.Expressions;
using System.Net;
using System.Reflection;
using System.Security;
using System.Security.Permissions;
using System.Threading;
using System.Windows.Forms;

namespace ADBManager
{
    public partial class MainForm : Form
    {
        ADB adb = new ADB();
        List<AndroidDevice> androidDevices = new List<AndroidDevice>();


        public MainForm()
        {
            InitializeComponent();
            ButtonRefresh_Click(null, null);
            StartDeviceMonitor();
        }


        private void StartDeviceMonitor()
        {
            var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            monitor.DeviceConnected += this.OnDeviceConnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.Start();
        }
        private void InstallAPK(string path, bool reinstall, DeviceData device)
        {
            PackageManager manager = new PackageManager(device);
            Dictionary<string, string> packages = manager.Packages;
            string packageName = GetPackageName(path);
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "adb.exe",
                    Arguments = $"-s {device.Serial} install {path}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();
            Thread changeLastStatusThread = new Thread(unused => ChangeLastStatus("Installing apk..."));
            changeLastStatusThread.Start();

        }

        private string GetPackageName(string apkPath)
        {
            //.\aapt2.exe dump badging path
            var proc = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "aapt2.exe",
                    Arguments = $"dump badging {apkPath}",
                    UseShellExecute = false,
                    RedirectStandardOutput = true,
                    CreateNoWindow = true
                }
            };
            proc.Start();


            while (!proc.StandardOutput.EndOfStream)
            {
                string line = proc.StandardOutput.ReadLine();

            }

            return "";
        }

        private void InstallAPK(string path, bool reinstall, List<DeviceData> devices)
        {
            foreach (DeviceData item in devices)
            {
                PackageManager manager = new PackageManager(item);
                Dictionary<string, string> packages = manager.Packages;
                ProcessStartInfo processStartInfo = new ProcessStartInfo();
                processStartInfo.FileName = "adb";
                processStartInfo.Arguments = $"-s {item.Serial} install {path}";
                processStartInfo.CreateNoWindow = true;
                processStartInfo.UseShellExecute = false;
                Process.Start(processStartInfo);
            }
        }
        private void ChangeLastStatus(string lastStatus)
        {
            toolStripStatusLabelDevice.Text = "Last Status: ";
            toolStripStatusLabelDevice.Text += lastStatus;
        }
        #region Events
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.comboBoxRebootOptions.SelectedIndex = 0;
            if (comboBoxDevices.Items.Count > 0)
                comboBoxDevices.SelectedIndex = 0;
        }
        internal void OnDeviceConnected(object sender, DeviceDataEventArgs e)
        {
            androidDevices = ADB.GetConnectedDevice();
            string s = "";
            foreach (AndroidDevice item in androidDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    s = item.Model;
                }
            }
            ChangeLastStatus($"The device {s} has connected to this PC");
        }
        protected void OnDeviceDisconnected(object sender, DeviceDataEventArgs e)
        {
            string s = "";
            foreach (AndroidDevice item in androidDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    s = item.Model;
                }
            }
            ChangeLastStatus($"The device {s} has disconnected to this PC");
        }
        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            comboBoxDevices.Items.Clear();
            androidDevices = ADB.GetConnectedDevice();
            foreach (AndroidDevice item in androidDevices)
            {
                comboBoxDevices.Items.Add(item.Model);
            }
            comboBoxDevices.Sorted = true;
            if (comboBoxDevices.Items.Count > 0)
                comboBoxDevices.SelectedIndex = 0;
        }
        private void ButtonReboot_Click(object sender, EventArgs e)
        {
            DeviceData device = new DeviceData();
            foreach (AndroidDevice item in androidDevices)
            {
                if (comboBoxDevices.Text == item.Model)
                {
                    device = item.GetDevice();
                }
            }
            switch (comboBoxRebootOptions.SelectedIndex)
            {
                case 0: //Reboot
                    ADB.RebootDevice(device);//modes are: recovery, bootloader, fastboot
                    break;
                case 1: //Recovery
                    ADB.RebootDevice("recovery", device);//modes are: recovery, bootloader, fastboot
                    break;
                case 2: // Bootloader
                    ADB.RebootDevice("bootloader", device);//modes are: recovery, bootloader, fastboot
                    break;
                case 3: //fastboot
                    ADB.RebootDevice("fastboot", device);//modes are: recovery, bootloader, fastboot
                    break;
                default:
                    MessageBox.Show("Unknown mode", "Unknown Input");
                    break;
            }
        }
        private void ButtonShellCommand_Click(object sender, EventArgs e)
        {
            List<AndroidDevice> androidDevices = ADB.GetConnectedDevice();
            DeviceData device = androidDevices.First().GetDevice();
            ADB.SendShellCommand(this.textBoxShellCommand.Text, device);
            this.textBoxShellCommand.Clear();
        }
        private void ButtonInstall_Click(object sender, EventArgs e)
        {
            OpenFileDialog openFileDialog = new OpenFileDialog();
            openFileDialog.Filter = "Android Application Package | *.apk";
            DialogResult apkFile = openFileDialog.ShowDialog();
            if (apkFile == DialogResult.OK)
            {
                if (!checkBoxAllDevices.Checked)
                {
                    DialogResult install = MessageBox.Show($"Are you sure you want to install {openFileDialog.SafeFileName} on {comboBoxDevices.Text}?", "Attention!", MessageBoxButtons.YesNo);
                    if (install == DialogResult.Yes)
                    {
                        foreach (AndroidDevice item in androidDevices)
                        {
                            if (comboBoxDevices.Text == item.Model)
                            {
                                //InstallAPK(openFileDialog.FileName, false, item.GetDevice());
                                Thread installThread = new Thread(unused => InstallAPK(openFileDialog.FileName, false, item.GetDevice()));
                                installThread.Start();
                            }
                        }
                    }
                }
                else
                {
                    DialogResult install = MessageBox.Show($"Are you sure you want to install {openFileDialog.SafeFileName} on all connected devices?", "Attention!", MessageBoxButtons.YesNo);
                    if (install == DialogResult.Yes)
                    {
                        List<DeviceData> devices = new List<DeviceData>();
                        foreach (AndroidDevice item in androidDevices)
                        {
                            devices.Add(item.GetDevice());
                        }
                        InstallAPK(openFileDialog.FileName, false, devices);
                    }

                }
            }
        }
        #endregion
    }
}
