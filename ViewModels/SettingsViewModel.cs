using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.ViewModels
{
    public partial class SettingsViewModel : ObservableObject
    {
        private readonly ConfigService _configService;

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

#pragma warning disable MVVMTK0045
        [ObservableProperty]
        private string? _defaultWallpaperId = null;
        [ObservableProperty]
        private bool _autoStart;

        [ObservableProperty]
        private bool _closeMinimizesToTray;

        [ObservableProperty]
        private bool _notifyOnChange;

        [ObservableProperty]
        private string _wallpaperStyle = "Fill";

        [ObservableProperty]
        private string _theme = "system";
#pragma warning restore MVVMTK0045

        public SettingsViewModel(ConfigService configService)
        {
            _configService = configService;
            var settings = _configService.Config.Settings;
            AutoStart = settings.AutoStart;
            CloseMinimizesToTray = settings.CloseButtonMinimizesToTray;
            NotifyOnChange = settings.NotifyOnWallpaperChange;
            WallpaperStyle = settings.WallpaperStyle;
            Theme = settings.ThemeOverride;
            DefaultWallpaperId = settings.DefaultWallpaperId;
            foreach (var wp in configService.Config.WallpaperLibrary) Wallpapers.Add(wp);
        }

        public void ToggleAutoStart()
        {
            _configService.Config.Settings.AutoStart = AutoStart;
            _configService.SaveConfig();
            AutoStartService.SetAutoStart(AutoStart);
        }

        public void ToggleCloseMinimizes()
        {
            _configService.Config.Settings.CloseButtonMinimizesToTray = CloseMinimizesToTray;
            _configService.SaveConfig();
        }

        public void ToggleNotify()
        {
            _configService.Config.Settings.NotifyOnWallpaperChange = NotifyOnChange;
            _configService.SaveConfig();
        }

        public void UpdateStyle()
        {
            _configService.Config.Settings.WallpaperStyle = WallpaperStyle;
            _configService.SaveConfig();
        }

        public void SetWallpaperStyle(string style)
        {
            WallpaperStyle = style;
            _configService.Config.Settings.WallpaperStyle = style;
            _configService.SaveConfig();
        }

        public void UpdateTheme()
        {
            _configService.Config.Settings.ThemeOverride = Theme;
            _configService.SaveConfig();
        }

        public void UpdateDefaultWallpaper()
        {
            _configService.Config.Settings.DefaultWallpaperId = DefaultWallpaperId;
            _configService.SaveConfig();
        }
    }
}