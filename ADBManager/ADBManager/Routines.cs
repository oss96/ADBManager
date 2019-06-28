using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Threading;
using System.Windows.Forms;

namespace ADBManager
{
    public partial class Routines : Form
    {
        string selectedItem = string.Empty;

        private delegate void ChangeAppNameCallback();

        private string apkVersion;

        private Commands commands = new Commands();

        List<DeviceData> devices = AdbClient.Instance.GetDevices();
        readonly MainForm mainForm;

        public string ApkName { get; set; }

        /// <summary>
        /// Initializes a new instance of the <see cref="Routines"/> class.
        /// </summary>
        /// <param name="inputMainForm"></param>
        public Routines(MainForm inputMainForm)
        {
            InitializeComponent();
            MinimumSize = Size;
            mainForm = inputMainForm;
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
                ListViewItem lvi = new ListViewItem
                {
                    ImageIndex = i,
                    Text = commands[i]
                };
                listViewCommands.Items.Add(lvi);
            }
            pictureBoxCommand.Size = new Size(64, 64);
            buttonOpenFile.Left = (panelInstall.Width - buttonOpenFile.Width) / 2;
            labelApkName.Left = (panelInstall.Width - labelApkName.Width) / 2;
            pictureBoxCommand.Left = (Width - pictureBoxCommand.Width) / 2;
            labelCommand.Left = (Width - labelCommand.Width) / 2;
            comboBoxRebootOptions.Left = (panelRestart.Width - comboBoxRebootOptions.Width) / 2;
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
                    switch (comboBoxRebootOptions.SelectedIndex)
                    {
                        case 0: //Reboot
                            break;
                        case 1: //Recovery
                            break;
                        case 2: // Bootloader
                            break;
                        case 3: //fastboot
                            break;
                        default:
                            MessageBox.Show("Unknown mode", "Unknown Input");
                            break;
                    }

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
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Uninstall":
                    commands = Commands.Uninstall;
                    pictureBoxCommand.Image = Properties.Resources.Uninstall;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Shell":
                    commands = Commands.Shell;
                    pictureBoxCommand.Image = Properties.Resources.Shell;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Restart":
                    commands = Commands.Restart;
                    pictureBoxCommand.Image = Properties.Resources.Restart;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    comboBoxRebootOptions.SelectedIndex = 0;
                    break;
                case "Pull":
                    commands = Commands.Pull;
                    pictureBoxCommand.Image = Properties.Resources.Left;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
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
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                default:
                    break;
            }
            buttonAdd.Enabled = false;
            buttonCancel.Enabled = false;
        }
        private void ButtonOpenFile_Click(object sender, EventArgs e)
        {
            openFileDialogApk = new OpenFileDialog
            {
                Filter = "Android Application Package | *.apk"
            };
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
                labelApkName.Left = (panelInstall.Width - labelApkName.Width) / 2;
                labelApkName.Show();
            }
        }
        private void HandleThreadDone(object sender, EventArgs e)
        {
            ApkName = (sender as ThreadWorkerGetAppName).AppName;
            apkVersion = (sender as ThreadWorkerGetAppName).ApkVersion;
            ChangeAppName();
        }
        private void ChangeAppName()
        {
            if (InvokeRequired)
            {
                ChangeAppNameCallback callback = new ChangeAppNameCallback(ChangeAppName);
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
                labelApkName.Text = $"{ApkName}: {apkVersion}";
                labelApkName.Left = (panelInstall.Width - labelApkName.Width) / 2;
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
            if (e.Node.Nodes.Count == 0)
            {
                ADB adb = new ADB();
                DeviceData device = new DeviceData();
                foreach (DeviceData item in devices)
                {
                    if (e.Node.Parent != null)
                    {
                        string rootNode = e.Node.FullPath;
                        rootNode = rootNode.Remove(rootNode.IndexOf("\\"), rootNode.Length - rootNode.IndexOf("\\"));
                        if (rootNode.Contains(item.Serial))
                            device = item;
                    }
                    else
                    {
                        if (e.Node.Text.Contains(item.Serial))
                            device = item;
                    }
                }
                List<TreeNode> treeNodes;
                if (e.Node.Level == 0)
                {
                    treeNodes = adb.GetDeviceDirectory("", device);
                }
                else
                {
                    string path = e.Node.FullPath;
                    path = path.Remove(0, path.IndexOf("\\") + 1);
                    path = path.Replace("\\", "/");
                    treeNodes = adb.GetDeviceDirectory(path, device);
                    e.Node.BackColor = Color.White;
                }

                foreach (TreeNode node in treeNodes)
                {
                    e.Node.Nodes.Add(node);
                }
                if (treeNodes.Count == 0)
                {
                    e.Node.BackColor = Color.Red;
                    //Set Status not found
                }
                else
                {
                    e.Node.Expand();
                }
                buttonAdd.Enabled = true;
                buttonCancel.Enabled = true;
            }
        }
    }
    class ThreadWorkerGetAppName
    {
        public event EventHandler ThreadDone;
        ADB adb = new ADB();

        public string FileName { get; set; }
        public string AppName { get; set; }
        public string ApkVersion { get; set; }

        public ThreadWorkerGetAppName(string fileName)
        {
            FileName = fileName;
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
