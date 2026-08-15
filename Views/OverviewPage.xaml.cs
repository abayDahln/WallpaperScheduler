using System;
using System.Linq;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.ViewModels;

namespace WallpaperScheduler.Views
{
    public sealed partial class OverviewPage : Page
    {
        public OverviewViewModel ViewModel { get; private set; } = null!;

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

            CurrentImageFrame.SizeChanged += OnCurrentFrameSizeChanged;
            DefaultThumbFrame.SizeChanged += OnDefaultFrameSizeChanged;
            Loaded += (_, _) => UpdateDefaultLabel();
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
                CurrentImage.Source = null;
                CurrentLabel.Text = "No wallpaper applies right now";
            }
            NextChangeText.Text = $"Next change: {ViewModel.NextChangeTime:ddd, dd MMM HH:mm}";
        }

        private void OnCurrentFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetFrameHeightTo16x9(CurrentImageFrame, e);
        }

        private void OnDefaultFrameSizeChanged(object sender, SizeChangedEventArgs e)
        {
            SetFrameHeightTo16x9(DefaultThumbFrame, e);
        }

        private static void SetFrameHeightTo16x9(FrameworkElement frame, SizeChangedEventArgs e)
        {
            if (e.NewSize.Width > 0)
            {
                double target = e.NewSize.Width * 9.0 / 16.0;
                if (Math.Abs(frame.Height - target) > 1) frame.Height = target;
            }
        }

        private void UpdateDefaultLabel()
        {
            string? id = ViewModel.DefaultWallpaperId;
            var item = string.IsNullOrEmpty(id) ? null : ViewModel.Wallpapers.FirstOrDefault(w => w.Id == id);
            DefaultLabelText.Text = item?.Label ?? "none";
            DefaultThumb.Source = item?.Thumbnail;
            DefaultThumbFrame.Visibility = item == null ? Visibility.Collapsed : Visibility.Visible;
        }

        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            var imported = await WallpaperImport.PickAndImportAsync(((App)Application.Current).ConfigService);
            if (imported.Count == 0) return;

            foreach (var wp in imported) ViewModel.Wallpapers.Add(wp);
            ViewModel.SetDefaultWallpaper(imported[0].Id);
            StatWallpapers.Text = ViewModel.Wallpapers.Count.ToString();
            UpdateDefaultLabel();
            LoadCurrentCard();
        }

        private void OnClearDefaultClick(object sender, RoutedEventArgs e)
        {
            ViewModel.ClearDefaultWallpaper();
            UpdateDefaultLabel();
            LoadCurrentCard();
        }
    }
}