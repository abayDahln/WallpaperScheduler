using System;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Views
{
    public sealed partial class MonthlyOverridesPage : Page
    {
        private readonly ConfigService _configService;
        private int _selectedDay = -1;

        public ObservableCollection<DayCell> DayCells { get; } = new();
        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public MonthlyOverridesPage()
        {
            InitializeComponent();
            _configService = ((App)Application.Current).ConfigService;
            foreach (var wp in _configService.Config.WallpaperLibrary) Wallpapers.Add(wp);
            DataContext = this;
            LoadCalendar();
            DayGrid.SelectedIndex = 0;
            _selectedDay = 1;
            SelectedDayTitle.Text = "Day 1";
            ReloadSlots();
        }

        public static Visibility HasOverrideToVisibility(bool v) => v ? Visibility.Visible : Visibility.Collapsed;

        private void LoadCalendar()
        {
            DayCells.Clear();
            for (int d = 1; d <= 31; d++)
            {
                DayCells.Add(new DayCell(d, GetOverride(d) != null));
            }
        }

        private MonthlyOverride? GetOverride(int day)
            => _configService.Config.MonthlyOverrides.FirstOrDefault(mo => mo.DayOfMonth == day);

        private void OnDaySelected(object sender, SelectionChangedEventArgs e)
        {
            if (DayGrid.SelectedItem is not DayCell cell) return;
            _selectedDay = cell.Day;
            SelectedDayTitle.Text = $"Day {cell.Day}";
            ReloadSlots();
        }

        private void ReloadSlots()
        {
            SlotListPanel.Children.Clear();
            var ov = GetOverride(_selectedDay);
            if (ov == null)
            {
                EmptyState.Visibility = Visibility.Visible;
                return;
            }
            EmptyState.Visibility = Visibility.Collapsed;
            foreach (var slot in ov.Slots)
            {
                SlotListPanel.Children.Add(CreateRow(slot));
            }
        }

        private TimeSlotRow CreateRow(TimeSlot slot)
        {
            var row = new TimeSlotRow();
            row.SetSlot(slot, Wallpapers);
            row.Edited += (_, _) => { SaveConfig(affectsToday: _selectedDay == DateTime.Now.Day); };
            row.RemoveRequested += (_, _) => RemoveSlot(row);
            return row;
        }

        private async void OnAddSlotClick(object sender, RoutedEventArgs e)
        {
            if (_selectedDay < 0)
            {
                ShowMessage("Select a day first.");
                return;
            }

            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count == 0) return;

            var ov = GetOverride(_selectedDay);
            if (ov == null)
            {
                ov = new MonthlyOverride
                {
                    DayOfMonth = _selectedDay,
                    Label = $"Day {_selectedDay}",
                    Slots = new()
                };
                _configService.Config.MonthlyOverrides.Add(ov);
            }

            // start after the last existing slot so new slots never overlap
            TimeSpan nextStart = TimeSpan.Zero;
            foreach (var s in ov.Slots)
                if (s.EndTimeSpan > nextStart) nextStart = s.EndTimeSpan;
            if (nextStart >= TimeSpan.FromDays(1))
            {
                ShowMessage("This day is already fully scheduled. Remove a slot first.");
                return;
            }
            TimeSpan free = TimeSpan.FromDays(1) - nextStart;
            TimeSpan each = free / imported.Count;
            for (int i = 0; i < imported.Count; i++)
            {
                var wp = imported[i];
                TimeSpan start = nextStart + each * i;
                TimeSpan end = i == imported.Count - 1 ? TimeSpan.FromDays(1) : nextStart + each * (i + 1);
                var slot = new TimeSlot
                {
                    Start = start.ToString(@"hh\:mm"),
                    End = end == TimeSpan.FromDays(1) ? "24:00" : end.ToString(@"hh\:mm"),
                    WallpaperId = wp.Id,
                    WallpaperStyle = _configService.Config.Settings.WallpaperStyle
                };
                ov.Slots.Add(slot);
            }

            SaveConfig(affectsToday: _selectedDay == DateTime.Now.Day);
            RefreshCalendar();
            ReloadSlots();
        }

        private void RemoveSlot(TimeSlotRow row)
        {
            var ov = GetOverride(_selectedDay);
            if (ov == null) return;
            ov.Slots.Remove(row.Slot);
            if (ov.Slots.Count == 0) _configService.Config.MonthlyOverrides.Remove(ov);
            SaveConfig(affectsToday: _selectedDay == DateTime.Now.Day);
            RefreshCalendar();
            ReloadSlots();
        }

        private void RefreshCalendar()
        {
            foreach (var cell in DayCells)
            {
                cell.HasOverride = GetOverride(cell.Day) != null;
                cell.RaiseChanged();
            }
        }

        private void SaveConfig(bool affectsToday)
        {
            _configService.SaveConfig();
            ((App)Application.Current).SchedulerEngine.ForceReevaluate(fresh: affectsToday, force: true);
        }

        private void ShowMessage(string msg)
        {
            _ = new ContentDialog
            {
                Title = "Monthly Overrides",
                Content = msg,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
        }
    }

    public class DayCell : INotifyPropertyChanged
    {
        public event PropertyChangedEventHandler? PropertyChanged;

        public int Day { get; }
        public bool HasOverride { get; set; }

        public DayCell(int day, bool hasOverride)
        {
            Day = day;
            HasOverride = hasOverride;
        }

        public void RaiseChanged()
            => PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(HasOverride)));
    }
}
