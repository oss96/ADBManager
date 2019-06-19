using SharpAdbClient;
using SharpAdbClient.DeviceCommands;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADBManager
{
    public partial class Routines : Form
    {
        string selectedItem = string.Empty;
        private delegate void ChangeAppNameCallback();
        string apkName;
        string apkVersion;
        Commands commands = new Commands();
        List<DeviceData> devices = AdbClient.Instance.GetDevices();

        public Routines()
        {
            InitializeComponent();
            this.MinimumSize = this.Size;
        }

        private void Routines_Load(object sender, EventArgs e)
        {
            ImageList imageList = new ImageList();
            imageList.Images.Add(Properties.Resources.Install);
            imageList.Images.Add(Properties.Resources.Uninstall);
            imageList.Images.Add(Properties.Resources.Shell);
            imageList.Images.Add(Properties.Resources.Restart);
            imageList.Images.Add(Properties.Resources.Left);
            imageList.Images.Add(Properties.Resources.Right);
            imageList.ImageSize = new Size(32, 32);
            listViewCommands.LargeImageList = imageList;
            string[] commands = new string[] { "Install", "Uninstall", "Shell", "Restart", "Pull", "Push" };
            for (int i = 0; i < imageList.Images.Count; i++)
            {
                ListViewItem lvi = new ListViewItem();
                lvi.ImageIndex = i;
                lvi.Text = commands[i];
                listViewCommands.Items.Add(lvi);
            }
            pictureBoxCommand.Size = new Size(64, 64);
            buttonOpenFile.Left = (this.panelInstall.Width - buttonOpenFile.Width) / 2;
            labelApkName.Left = (this.panelInstall.Width - labelApkName.Width) / 2;
            pictureBoxCommand.Left = (this.Width - pictureBoxCommand.Width) / 2;
            labelCommand.Left = (this.Width - labelCommand.Width) / 2;
        }
        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            switch (commands)
            {
                case Commands.Install:
                    openFileDialogApk = new OpenFileDialog();
                    labelApkName.Text = "Loading Name...";
                    labelApkName.Hide();
                    buttonOpenFile.Focus();
                    break;
                case Commands.Uninstall:
                    break;
                case Commands.Shell:
                    richTextBoxShell.Clear();
                    richTextBoxShell.Focus();
                    break;
                case Commands.Restart:
                    break;
                case Commands.Pull:
                    break;
                case Commands.Push:
                    break;
                default:
                    break;
            }
            buttonAdd.Enabled = false;
            buttonCancel.Enabled = false;
        }
        private void ListViewCommands_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            selectedItem = e.Item.Text;
            switch (selectedItem)
            {
                case "Install":
                    commands = Commands.Install;
                    pictureBoxCommand.Image = Properties.Resources.Install;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Uninstall":
                    commands = Commands.Uninstall;
                    pictureBoxCommand.Image = Properties.Resources.Uninstall;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Shell":
                    commands = Commands.Shell;
                    pictureBoxCommand.Image = Properties.Resources.Shell;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Restart":
                    commands = Commands.Restart;
                    pictureBoxCommand.Image = Properties.Resources.Restart;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Pull":
                    commands = Commands.Pull;
                    pictureBoxCommand.Image = Properties.Resources.Left;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    foreach (DeviceData device in devices)
                    {
                        treeViewDeviceTree.Nodes.Add($"{device.Model} {device.Serial}");
                    }
                    break;
                case "Push":
                    commands = Commands.Push;
                    pictureBoxCommand.Image = Properties.Resources.Right;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (this.Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                default:
                    break;
            }
        }
        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            openFileDialogApk = new OpenFileDialog();
            openFileDialogApk.Filter = "Android Application Package | *.apk";
            DialogResult r = openFileDialogApk.ShowDialog();

            if (r == DialogResult.OK)
            {
                buttonAdd.Enabled = true;
                buttonCancel.Enabled = true;
                ThreadWorkerGetAppName worker = new ThreadWorkerGetAppName(openFileDialogApk.FileName);
                worker.ThreadDone += HandleThreadDone;
                Thread threadGetAppName = new Thread(worker.Run);
                threadGetAppName.Start();
                labelApkName.Text = "Loading Name...";
                labelApkName.Left = (this.panelInstall.Width - labelApkName.Width) / 2;
                labelApkName.Show();
            }
        }
        private void HandleThreadDone(object sender, EventArgs e)
        {
            // You should get the idea this is just an example
            apkName = (sender as ThreadWorkerGetAppName).AppName;
            apkVersion = (sender as ThreadWorkerGetAppName).ApkVersion;
            ChangeAppName();
        }
        private void ChangeAppName()
        {
            if (this.InvokeRequired)
            {
                ChangeAppNameCallback callback = new ChangeAppNameCallback(ChangeAppName);
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
                labelApkName.Text = $"{apkName}: {apkVersion}";
                labelApkName.Left = (this.panelInstall.Width - labelApkName.Width) / 2;
            }
        }
        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            switch (commands)
            {
                case Commands.Install:
                    openFileDialogApk = new OpenFileDialog();
                    labelApkName.Text = "Loading Name...";
                    labelApkName.Hide();
                    buttonOpenFile.Focus();
                    break;
                case Commands.Uninstall:
                    break;
                case Commands.Shell:
                    richTextBoxShell.Clear();
                    richTextBoxShell.Focus();
                    break;
                case Commands.Restart:
                    break;
                case Commands.Pull:
                    break;
                case Commands.Push:
                    break;
                default:
                    break;
            }
            buttonAdd.Enabled = false;
            buttonCancel.Enabled = false;
        }
        private void RichTextBoxShell_TextChanged(object sender, EventArgs e)
        {
            if (richTextBoxShell.Text != "")
            {
                buttonAdd.Enabled = true;
                buttonCancel.Enabled = true;
            }
            else
            {
                buttonAdd.Enabled = false;
                buttonCancel.Enabled = false;
            }
        }
        private void ShowPanel(Commands command)
        {
            switch (command)
            {
                case Commands.Install:
                    panelInstall.Show();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Commands.Uninstall:
                    panelInstall.Hide();
                    panelUninstall.Show();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Commands.Shell:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Show();
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Commands.Restart:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Show();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Commands.Pull:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPull.Show();
                    panelPush.Hide();
                    break;
                case Commands.Push:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPush.Show();
                    panelPull.Hide();
                    break;
                default:
                    break;
            }
        }

        private void TreeViewDeviceTree_NodeMouseClick(object sender, TreeNodeMouseClickEventArgs e)
        {
            ADB adb = new ADB();
            DeviceData device = new DeviceData();
            foreach (DeviceData item in devices)
            {
                if (e.Node.Text.Contains(item.Serial))
                {
                    device = item;
                }
            }
            List<TreeNode> treeNodes = adb.GetDeviceDirectory("", device);
            foreach (TreeNode node in treeNodes)
            {
                e.Node.Nodes.Add(node);
            }
            e.Node.Expand();
        }
    }

    class ThreadWorkerGetAppName
    {
        public event EventHandler ThreadDone;
        string appName;
        ADB adb = new ADB();
        private string fileName;
        private string apkVersion;

        public string FileName { get => fileName; set => fileName = value; }
        public string AppName { get => appName; set => appName = value; }
        public string ApkVersion { get => apkVersion; set => apkVersion = value; }

        public ThreadWorkerGetAppName(string fileName)
        {
            this.FileName = fileName;
        }

        public void Run()
        {
            // Do a task
            ApkVersion = adb.GetAPKVersion(FileName);
            AppName = adb.GetAppName(FileName);
            ThreadDone?.Invoke(this, EventArgs.Empty);
        }
    }
}
