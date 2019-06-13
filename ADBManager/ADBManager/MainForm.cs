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
        readonly List<AndroidDevice> workDevices = new List<AndroidDevice>();
        Thread changeLastStatusThread;
        readonly ADB adb;
        delegate void RefreshCallback();

        public MainForm()
        {
            InitializeComponent();
            adb = new ADB(this);
            this.StartDeviceMonitor();
        }

        internal void StartDeviceMonitor()
        {
            try
            {
                adb.StartServer();
                DeviceMonitor monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
                monitor.DeviceConnected += this.OnDeviceConnected;
                monitor.DeviceDisconnected += this.OnDeviceDisconnected;
                monitor.Start();
                Fastboot fastboot = new Fastboot();
                fastboot.StartFastbootWatch();
                fastboot.FastbootDeviceConnected += OnFastbootDeviceConnected;
                fastboot.FastbootDeviceConnected += OnFastbootDeviceDisconnected;
            }
            catch (System.Net.Sockets.SocketException se)
            {
                MessageBox.Show(se.Message);
            }
        }
        public void OnFastbootDeviceConnected(object source, FastbootDeviceEventArgs args)
        {

        }
        public void OnFastbootDeviceDisconnected(object source, FastbootDeviceEventArgs args)
        {

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
            comboBoxRebootOptions.SelectedIndex = 0;
            buttonInstall.Enabled = false;
            buttonUninstall.Enabled = false;
            buttonReboot.Enabled = false;
            buttonShellCommand.Enabled = false;
            dataGridViewADB.ClearSelection();
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
        internal void OnDeviceDisconnected(object sender, DeviceDataEventArgs e)
        {
            string s = "";
            foreach (AndroidDevice item in androidDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    s = $"{item.Model} ({item.Serial})";
                }
            }
            ChangeLastStatus($"The device {s} has disconnected from this PC");
            RefreshDevices();
        }
        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            dataGridViewADB.Rows.Clear();
            dataGridViewFastboot.Rows.Clear();
            androidDevices = ADB.GetConnectedDevice();
            foreach (AndroidDevice item in androidDevices)
            {
                _ = dataGridViewADB.Rows.Add(false, item.Model, item.Serial);
            }
            dataGridViewADB.ClearSelection();
            this.buttonInstall.Enabled = false;
            this.buttonUninstall.Enabled = false;
            this.buttonReboot.Enabled = false;
            this.buttonShellCommand.Enabled = false;
        }
        private void ButtonReboot_Click(object sender, EventArgs e)
        {
            List<DeviceData> devices = new List<DeviceData>();
            foreach (AndroidDevice device in workDevices)
            {
                devices.Add(device.GetDevice());
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
            using (OpenFileDialog openFileDialog = new OpenFileDialog
            {
                Filter = "Android Application Package | *.apk"
            })
            {
                DialogResult apkFile = openFileDialog.ShowDialog();
                if (apkFile == DialogResult.OK)
                {
                    string appName = adb.GetAppName(openFileDialog.FileName);
                    DialogResult install = MessageBox.Show($"Are you sure you want to install \"{appName}\" on all selected devices?", "Attention!", MessageBoxButtons.YesNo);
                    if (install == DialogResult.Yes)
                    {
                        List<DeviceData> devices = new List<DeviceData>();
                        foreach (AndroidDevice item in workDevices)
                        {
                            devices.Add(item.GetDevice());
                        }
                        Thread installThread = new Thread(unused => adb.InstallAPK(openFileDialog.FileName, devices, appName));
                        installThread.Start();
                    }
                }
            }
        }
        private void ButtonAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewADB.Rows.Count; i++)
            {
                dataGridViewADB.Rows[i].Cells[0].Value = true;
            }
            dataGridViewADB.ClearSelection();
            this.buttonInstall.Enabled = true;
            this.buttonUninstall.Enabled = true;
            this.buttonReboot.Enabled = true;
            this.buttonShellCommand.Enabled = true;

        }
        private void ButtonNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewADB.Rows.Count; i++)
            {
                dataGridViewADB.Rows[i].Cells[0].Value = false;
            }
            this.buttonInstall.Enabled = false;
            this.buttonUninstall.Enabled = false;
            this.buttonReboot.Enabled = false;
            this.buttonShellCommand.Enabled = false;
        }
        private void ButtonUninstall_Click(object sender, EventArgs e)
        {
            List<DeviceData> devices = new List<DeviceData>();
            foreach (AndroidDevice item in workDevices)
            {
                devices.Add(item.GetDevice());
            }
            adb.UninstallAPP(devices);
        }
        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            AdbServerStatus adb = new AdbServerStatus();
            if (adb.IsRunning)
            {
                AdbClient.Instance.KillAdb();
            }
        }
        private void DataGridViewADB_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if ((bool)dataGridViewADB.Rows[e.RowIndex].Cells[0].Value)
                dataGridViewADB.Rows[e.RowIndex].Cells[0].Value = false;
            else
                dataGridViewADB.Rows[e.RowIndex].Cells[0].Value = true;
            dataGridViewADB.ClearSelection();
            foreach (DataGridViewRow item in dataGridViewADB.Rows)
            {
                if ((bool)item.Cells[0].Value)
                {
                    buttonInstall.Enabled = true;
                    buttonUninstall.Enabled = true;
                    buttonReboot.Enabled = true;
                    buttonShellCommand.Enabled = true;
                    break;
                }
                else
                {
                    buttonInstall.Enabled = false;
                    buttonUninstall.Enabled = false;
                    buttonReboot.Enabled = false;
                    buttonShellCommand.Enabled = false;
                }
            }
        }
        private void DataGridViewADB_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            workDevices.Clear();
            List<string> cells = new List<string>();
            foreach (DataGridViewRow row in dataGridViewADB.Rows)
            {
                if ((bool)row.Cells[0].Value)
                {
                    cells.Add(row.Cells[2].Value.ToString());
                }
            }
            foreach (AndroidDevice device in androidDevices)
            {
                if (cells.Contains(device.Serial))
                {
                    workDevices.Add(device);
                }
            }
        }
        #endregion

    }
}
