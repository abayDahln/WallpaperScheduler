using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.ViewModels
{
    public partial class OverviewViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly SchedulerEngine _schedulerEngine;

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public int WallpaperCount { get; }
        public int WeeklySlotCount { get; }
        public int MonthlyCount { get; }
        public int DateCount { get; }

        public OverviewViewModel(ConfigService configService, SchedulerEngine schedulerEngine)
        {
            _configService = configService;
            _schedulerEngine = schedulerEngine;

            LoadWallpapers();

            var weekly = _configService.Config.WeeklySchedule;
            WeeklySlotCount = weekly.Monday.Count + weekly.Tuesday.Count + weekly.Wednesday.Count
                + weekly.Thursday.Count + weekly.Friday.Count + weekly.Saturday.Count + weekly.Sunday.Count;
            MonthlyCount = _configService.Config.MonthlyOverrides.Count;
            DateCount = _configService.Config.DateOverrides.Count;
            WallpaperCount = Wallpapers.Count;
        }

        public WallpaperItem? CurrentWallpaper
        {
            get
            {
                string? id = _schedulerEngine.LastAppliedWallpaperId;
                if (string.IsNullOrEmpty(id))
                {
                string? lastApplied = null;
                id = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, DateTime.Now, ref lastApplied).WallpaperId;
                }
                return string.IsNullOrEmpty(id) ? null : Wallpapers.FirstOrDefault(w => w.Id == id);
            }
        }

        public DateTime NextChangeTime => ScheduleResolver.GetNextEventTime(_configService.Config, DateTime.Now);

        public string DefaultWallpaperId
        {
            get => _configService.Config.Settings.DefaultWallpaperId ?? string.Empty;
        }

        public void SetDefaultWallpaper(string? id)
        {
            _configService.Config.Settings.DefaultWallpaperId = string.IsNullOrEmpty(id) ? null : id;
            _configService.SaveConfig();
            _schedulerEngine.ForceReevaluate();
        }

        public void ClearDefaultWallpaper() => SetDefaultWallpaper(null);

        public void LoadWallpapers()
        {
            Wallpapers.Clear();
            foreach (var item in _configService.Config.WallpaperLibrary)
            {
                Wallpapers.Add(item);
            }
        }

    }
}