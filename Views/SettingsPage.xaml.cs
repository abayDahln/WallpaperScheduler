using System;
using System.Collections.Generic;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Services;
using WallpaperScheduler.ViewModels;

namespace WallpaperScheduler.Views
{
    public sealed partial class SettingsPage : Page
    {
        public SettingsViewModel ViewModel { get; }
        public List<string> StyleOptions { get; } = new() { "Fill", "Fit", "Stretch", "Tile", "Center", "Span" };
        public List<string> ThemeOptions { get; } = new() { "system", "light", "dark" };

        public SettingsPage()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            ViewModel = new SettingsViewModel(app.ConfigService);
            DataContext = this;
        }

        private void OnAutoStartToggled(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleAutoStart();
        }

        private void OnCloseMinimizesToggled(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleCloseMinimizes();
        }

        private void OnNotifyToggled(object sender, RoutedEventArgs e)
        {
            ViewModel.ToggleNotify();
        }

        private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (e.AddedItems.Count > 0 && e.AddedItems[0] is string style)
            {
                ViewModel.SetWallpaperStyle(style);
                ((App)Application.Current).SchedulerEngine.ReapplyCurrentWallpaper();
            }
        }

        private void OnThemeChanged(object sender, SelectionChangedEventArgs e)
        {
            ViewModel.UpdateTheme();
            if (App.MainWindow != null)
            {
                ThemeService.Apply(App.MainWindow, ViewModel.Theme);
            }
        }
    }
}