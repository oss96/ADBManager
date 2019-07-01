namespace ADBManager
{
    using SharpAdbClient;
    using System;
    using System.Collections.Generic;
    using System.Net;
    using System.Threading;
    using System.Windows.Forms;

    public partial class MainForm : Form
    {
        private List<AndroidDevice> adbAndroidDevices = new List<AndroidDevice>();
        private List<AndroidDevice> adbWorkDevices = new List<AndroidDevice>();
        private Thread changeLastStatusThread;
        private readonly ADB adb;
        private List<string> fastbootDevices = new List<string>();
        private List<string> fastbootWorkDevices = new List<string>();

        private delegate void RefreshCallback();

        private delegate void AddDataGridRowCallback_Fastboot();

        private delegate void RemoveDataGridRowCallback_Fastboot();

        private Fastboot fastboot = new Fastboot();

        public string NewDevice { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="MainForm"/> class.
        /// </summary>
        public MainForm()
        {
            InitializeComponent();
            MinimumSize = Size;
            MaximumSize = Size;
            adb = new ADB(this);
            StartDeviceMonitor();
        }


        internal void StartDeviceMonitor()
        {
            try
            {
                adb.StartServer();
                DeviceMonitor monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
                monitor.DeviceConnected += OnDeviceConnected;
                monitor.DeviceDisconnected += OnDeviceDisconnected;
                monitor.Start();
                fastboot.FastbootDeviceConnected += FastbootDevice_Connected;
                fastboot.FastbootDeviceDisconnected += FastbootDevice_Disconnected;
                fastboot.StartWatch();
            }
            catch (System.Net.Sockets.SocketException se)
            {
                MessageBox.Show(se.Message);
            }
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
            if (InvokeRequired)
            {
                RefreshCallback callback = new RefreshCallback(RefreshDevices);
                try
                {
                    Invoke(callback);
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
        internal void AddDataGridRow_Fastboot()
        {
            if (dataGridViewFastboot.InvokeRequired)
            {
                AddDataGridRowCallback_Fastboot callback = new AddDataGridRowCallback_Fastboot(AddDataGridRow_Fastboot);
                try
                {
                    Invoke(callback);
                }
                catch (Exception)
                {
                    return;
                }
            }
            else
            {
                AddRowFastboot(NewDevice);
            }
        }
        internal void RemoveDataGridRow_Fastboot()
        {
            if (dataGridViewFastboot.InvokeRequired)
            {
                RemoveDataGridRowCallback_Fastboot callback = new RemoveDataGridRowCallback_Fastboot(RemoveDataGridRow_Fastboot);
                try
                {
                    Invoke(callback);
                }
                catch (Exception)
                {
                    return;
                }
            }
            else
            {
                RemoveRowFastboot(NewDevice);
            }
        }
        internal void RemoveRowFastboot(string device)
        {
            foreach (DataGridViewRow row in dataGridViewFastboot.Rows)
            {
                if ((string)row.Cells[1].Value == device)
                {
                    dataGridViewFastboot.Rows.Remove(row);
                    break;
                }
            }
        }
        internal void AddRowFastboot(string device)
        {
            dataGridViewFastboot.Rows.Add(false, device);
        }
        internal void RebootADBDevice(List<DeviceData> devices)
        {
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

        #region Events
        private void MainForm_Load(object sender, EventArgs e)
        {
            comboBoxRebootOptions.SelectedIndex = 0;
            buttonInstall.Enabled = false;
            buttonUninstall.Enabled = false;
            buttonReboot.Enabled = false;
            buttonShellCommand.Enabled = false;
            buttonADBNone.Enabled = false;
            buttonFastbootNone.Enabled = false;
            RefreshDevices();
        }
        internal void OnDeviceConnected(object sender, DeviceDataEventArgs e)
        {
            adbAndroidDevices = ADB.GetConnectedDevice();
            string s = "";
            foreach (AndroidDevice item in adbAndroidDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    s = item.Model;
                }
            }
            ChangeLastStatus($"The device {s} has connected to this PC via ADB");
            RefreshDevices();
        }
        internal void OnDeviceDisconnected(object sender, DeviceDataEventArgs e)
        {
            string s = "";
            foreach (AndroidDevice item in adbAndroidDevices)
            {
                if (item.Serial == e.Device.Serial)
                {
                    s = $"{item.Model} ({item.Serial})";
                }
            }
            ChangeLastStatus($"The device {s} has disconnected from this PC via ADB");
            RefreshDevices();
        }
        private void FastbootDevice_Connected(object source, FastbootDeviceEventArgs args)
        {
            NewDevice = args.Device;
            AddDataGridRow_Fastboot();
            ChangeLastStatus($"The device {args.Device} has connected to this PC via Fastboot");
        }
        private void FastbootDevice_Disconnected(object source, FastbootDeviceEventArgs args)
        {
            NewDevice = args.Device;
            RemoveDataGridRow_Fastboot();
            ChangeLastStatus($"The device {args.Device} has disconnected to this PC via Fastboot");
        }
        private void ButtonRefresh_Click(object sender, EventArgs e)
        {
            dataGridViewADB.Rows.Clear();
            dataGridViewFastboot.Rows.Clear();
            adbAndroidDevices = ADB.GetConnectedDevice();
            fastbootDevices = fastboot.GetDevices();
            foreach (AndroidDevice item in adbAndroidDevices)
            {
                _ = dataGridViewADB.Rows.Add(false, item.Model, item.Serial);
            }
            foreach (string device in fastbootDevices)
            {
                _ = dataGridViewFastboot.Rows.Add(false, device);
            }
            dataGridViewADB.ClearSelection();
            dataGridViewFastboot.ClearSelection();
            buttonInstall.Enabled = false;
            buttonUninstall.Enabled = false;
            buttonReboot.Enabled = false;
            buttonShellCommand.Enabled = false;
        }
        private void ButtonReboot_Click(object sender, EventArgs e)
        {
            if (adbWorkDevices.Count != 0)
            {
                List<DeviceData> devices = new List<DeviceData>();
                foreach (AndroidDevice device in adbWorkDevices)
                {
                    devices.Add(device.GetDevice());
                }
                RebootADBDevice(devices);
            }
            else if (fastbootDevices.Count != 0)
            {
                DialogResult r = MessageBox.Show("Due to limitations of Fastboot you only can restart all devices together. Do you want to Restart all devices?",
                    "Warning", MessageBoxButtons.OKCancel, MessageBoxIcon.Information, MessageBoxDefaultButton.Button1);
                if (r == DialogResult.OK)
                    fastboot.RebootDevices();
            }
        }
        private void ButtonShellCommand_Click(object sender, EventArgs e)
        {
            foreach (AndroidDevice device in adbWorkDevices)
            {
                ADB.SendShellCommand(richTextBoxShell.Text, device.GetDevice());
            }
            richTextBoxShell.Clear();
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
                        foreach (AndroidDevice item in adbWorkDevices)
                        {
                            devices.Add(item.GetDevice());
                        }
                        Thread installThread = new Thread(unused => adb.InstallAPK(openFileDialog.FileName, devices, appName));
                        installThread.Start();
                    }
                }
            }
        }
        private void ButtonADBAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewADB.Rows.Count; i++)
            {
                dataGridViewADB.Rows[i].Cells[0].Value = true;
            }
            buttonFastbootNone.PerformClick();
            buttonInstall.Enabled = true;
            buttonUninstall.Enabled = true;
            buttonReboot.Enabled = true;
            buttonShellCommand.Enabled = true;
        }
        private void ButtonADBNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewADB.Rows.Count; i++)
            {
                dataGridViewADB.Rows[i].Cells[0].Value = false;
            }
            buttonInstall.Enabled = false;
            buttonUninstall.Enabled = false;
            buttonReboot.Enabled = false;
            buttonShellCommand.Enabled = false;
            buttonFastbootNone.Enabled = false;
            buttonADBNone.Enabled = false;
        }
        private void ButtonFastbootAll_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewFastboot.Rows.Count; i++)
            {
                dataGridViewFastboot.Rows[i].Cells[0].Value = true;
            }
            buttonADBNone.PerformClick();
            buttonInstall.Enabled = true;
            buttonUninstall.Enabled = true;
            buttonReboot.Enabled = true;
            buttonShellCommand.Enabled = true;
        }
        private void ButtonFastbootNone_Click(object sender, EventArgs e)
        {
            for (int i = 0; i < dataGridViewFastboot.Rows.Count; i++)
            {
                dataGridViewFastboot.Rows[i].Cells[0].Value = false;
            }
            buttonInstall.Enabled = false;
            buttonUninstall.Enabled = false;
            buttonReboot.Enabled = false;
            buttonShellCommand.Enabled = false;
            buttonFastbootNone.Enabled = false;
            buttonADBNone.Enabled = false;
        }
        private void ButtonUninstall_Click(object sender, EventArgs e)
        {
            List<DeviceData> devices = new List<DeviceData>();
            foreach (AndroidDevice item in adbWorkDevices)
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
            fastboot.Dispose();
        }
        private void DataGridViewADB_CellClick(object sender, DataGridViewCellEventArgs e)
        {
            if (e.RowIndex >= 0)
            {
                if ((bool)(sender as DataGridView).Rows[e.RowIndex].Cells[0].Value)
                    (sender as DataGridView).Rows[e.RowIndex].Cells[0].Value = false;
                else
                    (sender as DataGridView).Rows[e.RowIndex].Cells[0].Value = true;
                (sender as DataGridView).ClearSelection();
                foreach (DataGridViewRow item in (sender as DataGridView).Rows)
                {
                    if ((bool)item.Cells[0].Value)
                    {
                        if ((sender as DataGridView).ColumnCount > 2)
                        {
                            buttonFastbootNone.PerformClick();
                            buttonADBNone.Enabled = true;
                            fastbootDevices = new List<string>();
                        }
                        else
                        {
                            buttonADBNone.PerformClick();
                            buttonFastbootNone.Enabled = true;
                            adbWorkDevices = new List<AndroidDevice>();
                        }
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
                        if ((sender as DataGridView).ColumnCount == 3)
                        {
                            buttonADBNone.Enabled = false;
                        }
                        else if ((sender as DataGridView).ColumnCount == 2)
                        {
                            buttonFastbootNone.Enabled = false;
                        }
                    }
                }
            }
        }
        private void DataGridViewADB_CellValueChanged(object sender, DataGridViewCellEventArgs e)
        {
            adbWorkDevices.Clear();
            List<string> cells = new List<string>();
            foreach (DataGridViewRow row in (sender as DataGridView).Rows)
            {
                if ((bool)row.Cells[0].Value)
                {
                    if ((sender as DataGridView).ColumnCount > 2)
                    {
                        cells.Add(row.Cells[2].Value.ToString());
                    }
                    else
                    {
                        cells.Add(row.Cells[1].Value.ToString());
                    }
                }
            }
            if ((sender as DataGridView).ColumnCount == 3)
            {
                foreach (AndroidDevice adbDevice in adbAndroidDevices)
                {
                    if (cells.Contains(adbDevice.Serial))
                    {
                        adbWorkDevices.Add(adbDevice);
                    }
                }
            }
            else if ((sender as DataGridView).ColumnCount == 2)
            {
                foreach (string fastbootDevice in fastboot.GetDevices())
                {
                    if (cells.Contains(fastbootDevice))
                    {
                        fastbootWorkDevices.Add(fastbootDevice);
                    }
                }
            }
        }
        private void ButtonAddRoutine_Click(object sender, EventArgs e)
        {
            Routines routines = new Routines(this);
            routines.ShowDialog();
        }
        private void ButtonPush_Click(object sender, EventArgs e)
        {

        }
        #endregion
    }
}
