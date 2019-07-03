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
            this.components = new System.ComponentModel.Container();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle3 = new System.Windows.Forms.DataGridViewCellStyle();
            System.Windows.Forms.DataGridViewCellStyle dataGridViewCellStyle4 = new System.Windows.Forms.DataGridViewCellStyle();
            this.buttonRefresh = new System.Windows.Forms.Button();
            this.buttonReboot = new System.Windows.Forms.Button();
            this.buttonShellCommand = new System.Windows.Forms.Button();
            this.comboBoxRebootOptions = new System.Windows.Forms.ComboBox();
            this.buttonInstall = new System.Windows.Forms.Button();
            this.statusStrip1 = new System.Windows.Forms.StatusStrip();
            this.toolStripStatusLabelDevice = new System.Windows.Forms.ToolStripStatusLabel();
            this.panel1 = new System.Windows.Forms.Panel();
            this.buttonFastbootNone = new System.Windows.Forms.Button();
            this.buttonFastbootAll = new System.Windows.Forms.Button();
            this.dataGridViewFastboot = new System.Windows.Forms.DataGridView();
            this.dataGridViewCheckBoxColumnFastboot = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.dataGridViewTextBoxColumnFastbootSerial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.buttonADBNone = new System.Windows.Forms.Button();
            this.buttonADBAll = new System.Windows.Forms.Button();
            this.buttonUninstall = new System.Windows.Forms.Button();
            this.toolTipReboot = new System.Windows.Forms.ToolTip(this.components);
            this.buttonAddRoutine = new System.Windows.Forms.Button();
            this.richTextBoxShell = new System.Windows.Forms.RichTextBox();
            this.buttonPush = new System.Windows.Forms.Button();
            this.dataGridViewADB = new System.Windows.Forms.DataGridView();
            this.ColumnSerial = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnIMEI = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnName = new System.Windows.Forms.DataGridViewTextBoxColumn();
            this.ColumnCheck = new System.Windows.Forms.DataGridViewCheckBoxColumn();
            this.statusStrip1.SuspendLayout();
            this.panel1.SuspendLayout();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewFastboot)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.dataGridViewADB)).BeginInit();
            this.SuspendLayout();
            // 
            // buttonRefresh
            // 
            this.buttonRefresh.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonRefresh.Location = new System.Drawing.Point(681, 136);
            this.buttonRefresh.Name = "buttonRefresh";
            this.buttonRefresh.Size = new System.Drawing.Size(129, 23);
            this.buttonRefresh.TabIndex = 6;
            this.buttonRefresh.Text = "Refresh";
            this.buttonRefresh.UseVisualStyleBackColor = true;
            this.buttonRefresh.Click += new System.EventHandler(this.ButtonRefresh_Click);
            // 
            // buttonReboot
            // 
            this.buttonReboot.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonReboot.Location = new System.Drawing.Point(681, 223);
            this.buttonReboot.Name = "buttonReboot";
            this.buttonReboot.Size = new System.Drawing.Size(101, 23);
            this.buttonReboot.TabIndex = 9;
            this.buttonReboot.Text = "Reboot Device";
            this.toolTipReboot.SetToolTip(this.buttonReboot, "Reboot options only work on devices connected via ADB");
            this.buttonReboot.UseVisualStyleBackColor = true;
            this.buttonReboot.Click += new System.EventHandler(this.ButtonReboot_Click);
            // 
            // buttonShellCommand
            // 
            this.buttonShellCommand.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonShellCommand.Location = new System.Drawing.Point(681, 279);
            this.buttonShellCommand.Name = "buttonShellCommand";
            this.buttonShellCommand.Size = new System.Drawing.Size(129, 23);
            this.buttonShellCommand.TabIndex = 11;
            this.buttonShellCommand.Text = "Send Shell Command";
            this.buttonShellCommand.UseVisualStyleBackColor = true;
            this.buttonShellCommand.Click += new System.EventHandler(this.ButtonShellCommand_Click);
            // 
            // comboBoxRebootOptions
            // 
            this.comboBoxRebootOptions.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.comboBoxRebootOptions.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.comboBoxRebootOptions.FormattingEnabled = true;
            this.comboBoxRebootOptions.Items.AddRange(new object[] {
            "Reboot",
            "Recovery",
            "Bootloader",
            "Fastboot"});
            this.comboBoxRebootOptions.Location = new System.Drawing.Point(681, 252);
            this.comboBoxRebootOptions.Name = "comboBoxRebootOptions";
            this.comboBoxRebootOptions.Size = new System.Drawing.Size(101, 21);
            this.comboBoxRebootOptions.TabIndex = 10;
            // 
            // buttonInstall
            // 
            this.buttonInstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonInstall.Location = new System.Drawing.Point(681, 165);
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
            this.statusStrip1.Size = new System.Drawing.Size(822, 22);
            this.statusStrip1.SizingGrip = false;
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
            this.panel1.Anchor = ((System.Windows.Forms.AnchorStyles)((((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Left) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.panel1.BackColor = System.Drawing.Color.White;
            this.panel1.BorderStyle = System.Windows.Forms.BorderStyle.Fixed3D;
            this.panel1.Controls.Add(this.dataGridViewADB);
            this.panel1.Controls.Add(this.dataGridViewFastboot);
            this.panel1.Controls.Add(this.buttonFastbootNone);
            this.panel1.Controls.Add(this.buttonFastbootAll);
            this.panel1.Controls.Add(this.buttonADBNone);
            this.panel1.Controls.Add(this.buttonADBAll);
            this.panel1.Location = new System.Drawing.Point(12, 12);
            this.panel1.Name = "panel1";
            this.panel1.Size = new System.Drawing.Size(663, 413);
            this.panel1.TabIndex = 99;
            // 
            // buttonFastbootNone
            // 
            this.buttonFastbootNone.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFastbootNone.Location = new System.Drawing.Point(505, 5);
            this.buttonFastbootNone.Name = "buttonFastbootNone";
            this.buttonFastbootNone.Size = new System.Drawing.Size(75, 23);
            this.buttonFastbootNone.TabIndex = 5;
            this.buttonFastbootNone.Text = "Select None";
            this.buttonFastbootNone.UseVisualStyleBackColor = true;
            this.buttonFastbootNone.Click += new System.EventHandler(this.ButtonFastbootNone_Click);
            // 
            // buttonFastbootAll
            // 
            this.buttonFastbootAll.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonFastbootAll.Location = new System.Drawing.Point(424, 5);
            this.buttonFastbootAll.Name = "buttonFastbootAll";
            this.buttonFastbootAll.Size = new System.Drawing.Size(75, 23);
            this.buttonFastbootAll.TabIndex = 4;
            this.buttonFastbootAll.Text = "Select All";
            this.buttonFastbootAll.UseVisualStyleBackColor = true;
            this.buttonFastbootAll.Click += new System.EventHandler(this.ButtonFastbootAll_Click);
            // 
            // dataGridViewFastboot
            // 
            this.dataGridViewFastboot.AllowUserToAddRows = false;
            this.dataGridViewFastboot.AllowUserToDeleteRows = false;
            this.dataGridViewFastboot.AllowUserToResizeRows = false;
            this.dataGridViewFastboot.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewFastboot.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewFastboot.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewFastboot.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.dataGridViewCheckBoxColumnFastboot,
            this.dataGridViewTextBoxColumnFastbootSerial});
            dataGridViewCellStyle3.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle3.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle3.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(254)));
            dataGridViewCellStyle3.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle3.NullValue = "False";
            dataGridViewCellStyle3.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle3.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle3.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewFastboot.DefaultCellStyle = dataGridViewCellStyle3;
            this.dataGridViewFastboot.Location = new System.Drawing.Point(334, 34);
            this.dataGridViewFastboot.Name = "dataGridViewFastboot";
            this.dataGridViewFastboot.ReadOnly = true;
            this.dataGridViewFastboot.RowHeadersVisible = false;
            this.dataGridViewFastboot.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewFastboot.ShowEditingIcon = false;
            this.dataGridViewFastboot.Size = new System.Drawing.Size(322, 372);
            this.dataGridViewFastboot.TabIndex = 3;
            this.dataGridViewFastboot.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellClick);
            this.dataGridViewFastboot.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellValueChanged);
            // 
            // dataGridViewCheckBoxColumnFastboot
            // 
            this.dataGridViewCheckBoxColumnFastboot.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.dataGridViewCheckBoxColumnFastboot.HeaderText = "";
            this.dataGridViewCheckBoxColumnFastboot.Name = "dataGridViewCheckBoxColumnFastboot";
            this.dataGridViewCheckBoxColumnFastboot.ReadOnly = true;
            this.dataGridViewCheckBoxColumnFastboot.Width = 20;
            // 
            // dataGridViewTextBoxColumnFastbootSerial
            // 
            this.dataGridViewTextBoxColumnFastbootSerial.HeaderText = "Serial";
            this.dataGridViewTextBoxColumnFastbootSerial.Name = "dataGridViewTextBoxColumnFastbootSerial";
            this.dataGridViewTextBoxColumnFastbootSerial.ReadOnly = true;
            this.dataGridViewTextBoxColumnFastbootSerial.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.dataGridViewTextBoxColumnFastbootSerial.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // buttonADBNone
            // 
            this.buttonADBNone.Location = new System.Drawing.Point(163, 5);
            this.buttonADBNone.Name = "buttonADBNone";
            this.buttonADBNone.Size = new System.Drawing.Size(75, 23);
            this.buttonADBNone.TabIndex = 2;
            this.buttonADBNone.Text = "Select None";
            this.buttonADBNone.UseVisualStyleBackColor = true;
            this.buttonADBNone.Click += new System.EventHandler(this.ButtonADBNone_Click);
            // 
            // buttonADBAll
            // 
            this.buttonADBAll.Location = new System.Drawing.Point(82, 5);
            this.buttonADBAll.Name = "buttonADBAll";
            this.buttonADBAll.Size = new System.Drawing.Size(75, 23);
            this.buttonADBAll.TabIndex = 1;
            this.buttonADBAll.Text = "Select All";
            this.buttonADBAll.UseVisualStyleBackColor = true;
            this.buttonADBAll.Click += new System.EventHandler(this.ButtonADBAll_Click);
            // 
            // buttonUninstall
            // 
            this.buttonUninstall.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonUninstall.Location = new System.Drawing.Point(681, 194);
            this.buttonUninstall.Name = "buttonUninstall";
            this.buttonUninstall.Size = new System.Drawing.Size(75, 23);
            this.buttonUninstall.TabIndex = 8;
            this.buttonUninstall.Text = "Uninstall";
            this.buttonUninstall.UseVisualStyleBackColor = true;
            this.buttonUninstall.Click += new System.EventHandler(this.ButtonUninstall_Click);
            // 
            // buttonAddRoutine
            // 
            this.buttonAddRoutine.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonAddRoutine.Location = new System.Drawing.Point(682, 397);
            this.buttonAddRoutine.Name = "buttonAddRoutine";
            this.buttonAddRoutine.Size = new System.Drawing.Size(128, 23);
            this.buttonAddRoutine.TabIndex = 100;
            this.buttonAddRoutine.Text = "Add Routine";
            this.buttonAddRoutine.UseVisualStyleBackColor = true;
            this.buttonAddRoutine.Click += new System.EventHandler(this.ButtonAddRoutine_Click);
            // 
            // richTextBoxShell
            // 
            this.richTextBoxShell.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.richTextBoxShell.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.richTextBoxShell.DetectUrls = false;
            this.richTextBoxShell.Location = new System.Drawing.Point(682, 308);
            this.richTextBoxShell.Name = "richTextBoxShell";
            this.richTextBoxShell.Size = new System.Drawing.Size(128, 53);
            this.richTextBoxShell.TabIndex = 101;
            this.richTextBoxShell.Text = "";
            // 
            // buttonPush
            // 
            this.buttonPush.Anchor = ((System.Windows.Forms.AnchorStyles)((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Right)));
            this.buttonPush.Location = new System.Drawing.Point(682, 368);
            this.buttonPush.Name = "buttonPush";
            this.buttonPush.Size = new System.Drawing.Size(128, 23);
            this.buttonPush.TabIndex = 102;
            this.buttonPush.Text = "Push";
            this.buttonPush.UseVisualStyleBackColor = true;
            this.buttonPush.Click += new System.EventHandler(this.ButtonPush_Click);
            // 
            // dataGridViewADB
            // 
            this.dataGridViewADB.AllowUserToAddRows = false;
            this.dataGridViewADB.AllowUserToDeleteRows = false;
            this.dataGridViewADB.AllowUserToResizeRows = false;
            this.dataGridViewADB.Anchor = ((System.Windows.Forms.AnchorStyles)(((System.Windows.Forms.AnchorStyles.Top | System.Windows.Forms.AnchorStyles.Bottom) 
            | System.Windows.Forms.AnchorStyles.Right)));
            this.dataGridViewADB.AutoSizeColumnsMode = System.Windows.Forms.DataGridViewAutoSizeColumnsMode.Fill;
            this.dataGridViewADB.ColumnHeadersHeightSizeMode = System.Windows.Forms.DataGridViewColumnHeadersHeightSizeMode.AutoSize;
            this.dataGridViewADB.Columns.AddRange(new System.Windows.Forms.DataGridViewColumn[] {
            this.ColumnCheck,
            this.ColumnName,
            this.ColumnIMEI,
            this.ColumnSerial});
            dataGridViewCellStyle4.Alignment = System.Windows.Forms.DataGridViewContentAlignment.MiddleLeft;
            dataGridViewCellStyle4.BackColor = System.Drawing.SystemColors.Window;
            dataGridViewCellStyle4.Font = new System.Drawing.Font("Microsoft Sans Serif", 8.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(254)));
            dataGridViewCellStyle4.ForeColor = System.Drawing.SystemColors.ControlText;
            dataGridViewCellStyle4.NullValue = false;
            dataGridViewCellStyle4.SelectionBackColor = System.Drawing.SystemColors.Highlight;
            dataGridViewCellStyle4.SelectionForeColor = System.Drawing.SystemColors.HighlightText;
            dataGridViewCellStyle4.WrapMode = System.Windows.Forms.DataGridViewTriState.False;
            this.dataGridViewADB.DefaultCellStyle = dataGridViewCellStyle4;
            this.dataGridViewADB.Location = new System.Drawing.Point(3, 34);
            this.dataGridViewADB.Name = "dataGridViewADB";
            this.dataGridViewADB.ReadOnly = true;
            this.dataGridViewADB.RowHeadersVisible = false;
            this.dataGridViewADB.SelectionMode = System.Windows.Forms.DataGridViewSelectionMode.FullRowSelect;
            this.dataGridViewADB.ShowEditingIcon = false;
            this.dataGridViewADB.Size = new System.Drawing.Size(322, 372);
            this.dataGridViewADB.TabIndex = 0;
            this.dataGridViewADB.CellClick += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellClick);
            this.dataGridViewADB.CellValueChanged += new System.Windows.Forms.DataGridViewCellEventHandler(this.DataGridViewADB_CellValueChanged);
            // 
            // ColumnSerial
            // 
            this.ColumnSerial.HeaderText = "Serial";
            this.ColumnSerial.Name = "ColumnSerial";
            this.ColumnSerial.ReadOnly = true;
            this.ColumnSerial.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnSerial.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnIMEI
            // 
            this.ColumnIMEI.HeaderText = "IMEI";
            this.ColumnIMEI.Name = "ColumnIMEI";
            this.ColumnIMEI.ReadOnly = true;
            this.ColumnIMEI.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnName
            // 
            this.ColumnName.HeaderText = "Name";
            this.ColumnName.Name = "ColumnName";
            this.ColumnName.ReadOnly = true;
            this.ColumnName.Resizable = System.Windows.Forms.DataGridViewTriState.True;
            this.ColumnName.SortMode = System.Windows.Forms.DataGridViewColumnSortMode.NotSortable;
            // 
            // ColumnCheck
            // 
            this.ColumnCheck.AutoSizeMode = System.Windows.Forms.DataGridViewAutoSizeColumnMode.None;
            this.ColumnCheck.HeaderText = "";
            this.ColumnCheck.Name = "ColumnCheck";
            this.ColumnCheck.ReadOnly = true;
            this.ColumnCheck.Width = 20;
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(96F, 96F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Dpi;
            this.ClientSize = new System.Drawing.Size(822, 450);
            this.Controls.Add(this.buttonInstallBlueworld);
            this.Controls.Add(this.buttonPush);
            this.Controls.Add(this.richTextBoxShell);
            this.Controls.Add(this.buttonAddRoutine);
            this.Controls.Add(this.buttonUninstall);
            this.Controls.Add(this.panel1);
            this.Controls.Add(this.statusStrip1);
            this.Controls.Add(this.buttonInstall);
            this.Controls.Add(this.comboBoxRebootOptions);
            this.Controls.Add(this.buttonShellCommand);
            this.Controls.Add(this.buttonReboot);
            this.Controls.Add(this.buttonRefresh);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;
            this.Name = "MainForm";
            this.SizeGripStyle = System.Windows.Forms.SizeGripStyle.Hide;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
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
        private System.Windows.Forms.ComboBox comboBoxRebootOptions;
        private System.Windows.Forms.Button buttonInstall;
        private System.Windows.Forms.StatusStrip statusStrip1;
        private System.Windows.Forms.ToolStripStatusLabel toolStripStatusLabelDevice;
        private System.Windows.Forms.Panel panel1;
        private System.Windows.Forms.Button buttonADBNone;
        private System.Windows.Forms.Button buttonADBAll;
        private System.Windows.Forms.Button buttonUninstall;
        private System.Windows.Forms.DataGridView dataGridViewFastboot;
        private System.Windows.Forms.Button buttonFastbootNone;
        private System.Windows.Forms.Button buttonFastbootAll;
        private System.Windows.Forms.DataGridViewCheckBoxColumn dataGridViewCheckBoxColumnFastboot;
        private System.Windows.Forms.DataGridViewTextBoxColumn dataGridViewTextBoxColumnFastbootSerial;
        private System.Windows.Forms.ToolTip toolTipReboot;
        private System.Windows.Forms.Button buttonAddRoutine;
        private System.Windows.Forms.RichTextBox richTextBoxShell;
        private System.Windows.Forms.Button buttonPush;
        private System.Windows.Forms.DataGridView dataGridViewADB;
        private System.Windows.Forms.DataGridViewCheckBoxColumn ColumnCheck;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnName;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnIMEI;
        private System.Windows.Forms.DataGridViewTextBoxColumn ColumnSerial;
    }
}

