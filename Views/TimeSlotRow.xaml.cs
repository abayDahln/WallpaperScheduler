using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Views
{
    public sealed partial class TimeSlotRow : UserControl
    {
        private TimeSlot _slot = new();
        private ObservableCollection<WallpaperItem> _wallpapers = new();
        private bool _settingStyle;

        public event EventHandler? Edited;
        public event EventHandler? RemoveRequested;

        public IReadOnlyList<string> StyleOptions { get; } = new List<string> { "Fill", "Fit", "Stretch", "Tile", "Center", "Span", "Custom" };

        public TimeSlotRow()
        {
            InitializeComponent();
        }

        public TimeSlot Slot => _slot;

        public void SetSlot(TimeSlot slot, ObservableCollection<WallpaperItem> wallpapers)
        {
            _slot = slot;
            _wallpapers = wallpapers;
            _settingStyle = true;
            StyleCombo.SelectedItem = EffectiveStyle;
            _settingStyle = false;
            Bindings.Update();
        }

        private WallpaperItem? CurrentWallpaper => _wallpapers.FirstOrDefault(w => w.Id == _slot.WallpaperId);

        public ImageSource? WallpaperThumbnail => CurrentWallpaper?.Thumbnail;

        public string WallpaperLabel => CurrentWallpaper?.Label ?? "Pick wallpaper…";

        public string StartLabel => FormatTime(_slot.StartTimeSpan);

        public string EndLabel => FormatTime(_slot.EndTimeSpan);

        public string EffectiveStyle => string.IsNullOrEmpty(_slot.WallpaperStyle)
            ? ((App)Application.Current).ConfigService.Config.Settings.WallpaperStyle
            : _slot.WallpaperStyle;

        private static string FormatTime(TimeSpan t)
            => t >= TimeSpan.FromDays(1) ? "24:00" : t.ToString(@"hh\:mm");

        private void OnStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_settingStyle || StyleCombo.SelectedItem is not string style) return;
            if (style == _slot.WallpaperStyle) return;
            _slot.WallpaperStyle = style;
            Edited?.Invoke(this, EventArgs.Empty);
        }

        private async void OnCropClick(object sender, RoutedEventArgs e)
        {
            if (CurrentWallpaper is not WallpaperItem wp) return;
            if (await CropHelper.EditCropAsync(XamlRoot, wp))
            {
                ((App)Application.Current).ConfigService.SaveConfig();
                Edited?.Invoke(this, EventArgs.Empty);
            }
        }

        private void OnStartClick(object sender, RoutedEventArgs e) => PickTime(StartBtn, isEnd: false);

        private void OnEndClick(object sender, RoutedEventArgs e) => PickTime(EndBtn, isEnd: true);

        private void PickTime(Button btn, bool isEnd)
        {
            var flyout = new TimePickerFlyout { ClockIdentifier = "24HourClock" };
            var current = isEnd ? _slot.EndTimeSpan : _slot.StartTimeSpan;
            flyout.Time = current >= TimeSpan.FromDays(1) ? TimeSpan.Zero : current;
            flyout.TimePicked += (_, args) =>
            {
                var t = args.NewTime;
                if (isEnd) _slot.End = t == TimeSpan.FromDays(1) ? "24:00" : t.ToString(@"hh\:mm");
                else _slot.Start = t.ToString(@"hh\:mm");
                Bindings.Update();
                Edited?.Invoke(this, EventArgs.Empty);
            };
            flyout.ShowAt(btn);
        }

        private async void OnWallpaperClick(object sender, RoutedEventArgs e)
        {
            var config = ((App)Application.Current).ConfigService;
            var imported = await WallpaperImport.PickAndImportAsync(config);
            foreach (var wp in imported) _wallpapers.Add(wp);
            if (imported.Count == 0) return;
            _slot.WallpaperId = imported[0].Id;
            Bindings.Update();
            Edited?.Invoke(this, EventArgs.Empty);
        }

        private void OnRemoveClick(object sender, RoutedEventArgs e) => RemoveRequested?.Invoke(this, EventArgs.Empty);
    }
}
