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
        private readonly WallpaperFrameService? _frameService;
        private System.Threading.Timer? _timer;
        private string? _lastAppliedWallpaperId;
        private string? _lastAppliedStyle;
        public bool IsPaused { get; private set; }
        public string? LastAppliedWallpaperId => _lastAppliedWallpaperId;

        public event EventHandler<string>? OnWallpaperChanged;
        public event EventHandler<string>? OnWallpaperSchedulerlyFailed;

        public SchedulerEngine(ConfigService configService, WallpaperFrameService? frameService = null)
        {
            _configService = configService;
            _frameService = frameService;
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

        public void ForceReevaluate(bool fresh = false, bool force = false)
        {
            if (!IsPaused) EvaluateAndScheduleNext(fresh, force);
        }

        public void ReapplyCurrentWallpaper(bool force = false)
        {
            if (IsPaused) return;
            DateTime now = DateTime.Now;
            string? fallback = _lastAppliedWallpaperId;
            var (id, style) = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, now, ref fallback);
            if (!string.IsNullOrEmpty(id))
            {
                ApplyById(id, style, force);
            }
        }

        public void OnWallpaperDeleted(string wallpaperId)
        {
            if (_lastAppliedWallpaperId == wallpaperId)
            {
                _lastAppliedWallpaperId = null;
            }
            ForceReevaluate();
        }

        private void EvaluateAndScheduleNext(bool fresh = false, bool force = false)
        {
            DateTime now = DateTime.Now;
            string? fallback = fresh ? null : _lastAppliedWallpaperId;
            var (id, style) = ScheduleResolver.ResolveActiveWallpaper(_configService.Config, now, ref fallback);

            if (!string.IsNullOrEmpty(id))
            {
                ApplyById(id, style, force);
            }

            ScheduleNextTimer();
        }

        private void ScheduleNextTimer()
        {
            DateTime now = DateTime.Now;
            DateTime nextEvent = ScheduleResolver.GetNextEventTime(_configService.Config, now);
            TimeSpan delay = nextEvent - now;
            if (delay < TimeSpan.FromSeconds(1)) delay = TimeSpan.FromSeconds(1);

            _timer?.Dispose();
            _timer = new System.Threading.Timer(_ => EvaluateAndScheduleNext(), null, delay, Timeout.InfiniteTimeSpan);
        }

        public bool ApplyById(string wallpaperId, string? style = null, bool force = false)
        {
            string effectiveStyle = string.IsNullOrEmpty(style)
                ? _configService.Config.Settings.WallpaperStyle
                : style;
            if (!force && wallpaperId == _lastAppliedWallpaperId && effectiveStyle == _lastAppliedStyle) return true;

            var item = _configService.Config.WallpaperLibrary.FirstOrDefault(w => w.Id == wallpaperId);
            if (item == null) return false;

            string fullPath = Path.Combine(ConfigService.WallpapersDir, item.FileName);
            string applyPath = fullPath;
            string applyStyle = effectiveStyle;
            if (string.Equals(applyStyle, "Custom", StringComparison.OrdinalIgnoreCase))
            {
                applyPath = CropHelper.GenerateCustom(item);
                applyStyle = "Fill";
            }

            bool success = WallpaperApplyService.ApplyWallpaper(applyPath, applyStyle);

            if (success)
            {
                _lastAppliedWallpaperId = wallpaperId;
                _lastAppliedStyle = effectiveStyle;
                _frameService?.ShowWallpaper(applyPath, applyStyle);
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
