using System;
using System.IO;
using System.Linq;
using System.Threading;
using Microsoft.Win32;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Services
{
    public class SchedulerEngine
    {
        private readonly ConfigService _configService;
        private System.Threading.Timer? _timer;
        private string? _lastAppliedWallpaperId;
        public bool IsPaused { get; private set; }

        public event EventHandler<string>? OnWallpaperChanged;
        public event EventHandler<string>? OnWallpaperSchedulerlyFailed;

        public SchedulerEngine(ConfigService configService)
        {
            _configService = configService;
            SystemEvents.PowerModeChanged += OnPowerModeChanged;
            SystemEvents.TimeChanged += OnTimeChanged;
        }

        public void Start()
        {
            IsPaused = false;
            EvaluateAndScheduleNext();
        }

        public void Pause()
        {
            IsPaused = true;
            _timer?.Change(Timeout.Infinite, Timeout.Infinite);
        }

        public void Resume()
        {
            IsPaused = false;
            EvaluateAndScheduleNext();
        }

        public void ForceReevaluate()
        {
            if (!IsPaused) EvaluateAndScheduleNext();
        }

        public void ReapplyCurrentWallpaper()
        {
            if (IsPaused) return;
            DateTime now = DateTime.Now;
            string? wallpaperId = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, now, ref _lastAppliedWallpaperId);
            if (!string.IsNullOrEmpty(wallpaperId))
            {
                ApplyById(wallpaperId);
            }
        }

        private void EvaluateAndScheduleNext()
        {
            DateTime now = DateTime.Now;
            string? wallpaperId = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, now, ref _lastAppliedWallpaperId);

            if (!string.IsNullOrEmpty(wallpaperId))
            {
                ApplyById(wallpaperId);
            }

            DateTime nextEvent = ScheduleResolver.GetNextEventTime(_configService.Config, now);
            TimeSpan delay = nextEvent - now;
            if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);

            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => EvaluateAndScheduleNext(), null, delay, Timeout.InfiniteTimeSpan);
        }

        public bool ApplyById(string wallpaperId)
        {
            var item = _configService.Config.WallpaperLibrary.FirstOrDefault(w => w.Id == wallpaperId);
            if (item == null) return false;

            string fullPath = Path.Combine(ConfigService.WallpapersDir, item.FileName);
            bool success = WallpaperApplyService.ApplyWallpaper(fullPath, _configService.Config.Settings.WallpaperStyle);

            if (success)
            {
                _lastAppliedWallpaperId = wallpaperId;
                OnWallpaperChanged?.Invoke(this, item.Label);
            }
            else
            {
                OnWallpaperSchedulerlyFailed?.Invoke(this, item.Label);
            }

            return success;
        }

        private void OnPowerModeChanged(object sender, PowerModeChangedEventArgs e)
        {
            if (e.Mode == PowerModes.Resume) ForceReevaluate();
        }

        private void OnTimeChanged(object? sender, EventArgs e) => ForceReevaluate();
    }
}
