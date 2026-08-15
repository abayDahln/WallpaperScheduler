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

        private const int SM_CXVIRTUALSCREEN = 78;
        private const int SM_CYVIRTUALSCREEN = 79;

        [DllImport("user32.dll")]
        private static extern int GetSystemMetrics(int nIndex);

        public static bool ApplySolidBlack(string style = "Fill")
        {
            string path = Path.Combine(ConfigService.WallpapersDir, "solidblack.bmp");
            if (!File.Exists(path)) WriteSolidBitmap(path);
            return ApplyWallpaper(path, style);
        }

        private static void WriteSolidBitmap(string path)
        {
            int width = Math.Max(1, GetSystemMetrics(SM_CXVIRTUALSCREEN));
            int height = Math.Max(1, GetSystemMetrics(SM_CYVIRTUALSCREEN));
            int rowSize = ((24 * width + 31) / 32) * 4;
            int dataSize = rowSize * height;

            using var w = new BinaryWriter(File.Create(path));
            // BITMAPFILEHEADER
            w.Write((byte)'B'); w.Write((byte)'M');
            w.Write(54 + dataSize);
            w.Write((ushort)0);
            w.Write((ushort)0);
            w.Write(54);
            // BITMAPINFOHEADER
            w.Write(40);
            w.Write(width);
            w.Write(height);
            w.Write((ushort)1);
            w.Write((ushort)24);
            w.Write(0);
            w.Write(dataSize);
            w.Write(2835);
            w.Write(2835);
            w.Write(0);
            w.Write(0);
            // pixel data: all black
            byte[] row = new byte[rowSize];
            for (int y = 0; y < height; y++) w.Write(row);
        }

        public static bool ApplyWallpaper(string filePath, string style = "Fill")
        {
            if (!File.Exists(filePath)) return false;

            try
            {
                var manager = (IDesktopWallpaper)new DesktopWallpaperClass();
                
                DesktopWallpaperPosition position = DesktopWallpaperPosition.Fill;
                switch (style.ToLowerInvariant())
                {
                    case "center": position = DesktopWallpaperPosition.Center; break;
                    case "tile": position = DesktopWallpaperPosition.Tile; break;
                    case "stretch": position = DesktopWallpaperPosition.Stretch; break;
                    case "fit": position = DesktopWallpaperPosition.Fit; break;
                    case "span": position = DesktopWallpaperPosition.Span; break;
                    case "fill":
                    default:
                        position = DesktopWallpaperPosition.Fill;
                        break;
                }

                manager.SetWallpaperOptions(position);
                manager.SetWallpaper(null, filePath);
                return true;
            }
            catch
            {
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

    public enum DesktopWallpaperPosition
    {
        Center = 0,
        Tile = 1,
        Stretch = 2,
        Fit = 3,
        Fill = 4,
        Span = 5
    }

    [StructLayout(LayoutKind.Sequential)]
    public struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [ComImport]
    [Guid("B92B56A9-8B55-4E14-9A89-0199BBB6F93B")]
    [InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
    public interface IDesktopWallpaper
    {
        void SetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] string wallpaper);
        void GetWallpaper([MarshalAs(UnmanagedType.LPWStr)] string? monitorID, [MarshalAs(UnmanagedType.LPWStr)] out string wallpaper);
        void GetMonitorDevicePathAt(uint monitorIndex, [MarshalAs(UnmanagedType.LPWStr)] out string monitorID);
        uint GetMonitorDevicePathCount();
        void GetMonitorRECT([MarshalAs(UnmanagedType.LPWStr)] string monitorID, out RECT displayRect);
        void SetBackgroundColor(uint color);
        void GetBackgroundColor(out uint color);
        void SetWallpaperOptions(DesktopWallpaperPosition position);
        void GetWallpaperOptions(out DesktopWallpaperPosition position);
    }

    [ComImport]
    [Guid("C2CF3110-460E-4fc1-B9D0-8A1C0C9CC4BD")]
    public class DesktopWallpaperClass
    {
    }
}
