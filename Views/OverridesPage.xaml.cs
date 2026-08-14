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
    public sealed partial class OverridesPage : Page
    {
        private readonly ConfigService _configService;

        public ObservableCollection<MonthlyRecord> MonthlyOverrides { get; } = new();
        public ObservableCollection<DateRecord> DateOverrides { get; } = new();
        public ObservableCollection<WallpaperItem> Wallpapers { get; } = new();
        public List<string> DayNumbers { get; } = Enumerable.Range(1, 31).Select(n => n.ToString()).ToList();

        public OverridesPage()
        {
            InitializeComponent();
            _configService = ((App)Application.Current).ConfigService;
            foreach (var wp in _configService.Config.WallpaperLibrary) Wallpapers.Add(wp);
            LoadOverrides();
            DataContext = this;
        }

        private void LoadOverrides()
        {
            MonthlyOverrides.Clear();
            foreach (var mo in _configService.Config.MonthlyOverrides)
                MonthlyOverrides.Add(new MonthlyRecord(mo, GetLabel(mo.Slots.FirstOrDefault()?.WallpaperId)));

            DateOverrides.Clear();
            foreach (var d in _configService.Config.DateOverrides)
                DateOverrides.Add(new DateRecord(d, GetLabel(d.Slots.FirstOrDefault()?.WallpaperId)));
        }

        private string GetLabel(string? id)
        {
            if (id == null) return "—";
            return _configService.Config.WallpaperLibrary.FirstOrDefault(w => w.Id == id)?.Label ?? "(missing " + id + ")";
        }

        private async void OnImportMonthlyClick(object sender, RoutedEventArgs e)
        {
            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count > 0) MonthlyWallpaperCombo.SelectedValue = imported[0].Id;
        }

        private async void OnImportDateClick(object sender, RoutedEventArgs e)
        {
            var imported = await WallpaperImport.PickAndImportAsync(_configService);
            foreach (var wp in imported) Wallpapers.Add(wp);
            if (imported.Count > 0) DateWallpaperCombo.SelectedValue = imported[0].Id;
        }

        private void OnAddMonthlyClick(object sender, RoutedEventArgs e)
        {
            if (MonthlyWallpaperCombo.SelectedValue is not string wpId || MonthlyDayInput.SelectedItem is not string dayStr)
            {
                ShowError("Select a day and a wallpaper first.");
                return;
            }

            var mo = new MonthlyOverride
            {
                DayOfMonth = int.Parse(dayStr),
                Label = $"Day {dayStr}",
                Slots = new() { new TimeSlot { Start = "00:00", End = "24:00", WallpaperId = wpId } }
            };

            _configService.Config.MonthlyOverrides.Add(mo);
            _configService.SaveConfig();
            ((App)Application.Current).SchedulerEngine.ForceReevaluate();
            MonthlyOverrides.Add(new MonthlyRecord(mo, GetLabel(wpId)));
            MonthlyWallpaperCombo.SelectedItem = null;
        }

        private void OnRemoveMonthlyClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is MonthlyRecord rec)
            {
                _configService.Config.MonthlyOverrides.Remove(rec.Override);
                _configService.SaveConfig();
                ((App)Application.Current).SchedulerEngine.ForceReevaluate();
                MonthlyOverrides.Remove(rec);
            }
        }

        private void OnAddDateClick(object sender, RoutedEventArgs e)
        {
            if (DatePicker.Date == null || DateWallpaperCombo.SelectedValue is not string wpId)
            {
                ShowError("Select a date and a wallpaper first.");
                return;
            }

            var date = DatePicker.Date.Value.DateTime;
            var d = new DateOverride
            {
                Date = date.ToString("yyyy-MM-dd"),
                Label = date.ToString("dd MMM yyyy"),
                Slots = new() { new TimeSlot { Start = "00:00", End = "24:00", WallpaperId = wpId } }
            };

            _configService.Config.DateOverrides.Add(d);
            _configService.SaveConfig();
            ((App)Application.Current).SchedulerEngine.ForceReevaluate();
            DateOverrides.Add(new DateRecord(d, GetLabel(wpId)));
            DateWallpaperCombo.SelectedItem = null;
            DatePicker.Date = null;
        }

        private void OnRemoveDateClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is DateRecord rec)
            {
                _configService.Config.DateOverrides.Remove(rec.Override);
                _configService.SaveConfig();
                ((App)Application.Current).SchedulerEngine.ForceReevaluate();
                DateOverrides.Remove(rec);
            }
        }

        private void ShowError(string msg)
        {
            _ = new ContentDialog
            {
                Title = "Overrides",
                Content = msg,
                CloseButtonText = "OK",
                XamlRoot = this.XamlRoot
            }.ShowAsync();
        }
    }

    public class MonthlyRecord
    {
        public MonthlyOverride Override { get; }
        public string DayLabel => $"Day {Override.DayOfMonth}";
        public string WallpaperLabel { get; }

        public MonthlyRecord(MonthlyOverride o, string wallpaperLabel)
        {
            Override = o;
            WallpaperLabel = wallpaperLabel;
        }
    }

    public class DateRecord
    {
        public DateOverride Override { get; }
        public string DateLabel { get; }
        public string WallpaperLabel { get; }

        public DateRecord(DateOverride o, string wallpaperLabel)
        {
            Override = o;
            DateLabel = Override.Label;
            WallpaperLabel = wallpaperLabel;
        }
    }
}