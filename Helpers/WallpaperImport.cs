using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Windows.Storage.Pickers;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;
using WinRT.Interop;

namespace WallpaperScheduler.Helpers
{
    public static class WallpaperImport
    {
        private static readonly string[] Extensions = { ".jpg", ".jpeg", ".png", ".bmp" };

        public static async Task<List<WallpaperItem>> PickAndImportAsync(ConfigService config)
        {
            var imported = new List<WallpaperItem>();
            if (App.MainWindow == null) return imported;

            var picker = new FileOpenPicker();
            InitializeWithWindow.Initialize(picker, WindowNative.GetWindowHandle(App.MainWindow));
            foreach (var ext in Extensions) picker.FileTypeFilter.Add(ext);

            var files = await picker.PickMultipleFilesAsync();
            if (files == null || files.Count == 0) return imported;

            foreach (var file in files)
            {
                string destName = $"{Guid.NewGuid():N}{Path.GetExtension(file.Path)}";
                string dest = Path.Combine(ConfigService.WallpapersDir, destName);
                File.Copy(file.Path, dest, overwrite: true);

                var item = new WallpaperItem
                {
                    FileName = destName,
                    Label = Path.GetFileNameWithoutExtension(file.Path)
                };
                config.Config.WallpaperLibrary.Add(item);
                imported.Add(item);
            }

            config.SaveConfig();

            foreach (var item in imported)
            {
                await ThumbnailGenerator.EnsureThumbAsync(item);
            }

            return imported;
        }
    }
}