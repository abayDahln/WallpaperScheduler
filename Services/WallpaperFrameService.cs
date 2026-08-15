using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.UI.Dispatching;
using Microsoft.UI.Xaml;

namespace WallpaperScheduler.Services
{
    /// <summary>
    /// Wallpaper-Engine-style frame layer. Renders the wallpaper image in a raw
    /// Win32 child window parented to the desktop WorkerW, so it sits BEHIND the
    /// desktop icons but in front of the native OS wallpaper (which the app sets
    /// separately as the base layer).
    ///
    /// Why a raw Win32 window and not XAML:
    ///  - Reparenting a WinUI 3 Window is unsupported; its content is covered by
    ///    the DesktopChildSiteBridge (microsoft-ui-xaml #8779).
    ///  - DesktopWindowXamlSource.Initialize requires a same-thread parent HWND;
    ///    WorkerW is owned by explorer.exe, so the island throws "created on a
    ///    different thread".
    ///  - GDI+ is the only reliable way to draw into an arbitrary foreign HWND.
    ///
    /// The native wallpaper is always applied first (SchedulerEngine.ApplyById)
    /// as the base/fallback, so the desktop stays correct even if the frame
    /// cannot attach (no WorkerW, explorer restart, etc.).
    /// </summary>
    public sealed class WallpaperFrameService
    {
        private const string ClassName = "WallpaperScheduler.Frame";
        private const uint WM_PAINT = 0x000F;
        private const uint WM_ERASEBKGND = 0x0014;
        private const uint WM_DESTROY = 0x0002;
        private const uint WS_CHILD = 0x40000000;
        private const uint WS_VISIBLE = 0x10000000;
        private const int WS_EX_NOACTIVATE = 0x08000000;
        private const int SWP_SHOWWINDOW = 0x0040;
        private const int SWP_NOACTIVATE = 0x0010;
        private const int SWP_NOMOVE = 0x0002;
        private const int SWP_NOSIZE = 0x0001;

        private static readonly WndProcDelegate _wndProc = WndProc;
        private static ushort _classAtom;

        private readonly DispatcherQueue _dispatcher;
        private static WallpaperFrameService? _instance;
        private IntPtr _hwnd;
        private IntPtr _workerw;
        private bool _attached;
        private int _width;
        private int _height;
        private Bitmap? _oldBitmap;
        private Bitmap? _newBitmap;
        private float _fadeAlpha;         // 0..1, alpha of _newBitmap
        private DispatcherTimer? _fadeTimer;

        public WallpaperFrameService(DispatcherQueue dispatcher)
        {
            _dispatcher = dispatcher;
            _instance = this;
        }

        /// <summary>Thread-safe; dispatches to the UI thread.</summary>
        public void ShowWallpaper(string filePath, string style)
        {
            _dispatcher.TryEnqueue(() =>
            {
                try { ShowWallpaperCore(filePath, style); }
                catch { /* frame is best-effort; native wallpaper already applied */ }
            });
        }

        private void ShowWallpaperCore(string filePath, string style)
        {
            if (!File.Exists(filePath)) return;
            EnsureAttached();
            if (!_attached) return;

            using var src = new Bitmap(filePath);
            var next = ScaleToFit(src, _width, _height, style);

            // Crossfade: keep current as old, fade new in.
            if (_fadeTimer != null) { _fadeTimer.Stop(); _fadeTimer = null; }
            _oldBitmap?.Dispose();
            _oldBitmap = _newBitmap;
            _newBitmap = next;
            _fadeAlpha = 0f;

            _fadeTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
            _fadeTimer.Tick += (_, _) =>
            {
                _fadeAlpha += 0.03f;
                Invalidate();
                if (_fadeAlpha >= 1f)
                {
                    _fadeAlpha = 1f;
                    _fadeTimer.Stop();
                    _fadeTimer = null;
                    _oldBitmap?.Dispose();
                    _oldBitmap = null;
                    Invalidate();
                }
            };
            _fadeTimer.Start();
            Invalidate();
        }

