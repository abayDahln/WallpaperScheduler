using System;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Media.Imaging;
using Windows.Storage.Pickers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;
using WallpaperScheduler.ViewModels;
using WinRT.Interop;

namespace WallpaperScheduler.Views
{
    public sealed partial class LibraryPage : Page
    {
        public LibraryViewModel ViewModel { get; private set; } = null!;
        private WallpaperItem? _selected;

        public LibraryPage()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            ViewModel = new LibraryViewModel(app.ConfigService, app.SchedulerEngine);
            DataContext = this;
        }

        private async void OnImportClick(object sender, RoutedEventArgs e)
        {
            if (App.MainWindow == null) return;

            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            picker.FileTypeFilter.Add(".jpg");
            picker.FileTypeFilter.Add(".jpeg");
            picker.FileTypeFilter.Add(".png");
            picker.FileTypeFilter.Add(".bmp");

            var files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return;

            foreach (var file in files)
            {
                ViewModel.AddWallpaper(file.Path);
            }
        }

        private void OnSelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (WallpaperList.SelectedItem is WallpaperItem item)
            {
                _selected = item;
                DetailPane.Visibility = Visibility.Visible;
                LabelBox.Text = item.Label;
                FileNameText.Text = $"{item.FileName}  \u00b7  added {item.AddedAt:dd MMM yyyy}";
                PreviewImage.Source = new BitmapImage(new Uri(item.ThumbnailPath));
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
        }
    }
}