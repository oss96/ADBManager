using SharpAdbClient;

namespace ADBManager
{
    public class AndroidDevice
    {
        public string Model { get; set; }
        public string Product { get; set; }
        public string Name { get; set; }
        public string Serial { get; set; }
        public DeviceState State { get; set; }
        public string TransportID { get; set; }
        public string IMEI { get; set; }


        public AndroidDevice(string inputModel, string inputName, string inputProduct, string inputSerial, DeviceState inputState, string inputTransportID, string inputIMEI)
        {

            Model = inputModel;
            Name = inputName;
            Product = inputProduct;
            Serial = inputSerial;
            State = inputState;
            TransportID = inputTransportID;
            IMEI = inputIMEI;
        }
        public AndroidDevice()
        {
            Model = string.Empty;
            Name = string.Empty;
            Product = string.Empty;
            Serial = string.Empty;
            State = DeviceState.Unknown;
            TransportID = string.Empty;
        }

        public DeviceData GetDevice()
        {
            DeviceData device = new DeviceData();
            device.Model = Model;
            device.Name = Name;
            device.Product = Product;
            device.Serial = Serial;
            device.State = State;
            device.TransportId = TransportID;
            return device;
        }

    }
}
