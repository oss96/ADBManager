using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADBManager
{
    public partial class Log : Form
    {
        string txt;
        private delegate void RefreshCallback(string newLine);
        private delegate void ClearCallback();


        public Log()
        {
            InitializeComponent();
            RefreshLog("");
        }

        internal void newLine(string newLine)
        {
            txt = newLine + "\n";
            RefreshLog(txt);
        }

        internal void RefreshLog(string newLine)
        {
            if (InvokeRequired)
            {
                RefreshCallback callback = new RefreshCallback(RefreshLog);
                try
                {
                    Invoke(callback, new object[] { newLine });
                }
                catch (Exception)
                {
                }
            }
            else
            {
                RefreshLogLines(newLine);
            }
        }
        internal void ClearLog()
        {
            if (InvokeRequired)
            {
                ClearCallback callback = new ClearCallback(ClearLog);
                try
                {
                    Invoke(callback);
                }
                catch (Exception)
                {
                }
            }
            else
            {
                Clear();
            }
        }

        private void Clear()
        {
            richTextBox1.Text = "";
        }

        private void RefreshLogLines(string newLine)
        {
            richTextBox1.Text += newLine;
        }

        private void buttonExit_Click(object sender, EventArgs e)
        {
            this.Close();
        }

        private void buttonClear_Click(object sender, EventArgs e)
        {
            ClearLog();
        }

        private void richTextBox1_TextChanged(object sender, EventArgs e)
        {
            // set the current caret position to the end
            richTextBox1.SelectionStart = richTextBox1.Text.Length;
            // scroll it automatically
            richTextBox1.ScrollToCaret();
        }
    }
}
