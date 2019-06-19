namespace ADBManager
{
    partial class Routines
    {
        /// <summary>
        /// Required designer variable.
        /// </summary>
        private System.ComponentModel.IContainer components = null;

        /// <summary>
        /// Clean up any resources being used.
        /// </summary>
        /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
            {
                components.Dispose();
            }
            base.Dispose(disposing);
        }

        #region Windows Form Designer generated code

        /// <summary>
        /// Required method for Designer support - do not modify
        /// the contents of this method with the code editor.
        /// </summary>
        private void InitializeComponent()
        {
            this.listViewRoutine = new System.Windows.Forms.ListView();
            this.listViewCommands = new System.Windows.Forms.ListView();
            this.buttonAdd = new System.Windows.Forms.Button();
            this.buttonRemove = new System.Windows.Forms.Button();
            this.pictureBoxCommand = new System.Windows.Forms.PictureBox();
            this.panelInstall = new System.Windows.Forms.Panel();
            this.buttonOpenFile = new System.Windows.Forms.Button();
            this.labelApkName = new System.Windows.Forms.Label();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.openFileDialogApk = new System.Windows.Forms.OpenFileDialog();
            this.panelUninstall = new System.Windows.Forms.Panel();
            this.panelShell = new System.Windows.Forms.Panel();
            this.richTextBoxShell = new System.Windows.Forms.RichTextBox();
            this.panelRestart = new System.Windows.Forms.Panel();
            this.panelPull = new System.Windows.Forms.Panel();
            this.treeViewDeviceTree = new System.Windows.Forms.TreeView();
            this.panelPush = new System.Windows.Forms.Panel();
            this.labelCommand = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxCommand)).BeginInit();
            this.panelInstall.SuspendLayout();
            this.panelShell.SuspendLayout();
            this.panelPull.SuspendLayout();
            this.SuspendLayout();
            // 
            // listViewRoutine
            // 
            this.listViewRoutine.HideSelection = false;
            this.listViewRoutine.Location = new System.Drawing.Point(12, 12);
            this.listViewRoutine.Name = "listViewRoutine";
            this.listViewRoutine.Size = new System.Drawing.Size(203, 426);
            this.listViewRoutine.TabIndex = 0;
            this.listViewRoutine.UseCompatibleStateImageBehavior = false;
            // 
            // listViewCommands
            // 
            this.listViewCommands.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewCommands.HideSelection = false;
            this.listViewCommands.Location = new System.Drawing.Point(585, 12);
            this.listViewCommands.Name = "listViewCommands";
            this.listViewCommands.Size = new System.Drawing.Size(203, 426);
            this.listViewCommands.TabIndex = 1;
            this.listViewCommands.UseCompatibleStateImageBehavior = false;
            this.listViewCommands.ItemSelectionChanged += new System.Windows.Forms.ListViewItemSelectionChangedEventHandler(this.ListViewCommands_ItemSelectionChanged);
            // 
            // buttonAdd
            // 
            this.buttonAdd.Enabled = false;
            this.buttonAdd.Location = new System.Drawing.Point(348, 386);
            this.buttonAdd.Name = "buttonAdd";
            this.buttonAdd.Size = new System.Drawing.Size(92, 23);
            this.buttonAdd.TabIndex = 2;
            this.buttonAdd.Text = "Add";
            this.buttonAdd.UseVisualStyleBackColor = true;
            this.buttonAdd.Click += new System.EventHandler(this.ButtonAdd_Click);
            // 
            // buttonRemove
            // 
            this.buttonRemove.Location = new System.Drawing.Point(221, 12);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(92, 23);
            this.buttonRemove.TabIndex = 3;
            this.buttonRemove.Text = "Remove";
            this.buttonRemove.UseVisualStyleBackColor = true;
            // 
            // pictureBoxCommand
            // 
            this.pictureBoxCommand.Location = new System.Drawing.Point(371, 12);
            this.pictureBoxCommand.Name = "pictureBoxCommand";
            this.pictureBoxCommand.Size = new System.Drawing.Size(114, 63);
            this.pictureBoxCommand.SizeMode = System.Windows.Forms.PictureBoxSizeMode.StretchImage;
            this.pictureBoxCommand.TabIndex = 4;
            this.pictureBoxCommand.TabStop = false;
            // 
            // panelInstall
            // 
            this.panelInstall.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelInstall.Controls.Add(this.buttonOpenFile);
            this.panelInstall.Controls.Add(this.labelApkName);
            this.panelInstall.Location = new System.Drawing.Point(221, 100);
            this.panelInstall.Name = "panelInstall";
            this.panelInstall.Size = new System.Drawing.Size(358, 52);
            this.panelInstall.TabIndex = 5;
            this.panelInstall.Visible = false;
            // 
            // buttonOpenFile
            // 
            this.buttonOpenFile.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.buttonOpenFile.Location = new System.Drawing.Point(134, 3);
            this.buttonOpenFile.Name = "buttonOpenFile";
            this.buttonOpenFile.Size = new System.Drawing.Size(92, 23);
            this.buttonOpenFile.TabIndex = 0;
            this.buttonOpenFile.Text = "Choose APK";
            this.buttonOpenFile.UseVisualStyleBackColor = true;
            this.buttonOpenFile.Click += new System.EventHandler(this.ButtonOpenFile_Click);
            // 
            // labelApkName
            // 
            this.labelApkName.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.labelApkName.AutoSize = true;
            this.labelApkName.Location = new System.Drawing.Point(133, 29);
            this.labelApkName.Name = "labelApkName";
            this.labelApkName.Size = new System.Drawing.Size(85, 13);
            this.labelApkName.TabIndex = 1;
            this.labelApkName.Text = "Loading Name...";
            this.labelApkName.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.labelApkName.Visible = false;
            // 
            // buttonCancel
            // 
            this.buttonCancel.Enabled = false;
            this.buttonCancel.Location = new System.Drawing.Point(348, 415);
            this.buttonCancel.Name = "buttonCancel";
            this.buttonCancel.Size = new System.Drawing.Size(92, 23);
            this.buttonCancel.TabIndex = 6;
            this.buttonCancel.Text = "Cancel";
            this.buttonCancel.UseVisualStyleBackColor = true;
            this.buttonCancel.Click += new System.EventHandler(this.ButtonCancel_Click);
            // 
            // openFileDialogApk
            // 
            this.openFileDialogApk.FileName = "openFileDialog1";
            // 
            // panelUninstall
            // 
            this.panelUninstall.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelUninstall.Location = new System.Drawing.Point(221, 250);
            this.panelUninstall.Name = "panelUninstall";
            this.panelUninstall.Size = new System.Drawing.Size(358, 28);
            this.panelUninstall.TabIndex = 6;
            this.panelUninstall.Visible = false;
            // 
            // panelShell
            // 
            this.panelShell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelShell.Controls.Add(this.richTextBoxShell);
            this.panelShell.Location = new System.Drawing.Point(221, 100);
            this.panelShell.Name = "panelShell";
            this.panelShell.Size = new System.Drawing.Size(358, 87);
            this.panelShell.TabIndex = 7;
            this.panelShell.Visible = false;
            // 
            // richTextBoxShell
            // 
            this.richTextBoxShell.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxShell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.richTextBoxShell.DetectUrls = false;
            this.richTextBoxShell.Location = new System.Drawing.Point(10, 10);
            this.richTextBoxShell.Margin = new System.Windows.Forms.Padding(10);
            this.richTextBoxShell.Name = "richTextBoxShell";
            this.richTextBoxShell.Size = new System.Drawing.Size(336, 65);
            this.richTextBoxShell.TabIndex = 102;
            this.richTextBoxShell.Text = "";
            this.richTextBoxShell.TextChanged += new System.EventHandler(this.RichTextBoxShell_TextChanged);
            // 
            // panelRestart
            // 
            this.panelRestart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelRestart.Location = new System.Drawing.Point(221, 284);
            this.panelRestart.Name = "panelRestart";
            this.panelRestart.Size = new System.Drawing.Size(358, 28);
            this.panelRestart.TabIndex = 7;
            this.panelRestart.Visible = false;
            // 
            // panelPull
            // 
            this.panelPull.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPull.Controls.Add(this.treeViewDeviceTree);
            this.panelPull.Location = new System.Drawing.Point(221, 98);
            this.panelPull.Name = "panelPull";
            this.panelPull.Size = new System.Drawing.Size(358, 214);
            this.panelPull.TabIndex = 7;
            this.panelPull.Visible = false;
            // 
            // treeViewDeviceTree
            // 
            this.treeViewDeviceTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewDeviceTree.Location = new System.Drawing.Point(10, 12);
            this.treeViewDeviceTree.Name = "treeViewDeviceTree";
            this.treeViewDeviceTree.Size = new System.Drawing.Size(336, 188);
            this.treeViewDeviceTree.TabIndex = 0;
            this.treeViewDeviceTree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.TreeViewDeviceTree_NodeMouseClick);
            // 
            // panelPush
            // 
            this.panelPush.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPush.Location = new System.Drawing.Point(221, 352);
            this.panelPush.Name = "panelPush";
            this.panelPush.Size = new System.Drawing.Size(358, 28);
            this.panelPush.TabIndex = 7;
            this.panelPush.Visible = false;
            // 
            // labelCommand
            // 
            this.labelCommand.AutoSize = true;
            this.labelCommand.Location = new System.Drawing.Point(371, 82);
            this.labelCommand.Name = "labelCommand";
            this.labelCommand.Size = new System.Drawing.Size(35, 13);
            this.labelCommand.TabIndex = 8;
            this.labelCommand.Text = "label1";
            this.labelCommand.Visible = false;
            // 
            // Routines
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panelPull);
            this.Controls.Add(this.labelCommand);
            this.Controls.Add(this.panelPush);
            this.Controls.Add(this.panelRestart);
            this.Controls.Add(this.panelShell);
            this.Controls.Add(this.panelUninstall);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.panelInstall);
            this.Controls.Add(this.pictureBoxCommand);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.buttonAdd);
            this.Controls.Add(this.listViewCommands);
            this.Controls.Add(this.listViewRoutine);
            this.Name = "Routines";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Routines";
            this.Load += new System.EventHandler(this.Routines_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxCommand)).EndInit();
            this.panelInstall.ResumeLayout(false);
            this.panelInstall.PerformLayout();
            this.panelShell.ResumeLayout(false);
            this.panelPull.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.ListView listViewRoutine;
        private System.Windows.Forms.ListView listViewCommands;
        private System.Windows.Forms.Button buttonAdd;
        private System.Windows.Forms.Button buttonRemove;
        private System.Windows.Forms.PictureBox pictureBoxCommand;
        private System.Windows.Forms.Panel panelInstall;
        private System.Windows.Forms.Button buttonOpenFile;
        private System.Windows.Forms.Label labelApkName;
        private System.Windows.Forms.Button buttonCancel;
        private System.Windows.Forms.OpenFileDialog openFileDialogApk;
        private System.Windows.Forms.Panel panelUninstall;
        private System.Windows.Forms.Panel panelShell;
        private System.Windows.Forms.Panel panelRestart;
        private System.Windows.Forms.Panel panelPull;
        private System.Windows.Forms.Panel panelPush;
        private System.Windows.Forms.RichTextBox richTextBoxShell;
        private System.Windows.Forms.Label labelCommand;
        private System.Windows.Forms.TreeView treeViewDeviceTree;
    }
}