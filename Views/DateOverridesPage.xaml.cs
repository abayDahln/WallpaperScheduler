using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media;
using Microsoft.UI.Xaml.Shapes;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;
using Windows.UI;

namespace WallpaperScheduler.Views
{
    public sealed partial class DateOverridesPage : Page
    {
        private readonly ConfigService _configService;
        private DateTime _selectedDate;
        private Color _accent;

        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();

        public DateOverridesPage()
        {
            InitializeComponent();
            _configService = ((App)Application.Current).ConfigService;
            foreach (var wp in _configService.Config.WallpaperLibrary) Wallpapers.Add(wp);
            _accent = (Color)Application.Current.Resources["SystemAccentColor"];
            DateCalendar.SelectedDates.Add(DateTimeOffset.Now);
        }

        private DateOverride? GetOverride(string dateStr)
            => _configService.Config.DateOverrides.FirstOrDefault(d => d.Date == dateStr);

        private void OnDayItemChanging(CalendarView sender, CalendarViewDayItemChangingEventArgs args)
        {
            var dateStr = args.Item.Date.Date.ToString("yyyy-MM-dd");
            ApplyDot(args.Item, GetOverride(dateStr) != null);
        }

        private const string DotTag = "OverrideDot";

        private void ApplyDot(CalendarViewDayItem item, bool has)
        {
            Ellipse? dot = FindVisualChildren<Ellipse>(item).FirstOrDefault(el => el.Tag as string == DotTag);
            if (has && dot == null)
            {
                if (VisualTreeHelper.GetChild(item, 0) is not Panel root) return;
                dot = new Ellipse
                {
                    Tag = DotTag,
                    Width = 8,
                    Height = 8,
                    Fill = new SolidColorBrush(_accent),
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Bottom,
                    Margin = new Thickness(0, 0, 0, 3)
                };
                root.Children.Add(dot);
            }
            if (dot != null) dot.Visibility = has ? Visibility.Visible : Visibility.Collapsed;
        }

        private void OnDateSelected(object sender, CalendarViewSelectedDatesChangedEventArgs e)
        {
            if (e.AddedDates.Count == 0) return;
            _selectedDate = e.AddedDates[0].Date;
            SelectedDateTitle.Text = _selectedDate.ToString("ddd, dd MMM yyyy");
            ReloadSlots();
        }

        private void ReloadSlots()
        {
            SlotListPanel.Children.Clear();
            var ov = GetOverride(_selectedDate.ToString("yyyy-MM-dd"));
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
            row.Edited += (_, _) => { SaveConfig(affectsToday: _selectedDate.Date == DateTime.Today); };
            row.RemoveRequested += (_, _) => RemoveSlot(row);
            return row;
        }

        private async void OnAddSlotClick(object sender, RoutedEventArgs e)
        {
            if (_selectedDate == default)
            {
                ShowMessage("Select a date first.");
                return;
            }

            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count == 0) return;

            string dateStr = _selectedDate.ToString("yyyy-MM-dd");
            foreach (var wp in imported)
            {
                var slot = new TimeSlot
                {
                    Start = "00:00",
                    End = "24:00",
                    WallpaperId = wp.Id,
                    WallpaperStyle = _configService.Config.Settings.WallpaperStyle
                };
                var ov = GetOverride(dateStr);
                if (ov == null)
                {
                    ov = new DateOverride
                    {
                        Date = dateStr,
                        Label = _selectedDate.ToString("dd MMM yyyy"),
                        Slots = new()
                    };
                    _configService.Config.DateOverrides.Add(ov);
                }
                ov.Slots.Add(slot);
            }

            SaveConfig(affectsToday: _selectedDate.Date == DateTime.Today);
            ReloadSlots();
        }

        private void RemoveSlot(TimeSlotRow row)
        {
            string dateStr = _selectedDate.ToString("yyyy-MM-dd");
            var ov = GetOverride(dateStr);
            if (ov == null) return;
            ov.Slots.Remove(row.Slot);
            if (ov.Slots.Count == 0) _configService.Config.DateOverrides.Remove(ov);
            SaveConfig(affectsToday: _selectedDate.Date == DateTime.Today);
            ReloadSlots();
        }

        private void SaveConfig(bool affectsToday)
        {
            _configService.SaveConfig();
            ((App)Application.Current).SchedulerEngine.ForceReevaluate(fresh: affectsToday, force: true);
            RefreshCalendarIndicators();
        }

        private void RefreshCalendarIndicators()
        {
            foreach (var item in FindVisualChildren<CalendarViewDayItem>(DateCalendar))
            {
                var dateStr = item.Date.Date.ToString("yyyy-MM-dd");
                ApplyDot(item, GetOverride(dateStr) != null);
            }
        }

        private static IEnumerable<T> FindVisualChildren<T>(DependencyObject root) where T : DependencyObject
        {
            int count = VisualTreeHelper.GetChildrenCount(root);
            for (int i = 0; i < count; i++)
            {
                var child = VisualTreeHelper.GetChild(root, i);
                if (child is T t) yield return t;
                foreach (var sub in FindVisualChildren<T>(child)) yield return sub;
            }
        }

        private void ShowMessage(string msg)
        {
            _ = new ContentDialog
            {
                Title = "Date Overrides",
                Content = msg,
                CloseButtonText = "OK",
                XamlRoot = XamlRoot
            }.ShowAsync();
        }
    }
}
