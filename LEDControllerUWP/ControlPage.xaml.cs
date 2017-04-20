using System.Threading.Tasks;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=234238

namespace LEDControllerUWP {
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class ControlPage : Page
    {

        private DeviceActionQueue _actionQueue;

        public ControlPage() {
            this.InitializeComponent();
        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);
            if (EventHandlerForDevice.Current.IsDeviceConnected) {
                SetAllControlsActive(true);
                _actionQueue = EventHandlerForDevice.Current.ActionQueue;
                DisabledReason.Visibility = Visibility.Collapsed;
            } else {
                SetAllControlsActive(false);
                DisabledReason.Visibility = Visibility.Visible;
            }
        }

        private void SetAllControlsActive(bool active)
        {
            foreach (var child in ActionList.Children)
            {
                var button = child as Control;
                if (button != null)
                {
                    button.IsEnabled = active;
                }
            }
        }

        private void ButtonRest_Click(object sender, RoutedEventArgs e) {
            if (!EventHandlerForDevice.Current.IsDeviceConnected) return;

            //NotifyUser("Sending Reset Signal...", NotifyType.StatusMessage);

            _actionQueue.Enqueue(DeviceActionType.Reset, new DeviceActionParams(), false,
                success => {
                    if (success) {
                        MainPage.Current.NotifyUser("Device Reset!", NotifyType.StatusMessage);
                    } else {
                        MainPage.Current.NotifyUser("Failed to reset device!", NotifyType.ErrorMessage);
                    }

                });

        }


        private void SliderBrightnessChanged(object sender, RangeBaseValueChangedEventArgs e) {
            if (!EventHandlerForDevice.Current.IsDeviceConnected) return;
            var value = (byte)SliderBrightness.Value;

            //NotifyUser("Setting brightness...", NotifyType.StatusMessage);
            Task.Run(() => _actionQueue.Enqueue(DeviceActionType.SetBrightness, new DeviceActionParams { Brightness = value }, true,
                success => {
                    if (success) {
                        MainPage.Current.NotifyUser($"Brightness set to {value}", NotifyType.StatusMessage);
                    } else {
                        MainPage.Current.NotifyUser("Failed to set Brightness", NotifyType.ErrorMessage);
                    }
                }));
        }
    }
}
