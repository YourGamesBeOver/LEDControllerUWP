﻿using System;
using Windows.UI.Core;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
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

        private readonly MenuItem[] _menuItems =
        {
            new MenuItem {Name = "Device Selection", Glyph = '\uE950', Page = typeof(ConnectDisconnectPage)},
            new MenuItem {Name = "General Controls", Glyph = '\uE713', Page = typeof(ControlPage)},
            new MenuItem {Name = "LEDs", Glyph = '\uE781', Page = null}
        };


        public MainPage()
        {
            InitializeComponent();
            Current = this;
            MenuList.Loaded += MenuList_Loaded;
        }

        private void MenuList_Loaded(object sender, RoutedEventArgs e)
        {
            MenuList.SelectedIndex = 0;
        }

        public void GoToPage(int index)
        {
            MenuList.SelectedIndex = index;
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
            var item = menu?.SelectedItem as MenuItem;
            if (item?.Page == null) return;
            ContentFrame.Navigate(item.Page);
            Splitter.IsPaneOpen = false;           
        }

        private void MenuButton_OnClick(object sender, RoutedEventArgs e)
        {
            Splitter.IsPaneOpen = !Splitter.IsPaneOpen;
        }
    }

    public enum NotifyType {
        StatusMessage,
        ErrorMessage
    }
}
