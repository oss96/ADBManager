using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SharpAdbClient;

namespace TraceMate_Install
{
    public class AndroidDevice
    {
        string model;
        string name;
        string product;
        string serial;
        string transportID;
        private DeviceState state;

        public string Model { get => model; set => model = value; }
        public string Product { get => product; set => product = value; }
        public string Name { get => name; set => name = value; }
        public string Serial { get => serial; set => serial = value; }
        public DeviceState State { get => state; set => state = value; }
        public string TransportID { get => transportID; set => transportID = value; }


        public AndroidDevice(string inputModel, string inputName, string inputProduct, string inputSerial, DeviceState inputState, string inputTransportID)
        {

            this.Model = inputModel;
            this.Name = inputName;
            this.Product = inputProduct;
            this.Serial = inputSerial;
            this.State = inputState;
            this.TransportID = inputTransportID;
        }

        public DeviceData GetDevice()
        {
            DeviceData device = new DeviceData();
            device.Model = this.Model;
            device.Name = this.Name;
            device.Product = this.Product;
            device.Serial = this.Serial;
            device.State = this.State;
            device.TransportId = this.TransportID;
            return device;
        }
    }
}
