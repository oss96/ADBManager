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
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle1 = new System.Windows.Forms.DataGridViewCellStyle();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonReboot = new System.Windows.Forms.Button();
            this.buttonShellCommand = new System.Windows.Forms.Button();
            this.textBoxShellCommand = new System.Windows.Forms.TextBox();
            this.comboBoxRebootOptions = new System.Windows.Forms.ComboBox();
            this.buttonInstall = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelDevice = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.dataGridViewFastboot = new System.Windows.Forms.DataGridView();
            this.dataGridViewCheckBoxColumnFastboot = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.dataGridViewTextBoxColumnFastbootName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewTextBoxColumnFastbootSerial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.dataGridViewADB = new System.Windows.Forms.DataGridView();
            this.buttonNone = new System.Windows.Forms.Button();
            this.buttonAll = new System.Windows.Forms.Button();
            this.buttonUninstall = new System.Windows.Forms.Button();
            this.ColumnFastbootCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnSerial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.button1 = new System.Windows.Forms.Button();
            this.button2 = new System.Windows.Forms.Button();
            this.statusStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFastboot)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewADB)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Location = new System.Drawing.Point(659, 17);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(129, 23);
            this.buttonRefresh.TabIndex = 6;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.ButtonRefresh_Click);
            // 
            // buttonReboot
            // 
            this.buttonReboot.Location = new System.Drawing.Point(659, 104);
            this.buttonReboot.Name = "buttonReboot";
            this.buttonReboot.Size = new System.Drawing.Size(101, 23);
            this.buttonReboot.TabIndex = 9;
            this.buttonReboot.Text = "Reboot Device";
            this.buttonReboot.UseVisualStyleBackColor = true;
            this.buttonReboot.Click += new System.EventHandler(this.ButtonReboot_Click);
            // 
            // buttonShellCommand
            // 
            this.buttonShellCommand.Location = new System.Drawing.Point(659, 160);
            this.buttonShellCommand.Name = "buttonShellCommand";
            this.buttonShellCommand.Size = new System.Drawing.Size(129, 23);
            this.buttonShellCommand.TabIndex = 11;
            this.buttonShellCommand.Text = "Send Shell Command";
            this.buttonShellCommand.UseVisualStyleBackColor = true;
            this.buttonShellCommand.Click += new System.EventHandler(this.ButtonShellCommand_Click);
            // 
            // textBoxShellCommand
            // 
            this.textBoxShellCommand.Location = new System.Drawing.Point(659, 189);
            this.textBoxShellCommand.Name = "textBoxShellCommand";
            this.textBoxShellCommand.Size = new System.Drawing.Size(129, 20);
            this.textBoxShellCommand.TabIndex = 12;
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
            this.comboBoxRebootOptions.Location = new System.Drawing.Point(659, 133);
            this.comboBoxRebootOptions.Name = "comboBoxRebootOptions";
            this.comboBoxRebootOptions.Size = new System.Drawing.Size(101, 21);
            this.comboBoxRebootOptions.TabIndex = 10;
            // 
            // buttonInstall
            // 
            this.buttonInstall.Location = new System.Drawing.Point(659, 46);
            this.buttonInstall.Name = "buttonInstall";
            this.buttonInstall.Size = new System.Drawing.Size(75, 23);
            this.buttonInstall.TabIndex = 7;
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
            this.statusStrip1.TabIndex = 99;
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
            this.panel1.Controls.Add(this.button1);
            this.panel1.Controls.Add(this.button2);
            this.panel1.Controls.Add(this.dataGridViewFastboot);
            this.panel1.Controls.Add(this.dataGridViewADB);
            this.panel1.Controls.Add(this.buttonNone);
            this.panel1.Controls.Add(this.buttonAll);
            this.panel1.Location = new System.Drawing.Point(12, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(641, 413);
            this.panel1.TabIndex = 99;
            // 
            // dataGridViewFastboot
            // 
            this.dataGridViewFastboot.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewFastboot.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewFastboot.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewCheckBoxColumnFastboot,
            this.dataGridViewTextBoxColumnFastbootName,
            this.dataGridViewTextBoxColumnFastbootSerial});
            this.dataGridViewFastboot.Location = new System.Drawing.Point(320, 34);
            this.dataGridViewFastboot.Name = "dataGridViewFastboot";
            this.dataGridViewFastboot.RowHeadersVisible = false;
            this.dataGridViewFastboot.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFastboot.Size = new System.Drawing.Size(314, 372);
            this.dataGridViewFastboot.TabIndex = 3;
            // 
            // dataGridViewCheckBoxColumnFastboot
            // 
            this.dataGridViewCheckBoxColumnFastboot.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewCheckBoxColumnFastboot.HeaderText = "";
            this.dataGridViewCheckBoxColumnFastboot.Name = "dataGridViewCheckBoxColumnFastboot";
            this.dataGridViewCheckBoxColumnFastboot.Width = 20;
            // 
            // dataGridViewTextBoxColumnFastbootName
            // 
            this.dataGridViewTextBoxColumnFastbootName.HeaderText = "Name";
            this.dataGridViewTextBoxColumnFastbootName.Name = "dataGridViewTextBoxColumnFastbootName";
            this.dataGridViewTextBoxColumnFastbootName.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumnFastbootName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewTextBoxColumnFastbootSerial
            // 
            this.dataGridViewTextBoxColumnFastbootSerial.HeaderText = "Serial";
            this.dataGridViewTextBoxColumnFastbootSerial.Name = "dataGridViewTextBoxColumnFastbootSerial";
            this.dataGridViewTextBoxColumnFastbootSerial.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumnFastbootSerial.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // dataGridViewADB
            // 
            this.dataGridViewADB.AllowUserToAddRows = false;
            this.dataGridViewADB.AllowUserToDeleteRows = false;
            this.dataGridViewADB.AllowUserToOrderColumns = true;
            this.dataGridViewADB.AllowUserToResizeRows = false;
            this.dataGridViewADB.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewADB.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewADB.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnFastbootCheck,
            this.ColumnName,
            this.ColumnSerial});
            dataGridViewCellStyle1.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle1.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle1.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(254)));
            dataGridViewCellStyle1.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle1.NullValue = false;
            dataGridViewCellStyle1.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle1.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle1.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewADB.DefaultCellStyle = dataGridViewCellStyle1;
            this.dataGridViewADB.Location = new System.Drawing.Point(3, 34);
            this.dataGridViewADB.Name = "dataGridViewADB";
            this.dataGridViewADB.ReadOnly = true;
            this.dataGridViewADB.RowHeadersVisible = false;
            this.dataGridViewADB.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewADB.ShowEditingIcon = false;
            this.dataGridViewADB.Size = new System.Drawing.Size(314, 372);
            this.dataGridViewADB.TabIndex = 0;
            this.dataGridViewADB.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellClick);
            this.dataGridViewADB.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellValueChanged);
            // 
            // buttonNone
            // 
            this.buttonNone.Location = new System.Drawing.Point(163, 5);
            this.buttonNone.Name = "buttonNone";
            this.buttonNone.Size = new System.Drawing.Size(75, 23);
            this.buttonNone.TabIndex = 2;
            this.buttonNone.Text = "Select None";
            this.buttonNone.UseVisualStyleBackColor = true;
            this.buttonNone.Click += new System.EventHandler(this.ButtonNone_Click);
            // 
            // buttonAll
            // 
            this.buttonAll.Location = new System.Drawing.Point(82, 5);
            this.buttonAll.Name = "buttonAll";
            this.buttonAll.Size = new System.Drawing.Size(75, 23);
            this.buttonAll.TabIndex = 1;
            this.buttonAll.Text = "Select All";
            this.buttonAll.UseVisualStyleBackColor = true;
            this.buttonAll.Click += new System.EventHandler(this.ButtonAll_Click);
            // 
            // buttonUninstall
            // 
            this.buttonUninstall.Location = new System.Drawing.Point(659, 75);
            this.buttonUninstall.Name = "buttonUninstall";
            this.buttonUninstall.Size = new System.Drawing.Size(75, 23);
            this.buttonUninstall.TabIndex = 8;
            this.buttonUninstall.Text = "Uninstall";
            this.buttonUninstall.UseVisualStyleBackColor = true;
            this.buttonUninstall.Click += new System.EventHandler(this.ButtonUninstall_Click);
            // 
            // ColumnFastbootCheck
            // 
            this.ColumnFastbootCheck.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.ColumnFastbootCheck.HeaderText = "";
            this.ColumnFastbootCheck.Name = "ColumnFastbootCheck";
            this.ColumnFastbootCheck.ReadOnly = true;
            this.ColumnFastbootCheck.Width = 20;
            // 
            // ColumnName
            // 
            this.ColumnName.HeaderText = "Name";
            this.ColumnName.Name = "ColumnName";
            this.ColumnName.ReadOnly = true;
            this.ColumnName.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnSerial
            // 
            this.ColumnSerial.HeaderText = "Serial";
            this.ColumnSerial.Name = "ColumnSerial";
            this.ColumnSerial.ReadOnly = true;
            this.ColumnSerial.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnSerial.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // button1
            // 
            this.button1.Location = new System.Drawing.Point(483, 5);
            this.button1.Name = "button1";
            this.button1.Size = new System.Drawing.Size(75, 23);
            this.button1.TabIndex = 5;
            this.button1.Text = "Select None";
            this.button1.UseVisualStyleBackColor = true;
            // 
            // button2
            // 
            this.button2.Location = new System.Drawing.Point(402, 5);
            this.button2.Name = "button2";
            this.button2.Size = new System.Drawing.Size(75, 23);
            this.button2.TabIndex = 4;
            this.button2.Text = "Select All";
            this.button2.UseVisualStyleBackColor = true;
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
            this.Text = "ADB/Fastboot Manager";
            this.FormClosing += new System.Windows.Forms.FormClosingEventHandler(this.MainForm_FormClosing);
            this.Load += new System.EventHandler(this.MainForm_Load);
            this.statusStrip1.ResumeLayout(false);
            this.statusStrip1.PerformLayout();
            this.panel1.ResumeLayout(false);
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFastboot)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewADB)).EndInit();
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
        private System.Windows.Forms.Button buttonNone;
        private System.Windows.Forms.Button buttonAll;
        private System.Windows.Forms.Button buttonUninstall;
        private System.Windows.Forms.DataGridView dataGridViewFastboot;
        private System.Windows.Forms.DataGridView dataGridViewADB;
        private System.Windows.Forms.DataGridViewCheckBoxColumn dataGridViewCheckBoxColumnFastboot;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumnFastbootName;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumnFastbootSerial;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnFastbootCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnSerial;
        private System.Windows.Forms.Button button1;
        private System.Windows.Forms.Button button2;
    }
}