        private void EnsureAttached()
        {
            if (_attached) return;
            if (_hwnd != IntPtr.Zero) DestroyFrame();

            _workerw = LocateWorkerW();
            if (_workerw == IntPtr.Zero) return;

            try
            {
                RegisterClass();
                GetClientRect(_workerw, out RECT rc);
                _width = rc.Right;
                _height = rc.Bottom;

                _hwnd = CreateWindowEx(
                    WS_EX_NOACTIVATE,
                    ClassName, "WallpaperScheduler.Frame",
                    WS_CHILD | WS_VISIBLE,
                    0, 0, _width, _height,
                    _workerw, IntPtr.Zero, IntPtr.Zero, IntPtr.Zero);
                if (_hwnd == IntPtr.Zero) { _attached = false; return; }

                SetParent(_hwnd, _workerw);
                SetWindowPos(_hwnd, IntPtr.Zero, 0, 0, _width, _height, SWP_SHOWWINDOW | SWP_NOACTIVATE | SWP_NOMOVE | SWP_NOSIZE);
                _attached = true;
            }
            catch
            {
                DestroyFrame();
            }
        }

        private void DestroyFrame()
        {
            if (_fadeTimer != null) { _fadeTimer.Stop(); _fadeTimer = null; }
            _oldBitmap?.Dispose(); _oldBitmap = null;
            _newBitmap?.Dispose(); _newBitmap = null;
            if (_hwnd != IntPtr.Zero) { DestroyWindow(_hwnd); _hwnd = IntPtr.Zero; }
            _attached = false;
        }

        private void Invalidate() => InvalidateRect(_hwnd, IntPtr.Zero, false);

        private static Bitmap ScaleToFit(Bitmap src, int w, int h, string style)
        {
            var bmp = new Bitmap(Math.Max(1, w), Math.Max(1, h), PixelFormat.Format32bppArgb);
            using var g = Graphics.FromImage(bmp);
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.Clear(Color.Black);

            switch (style.ToLowerInvariant())
            {
                case "fit":
                    {
                        var s = Math.Min((float)w / src.Width, (float)h / src.Height);
                        int dw = (int)(src.Width * s), dh = (int)(src.Height * s);
                        g.DrawImage(src, (w - dw) / 2, (h - dh) / 2, dw, dh);
                        break;
                    }
                case "stretch":
                    g.DrawImage(src, 0, 0, w, h);
                    break;
                case "center":
                    g.DrawImage(src, (w - src.Width) / 2, (h - src.Height) / 2, src.Width, src.Height);
                    break;
                case "tile":
                    using (var b = new TextureBrush(src))
                    {
                        g.FillRectangle(b, 0, 0, w, h);
                    }
                    break;
                case "fill":
                case "span":
                default:
                    {
                        var s = Math.Max((float)w / src.Width, (float)h / src.Height);
                        int dw = (int)(src.Width * s), dh = (int)(src.Height * s);
                        g.DrawImage(src, (w - dw) / 2, (h - dh) / 2, dw, dh);
                        break;
                    }
            }
            return bmp;
        }

        // ---- Win32 WndProc ----
        private static IntPtr WndProc(IntPtr hwnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            switch (msg)
            {
                case WM_ERASEBKGND:
                    return new IntPtr(1);
                case WM_PAINT:
                    Paint(hwnd);
                    return IntPtr.Zero;
                default:
                    return DefWindowProc(hwnd, msg, wParam, lParam);
            }
        }

        private static void Paint(IntPtr hwnd)
        {
            PAINTSTRUCT ps;
            IntPtr dc = BeginPaint(hwnd, out ps);
            try
            {
                var frame = InstanceFor(hwnd);
                if (frame != null) frame.RenderFrame(dc);
            }
            finally
            {
                EndPaint(hwnd, ref ps);
            }
        }

        // Only one frame exists; the singleton instance is resolved via the hwnd.
        private static WallpaperFrameService? InstanceFor(IntPtr hwnd)
        {
            var f = _instance;
            return f != null && f._hwnd == hwnd ? f : null;
        }

        private void RenderFrame(IntPtr dc)
        {
            using var g = Graphics.FromHdc(dc);
            g.CompositingMode = CompositingMode.SourceCopy;
            g.Clear(Color.Black);

            if (_oldBitmap != null)
                g.DrawImage(_oldBitmap, 0, 0, _width, _height);

            if (_newBitmap != null)
            {
                var attrs = new ImageAttributes();
                var cm = new ColorMatrix();
                cm.Matrix33 = _fadeAlpha;
                attrs.SetColorMatrix(cm, ColorMatrixFlag.Default, ColorAdjustType.Bitmap);
                g.DrawImage(_newBitmap, new Rectangle(0, 0, _width, _height), 0, 0, _newBitmap.Width, _newBitmap.Height, GraphicsUnit.Pixel, attrs);
            }
        }

