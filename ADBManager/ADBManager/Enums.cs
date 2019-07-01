using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ADBManager
{
    public class Enums
    {
        public enum RoutineItemType
        {
            Install,
            Uninstall,
            Shell,
            Reboot,
            Pull,
            Push
        }
        public enum RestartOptions
        {
            Normal,
            Recovery,
            Bootloader,
            Fastboot
        }
        public enum Commands
        {
            Install,
            Uninstall,
            Shell,
            Restart,
            Pull,
            Push
        }
    }
}
