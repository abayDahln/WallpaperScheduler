using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Media;
using WallpaperScheduler.Models;
using WallpaperScheduler.Services;
using WallpaperScheduler.Views;

namespace WallpaperScheduler.Helpers
{
    public static class CropHelper
    {
        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

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
        public static string? GenerateCustom(WallpaperItem item)
        {
            try
            {
                if (!File.Exists(item.FullPath)) return null;

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
            catch
            {
                // missing / unreadable / unsupported image -> fall back to the normal apply path
                return null;
            }
        }

        public static async Task<bool> EditCropAsync(XamlRoot xamlRoot, WallpaperItem item)
        {
            // use the window's root XamlRoot so the dialog centers on the whole window
            if (App.MainWindow?.Content?.XamlRoot != null)
                xamlRoot = App.MainWindow.Content.XamlRoot;

            double scale = xamlRoot.RasterizationScale;
            double availW = xamlRoot.Size.Width / scale;
            double availH = xamlRoot.Size.Height / scale;
            double maxBoxW = Math.Max(320, availW - 200);
            double maxBoxH = Math.Max(220, availH - 260);

            var selector = new CropSelector();
            selector.Load(item, maxBoxW, maxBoxH);

            // dialog box: title + selector + buttons
            var title = new TextBlock
            {
                Text = "Select wallpaper area",
                Style = (Style)Application.Current.Resources["TitleTextBlockStyle"],
                Margin = new Thickness(0, 0, 0, 12)
            };
            var cancelBtn = new Button { Content = "Cancel", MinWidth = 80 };
            var saveBtn = new Button
            {
                Content = "Save",
                MinWidth = 80,
                Style = (Style)Application.Current.Resources["AccentButtonStyle"]
            };
            var footer = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                Spacing = 8,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 12, 0, 0)
            };
            footer.Children.Add(cancelBtn);
            footer.Children.Add(saveBtn);

            var dialogBox = new StackPanel
            {
                Padding = new Thickness(24, 20, 24, 16),
                Background = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["SolidBackgroundFillColorBaseBrush"],
                BorderBrush = (Microsoft.UI.Xaml.Media.Brush)Application.Current.Resources["CardStrokeColorDefaultBrush"],
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(8),
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                MinWidth = Math.Min(420, selector.Width + 48)
            };
            dialogBox.Children.Add(title);
            dialogBox.Children.Add(selector);
            dialogBox.Children.Add(footer);

            var overlay = new Grid
            {
                Width = availW,
                Height = availH,
                Background = new SolidColorBrush(Microsoft.UI.Colors.Black) { Opacity = 0.45 }
            };
            overlay.Children.Add(dialogBox);

            var popup = new Popup
            {
                XamlRoot = xamlRoot,
                Child = overlay,
                IsLightDismissEnabled = false
            };

            var tcs = new TaskCompletionSource<bool>();
            void Close() => tcs.TrySetResult(false);
            overlay.PointerPressed += (_, _) => Close();
            saveBtn.Click += (_, _) => { selector.ApplyTo(item); tcs.TrySetResult(true); };
            cancelBtn.Click += (_, _) => Close();

            popup.IsOpen = true;
            bool saved = await tcs.Task;
            popup.IsOpen = false;
            return saved;
        }
    }
}