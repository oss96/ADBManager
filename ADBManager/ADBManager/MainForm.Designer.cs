namespace TraceMate_Install
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
            this.buttonListDevices = new System.Windows.Forms.Button();
            this.richTextBoxDevices = new System.Windows.Forms.RichTextBox();
            this.buttonReboot = new System.Windows.Forms.Button();
            this.buttonShellCommand = new System.Windows.Forms.Button();
            this.SuspendLayout();
            // 
            // buttonListDevices
            // 
            this.buttonListDevices.Location = new System.Drawing.Point(12, 45);
            this.buttonListDevices.Name = "buttonListDevices";
            this.buttonListDevices.Size = new System.Drawing.Size(75, 23);
            this.buttonListDevices.TabIndex = 0;
            this.buttonListDevices.Text = "List Devices";
            this.buttonListDevices.UseVisualStyleBackColor = true;
            this.buttonListDevices.Click += new System.EventHandler(this.Button1_Click);
            // 
            // richTextBoxDevices
            // 
            this.richTextBoxDevices.Location = new System.Drawing.Point(12, 74);
            this.richTextBoxDevices.Name = "richTextBoxDevices";
            this.richTextBoxDevices.Size = new System.Drawing.Size(75, 200);
            this.richTextBoxDevices.TabIndex = 1;
            this.richTextBoxDevices.Text = "";
            // 
            // buttonReboot
            // 
            this.buttonReboot.Location = new System.Drawing.Point(713, 45);
            this.buttonReboot.Name = "buttonReboot";
            this.buttonReboot.Size = new System.Drawing.Size(75, 23);
            this.buttonReboot.TabIndex = 0;
            this.buttonReboot.Text = "Reboot Device";
            this.buttonReboot.UseVisualStyleBackColor = true;
            this.buttonReboot.Click += new System.EventHandler(this.ButtonReboot_Click);
            // 
            // buttonShellCommand
            // 
            this.buttonShellCommand.Location = new System.Drawing.Point(578, 45);
            this.buttonShellCommand.Name = "buttonShellCommand";
            this.buttonShellCommand.Size = new System.Drawing.Size(129, 23);
            this.buttonShellCommand.TabIndex = 2;
            this.buttonShellCommand.Text = "Send Shell Command";
            this.buttonShellCommand.UseVisualStyleBackColor = true;
            this.buttonShellCommand.Click += new System.EventHandler(this.ButtonShellCommand_Click);
            // 
            // MainForm
            // 
            this.AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            this.AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            this.ClientSize = new System.Drawing.Size(800, 450);
            this.Controls.Add(this.buttonShellCommand);
            this.Controls.Add(this.buttonReboot);
            this.Controls.Add(this.richTextBoxDevices);
            this.Controls.Add(this.buttonListDevices);
            this.Name = "MainForm";
            this.Text = "Form1";
            this.ResumeLayout(false);

        }

        #endregion

        private System.Windows.Forms.Button buttonListDevices;
        private System.Windows.Forms.RichTextBox richTextBoxDevices;
        private System.Windows.Forms.Button buttonReboot;
        private System.Windows.Forms.Button buttonShellCommand;
    }
}