        // ---- WorkerW (desktop wallpaper layer) locator ----
        // Same technique as Wallpaper Engine / Lively: tell Progman to spawn the
        // WorkerW behind the icons (0x052C), then find the empty WorkerW.

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindow(string? lpClassName, string? lpWindowName);

        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern IntPtr FindWindowEx(IntPtr hwndParent, IntPtr hwndChildAfter, string? lpszClass, string? lpszWindow);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessageTimeout(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam, uint fuFlags, uint uTimeout, out IntPtr result);

        [DllImport("user32.dll")]
        private static extern bool GetClientRect(IntPtr hWnd, out RECT rect);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr CreateWindowEx(int dwExStyle, string lpClassName, string lpWindowName,
            uint dwStyle, int x, int y, int nWidth, int nHeight, IntPtr hWndParent, IntPtr hMenu, IntPtr hInstance, IntPtr lpParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SetParent(IntPtr hWndChild, IntPtr hWndNewParent);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int x, int y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool DestroyWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern bool InvalidateRect(IntPtr hWnd, IntPtr lpRect, bool bErase);

        [DllImport("user32.dll")]
        private static extern IntPtr DefWindowProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr BeginPaint(IntPtr hWnd, out PAINTSTRUCT lpPaint);

        [DllImport("user32.dll")]
        private static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT lpPaint);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct RECT
        {
            public int Left;
            public int Top;
            public int Right;
            public int Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool fErase;
            public RECT rcPaint;
            public bool fRestore;
            public bool fIncUpdate;
            public byte rgbReserved0;
            public byte rgbReserved1;
            public byte rgbReserved2;
            public byte rgbReserved3;
            public byte rgbReserved4;
            public byte rgbReserved5;
            public byte rgbReserved6;
            public byte rgbReserved7;
        }

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct WNDCLASS
        {
            public uint style;
            public IntPtr lpfnWndProc;
            public int cbClsExtra;
            public int cbWndExtra;
            public IntPtr hInstance;
            public IntPtr hIcon;
            public IntPtr hCursor;
            public IntPtr hbrBackground;
            public string? lpszMenuName;
            public string lpszClassName;
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern ushort RegisterClassW(ref WNDCLASS lpWndClass);

        [DllImport("kernel32.dll")]
        private static extern IntPtr GetModuleHandle(string? lpModuleName);

        private static void RegisterClass()
        {
            if (_classAtom != 0) return;
            var wc = new WNDCLASS
            {
                lpfnWndProc = Marshal.GetFunctionPointerForDelegate(_wndProc),
                hInstance = GetModuleHandle(null),
                lpszClassName = ClassName
            };
            _classAtom = RegisterClassW(ref wc);
            if (_classAtom == 0)
            {
                int err = Marshal.GetLastWin32Error();
                if (err != 1410) throw new InvalidOperationException($"RegisterClassW failed err={err}");
            }
        }

        private static IntPtr LocateWorkerW()
        {
            IntPtr progman = FindWindow("Progman", null);
            if (progman != IntPtr.Zero)
            {
                // ponytail: 0xD/0x1 forces WorkerW creation on Win10/11;
                // 0/0 works on some builds. Sending both is harmless.
                SendMessageTimeout(progman, 0x052C, new IntPtr(0xD), new IntPtr(0x1), 0, 1000, out _);
            }

            IntPtr workerw = IntPtr.Zero;
            IntPtr after = IntPtr.Zero;
            do
            {
                after = FindWindowEx(IntPtr.Zero, after, "WorkerW", null);
                if (after == IntPtr.Zero) break;
                if (FindWindowEx(after, IntPtr.Zero, "SHELLDLL_DefView", null) != IntPtr.Zero)
                {
                    workerw = FindWindowEx(IntPtr.Zero, after, "WorkerW", null);
                    break;
                }
            } while (after != IntPtr.Zero);

            if (workerw == IntPtr.Zero && progman != IntPtr.Zero)
                workerw = FindWindowEx(progman, IntPtr.Zero, "WorkerW", null);

            return workerw;
        }
    }
}
