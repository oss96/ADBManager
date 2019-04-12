using SharpAdbClient;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TraceMate_Install
{
    public class ADB
    {
        public static List<AndroidDevice> GetConnectedDevice()
        {
            //Get the first entry from the list of connected Android devices
            List<AndroidDevice> androidDevices = new List<AndroidDevice>();
            try
            {
                AdbServer adb = new AdbServer();
                var result = adb.StartServer(@"C:\Users\o.al-alali\AppData\Local\Android\Sdk\platform-tools\adb.exe", restartServerIfNewer: false);

                AdbServerStatus adbServerStatus = adb.GetStatus();

                var devices = AdbClient.Instance.GetDevices();

                foreach (var item in devices)
                {
                    androidDevices.Add(new AndroidDevice(item.Model, item.Name, item.Product, item.Serial, item.State, item.TransportId));
                }
            }
            catch
            {

            }
            return androidDevices;
        }
        public static void RebootDevice(DeviceData device)
        {
            AdbClient.Instance.Reboot(device);
        }
        public static void RebootDevice(string mode, DeviceData device)
        {
            AdbClient.Instance.Reboot(mode, device);
        }
        public static void SendShellCommand(string command, DeviceData device)
        {
            OutputReceiver outputReceiver = new OutputReceiver(); 
            AdbClient.Instance.ExecuteRemoteCommand(command, device, outputReceiver);
        }

    }
    class OutputReceiver : IShellOutputReceiver
    {
        public bool ParsesErrors { get; set; }

        public void AddOutput(string line)
        {
        }

        public void Flush()
        {
        }
    }
}
