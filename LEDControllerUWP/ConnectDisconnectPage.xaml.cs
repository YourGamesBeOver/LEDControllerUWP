using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace LEDControllerUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ConnectDisconnectPage : Page
    {
        private SuspendingEventHandler _appSuspendEventHandler;
        private EventHandler<object> _appResumeEventHandler;


        private readonly ObservableCollection<DeviceListEntry> _listOfDevices;
        private readonly Dictionary<DeviceWatcher, string> _mapDeviceWatchersToDeviceSelector;

        private bool _watchersSuspended;
        private bool _watchersStarted;

        private DeviceConnection _connection;
        private DeviceActionQueue _actionQueue;

        // Has all the devices enumerated by the device watcher?
        private bool _isAllDevicesEnumerated;

        public ConnectDisconnectPage()
        {
            this.InitializeComponent();
            _listOfDevices = new ObservableCollection<DeviceListEntry>();
            _mapDeviceWatchersToDeviceSelector = new Dictionary<DeviceWatcher, string>();
            _watchersStarted = false;
            _watchersSuspended = false;

            _isAllDevicesEnumerated = false;
        }


        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            // If we are connected to the device, we should disable the list of devices
            // to prevent the user from opening a device without explicitly closing

            UpdateConnectDisconnectButtonsAndList(!EventHandlerForDevice.Current.IsDeviceConnected);

            if (EventHandlerForDevice.Current.IsDeviceConnected)
            {
                // These notifications will occur if we are waiting to reconnect to device when we start the page
                EventHandlerForDevice.Current.OnDeviceConnected = OnDeviceConnected;
                EventHandlerForDevice.Current.OnDeviceClose = OnDeviceClosing;
            }

            // Begin watching out for events
            StartHandlingAppEvents();

            // Initialize the desired device watchers so that we can watch for when devices are connected/removed
            InitializeDeviceWatchers();
            StartDeviceWatchers();

            DeviceListSource.Source = _listOfDevices;
        }

        /// <summary>
        /// Unregister from App events and DeviceWatcher events because this page will be unloaded.
        /// </summary>
        /// <param name="eventArgs"></param>
        protected override void OnNavigatedFrom(NavigationEventArgs eventArgs)
        {
            StopDeviceWatchers();
            StopHandlingAppEvents();

            // We no longer care about the device being connected
            EventHandlerForDevice.Current.OnDeviceConnected = null;
            EventHandlerForDevice.Current.OnDeviceClose = null;
        }

        private async void ButtonConnect_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count <= 0) return;
            var obj = selection[0];
            var entry = (DeviceListEntry) obj;

            if (entry == null) return;
            // Create an EventHandlerForDevice to watch for the device we are connecting to
            EventHandlerForDevice.CreateNewEventHandlerForDevice();

            // Get notified when the device was successfully connected to or about to be closed
            EventHandlerForDevice.Current.OnDeviceConnected = OnDeviceConnected;
            EventHandlerForDevice.Current.OnDeviceClose = OnDeviceClosing;

            // It is important that the FromIdAsync call is made on the UI thread because the consent prompt, when present,
            // can only be displayed on the UI thread. Since this method is invoked by the UI, we are already in the UI thread.
            bool openSuccess =
                await EventHandlerForDevice.Current.OpenDeviceAsync(entry.DeviceInformation, entry.DeviceSelector);

            // Disable connect button if we connected to the device
            UpdateConnectDisconnectButtonsAndList(!openSuccess);
        }

        private void ButtonDisconnect_Click(object sender, RoutedEventArgs e)
        {
            var selection = ConnectDevices.SelectedItems;

            if (selection.Count > 0)
            {
                var obj = selection[0];
                var entry = (DeviceListEntry) obj;

                if (entry != null)
                {
                    EventHandlerForDevice.Current.CloseDevice();
                }
            }

            UpdateConnectDisconnectButtonsAndList(true);
        }

        /// <summary>
        /// Initialize device watchers to watch for the Serial Devices.
        ///
        /// GetDeviceSelector return an AQS string that can be passed directly into DeviceWatcher.createWatcher() or  DeviceInformation.createFromIdAsync(). 
        ///
        /// In this sample, a DeviceWatcher will be used to watch for devices because we can detect surprise device removals.
        /// </summary>
        private void InitializeDeviceWatchers()
        {
            // Target all Serial Devices present on the system
            //var deviceSelector = SerialDevice.GetDeviceSelector();

            // Other variations of GetDeviceSelector() usage are commented for reference
            //
            // Target a specific USB Serial Device using its VID and PID (here Arduino VID/PID is used)
            var deviceSelector = SerialDevice.GetDeviceSelectorFromUsbVidPid(0x2A03, 0x0043);
            //
            // Target a specific Serial Device by its COM PORT Name - "COM3"
            // var deviceSelector = SerialDevice.GetDeviceSelector("COM3");
            //
            // Target a specific UART based Serial Device by its COM PORT Name (usually defined in ACPI) - "UART1"
            // var deviceSelector = SerialDevice.GetDeviceSelector("UART1");
            //

            // Create a device watcher to look for instances of the Serial Device that match the device selector
            // used earlier.

            var deviceWatcher = DeviceInformation.CreateWatcher(deviceSelector);

            // Allow the EventHandlerForDevice to handle device watcher events that relates or effects our device (i.e. device removal, addition, app suspension/resume)
            AddDeviceWatcher(deviceWatcher, deviceSelector);
        }

        private void StartHandlingAppEvents()
        {
            _appSuspendEventHandler = OnAppSuspension;
            _appResumeEventHandler = OnAppResume;

            // This event is raised when the app is exited and when the app is suspended
            Application.Current.Suspending += _appSuspendEventHandler;

            Application.Current.Resuming += _appResumeEventHandler;
        }

        private void StopHandlingAppEvents()
        {
            // This event is raised when the app is exited and when the app is suspended
            Application.Current.Suspending -= _appSuspendEventHandler;

            Application.Current.Resuming -= _appResumeEventHandler;
        }

        /// <summary>
        /// Registers for Added, Removed, and Enumerated events on the provided deviceWatcher before adding it to an internal list.
        /// </summary>
        /// <param name="deviceWatcher"></param>
        /// <param name="deviceSelector">The AQS used to create the device watcher</param>
        private void AddDeviceWatcher(DeviceWatcher deviceWatcher, string deviceSelector)
        {
            deviceWatcher.Added += OnDeviceAdded;
            deviceWatcher.Removed += OnDeviceRemoved;
            deviceWatcher.EnumerationCompleted += OnDeviceEnumerationComplete;

            _mapDeviceWatchersToDeviceSelector.Add(deviceWatcher, deviceSelector);
        }

        /// <summary>
        /// Starts all device watchers including ones that have been individually stopped.
        /// </summary>
        private void StartDeviceWatchers()
        {
            // Start all device watchers
            _watchersStarted = true;
            _isAllDevicesEnumerated = false;

            foreach (var deviceWatcher in _mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status != DeviceWatcherStatus.Started)
                    && (deviceWatcher.Status != DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Start();
                }
            }
        }

        /// <summary>
        /// Stops all device watchers.
        /// </summary>
        private void StopDeviceWatchers()
        {
            // Stop all device watchers
            foreach (var deviceWatcher in _mapDeviceWatchersToDeviceSelector.Keys)
            {
                if ((deviceWatcher.Status == DeviceWatcherStatus.Started)
                    || (deviceWatcher.Status == DeviceWatcherStatus.EnumerationCompleted))
                {
                    deviceWatcher.Stop();
                }
            }

            // Clear the list of devices so we don't have potentially disconnected devices around
            ClearDeviceEntries();

            _watchersStarted = false;
        }

        /// <summary>
        /// Creates a DeviceListEntry for a device and adds it to the list of devices in the UI
        /// </summary>
        /// <param name="deviceInformation">DeviceInformation on the device to be added to the list</param>
        /// <param name="deviceSelector">The AQS used to find this device</param>
        private void AddDeviceToList(DeviceInformation deviceInformation, string deviceSelector)
        {
            // search the device list for a device with a matching interface ID
            var match = FindDevice(deviceInformation.Id);

            // Add the device if it's new
            if (match == null)
            {
                // Create a new element for this device interface, and queue up the query of its
                // device information
                match = new DeviceListEntry(deviceInformation, deviceSelector);

                // Add the new element to the end of the list of devices
                _listOfDevices.Add(match);
            }
        }

        private void RemoveDeviceFromList(string deviceId)
        {
            // Removes the device entry from the interal list; therefore the UI
            var deviceEntry = FindDevice(deviceId);

            _listOfDevices.Remove(deviceEntry);
        }

        /// <summary>
        /// Searches through the existing list of devices for the first DeviceListEntry that has
        /// the specified device Id.
        /// </summary>
        /// <param name="deviceId">Id of the device that is being searched for</param>
        /// <returns>DeviceListEntry that has the provided Id; else a nullptr</returns>
        private DeviceListEntry FindDevice(string deviceId)
        {
            return deviceId == null
                ? null
                : _listOfDevices.FirstOrDefault(entry => entry.DeviceInformation.Id == deviceId);
        }

        private void ClearDeviceEntries()
        {
            _listOfDevices.Clear();
        }

        /// <summary>
        /// We must stop the DeviceWatchers because device watchers will continue to raise events even if
        /// the app is in suspension, which is not desired (drains battery). We resume the device watcher once the app resumes again.
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnAppSuspension(object sender, SuspendingEventArgs args)
        {
            if (_watchersStarted)
            {
                _watchersSuspended = true;
                StopDeviceWatchers();
            }
            else
            {
                _watchersSuspended = false;
            }
        }

        /// <summary>
        /// See OnAppSuspension for why we are starting the device watchers again
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private void OnAppResume(object sender, object args)
        {
            if (!_watchersSuspended) return;
            _watchersSuspended = false;
            StartDeviceWatchers();
        }

        /// <summary>
        /// We will remove the device from the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformationUpdate"></param>
        private async void OnDeviceRemoved(DeviceWatcher sender, DeviceInformationUpdate deviceInformationUpdate)
        {
            await MainPage.Current.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    MainPage.Current.NotifyUser(
                        "Device removed - " + deviceInformationUpdate.Properties["System.ItemNameDisplay"],
                        NotifyType.StatusMessage);
                    RemoveDeviceFromList(deviceInformationUpdate.Id);
                });
        }

        /// <summary>
        /// This function will add the device to the listOfDevices so that it shows up in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceAdded(DeviceWatcher sender, DeviceInformation deviceInformation)
        {
            await MainPage.Current.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    MainPage.Current.NotifyUser(
                        "Device added - " + deviceInformation.Properties["System.ItemNameDisplay"],
                        NotifyType.StatusMessage);
                    AddDeviceToList(deviceInformation, _mapDeviceWatchersToDeviceSelector[sender]);
                });
        }

        /// <summary>
        /// Notify the UI whether or not we are connected to a device
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="args"></param>
        private async void OnDeviceEnumerationComplete(DeviceWatcher sender, object args)
        {
            await MainPage.Current.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    _isAllDevicesEnumerated = true;

                    // If we finished enumerating devices and the device has not been connected yet, the OnDeviceConnected method
                    // is responsible for selecting the device in the device list (UI); otherwise, this method does that.
                    if (EventHandlerForDevice.Current.IsDeviceConnected)
                    {
                        SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

                        ButtonDisconnect.IsEnabled = true;

                        if (EventHandlerForDevice.Current.Device.PortName != "")
                        {
                            MainPage.Current.NotifyUser("Connected to - " +
                                                        EventHandlerForDevice.Current.Device.PortName +
                                                        " - " +
                                                        EventHandlerForDevice.Current.DeviceInformation.Id,
                                NotifyType.StatusMessage);
                        }
                        else
                        {
                            MainPage.Current.NotifyUser("Connected to - " +
                                                        EventHandlerForDevice.Current.DeviceInformation.Id,
                                NotifyType.StatusMessage);
                        }
                    }
                    else
                    {
                        ButtonDisconnect.IsEnabled = false;
                        MainPage.Current.NotifyUser("No device is currently connected", NotifyType.StatusMessage);
                    }
                });
        }

        /// <summary>
        /// If all the devices have been enumerated, select the device in the list we connected to. Otherwise let the EnumerationComplete event
        /// from the device watcher handle the device selection
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private void OnDeviceConnected(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            _actionQueue?.Dispose();
            _connection?.Dispose();
            _connection = new DeviceConnection(sender.Device);
            _actionQueue = new DeviceActionQueue(_connection);

            // Find and select our connected device
            if (_isAllDevicesEnumerated)
            {
                SelectDeviceInList(EventHandlerForDevice.Current.DeviceInformation.Id);

                ButtonDisconnect.IsEnabled = true;
            }

            if (EventHandlerForDevice.Current.Device.PortName != "")
            {
                MainPage.Current.NotifyUser("Connected to - " +
                                            EventHandlerForDevice.Current.Device.PortName +
                                            " - " +
                                            EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
            else
            {
                MainPage.Current.NotifyUser("Connected to - " +
                                            EventHandlerForDevice.Current.DeviceInformation.Id, NotifyType.StatusMessage);
            }
        }

        /// <summary>
        /// The device was closed. If we will autoreconnect to the device, reflect that in the UI
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="deviceInformation"></param>
        private async void OnDeviceClosing(EventHandlerForDevice sender, DeviceInformation deviceInformation)
        {
            _actionQueue?.Dispose();
            _connection?.Dispose();
            await MainPage.Current.Dispatcher.RunAsync(
                CoreDispatcherPriority.Normal,
                () =>
                {
                    // We were connected to the device that was unplugged, so change the "Disconnect from device" button
                    // to "Do not reconnect to device"
                    if (ButtonDisconnect.IsEnabled)
                    {
                        ButtonDisconnect.IsEnabled = false;
                    }
                });
        }

        /// <summary>
        /// Selects the item in the UI's listbox that corresponds to the provided device id. If there are no
        /// matches, we will deselect anything that is selected.
        /// </summary>
        /// <param name="deviceIdToSelect">The device id of the device to select on the list box</param>
        private void SelectDeviceInList(string deviceIdToSelect)
        {
            // Don't select anything by default.
            ConnectDevices.SelectedIndex = -1;

            for (var deviceListIndex = 0; deviceListIndex < _listOfDevices.Count; deviceListIndex++)
            {
                if (_listOfDevices[deviceListIndex].DeviceInformation.Id != deviceIdToSelect) continue;
                ConnectDevices.SelectedIndex = deviceListIndex;
                break;
            }
        }

        /// <summary>
        /// When ButtonConnectToDevice is disabled, ConnectDevices list will also be disabled.
        /// </summary>
        /// <param name="enableConnectButton">The state of ButtonConnectToDevice</param>
        private void UpdateConnectDisconnectButtonsAndList(bool enableConnectButton)
        {
            ButtonConnect.IsEnabled = enableConnectButton;
            ButtonDisconnect.IsEnabled = !enableConnectButton;

            ConnectDevices.IsEnabled = enableConnectButton;
            //foreach (var child in ActionList.Children) {
            //    var button = child as Control;
            //    if (button != null) {
            //        button.IsEnabled = !enableConnectButton;
            //    }
        }
    }
}