using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace WallpaperScheduler.Services
{
    public static class WallpaperApplyService
    {
        private const int SPI_SETDESKWALLPAPER = 0x0014;
        private const int SPIF_UPDATEINIFILE = 0x01;
        private const int SPIF_SENDCHANGE = 0x02;

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int SystemParametersInfo(int uAction, int uParam, string lpvParam, int fuWinIni);

        public static bool ApplyWallpaper(string filePath, string style = "Fill")
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                SetWallpaperStyleInRegistry(style);
                int result = SystemParametersInfo(SPI_SETDESKWALLPAPER, 0, filePath, SPIF_UPDATEINIFILE | SPIF_SENDCHANGE);
                return result != 0;
            }
            catch
            {
                return false;
            }
        }

        private static void SetWallpaperStyleInRegistry(string style)
        {
            using var key = Registry.CurrentUser.OpenSubKey(@"Control Panel\Desktop", writable: true);
            if (key == null) return;

            string wallpaperStyle = "10"; // Default Fill
            string tileWallpaper = "0";

            switch (style.ToLowerInvariant())
            {
                case "fit":
                    wallpaperStyle = "6";
                    break;
                case "stretch":
                    wallpaperStyle = "2";
                    break;
                case "tile":
                    wallpaperStyle = "0";
                    tileWallpaper = "1";
                    break;
                case "center":
                    wallpaperStyle = "0";
                    break;
                case "span":
                    wallpaperStyle = "22";
                    break;
                case "fill":
                default:
                    wallpaperStyle = "10";
                    break;
            }

            key.SetValue("WallpaperStyle", wallpaperStyle);
            key.SetValue("TileWallpaper", tileWallpaper);
        }
    }
}
