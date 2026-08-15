using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;
using WallpaperScheduler.Views;

namespace WallpaperScheduler.Helpers
{
    public static class CropHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        private const int SM_CXSCREEN = 0;
        private const int SM_CYSCREEN = 1;

        /// <summary>Crops the wallpaper's area and scales it to the primary screen.</summary>
        public static string GenerateCustom(WallpaperItem item)
        {
            string dest = Path.Combine(ConfigService.WallpapersDir, item.Id + "_custom.bmp");

            using var src = new Bitmap(item.FullPath);
            int x = Math.Clamp((int)(item.CropLeft * src.Width), 0, src.Width - 1);
            int y = Math.Clamp((int)(item.CropTop * src.Height), 0, src.Height - 1);
            int w = Math.Clamp((int)(item.CropWidth * src.Width), 1, src.Width - x);
            int h = Math.Clamp((int)(item.CropHeight * src.Height), 1, src.Height - y);

            int sw = Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
            int sh = Math.Max(1, GetSystemMetrics(SM_CYSCREEN));

            using var outBmp = new Bitmap(sw, sh);
            using var g = Graphics.FromImage(outBmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(src, new Rectangle(0, 0, sw, sh), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
            outBmp.Save(dest, ImageFormat.Bmp);
            return dest;
        }

        public static async Task<bool> EditCropAsync(XamlRoot xamlRoot, WallpaperItem item)
        {
            var selector = new CropSelector();
            selector.Load(item);

            var dialog = new ContentDialog
            {
                Title = "Set crop area",
                Content = selector,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                XamlRoot = xamlRoot
            };

            if (await dialog.ShowAsync() == ContentDialogResult.Primary)
            {
                selector.ApplyTo(item);
                return true;
            }
            return false;
        }
    }
}