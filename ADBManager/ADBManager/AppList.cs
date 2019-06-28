using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Windows.Input;

namespace ADBManager
{
    public partial class AppList : Form
    {
        readonly List<List<string>> listPackages;
        List<string> packages;
        readonly ADB adb;

        public AppList(List<Dictionary<string, string>> inputPackages, ADB inputADB)
        {
            InitializeComponent();
            listPackages = new List<List<string>>();
            foreach (var item in inputPackages)
            {
                listPackages.Add(item.Keys.ToList());
            }
            adb = inputADB;
        }
        public AppList(Dictionary<string, string> inputPackage, ADB inputADB)
        {
            InitializeComponent();
            packages = inputPackage.Keys.ToList();
            adb = inputADB;
        }

        private void AppList_Load(object sender, EventArgs e)
        {
            MinimumSize = Size;
            GetCommonPackages();
            checkedListBoxAppList.Items.AddRange(packages.ToArray());
        }
        private void TextBoxSearch_Enter(object sender, EventArgs e)
        {
            if ((sender as ExTextBox).Text == "Search")
            {
                (sender as ExTextBox).Select(0, 0);
            }
        }
        private void TextBoxSearch_MouseDown(object sender, System.Windows.Forms.MouseEventArgs e)
        {
            if ((sender as ExTextBox).Text == "Search")
            {
                (sender as ExTextBox).Select(0, 0);
            }
        }
        private void TextBoxSearch_KeyPress(object sender, KeyPressEventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Space))
            {
                e.Handled = true;
            }
            else if (char.IsLetterOrDigit(e.KeyChar))
            {
                if ((sender as ExTextBox).Text == "Search")
                {
                    (sender as ExTextBox).Text = "";
                    (sender as ExTextBox).ForeColor = Color.Black;
                    (sender as ExTextBox).Selectable = true;
                    checkedListBoxAppList.Focus();
                    (sender as ExTextBox).Focus();
                }
            }

        }
        private void TextBoxSearch_TextChanged(object sender, EventArgs e)
        {
            if (Keyboard.IsKeyDown(Key.Back))
            {
                if ((sender as ExTextBox).TextLength == 0)
                {
                    (sender as ExTextBox).Text = "Search";
                    (sender as ExTextBox).ForeColor = Color.DarkGray;
                    (sender as ExTextBox).Selectable = false;
                    checkedListBoxAppList.Focus();
                    (sender as ExTextBox).Focus();
                }
            }
            List<string> result = packages.FindAll(x => x.Contains((sender as ExTextBox).Text));
            if (result.Count == 0)
            {
                if (textBoxSearch.Text == "Search")
                {
                    result = packages;
                }
            }
            checkedListBoxAppList.Items.Clear();
            foreach (var item in result)
            {
                _ = checkedListBoxAppList.Items.Add(item);
            }
        }
        private void ButtonCancel_Click(object sender, EventArgs e)
        {
            Close();
        }
        private void ButtonSave_Click(object sender, EventArgs e)
        {
            List<string> unInstallList = new List<string>();
            try
            {
                foreach (var item in checkedListBoxAppList.CheckedItems)
                {
                    unInstallList.Add(item.ToString());
                }
            }
            catch (InvalidOperationException)
            {
            }
            adb.SetUninstallList(unInstallList);
            Close();
        }
        private void GetCommonPackages()
        {
            packages = new List<string>();
            if (listPackages.Count > 1)
            {
                //from: https://stackoverflow.com/questions/1674742/intersection-of-multiple-lists-with-ienumerable-intersect
                IEnumerable<string> intersection = listPackages.Aggregate<IEnumerable<string>>(
                (previousList, nextList) => previousList.Intersect(nextList)
                 ).ToList();
                packages.AddRange(intersection);
            }
            else
            {
                packages.AddRange(listPackages.First());
            }
        }
    }
}
