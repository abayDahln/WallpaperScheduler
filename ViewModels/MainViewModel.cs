using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Windows.Storage.Pickers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.ViewModels
{
    public partial class MainViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly SchedulerEngine _schedulerEngine;

#pragma warning disable MVVMTK0045
        [ObservableProperty]
        private bool _isPaused;

        [ObservableProperty]
        private string _statusText = "Scheduler Running";
#pragma warning restore MVVMTK0045

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public MainViewModel(ConfigService configService, SchedulerEngine schedulerEngine)
        {
            _configService = configService;
            _schedulerEngine = schedulerEngine;
            LoadWallpapers();
            IsPaused = _schedulerEngine.IsPaused;
        }

        private void LoadWallpapers()
        {
            Wallpapers.Clear();
            foreach (var wp in _configService.Config.WallpaperLibrary)
            {
                Wallpapers.Add(wp);
            }
        }

        [RelayCommand]
        public void TogglePause()
        {
            if (_schedulerEngine.IsPaused)
            {
                _schedulerEngine.Resume();
                StatusText = "Scheduler Running";
            }
            else
            {
                _schedulerEngine.Pause();
                StatusText = "Scheduler Paused";
            }
            IsPaused = _schedulerEngine.IsPaused;
        }

        [RelayCommand]
        public void ApplyWallpaperNow(WallpaperItem item)
        {
            if (item != null)
            {
                _schedulerEngine.ApplyById(item.Id);
            }
        }
    }
}
