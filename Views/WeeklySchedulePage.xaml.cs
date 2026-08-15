using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Views
{
    public sealed partial class WeeklySchedulePage : Page
    {
        private readonly ConfigService _configService;
        private readonly SchedulerEngine _schedulerEngine;

        public ObservableCollection<DayEntry> Days { get; } = new()
        {
            new DayEntry(DayOfWeek.Monday),
            new DayEntry(DayOfWeek.Tuesday),
            new DayEntry(DayOfWeek.Wednesday),
            new DayEntry(DayOfWeek.Thursday),
            new DayEntry(DayOfWeek.Friday),
            new DayEntry(DayOfWeek.Saturday),
            new DayEntry(DayOfWeek.Sunday),
        };

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public ObservableCollection<DaySlotEditor> Rows { get; } = new();

        public WeeklySchedulePage()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            _configService = app.ConfigService;
            _schedulerEngine = app.SchedulerEngine;
            foreach (var wp in _configService.Config.WallpaperLibrary) Wallpapers.Add(wp);
            DataContext = this;
            DayList.SelectedIndex = 0;
            if (DayList.SelectedIndex >= 0)
            {
                SelectedDayTitle.Text = Days[DayList.SelectedIndex].Name;
                ReloadRows();
            }
            RefreshDaySummary();
        }

        private DayOfWeek SelectedDay => (DayOfWeek)DayList.SelectedIndex;

        private List<TimeSlot> GetSlots(DayOfWeek day) => _configService.Config.WeeklySchedule.GetDaySlots(day);

        private bool _daySync;

        private void OnDaySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DayList.SelectedIndex < 0) return;
            _daySync = true;
            NarrowDayCombo.SelectedIndex = DayList.SelectedIndex;
            _daySync = false;
            SelectedDayTitle.Text = Days[DayList.SelectedIndex].Name;
            CopyTargetDay.SelectedIndex = -1;
            ReloadRows();
        }

        private void OnNarrowDaySelected(object sender, SelectionChangedEventArgs e)
        {
            if (_daySync || NarrowDayCombo.SelectedIndex < 0) return;
            DayList.SelectedIndex = NarrowDayCombo.SelectedIndex;
        }

        private void ReloadRows()
        {
            Rows.Clear();
            foreach (var s in GetSlots(SelectedDay))
            {
                Rows.Add(new DaySlotEditor(s, Wallpapers));
            }
            EmptyState.Visibility = Rows.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private async void OnAddSlotClick(object sender, RoutedEventArgs e)
        {
            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count == 0) return;

            foreach (var wp in imported)
            {
            var slot = new TimeSlot
            {
                Start = "00:00",
                End = "24:00",
                WallpaperId = imported[0].Id,
                WallpaperStyle = _configService.Config.Settings.WallpaperStyle
            };
                GetSlots(SelectedDay).Add(slot);
                Rows.Add(new DaySlotEditor(slot, Wallpapers));
            }
            SaveConfig();
            EmptyState.Visibility = Visibility.Collapsed;
            RefreshDaySummary();
        }

        private async void OnPickWallpaperClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DaySlotEditor editor) return;

            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count == 0) return;

            editor.WallpaperId = imported[0].Id;
            SaveConfig();
            ReloadRows();
            RefreshDaySummary();
        }

        private async void OnRemoveSlotClick(object sender, RoutedEventArgs e)
        {
            if (sender is not Button btn || btn.Tag is not DaySlotEditor editor) return;

            var dialog = new ContentDialog
            {
                Title = "Remove time slot",
                Content = $"Remove the {editor.StartTime:hh\\:mm}\u2013{editor.EndTime:hh\\:mm} slot for {SelectedDayTitle.Text}?",
                PrimaryButtonText = "Remove",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            var slots = GetSlots(SelectedDay);
            slots.Remove(editor.ToTimeSlot());
            SaveConfig();
            Rows.Remove(editor);
            RefreshDaySummary();
        }

        private async void OnClearDayClick(object sender, RoutedEventArgs e)
        {
            int count = GetSlots(SelectedDay).Count;
            if (count == 0) return;

            var dialog = new ContentDialog
            {
                Title = "Clear day",
                Content = $"Remove all {count} slot(s) for {SelectedDayTitle.Text}?",
                PrimaryButtonText = "Clear",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Close,
                XamlRoot = XamlRoot
            };
            if (await dialog.ShowAsync() != ContentDialogResult.Primary) return;

            GetSlots(SelectedDay).Clear();
            SaveConfig();
            Rows.Clear();
            RefreshDaySummary();
        }

        private void OnCopyToClick(object sender, RoutedEventArgs e)
        {
            if (CopyTargetDay.SelectedIndex < 0 || CopyTargetDay.SelectedIndex == DayList.SelectedIndex)
            {
                ShowMessage("Select a different target day first.");
                return;
            }

            var targetDay = (DayOfWeek)CopyTargetDay.SelectedIndex;
            var sourceSlots = GetSlots(SelectedDay);
            var copies = sourceSlots.Select(s => new TimeSlot
            {
                Start = s.Start,
                End = s.End,
                WallpaperId = s.WallpaperId
            }).ToList();

            SetSlots(targetDay, copies);
            SaveConfig();
            RefreshDaySummary();
            ShowMessage($"Copied {copies.Count} slot(s) to {Days[CopyTargetDay.SelectedIndex].Name}.");
        }

        private void OnStartTimeClick(object sender, RoutedEventArgs e) => PickTime(sender as Button, isEnd: false);

        private void OnEndTimeClick(object sender, RoutedEventArgs e) => PickTime(sender as Button, isEnd: true);

        private void PickTime(Button? btn, bool isEnd)
        {
            if (btn?.Tag is not DaySlotEditor editor) return;

            var flyout = new TimePickerFlyout { ClockIdentifier = "24HourClock" };
            var current = isEnd ? editor.EndTime : editor.StartTime;
            flyout.Time = current >= TimeSpan.FromDays(1) ? TimeSpan.Zero : current;

            flyout.TimePicked += (_, args) =>
            {
                if (isEnd) editor.EndTime = args.NewTime;
                else editor.StartTime = args.NewTime;
                btn.Content = DaySlotEditor.FormatTime(isEnd ? editor.EndTime : editor.StartTime);
                if (ValidateAndSave()) { }
            };
            flyout.ShowAt(btn);
        }

        private async void OnSlotStyleChanged(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is DaySlotEditor editor
                && cb.SelectedItem is string style && editor.WallpaperStyle != style)
            {
                editor.WallpaperStyle = style;
                if (string.Equals(style, "Custom", StringComparison.OrdinalIgnoreCase) && editor.Wallpaper is WallpaperItem wp)
                {
                    await CropHelper.EditCropAsync(XamlRoot, wp);
                    _configService.SaveConfig();
                }
                if (ValidateAndSave()) { }
            }
        }

        private bool ValidateAndSave()
        {
            var slots = Rows.Select(r => r.ToTimeSlot()).Where(s => !string.IsNullOrEmpty(s.WallpaperId)).ToList();

            for (int i = 0; i < slots.Count; i++)
            {
                for (int j = i + 1; j < slots.Count; j++)
                {
                    bool a = slots[i].StartTimeSpan < slots[j].EndTimeSpan;
                    bool b = slots[j].StartTimeSpan < slots[i].EndTimeSpan;
                    if (a && b)
                    {
                        ShowMessage("Time slots must not overlap.");
                        DispatcherQueue.TryEnqueue(ReloadRows);
                        return false;
                    }
                }
            }

            SetSlots(SelectedDay, slots);
            SaveConfig();
            RefreshDaySummary();
            return true;
        }

        private void ShowMessage(string msg)
        {
            _ = new ContentDialog
            {
                Title = "Weekly Schedule",
                Content = msg,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }

        private void SetSlots(DayOfWeek day, List<TimeSlot> slots)
        {
            switch (day)
            {
                case DayOfWeek.Monday: _configService.Config.WeeklySchedule.Monday = slots; break;
                case DayOfWeek.Tuesday: _configService.Config.WeeklySchedule.Tuesday = slots; break;
                case DayOfWeek.Wednesday: _configService.Config.WeeklySchedule.Wednesday = slots; break;
                case DayOfWeek.Thursday: _configService.Config.WeeklySchedule.Thursday = slots; break;
                case DayOfWeek.Friday: _configService.Config.WeeklySchedule.Friday = slots; break;
                case DayOfWeek.Saturday: _configService.Config.WeeklySchedule.Saturday = slots; break;
                case DayOfWeek.Sunday: _configService.Config.WeeklySchedule.Sunday = slots; break;
            }
        }

        private void SaveConfig()
        {
            _configService.SaveConfig();
            bool changedToday = SelectedDay == DateTime.Now.DayOfWeek;
            _schedulerEngine.ForceReevaluate(fresh: changedToday, force: true);
        }

        private void RefreshDaySummary()
        {
            for (int i = 0; i < Days.Count; i++)
            {
                Days[i].Refresh(GetSlots((DayOfWeek)i));
            }
        }
    }

    public class DayEntry : System.ComponentModel.INotifyPropertyChanged
    {
        public event System.ComponentModel.PropertyChangedEventHandler? PropertyChanged;

        public DayOfWeek Day { get; }
        public string Name { get; }
        public int SlotCount { get; private set; }
        public string Summary { get; private set; } = "No slots";

        public DayEntry(DayOfWeek day)
        {
            Day = day;
            Name = day switch
            {
                DayOfWeek.Monday => "Monday",
                DayOfWeek.Tuesday => "Tuesday",
                DayOfWeek.Wednesday => "Wednesday",
                DayOfWeek.Thursday => "Thursday",
                DayOfWeek.Friday => "Friday",
                DayOfWeek.Saturday => "Saturday",
                _ => "Sunday"
            };
        }

        public void Refresh(List<TimeSlot> slots)
        {
            SlotCount = slots.Count;
            Summary = slots.Count == 0
                ? "No slots"
                : $"{slots[0].Start}\u2013{slots[^1].End}, {slots.Count} slot(s)";
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(SlotCount)));
            PropertyChanged?.Invoke(this, new System.ComponentModel.PropertyChangedEventArgs(nameof(Summary)));
        }
    }

    public class DaySlotEditor
    {
        private readonly TimeSlot _slot;
        public ObservableCollection<WallpaperItem> Wallpapers { get; }

        public string WallpaperId
        {
            get => _slot.WallpaperId;
            set => _slot.WallpaperId = value;
        }

        public WallpaperItem? Wallpaper => Wallpapers.FirstOrDefault(w => w.Id == WallpaperId);

        public Microsoft.UI.Xaml.Media.ImageSource? Thumbnail => Wallpaper?.Thumbnail;

        public string WallpaperLabel => Wallpaper?.Label ?? "Pick wallpaper…";

        public IReadOnlyList<string> StyleOptions { get; } = new List<string> { "Fill", "Fit", "Stretch", "Tile", "Center", "Span", "Custom" };

        public string WallpaperStyle
        {
            get => _slot.WallpaperStyle;
            set => _slot.WallpaperStyle = value;
        }

        public string EffectiveStyle => string.IsNullOrEmpty(_slot.WallpaperStyle)
            ? ((App)Application.Current).ConfigService.Config.Settings.WallpaperStyle
            : _slot.WallpaperStyle;

        public string StartLabel => FormatTime(StartTime);

        public string EndLabel => FormatTime(EndTime);

        public static string FormatTime(TimeSpan t)
            => t >= TimeSpan.FromDays(1) ? "24:00" : t.ToString(@"hh\:mm");

        public TimeSpan StartTime
        {
            get => _slot.StartTimeSpan;
            set { _slot.Start = value.ToString(@"hh\:mm"); }
        }

        public TimeSpan EndTime
        {
            get => _slot.EndTimeSpan;
            set { _slot.End = value == TimeSpan.FromDays(1) ? "24:00" : value.ToString(@"hh\:mm"); }
        }

        public DaySlotEditor(TimeSlot slot, ObservableCollection<WallpaperItem> wallpapers)
        {
            _slot = slot;
            Wallpapers = wallpapers;
        }

        public TimeSlot ToTimeSlot()
        {
            _slot.Start = StartTime.ToString(@"hh\:mm");
            _slot.End = EndTime == TimeSpan.FromDays(1) ? "24:00" : EndTime.ToString(@"hh\:mm");
            _slot.WallpaperId = WallpaperId;
            return _slot;
        }
    }
}