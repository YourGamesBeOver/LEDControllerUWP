using Windows.Devices.Enumeration;

namespace LEDControllerUWP {
    public class DeviceListEntry
    {

        public string DisplayName => DeviceInformation.Properties["System.ItemNameDisplay"] as string;

        public DeviceInformation DeviceInformation { get; }

        public string DeviceSelector { get; }

        /// <summary>
        /// The class is mainly used as a DeviceInformation wrapper so that the UI can bind to a list of these.
        /// </summary>
        /// <param name="deviceInformation"></param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        public DeviceListEntry(DeviceInformation deviceInformation, string deviceSelector) {
            this.DeviceInformation = deviceInformation;
            this.DeviceSelector = deviceSelector;
        }
    }
}
