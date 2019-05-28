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
        List<AndroidDevice> androidDevices = new List<AndroidDevice>();
        Thread changeLastStatusThread;
        readonly ADB adb;
        delegate void RefreshCallback();

        public MainForm()
        {
            InitializeComponent();
            StartDeviceMonitor();
            adb = new ADB(this);
        }


        private void StartDeviceMonitor()
        {
            var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            monitor.DeviceConnected += this.OnDeviceConnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.Start();
        }
        internal void SetLastStatus(string status)
        {
            if (changeLastStatusThread != null)
            {
                changeLastStatusThread.Abort();
                changeLastStatusThread = null;
            }
            changeLastStatusThread = new Thread(unused => ChangeLastStatus(status));
            changeLastStatusThread.Start();
        }
        internal void ChangeLastStatus(string lastStatus)
        {
            toolStripStatusLabelDevice.Text = "Last Status: ";
            toolStripStatusLabelDevice.Text += lastStatus;
        }
        internal void RefreshDevices()
        {
            if (this.InvokeRequired)
            {
                RefreshCallback callback = new RefreshCallback(RefreshDevices);
                try
                {
                    this.Invoke(callback);
                }
                catch (Exception)
                {
                    return;
                }
            }
            else
            {
                ButtonRefresh_Click(null, null);
            }

        }

        #region Events
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.comboBoxRebootOptions.SelectedIndex = 0;
            this.buttonInstall.Enabled = false;
            this.buttonReboot.Enabled = false;
            this.buttonShellCommand.Enabled = false;
            checkedListBoxDevices.CheckOnClick = true;
            RefreshDevices();
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
            RefreshDevices();
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
            RefreshDevices();
        }
        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            checkedListBoxDevices.Items.Clear();
            androidDevices = ADB.GetConnectedDevice();
            foreach (AndroidDevice item in androidDevices)
            {
                checkedListBoxDevices.Items.Add(item.Model);
            }
            checkedListBoxDevices.Sorted = true;
        }
        private void ButtonReboot_Click(object sender, EventArgs e)
        {
            List<DeviceData> devices = new List<DeviceData>();

            foreach (string checkItems in checkedListBoxDevices.CheckedItems)
            {
                foreach (AndroidDevice device in androidDevices)
                {
                    if (checkItems == device.Model)
                    {
                        devices.Add(device.GetDevice());
                    }
                }
            }
            switch (comboBoxRebootOptions.SelectedIndex)
            {
                case 0: //Reboot
                    ADB.RebootDevice(devices);
                    break;
                case 1: //Recovery
                    ADB.RebootDevice("recovery", devices);
                    break;
                case 2: // Bootloader
                    ADB.RebootDevice("bootloader", devices);
                    break;
                case 3: //fastboot
                    ADB.RebootDevice("fastboot", devices);
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
            OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Android Application Package | *.apk"
            };
            DialogResult apkFile = openFileDialog.ShowDialog();
            if (apkFile == DialogResult.OK)
            {
                string appName = adb.GetAppName(openFileDialog.FileName);
                DialogResult install = MessageBox.Show($"Are you sure you want to install \"{appName}\" on all selected devices?", "Attention!", MessageBoxButtons.YesNo);
                if (install == DialogResult.Yes)
                {
                    List<DeviceData> devices = new List<DeviceData>();
                    foreach (AndroidDevice item in androidDevices)
                    {
                        devices.Add(item.GetDevice());
                    }
                    Thread installThread = new Thread(unused => adb.InstallAPK(openFileDialog.FileName, devices,appName));
                    installThread.Start();
                }
            }
        }
        private void ButtonAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBoxDevices.Items.Count; i++)
            {
                checkedListBoxDevices.SetItemChecked(i, true);
            }
        }
        private void ButtonNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < checkedListBoxDevices.Items.Count; i++)
            {
                checkedListBoxDevices.SetItemChecked(i, false);
            }
        }
        private void CheckedListBoxDevices_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (checkedListBoxDevices.CheckedIndices.Count > 0)
            {
                this.buttonInstall.Enabled = true;
                this.buttonReboot.Enabled = true;
                this.buttonShellCommand.Enabled = true;
            }
            else
            {
                this.buttonInstall.Enabled = false;
                this.buttonReboot.Enabled = false;
                this.buttonShellCommand.Enabled = false;
            }

        }
        private void ButtonUninstall_Click(object sender, EventArgs e)
        {
            List<DeviceData> devices = new List<DeviceData>();
            foreach (AndroidDevice item in androidDevices)
            {
                devices.Add(item.GetDevice());
            }

            adb.UninstallAPP("", devices);
        }
        #endregion

    }
}
