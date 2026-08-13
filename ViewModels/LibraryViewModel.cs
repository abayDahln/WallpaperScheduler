using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.ViewModels
{
    public partial class LibraryViewModel : ObservableObject
    {
        private readonly ConfigService _configService;
        private readonly SchedulerEngine _schedulerEngine;

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public LibraryViewModel(ConfigService configService, SchedulerEngine schedulerEngine)
        {
            _configService = configService;
            _schedulerEngine = schedulerEngine;
            LoadWallpapers();
        }

        public void LoadWallpapers()
        {
            Wallpapers.Clear();
            foreach (var item in _configService.Config.WallpaperLibrary)
            {
                Wallpapers.Add(item);
            }
        }

        public WallpaperItem AddWallpaper(string sourcePath)
        {
            string ext = Path.GetExtension(sourcePath);
            string newFileName = $"{Guid.NewGuid():N}{ext}";
            string destPath = Path.Combine(ConfigService.WallpapersDir, newFileName);

            File.Copy(sourcePath, destPath, overwrite: true);

            var item = new WallpaperItem
            {
                FileName = newFileName,
                Label = Path.GetFileNameWithoutExtension(sourcePath)
            };

            _configService.Config.WallpaperLibrary.Add(item);
            _configService.SaveConfig();
            Wallpapers.Add(item);
            return item;
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

            // Check weekly
            var w = _configService.Config.WeeklySchedule;
            var allWeeklySlots = w.Monday.Concat(w.Tuesday).Concat(w.Wednesday)
                .Concat(w.Thursday).Concat(w.Friday).Concat(w.Saturday).Concat(w.Sunday);
            count += allWeeklySlots.Count(s => s.WallpaperId == wallpaperId);

            // Check monthly
            count += _configService.Config.MonthlyOverrides.Sum(m => m.Slots.Count(s => s.WallpaperId == wallpaperId));

            // Check date overrides
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

            // Remove references from schedules
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Monday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Tuesday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Wednesday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Thursday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Friday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Saturday, item.Id);
            RemoveIdFromSlots(_configService.Config.WeeklySchedule.Sunday, item.Id);

            foreach (var m in _configService.Config.MonthlyOverrides) RemoveIdFromSlots(m.Slots, item.Id);
            foreach (var d in _configService.Config.DateOverrides) RemoveIdFromSlots(d.Slots, item.Id);

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
