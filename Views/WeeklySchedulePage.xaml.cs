using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
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

        public ObservableCollection<DaySlotEditor> Rows { get; } = new();

        public WeeklySchedulePage()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            _configService = app.ConfigService;
            _schedulerEngine = app.SchedulerEngine;
            DataContext = this;
            DayList.SelectedIndex = 0;
        }

        private DayOfWeek SelectedDay => (DayOfWeek)DayList.SelectedIndex;

        private List<TimeSlot> GetSlots(DayOfWeek day) => _configService.Config.WeeklySchedule.GetDaySlots(day);

        private void OnDaySelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (DayList.SelectedIndex < 0) return;
            SelectedDayTitle.Text = Days[DayList.SelectedIndex].Name;
            CopyTargetDay.SelectedIndex = -1;
            ReloadRows();
        }

        private void ReloadRows()
        {
            Rows.Clear();
            foreach (var s in GetSlots(SelectedDay))
            {
                Rows.Add(new DaySlotEditor(s, WallpapersForSelected()));
            }
        }

        private ObservableCollection<WallpaperItem> WallpapersForSelected()
        {
            var col = new ObservableCollection<WallpaperItem>();
            foreach (var wp in _configService.Config.WallpaperLibrary) col.Add(wp);
            return col;
        }

        private void OnAddSlotClick(object sender, RoutedEventArgs e)
        {
            var wallpapers = WallpapersForSelected();
            var slot = new TimeSlot
            {
                Start = "00:00",
                End = "24:00",
                WallpaperId = wallpapers.FirstOrDefault()?.Id ?? string.Empty
            };
            GetSlots(SelectedDay).Add(slot);
            SaveConfig();
            Rows.Add(new DaySlotEditor(slot, wallpapers));
            RefreshDaySummary();
        }

        private void OnRemoveSlotClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DaySlotEditor editor)
            {
                var slots = GetSlots(SelectedDay);
                slots.Remove(editor.ToTimeSlot());
                SaveConfig();
                Rows.Remove(editor);
                RefreshDaySummary();
            }
        }

        private void OnClearDayClick(object sender, RoutedEventArgs e)
        {
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

        private void OnSlotEdited(object sender, object e)
        {
            if (sender is FrameworkElement fe && fe.DataContext is DaySlotEditor editor)
            {
                if (ValidateAndSave()) { }
            }
        }

        private void OnWallpaperSelected(object sender, SelectionChangedEventArgs e)
        {
            if (sender is ComboBox cb && cb.DataContext is DaySlotEditor editor)
            {
                var newId = cb.SelectedValue as string;
                if (string.IsNullOrEmpty(newId) || newId == editor.WallpaperId) return;
                editor.WallpaperId = newId;
                OnSlotEdited(sender, e);
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
            _schedulerEngine.ForceReevaluate();
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