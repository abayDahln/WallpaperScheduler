using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Services;
using WallpaperScheduler.ViewModels;
using WallpaperScheduler.Views;
using WinRT.Interop;

namespace WallpaperScheduler
{
    public sealed partial class MainWindow : Window
    {
        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            ViewModel = app.MainViewModel;
            this.Closed += MainWindow_Closed;
            ThemeService.Apply(this, app.ConfigService.Config.Settings.ThemeOverride);

            NavView.SelectionChanged += OnNavSelectionChanged;
            NavView.SelectedItem = NavView.MenuItems[0];
        }

        private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string tag = item.Tag?.ToString() ?? "library";
                switch (tag)
                {
                    case "library": ContentFrame.Navigate(typeof(LibraryPage)); break;
                    case "weekly": ContentFrame.Navigate(typeof(WeeklySchedulePage)); break;
                    case "overrides": ContentFrame.Navigate(typeof(OverridesPage)); break;
                    case "settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            var config = ((App)Application.Current).ConfigService.Config;
            if (config.Settings.CloseButtonMinimizesToTray)
            {
                args.Handled = true;
                this.AppWindow.Hide();
            }
        }

        private void OnOpenAppClick(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Show();
            this.Activate();
        }

        private void OnTogglePauseClick(object sender, RoutedEventArgs e)
        {
            ViewModel.TogglePause();
            TrayPauseItem.Text = ViewModel.IsPaused ? "Resume Schedule" : "Pause Schedule";
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            this.Closed -= MainWindow_Closed;
            Application.Current.Exit();
        }
    }
}