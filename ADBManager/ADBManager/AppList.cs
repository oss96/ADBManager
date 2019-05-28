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
    public partial class AppList : Form
    {
        public AppList()
        {
            InitializeComponent();
        }
        public AppList(Dictionary<string, string> packages)
        {
            InitializeComponent();
            checkedListBoxAppList.Items.AddRange(packages.Keys.ToArray());
        }
    }
}
