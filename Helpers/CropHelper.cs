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

        /// <summary>Aspect ratio (width/height) of the primary screen.</summary>
        public static double ScreenAspect
        {
            get
            {
                int sw = Math.Max(1, GetSystemMetrics(SM_CXSCREEN));
                int sh = Math.Max(1, GetSystemMetrics(SM_CYSCREEN));
                return (double)sw / sh;
            }
        }

        /// <summary>Crops the wallpaper's area and scales it to the primary screen (no distortion).</summary>
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

            // Cover-scale the crop into the screen rect, preserving aspect (center, crop overflow)
            double srcAspect = (double)w / h;
            double dstAspect = (double)sw / sh;
            int dw, dh, dx, dy;
            if (srcAspect > dstAspect)
            {
                dh = sh;
                dw = (int)Math.Round(sh * srcAspect);
                dx = (sw - dw) / 2;
                dy = 0;
            }
            else
            {
                dw = sw;
                dh = (int)Math.Round(sw / srcAspect);
                dx = 0;
                dy = (sh - dh) / 2;
            }

            using var outBmp = new Bitmap(sw, sh);
            using var g = Graphics.FromImage(outBmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingMode = CompositingMode.SourceCopy;
            g.DrawImage(src, new Rectangle(dx, dy, dw, dh), new Rectangle(x, y, w, h), GraphicsUnit.Pixel);
            outBmp.Save(dest, ImageFormat.Bmp);
            return dest;
        }

        public static async Task<bool> EditCropAsync(XamlRoot xamlRoot, WallpaperItem item)
        {
            var selector = new CropSelector();
            selector.Load(item);

            var dialog = new ContentDialog
            {
                Title = "Select wallpaper area",
                Content = selector,
                PrimaryButtonText = "Save",
                CloseButtonText = "Cancel",
                DefaultButton = ContentDialogButton.Primary,
                MinWidth = 700,
                MaxWidth = 900,
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