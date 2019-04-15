using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
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
            StartDeviceMonitor();
        }
        private void StartDeviceMonitor()
        {
            var monitor = new DeviceMonitor(new AdbSocket(new IPEndPoint(IPAddress.Loopback, AdbClient.AdbServerPort)));
            monitor.DeviceConnected += this.OnDeviceConnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.DeviceDisconnected += this.OnDeviceDisconnected;
            monitor.DeviceChanged += this.OnDeviceChanged;
            monitor.Start();
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
            this.toolStripStatusLabelDevice.Text = ("Last Event: ");
            this.toolStripStatusLabelDevice.Text += ($"The device {s} has connected to this PC");
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
            this.toolStripStatusLabelDevice.Text = ("Last Event: ");
            this.toolStripStatusLabelDevice.Text += ($"The device {s} has disconnected to this PC");
        }
        protected void OnDeviceChanged(object sender, DeviceDataEventArgs e)
        {

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
        private void ChangeDeviceStatus(string v)
        {

        }
        private void MainForm_Load(object sender, EventArgs e)
        {
            this.comboBoxRebootOptions.SelectedIndex = 0;
            if (comboBoxDevices.Items.Count > 0)
                comboBoxDevices.SelectedIndex = 0;
        }
    }
}
