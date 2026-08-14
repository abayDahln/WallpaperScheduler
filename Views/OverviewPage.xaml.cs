using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Models;
using WallpaperScheduler.ViewModels;
using WallpaperScheduler.Services;

namespace WallpaperScheduler.Views
{
    public sealed partial class OverviewPage : Page
    {
        public OverviewViewModel ViewModel { get; private set; } = null!;
        private WallpaperItem? _selected;
        private bool _loadingDefault;

        public OverviewPage()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            ViewModel = new OverviewViewModel(app.ConfigService, app.SchedulerEngine);
            DataContext = this;

            LoadCurrentCard();
            StatWallpapers.Text = ViewModel.WallpaperCount.ToString();
            StatWeekly.Text = ViewModel.WeeklySlotCount.ToString();
            StatMonthly.Text = ViewModel.MonthlyCount.ToString();
            StatDates.Text = ViewModel.DateCount.ToString();

            Loaded += (_, _) => LoadDefault();
        }

        private void LoadCurrentCard()
        {
            var current = ViewModel.CurrentWallpaper;
            if (current != null)
            {
                CurrentImage.Source = current.Thumbnail;
                CurrentLabel.Text = current.Label;
            }
            else
            {
                CurrentLabel.Text = "No wallpaper applies right now";
            }
            NextChangeText.Text = $"Next change: {ViewModel.NextChangeTime:ddd, dd MMM HH:mm}";
        }

        private void LoadDefault()
        {
            _loadingDefault = true;
            DefaultCombo.SelectedValue = string.IsNullOrEmpty(ViewModel.DefaultWallpaperId) ? null : ViewModel.DefaultWallpaperId;
            _loadingDefault = false;
            UpdateDefaultLabel();
        }

        private void UpdateDefaultLabel()
        {
            string? id = ViewModel.DefaultWallpaperId;
            var item = string.IsNullOrEmpty(id) ? null : ViewModel.Wallpapers.FirstOrDefault(w => w.Id == id);
            DefaultLabelText.Text = item?.Label ?? "none";
        }

        private void OnApplyCurrentClick(object sender, RoutedEventArgs e)
        {
            var current = ViewModel.CurrentWallpaper;
            if (current != null) ViewModel.ApplyNow(current);
        }

        private void OnDefaultChanged(object sender, SelectionChangedEventArgs e)
        {
            if (_loadingDefault) return;
            ViewModel.SetDefaultWallpaper(DefaultCombo.SelectedValue as string);
            UpdateDefaultLabel();
        }

        private void OnClearDefaultClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearDefaultWallpaper();
            DefaultCombo.SelectedValue = null;
            UpdateDefaultLabel();
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WallpaperList.SelectedItem is WallpaperItem item)
            {
                _selected = item;
                DetailPane.Visibility = Visibility.Visible;
                LabelBox.Text = item.Label;
                FileNameText.Text = $"{item.FileName}  \u00b7  added {item.AddedAt:dd MMM yyyy}";
                PreviewImage.Source = item.Thumbnail;
            }
        }

        private void OnLabelChanged(object sender, TextChangedEventArgs e)
        {
            if (_selected != null && LabelBox.Text != _selected.Label)
            {
                _selected.Label = LabelBox.Text;
                ((App)Application.Current).ConfigService.SaveConfig();
            }
        }

        private void OnApplyClick(object sender, RoutedEventArgs e)
        {
            if (sender is Button btn && btn.Tag is WallpaperItem item)
            {
                ViewModel.ApplyNow(item);
            }
        }

        private void OnApplyDetailClick(object sender, RoutedEventArgs e)
        {
            if (_selected != null) ViewModel.ApplyNow(_selected);
        }

        private async void OnDeleteDetailClick(object sender, RoutedEventArgs e)
        {
            if (_selected == null) return;
            var victim = _selected;
            var usage = ViewModel.GetUsageCount(victim.Id);

            if (usage > 0)
            {
                var dialog = new ContentDialog
                {
                    Title = "Wallpaper in use",
                    Content = $"This wallpaper is used by {usage} scheduled slot(s). Deleting it will leave those slots unassigned. Continue?",
                    PrimaryButtonText = "Delete",
                    CloseButtonText = "Cancel",
                    DefaultButton = ContentDialogButton.Close,
                    XamlRoot = this.XamlRoot
                };

                var result = await dialog.ShowAsync();
                if (result == ContentDialogResult.Primary) ConfirmDelete(victim);
            }
            else
            {
                ConfirmDelete(victim);
            }
        }

        private void ConfirmDelete(WallpaperItem item)
        {
            ViewModel.DeleteWallpaper(item);
            _selected = null;
            DetailPane.Visibility = Visibility.Collapsed;
            LoadDefault();
        }
    }
}