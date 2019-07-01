using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using System.Windows.Forms;

namespace ADBManager
{
    public partial class Routines : Form
    {

        string selectedItem = string.Empty;
        private delegate void ChangeAppNameCallback();
        private string apkVersion;
        private Enums.Commands commands = new Enums.Commands();
        List<DeviceData> devices = AdbClient.Instance.GetDevices();
        readonly MainForm mainForm;
        public string ApkName { get; set; }
        ImageList imageList = new ImageList();
        private string apkPath;
        List<RoutineItem> routineItems = new List<RoutineItem>();


        /// <summary>
        /// Initializes a new instance of the <see cref="Routines"/> class.
        /// </summary>
        /// <param name="inputMainForm"></param>
        public Routines(MainForm inputMainForm)
        {
            InitializeComponent();
            MinimumSize = new Size(816, 489);
            mainForm = inputMainForm;
        }


        #region Events
        private void Routines_Load(object sender, EventArgs e)
        {
            listViewCommands.SendToBack();
            listViewRoutine.SendToBack();
            imageList.Images.Add(Properties.Resources.Install);
            imageList.Images.Add(Properties.Resources.Uninstall);
            imageList.Images.Add(Properties.Resources.Shell);
            imageList.Images.Add(Properties.Resources.Restart);
            imageList.Images.Add(Properties.Resources.Pull);
            imageList.Images.Add(Properties.Resources.Push);
            imageList.ImageSize = new Size(32, 32);
            listViewCommands.LargeImageList = imageList;
            listViewRoutine.LargeImageList = imageList;
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
            pictureBoxCommand.BackColor = Color.Transparent;
            buttonOpenFile.Left = (panelInstall.Width - buttonOpenFile.Width) / 2;
            labelApkName.Left = (panelInstall.Width - labelApkName.Width) / 2;
            pictureBoxCommand.Left = (Width - pictureBoxCommand.Width) / 2;
            labelCommand.Left = (Width - labelCommand.Width) / 2;
            comboBoxRebootOptions.Left = (panelRestart.Width - comboBoxRebootOptions.Width) / 2;
        }
        private void ButtonAdd_Click(object sender, EventArgs e)
        {
            RoutineItem routineItem = new RoutineItem();
            switch (commands)
            {
                case Enums.Commands.Install:
                    routineItem = new RoutineItem
                    {
                        ItemType = Enums.RoutineItemType.Install,
                        Name = $"{ApkName}: {apkVersion}"
                    };
                    break;
                case Enums.Commands.Uninstall:
                    AppList appList = new AppList();
                    appList.ShowDialog();
                    break;
                case Enums.Commands.Shell:
                    routineItem = new RoutineItem
                    {
                        ItemType = Enums.RoutineItemType.Shell,
                        Name = $"Shell Command: {richTextBoxShell.Text.Substring(0, richTextBoxShell.Text.IndexOf(" "))}",
                        Value = richTextBoxShell.Text
                    };
                    richTextBoxShell.Clear();
                    richTextBoxShell.Focus();
                    break;
                case Enums.Commands.Restart:
                    Enums.RestartOptions restartOptions = new Enums.RestartOptions();
                    switch (comboBoxRebootOptions.SelectedIndex)
                    {
                        case 0: //Reboot
                            restartOptions = Enums.RestartOptions.Normal;
                            break;
                        case 1: //Recovery
                            restartOptions = Enums.RestartOptions.Recovery;
                            break;
                        case 2: // Bootloader
                            restartOptions = Enums.RestartOptions.Bootloader;
                            break;
                        case 3: //Fastboot
                            restartOptions = Enums.RestartOptions.Fastboot;
                            break;
                        default:
                            MessageBox.Show("Unknown mode", "Unknown Input");
                            break;
                    }
                    routineItem = new RoutineItem
                    {
                        ItemType = Enums.RoutineItemType.Reboot,
                        Name = restartOptions.ToString()
                    };
                    break;
                case Enums.Commands.Pull:
                    break;
                case Enums.Commands.Push:
                    break;
                default:
                    break;
            }
            routineItems.Add(routineItem);
            listViewRoutine.Items.Add(routineItem.GetListViewItem(listViewRoutine.Items.Count));

            buttonAdd.Enabled = false;
            buttonCancel.Enabled = false;
        }
        private void ListViewCommands_ItemSelectionChanged(object sender, ListViewItemSelectionChangedEventArgs e)
        {
            selectedItem = e.Item.Text;
            switch (selectedItem)
            {
                case "Install":
                    commands = Enums.Commands.Install;
                    pictureBoxCommand.Image = Properties.Resources.Install;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Uninstall":
                    commands = Enums.Commands.Uninstall;
                    pictureBoxCommand.Image = Properties.Resources.Uninstall;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Shell":
                    commands = Enums.Commands.Shell;
                    pictureBoxCommand.Image = Properties.Resources.Shell;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    break;
                case "Restart":
                    commands = Enums.Commands.Restart;
                    pictureBoxCommand.Image = Properties.Resources.Restart;
                    labelCommand.Text = selectedItem;
                    labelCommand.Show();
                    labelCommand.Left = (Width - labelCommand.Width) / 2;
                    ShowPanel(commands);
                    comboBoxRebootOptions.SelectedIndex = -1;
                    break;
                case "Pull":
                    treeViewDeviceTree.Nodes.Clear();
                    commands = Enums.Commands.Pull;
                    pictureBoxCommand.Image = Properties.Resources.Pull;
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
                    commands = Enums.Commands.Push;
                    pictureBoxCommand.Image = Properties.Resources.Push;
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
                apkPath = openFileDialogApk.FileName;
                ThreadWorkerGetAppName worker = new ThreadWorkerGetAppName(openFileDialogApk.FileName);
                worker.ThreadDone += HandleThreadDone;
                Thread threadGetAppName = new Thread(worker.Run);
                threadGetAppName.Start();
                labelApkName.Text = "Loading Name...";
                labelApkName.Left = (panelInstall.Width - labelApkName.Width) / 2;
                labelApkName.Show();
            }
        }
        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            switch (commands)
            {
                case Enums.Commands.Install:
                    openFileDialogApk = new OpenFileDialog();
                    labelApkName.Text = "Loading Name...";
                    labelApkName.Hide();
                    buttonOpenFile.Focus();
                    break;
                case Enums.Commands.Uninstall:
                    break;
                case Enums.Commands.Shell:
                    richTextBoxShell.Clear();
                    richTextBoxShell.Focus();
                    break;
                case Enums.Commands.Restart:
                    break;
                case Enums.Commands.Pull:
                    break;
                case Enums.Commands.Push:
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
        private void ComboBoxRebootOptions_SelectedIndexChanged(object sender, EventArgs e)
        {
            if ((sender as ComboBox).SelectedIndex >= 0)
            {
                buttonAdd.Enabled = true;
            }
        }
        #endregion

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
                buttonAdd.Enabled = true;
                buttonCancel.Enabled = true;
            }
        }
        private void ShowPanel(Enums.Commands command)
        {
            switch (command)
            {
                case Enums.Commands.Install:
                    panelInstall.Show();
                    panelInstall.BringToFront();
                    panelInstall.Size = new Size(354, 274);
                    panelInstall.Location = new Point(3, 3);
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Enums.Commands.Uninstall:
                    panelInstall.Hide();
                    panelUninstall.Show();
                    panelUninstall.BringToFront();
                    panelUninstall.Size = new Size(354, 274);
                    panelUninstall.Location = new Point(3, 3);
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Enums.Commands.Shell:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Show();
                    panelShell.BringToFront();
                    panelShell.Size = new Size(354, 274);
                    panelShell.Location = new Point(3, 3);
                    panelRestart.Hide();
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Enums.Commands.Restart:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Show();
                    panelRestart.BringToFront();
                    panelRestart.Size = new Size(354, 274);
                    panelRestart.Location = new Point(3, 3);
                    panelPush.Hide();
                    panelPull.Hide();
                    break;
                case Enums.Commands.Pull:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPull.Show();
                    panelPull.BringToFront();
                    panelPull.Size = new Size(354, 274);
                    panelPull.Location = new Point(3, 3);
                    panelPush.Hide();
                    break;
                case Enums.Commands.Push:
                    panelInstall.Hide();
                    panelUninstall.Hide();
                    panelShell.Hide();
                    panelRestart.Hide();
                    panelPull.Hide();
                    panelPush.Show();
                    panelPush.BringToFront();
                    panelPush.Size = new Size(354, 274);
                    panelPush.Location = new Point(3, 3);
                    break;
                default:
                    break;
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
