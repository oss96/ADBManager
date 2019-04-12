using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace TraceMate_Install
{
    public partial class MainForm : Form
    {
        public MainForm()
        {
            InitializeComponent();
        }

        private void Button1_Click(object sender, EventArgs e)
        {
            richTextBoxDevices.Clear();
            List<AndroidDevice> androidDevices = ADB.GetConnectedDevice();
            foreach (AndroidDevice item in androidDevices)
            {
                richTextBoxDevices.Text += item.Model;
                richTextBoxDevices.Text += "\n";
                richTextBoxDevices.Text += item.Name;
                richTextBoxDevices.Text += "\n";
                richTextBoxDevices.Text += item.Serial;
                richTextBoxDevices.Text += "\n";
                richTextBoxDevices.Text += item.State;
                richTextBoxDevices.Text += "\n";
                richTextBoxDevices.Text += item.TransportID;
                richTextBoxDevices.Text += "\n";
                richTextBoxDevices.Text += "--------------";
                richTextBoxDevices.Text += "\n";
            }
        }

        private void ButtonReboot_Click(object sender, EventArgs e)
        {
            List<AndroidDevice> androidDevices = ADB.GetConnectedDevice();
            DeviceData device = androidDevices.First().GetDevice();
            ADB.RebootDevice(device);//modes are: recovery, bootloader, fastboot
        }

        private void ButtonShellCommand_Click(object sender, EventArgs e)
        {
            List<AndroidDevice> androidDevices = ADB.GetConnectedDevice();
            DeviceData device = androidDevices.First().GetDevice();
            ADB.SendShellCommand(@"ls", device);
        }
    }
}
