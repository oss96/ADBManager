namespace ADBManager
{
    partial class MainForm
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
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonReboot = new System.Windows.Forms.Button();
            this.buttonShellCommand = new System.Windows.Forms.Button();
            this.textBoxShellCommand = new System.Windows.Forms.TextBox();
            this.comboBoxRebootOptions = new System.Windows.Forms.ComboBox();
            this.buttonInstall = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelDevice = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.buttonNone = new System.Windows.Forms.Button();
            this.buttonAll = new System.Windows.Forms.Button();
            this.checkedListBoxDevices = new System.Windows.Forms.CheckedListBox();
            this.buttonUninstall = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            this.SuspendLayout();
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(12, 12);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(167, 23);
            this.buttonRefresh.TabIndex = 0;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.ButtonRefresh_Click);
            // 
            // buttonReboot
            // 
            this.buttonReboot.Location = new System.Drawing.Point(266, 12);
            this.buttonReboot.Name = "buttonReboot";
            this.buttonReboot.Size = new System.Drawing.Size(101, 23);
            this.buttonReboot.TabIndex = 0;
            this.buttonReboot.Text = "Reboot Device";
            this.buttonReboot.UseVisualStyleBackColor = true;
            this.buttonReboot.Click += new System.EventHandler(this.ButtonReboot_Click);
            // 
            // buttonShellCommand
            // 
            this.buttonShellCommand.Location = new System.Drawing.Point(373, 12);
            this.buttonShellCommand.Name = "buttonShellCommand";
            this.buttonShellCommand.Size = new System.Drawing.Size(129, 23);
            this.buttonShellCommand.TabIndex = 2;
            this.buttonShellCommand.Text = "Send Shell Command";
            this.buttonShellCommand.UseVisualStyleBackColor = true;
            this.buttonShellCommand.Click += new System.EventHandler(this.ButtonShellCommand_Click);
            // 
            // textBoxShellCommand
            // 
            this.textBoxShellCommand.Location = new System.Drawing.Point(373, 40);
            this.textBoxShellCommand.Name = "textBoxShellCommand";
            this.textBoxShellCommand.Size = new System.Drawing.Size(129, 20);
            this.textBoxShellCommand.TabIndex = 4;
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
            this.comboBoxRebootOptions.Location = new System.Drawing.Point(266, 39);
            this.comboBoxRebootOptions.Name = "comboBoxRebootOptions";
            this.comboBoxRebootOptions.Size = new System.Drawing.Size(101, 21);
            this.comboBoxRebootOptions.TabIndex = 5;
            // 
            // buttonInstall
            // 
            this.buttonInstall.Location = new System.Drawing.Point(185, 12);
            this.buttonInstall.Name = "buttonInstall";
            this.buttonInstall.Size = new System.Drawing.Size(75, 23);
            this.buttonInstall.TabIndex = 6;
            this.buttonInstall.Text = "Install APK";
            this.buttonInstall.UseVisualStyleBackColor = true;
            this.buttonInstall.Click += new System.EventHandler(this.ButtonInstall_Click);
            // 
            // statusStrip1
            // 
            this.statusStrip1.Items.AddRange(new System.Windows.Forms.ToolStripItem[] {
            this.toolStripStatusLabelDevice});
            this.statusStrip1.Location = new System.Drawing.Point(0, 428);
            this.statusStrip1.Name = "statusStrip1";
            this.statusStrip1.Size = new System.Drawing.Size(800, 22);
            this.statusStrip1.TabIndex = 9;
            this.statusStrip1.Text = "statusStrip1";
            // 
            // toolStripStatusLabelDevice
            // 
            this.toolStripStatusLabelDevice.Name = "toolStripStatusLabelDevice";
            this.toolStripStatusLabelDevice.Size = new System.Drawing.Size(108, 17);
            this.toolStripStatusLabelDevice.Text = "Last Event: nothing";
            // 
            // panel1
            // 
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.buttonNone);
            this.panel1.Controls.Add(this.buttonAll);
            this.panel1.Controls.Add(this.checkedListBoxDevices);
            this.panel1.Location = new System.Drawing.Point(12, 41);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(167, 311);
            this.panel1.TabIndex = 10;
            // 
            // buttonNone
            // 
            this.buttonNone.Location = new System.Drawing.Point(84, 3);
            this.buttonNone.Name = "buttonNone";
            this.buttonNone.Size = new System.Drawing.Size(75, 23);
            this.buttonNone.TabIndex = 2;
            this.buttonNone.Text = "Select None";
            this.buttonNone.UseVisualStyleBackColor = true;
            this.buttonNone.Click += new System.EventHandler(this.ButtonNone_Click);
            // 
            // buttonAll
            // 
            this.buttonAll.Location = new System.Drawing.Point(3, 3);
            this.buttonAll.Name = "buttonAll";
            this.buttonAll.Size = new System.Drawing.Size(75, 23);
            this.buttonAll.TabIndex = 1;
            this.buttonAll.Text = "Select All";
            this.buttonAll.UseVisualStyleBackColor = true;
            this.buttonAll.Click += new System.EventHandler(this.ButtonAll_Click);
            // 
            // checkedListBoxDevices
            // 
            this.checkedListBoxDevices.BackColor = System.Drawing.Color.White;
            this.checkedListBoxDevices.BorderStyle = System.Windows.Forms.BorderStyle.None;
            this.checkedListBoxDevices.FormattingEnabled = true;
            this.checkedListBoxDevices.Location = new System.Drawing.Point(3, 34);
            this.checkedListBoxDevices.Name = "checkedListBoxDevices";
            this.checkedListBoxDevices.Size = new System.Drawing.Size(157, 270);
            this.checkedListBoxDevices.TabIndex = 0;
            this.checkedListBoxDevices.SelectedIndexChanged += new System.EventHandler(this.CheckedListBoxDevices_SelectedIndexChanged);
            // 
            // buttonUninstall
            // 
            this.buttonUninstall.Location = new System.Drawing.Point(185, 41);
            this.buttonUninstall.Name = "buttonUninstall";
            this.buttonUninstall.Size = new System.Drawing.Size(75, 23);
            this.buttonUninstall.TabIndex = 11;
            this.buttonUninstall.Text = "Uninstall";
            this.buttonUninstall.UseVisualStyleBackColor = true;
            this.buttonUninstall.Click += new System.EventHandler(this.ButtonUninstall_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.buttonUninstall);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.buttonInstall);
            this.Controls.Add(this.comboBoxRebootOptions);
            this.Controls.Add(this.textBoxShellCommand);
            this.Controls.Add(this.buttonShellCommand);
            this.Controls.Add(this.buttonReboot);
            this.Controls.Add(this.buttonRefresh);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            this.ResumeLayout(false);
            this.PerformLayout();

        }

        #endregion

        private System.Windows.Forms.Button buttonRefresh;
        private System.Windows.Forms.Button buttonReboot;
        private System.Windows.Forms.Button buttonShellCommand;
        private System.Windows.Forms.TextBox textBoxShellCommand;
        private System.Windows.Forms.ComboBox comboBoxRebootOptions;
        private System.Windows.Forms.Button buttonInstall;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelDevice;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.CheckedListBox checkedListBoxDevices;
        private System.Windows.Forms.Button buttonNone;
        private System.Windows.Forms.Button buttonAll;
        private System.Windows.Forms.Button buttonUninstall;
    }
}

