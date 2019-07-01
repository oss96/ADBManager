using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace ADBManager
{
    class RoutineItem
    {
        string name;
        string value;
        int index;
        Enums.RoutineItemType itemType = new Enums.RoutineItemType();

        public string Name { get => name; set => name = value; }
        public string Value { get => value; set => this.value = value; }
        public Enums.RoutineItemType ItemType { get => itemType; set => itemType = value; }

        public RoutineItem()
        {

        }

        internal ListViewItem GetListViewItem(int inputIndex)
        {
            /* ImageIndex
             * 0: Install
             * 1: Uninstall
             * 2: Shell
             * 3: Reboot
             * 4: Pull
             * 5: Push
            */
            this.index = inputIndex;
            ListViewItem item = new ListViewItem
            {
                Text = Name
            };
            switch (ItemType)
            {
                case Enums.RoutineItemType.Install:
                    item.ImageIndex = 0;
                    break;
                case Enums.RoutineItemType.Uninstall:
                    item.ImageIndex = 1;
                    break;
                case Enums.RoutineItemType.Shell:
                    item.ImageIndex = 2;
                    break;
                case Enums.RoutineItemType.Reboot:
                    item.ImageIndex = 3;
                    break;
                case Enums.RoutineItemType.Pull:
                    item.ImageIndex = 4;
                    break;
                case Enums.RoutineItemType.Push:
                    item.ImageIndex = 5;
                    break;
                default:
                    break;
            }
            return item;
        }
    }
}
