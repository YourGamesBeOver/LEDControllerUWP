using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Windows.ApplicationModel;
using Windows.Devices.Enumeration;
using Windows.Devices.SerialCommunication;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace LEDControllerUWP
{
    /// <summary>
    /// An empty page that can be used on its own or navigated to within a Frame.
    /// </summary>
    public sealed partial class MainPage : Page
    {

        public static MainPage Current;

        private static readonly Type[] MenuTypes = {typeof(ConnectDisconnectPage), typeof(ControlPage)};

        public MainPage()
        {
            InitializeComponent();
            Current = this;

        }

        protected override void OnNavigatedTo(NavigationEventArgs e)
        {
            base.OnNavigatedTo(e);

            if (Window.Current.Bounds.Width < 640) {
                MenuList.SelectedIndex = -1;
            } else {
                MenuList.SelectedIndex = 0;
            }
        }

        #region Notification System

        /// <summary>
        /// Display a message to the user.
        /// This method may be called from any thread.
        /// </summary>
        /// <param name="strMessage"></param>
        /// <param name="type"></param>
        public void NotifyUser(string strMessage, NotifyType type) {
            // If called from the UI thread, then update immediately.
            // Otherwise, schedule a task on the UI thread to perform the update.
            if (Dispatcher.HasThreadAccess) {
                UpdateStatus(strMessage, type);
            } else {
                // ReSharper disable once UnusedVariable
                var task = Dispatcher.RunAsync(CoreDispatcherPriority.Normal, () => UpdateStatus(strMessage, type));
            }
        }

        private void UpdateStatus(string strMessage, NotifyType type) {
            switch (type) {
                case NotifyType.StatusMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Green);
                    break;
                case NotifyType.ErrorMessage:
                    StatusBorder.Background = new SolidColorBrush(Windows.UI.Colors.Red);
                    break;
            }

            StatusBlock.Text = strMessage;

            // Collapse the StatusBlock if it has no text to conserve real estate.
            StatusBorder.Visibility = (StatusBlock.Text != string.Empty) ? Visibility.Visible : Visibility.Collapsed;
            if (StatusBlock.Text != string.Empty) {
                StatusBorder.Visibility = Visibility.Visible;
                StatusPanel.Visibility = Visibility.Visible;
            } else {
                StatusBorder.Visibility = Visibility.Collapsed;
                StatusPanel.Visibility = Visibility.Collapsed;
            }
        }

        #endregion

        private void MenuList_OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            var menu = sender as ListBox;
            if (menu == null) return;
            if (menu.SelectedIndex >= 0 && menu.SelectedIndex < MenuTypes.Length)
            {
                ContentFrame.Navigate(MenuTypes[menu.SelectedIndex]);
            }
            if (Window.Current.Bounds.Width < 640) {
                Splitter.IsPaneOpen = false;
            }
        }
    }

    public enum NotifyType {
        StatusMessage,
        ErrorMessage
    }
}
