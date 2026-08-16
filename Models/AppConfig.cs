using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json.Serialization;

namespace WallpaperScheduler.Models
{
    public class AppSettings
    {
        public bool AutoStart { get; set; } = true;
        public bool CloseButtonMinimizesToTray { get; set; } = true;
        public bool NotifyOnWallpaperChange { get; set; } = false;
        public bool HideTrayIcon { get; set; } = false;
        public string? DefaultWallpaperId { get; set; }
        public string ThemeOverride { get; set; } = "system"; // system, light, dark
        public string WallpaperStyle { get; set; } = "Fill"; // Fill, Fit, Stretch, Tile, Center, Span
    }

    public class WallpaperItem
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string FileName { get; set; } = string.Empty;
        public string Label { get; set; } = string.Empty;
        public DateTime AddedAt { get; set; } = DateTime.Now;

        // Custom-style crop area, normalized 0..1 relative to the image
        public double CropLeft { get; set; }
        public double CropTop { get; set; }
        public double CropWidth { get; set; } = 1;
        public double CropHeight { get; set; } = 1;

        public bool HasCustomCrop =>
            CropWidth > 0 && CropHeight > 0
            && (CropLeft > 0 || CropTop > 0 || CropWidth < 1 || CropHeight < 1);

        [JsonIgnore]
        public string FullPath => Path.Combine(
            Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WallpaperSchedule", "Wallpapers"),
            FileName);

        [JsonIgnore]
        public string ThumbPath => WallpaperScheduler.Helpers.ThumbnailGenerator.ThumbPathFor(FileName);

        private Microsoft.UI.Xaml.Media.ImageSource? _thumbnail;

        [JsonIgnore]
        public Microsoft.UI.Xaml.Media.ImageSource Thumbnail
        {
            get
            {
                if (_thumbnail == null)
                {
                    string path = File.Exists(ThumbPath) ? ThumbPath : FullPath;
                    _thumbnail = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri(path))
                    {
                        DecodePixelWidth = 512
                    };
                }
                return _thumbnail;
            }
        }
    }

    public class TimeSlot
    {
        public string Start { get; set; } = "00:00"; // HH:mm
        public string End { get; set; } = "24:00";   // HH:mm or 24:00
        public string WallpaperId { get; set; } = string.Empty;
        public string WallpaperStyle { get; set; } = string.Empty; // empty = follow global setting

        [JsonIgnore]
        public TimeSpan StartTimeSpan => TimeSpan.Parse(Start == "24:00" ? "23:59:59" : Start);
        [JsonIgnore]
        public TimeSpan EndTimeSpan => (End == "24:00" || End == "00:00") ? TimeSpan.FromDays(1) : TimeSpan.Parse(End);
    }

    public class WeeklySchedule
    {
        public List<TimeSlot> Monday { get; set; } = new();
        public List<TimeSlot> Tuesday { get; set; } = new();
        public List<TimeSlot> Wednesday { get; set; } = new();
        public List<TimeSlot> Thursday { get; set; } = new();
        public List<TimeSlot> Friday { get; set; } = new();
        public List<TimeSlot> Saturday { get; set; } = new();
        public List<TimeSlot> Sunday { get; set; } = new();

        public List<TimeSlot> GetDaySlots(DayOfWeek dayOfWeek) => dayOfWeek switch
        {
            DayOfWeek.Monday => Monday,
            DayOfWeek.Tuesday => Tuesday,
            DayOfWeek.Wednesday => Wednesday,
            DayOfWeek.Thursday => Thursday,
            DayOfWeek.Friday => Friday,
            DayOfWeek.Saturday => Saturday,
            DayOfWeek.Sunday => Sunday,
            _ => new()
        };
    }

    public class MonthlyOverride
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public int DayOfMonth { get; set; } = 1;
        public string Label { get; set; } = string.Empty;
        public List<TimeSlot> Slots { get; set; } = new();
    }

    public class DateOverride
    {
        public string Id { get; set; } = Guid.NewGuid().ToString("N")[..8];
        public string Date { get; set; } = DateTime.Today.ToString("yyyy-MM-dd");
        public string Label { get; set; } = string.Empty;
        public List<TimeSlot> Slots { get; set; } = new();
    }

    public class AppConfig
    {
        public int Version { get; set; } = 1;
        public AppSettings Settings { get; set; } = new();
        public List<WallpaperItem> WallpaperLibrary { get; set; } = new();
        public WeeklySchedule WeeklySchedule { get; set; } = new();
        public List<MonthlyOverride> MonthlyOverrides { get; set; } = new();
        public List<DateOverride> DateOverrides { get; set; } = new();
    }
}
