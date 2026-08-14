using System;
using System.Collections.ObjectModel;
using System.IO;
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
                    id = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, DateTime.Now, ref lastApplied);
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

        public void RenameWallpaper(WallpaperItem item, string newLabel)
        {
            item.Label = newLabel;
            _configService.SaveConfig();
            int idx = Wallpapers.IndexOf(item);
            if (idx >= 0) Wallpapers[idx] = item;
        }

        public int GetUsageCount(string wallpaperId)
        {
            int count = 0;

            var w = _configService.Config.WeeklySchedule;
            var allWeeklySlots = w.Monday.Concat(w.Tuesday).Concat(w.Wednesday)
                .Concat(w.Thursday).Concat(w.Friday).Concat(w.Saturday).Concat(w.Sunday);
            count += allWeeklySlots.Count(s => s.WallpaperId == wallpaperId);

            count += _configService.Config.MonthlyOverrides.Sum(m => m.Slots.Count(s => s.WallpaperId == wallpaperId));
            count += _configService.Config.DateOverrides.Sum(d => d.Slots.Count(s => s.WallpaperId == wallpaperId));

            return count;
        }

        public void DeleteWallpaper(WallpaperItem item)
        {
            string path = Path.Combine(ConfigService.WallpapersDir, item.FileName);
            if (File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }

            _configService.Config.WallpaperLibrary.Remove(item);

            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Monday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Tuesday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Wednesday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Thursday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Friday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Saturday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Sunday, item.Id);

            foreach (var m in _configService.Config.MonthlyOverrides) RemoveIdFromSlots(m.Slots, item.Id);
            foreach (var d in _configService.Config.DateOverrides) RemoveIdFromSlots(d.Slots, item.Id);

            if (_configService.Config.Settings.DefaultWallpaperId == item.Id)
                _configService.Config.Settings.DefaultWallpaperId = null;

            _configService.SaveConfig();
            Wallpapers.Remove(item);
            _schedulerEngine.ForceReevaluate();
        }

        private static void RemoveIdFromSlots(System.Collections.Generic.List<TimeSlot> slots, string id)
        {
            slots.RemoveAll(s => s.WallpaperId == id);
        }

        public void ApplyNow(WallpaperItem item)
        {
            _schedulerEngine.ApplyById(item.Id);
        }
    }
}