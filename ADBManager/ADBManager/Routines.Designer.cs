﻿namespace ADBManager
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
            this.panelRestart = new System.Windows.Forms.Panel();
            this.comboBoxRebootOptions = new System.Windows.Forms.ComboBox();
            this.panelPush = new System.Windows.Forms.Panel();
            this.treeView1 = new System.Windows.Forms.TreeView();
            this.panelPull = new System.Windows.Forms.Panel();
            this.treeViewDeviceTree = new System.Windows.Forms.TreeView();
            this.buttonCancel = new System.Windows.Forms.Button();
            this.openFileDialogApk = new System.Windows.Forms.OpenFileDialog();
            this.panelUninstall = new System.Windows.Forms.Panel();
            this.panelShell = new System.Windows.Forms.Panel();
            this.richTextBoxShell = new System.Windows.Forms.RichTextBox();
            this.labelCommand = new System.Windows.Forms.Label();
            this.panelAll = new System.Windows.Forms.Panel();
            this.columnHeader1 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            this.columnHeader2 = ((System.Windows.Forms.ColumnHeader)(new System.Windows.Forms.ColumnHeader()));
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxCommand)).BeginInit();
            this.panelInstall.SuspendLayout();
            this.panelRestart.SuspendLayout();
            this.panelPush.SuspendLayout();
            this.panelPull.SuspendLayout();
            this.panelShell.SuspendLayout();
            this.panelAll.SuspendLayout();
            this.SuspendLayout();
            // 
            // listViewRoutine
            // 
            this.listViewRoutine.Columns.AddRange(new System.Windows.Forms.ColumnHeader[] {
            this.columnHeader1,
            this.columnHeader2});
            this.listViewRoutine.HideSelection = false;
            this.listViewRoutine.Location = new System.Drawing.Point(12, 12);
            this.listViewRoutine.Name = "listViewRoutine";
            this.listViewRoutine.Size = new System.Drawing.Size(203, 426);
            this.listViewRoutine.TabIndex = 0;
            this.listViewRoutine.UseCompatibleStateImageBehavior = false;
            this.listViewRoutine.View = System.Windows.Forms.View.Tile;
            // 
            // listViewCommands
            // 
            this.listViewCommands.HeaderStyle = System.Windows.Forms.ColumnHeaderStyle.Nonclickable;
            this.listViewCommands.HideSelection = false;
            this.listViewCommands.Location = new System.Drawing.Point(585, 12);
            this.listViewCommands.MultiSelect = false;
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
            this.buttonRemove.Location = new System.Drawing.Point(221, 415);
            this.buttonRemove.Name = "buttonRemove";
            this.buttonRemove.Size = new System.Drawing.Size(92, 23);
            this.buttonRemove.TabIndex = 3;
            this.buttonRemove.Text = "Remove";
            this.buttonRemove.UseVisualStyleBackColor = true;
            // 
            // pictureBoxCommand
            // 
            this.pictureBoxCommand.BackColor = System.Drawing.Color.Transparent;
            this.pictureBoxCommand.Location = new System.Drawing.Point(330, 12);
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
            this.panelInstall.Location = new System.Drawing.Point(3, 123);
            this.panelInstall.Name = "panelInstall";
            this.panelInstall.Size = new System.Drawing.Size(358, 31);
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
            // panelRestart
            // 
            this.panelRestart.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelRestart.Controls.Add(this.comboBoxRebootOptions);
            this.panelRestart.Location = new System.Drawing.Point(3, 37);
            this.panelRestart.Name = "panelRestart";
            this.panelRestart.Size = new System.Drawing.Size(358, 31);
            this.panelRestart.TabIndex = 7;
            this.panelRestart.Visible = false;
            // 
            // comboBoxRebootOptions
            // 
            this.comboBoxRebootOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRebootOptions.FormattingEnabled = true;
            this.comboBoxRebootOptions.Items.AddRange(new object[] {
            "Reboot",
            "Recovery",
            "Bootloader",
            "Fastboot"});
            this.comboBoxRebootOptions.Location = new System.Drawing.Point(125, 3);
            this.comboBoxRebootOptions.Name = "comboBoxRebootOptions";
            this.comboBoxRebootOptions.Size = new System.Drawing.Size(101, 21);
            this.comboBoxRebootOptions.TabIndex = 12;
            this.comboBoxRebootOptions.SelectedIndexChanged += new System.EventHandler(this.ComboBoxRebootOptions_SelectedIndexChanged);
            // 
            // panelPush
            // 
            this.panelPush.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPush.Controls.Add(this.treeView1);
            this.panelPush.Location = new System.Drawing.Point(3, 160);
            this.panelPush.Name = "panelPush";
            this.panelPush.Size = new System.Drawing.Size(357, 59);
            this.panelPush.TabIndex = 7;
            this.panelPush.Visible = false;
            // 
            // treeView1
            // 
            this.treeView1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeView1.LineColor = System.Drawing.Color.Gainsboro;
            this.treeView1.Location = new System.Drawing.Point(10, 12);
            this.treeView1.Name = "treeView1";
            this.treeView1.Size = new System.Drawing.Size(332, 34);
            this.treeView1.TabIndex = 1;
            // 
            // panelPull
            // 
            this.panelPull.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelPull.Controls.Add(this.treeViewDeviceTree);
            this.panelPull.Location = new System.Drawing.Point(2, 225);
            this.panelPull.Name = "panelPull";
            this.panelPull.Size = new System.Drawing.Size(357, 34);
            this.panelPull.TabIndex = 7;
            this.panelPull.Visible = false;
            // 
            // treeViewDeviceTree
            // 
            this.treeViewDeviceTree.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.treeViewDeviceTree.LineColor = System.Drawing.Color.Gainsboro;
            this.treeViewDeviceTree.Location = new System.Drawing.Point(10, 12);
            this.treeViewDeviceTree.Name = "treeViewDeviceTree";
            this.treeViewDeviceTree.Size = new System.Drawing.Size(332, 9);
            this.treeViewDeviceTree.TabIndex = 0;
            this.treeViewDeviceTree.NodeMouseClick += new System.Windows.Forms.TreeNodeMouseClickEventHandler(this.TreeViewDeviceTree_NodeMouseClick);
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
            this.panelUninstall.Location = new System.Drawing.Point(3, 3);
            this.panelUninstall.Name = "panelUninstall";
            this.panelUninstall.Size = new System.Drawing.Size(354, 28);
            this.panelUninstall.TabIndex = 6;
            this.panelUninstall.Visible = false;
            // 
            // panelShell
            // 
            this.panelShell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.panelShell.Controls.Add(this.richTextBoxShell);
            this.panelShell.Location = new System.Drawing.Point(2, 74);
            this.panelShell.Name = "panelShell";
            this.panelShell.Size = new System.Drawing.Size(358, 43);
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
            this.richTextBoxShell.Size = new System.Drawing.Size(336, 21);
            this.richTextBoxShell.TabIndex = 102;
            this.richTextBoxShell.Text = "";
            this.richTextBoxShell.TextChanged += new System.EventHandler(this.RichTextBoxShell_TextChanged);
            // 
            // labelCommand
            // 
            this.labelCommand.Anchor = System.Windows.Forms.AnchorStyles.Top;
            this.labelCommand.AutoSize = true;
            this.labelCommand.Location = new System.Drawing.Point(368, 84);
            this.labelCommand.Name = "labelCommand";
            this.labelCommand.Size = new System.Drawing.Size(35, 13);
            this.labelCommand.TabIndex = 8;
            this.labelCommand.Text = "label1";
            this.labelCommand.Visible = false;
            // 
            // panelAll
            // 
            this.panelAll.Controls.Add(this.panelUninstall);
            this.panelAll.Controls.Add(this.panelPull);
            this.panelAll.Controls.Add(this.panelInstall);
            this.panelAll.Controls.Add(this.panelPush);
            this.panelAll.Controls.Add(this.panelShell);
            this.panelAll.Controls.Add(this.panelRestart);
            this.panelAll.Location = new System.Drawing.Point(220, 100);
            this.panelAll.Name = "panelAll";
            this.panelAll.Size = new System.Drawing.Size(359, 280);
            this.panelAll.TabIndex = 9;
            // 
            // Routines
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.panelAll);
            this.Controls.Add(this.labelCommand);
            this.Controls.Add(this.buttonCancel);
            this.Controls.Add(this.buttonRemove);
            this.Controls.Add(this.buttonAdd);
            this.Controls.Add(this.listViewCommands);
            this.Controls.Add(this.listViewRoutine);
            this.Controls.Add(this.pictureBoxCommand);
            this.Name = "Routines";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Routines";
            this.Load += new System.EventHandler(this.Routines_Load);
            ((System.ComponentModel.ISupportInitialize)(this.pictureBoxCommand)).EndInit();
            this.panelInstall.ResumeLayout(false);
            this.panelInstall.PerformLayout();
            this.panelRestart.ResumeLayout(false);
            this.panelPush.ResumeLayout(false);
            this.panelPull.ResumeLayout(false);
            this.panelShell.ResumeLayout(false);
            this.panelAll.ResumeLayout(false);
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
        private System.Windows.Forms.ComboBox comboBoxRebootOptions;
        private System.Windows.Forms.TreeView treeView1;
        private System.Windows.Forms.TreeView treeViewDeviceTree;
        private System.Windows.Forms.Panel panelAll;
        private System.Windows.Forms.ColumnHeader columnHeader1;
        private System.Windows.Forms.ColumnHeader columnHeader2;
    }
}