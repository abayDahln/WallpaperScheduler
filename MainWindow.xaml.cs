using System;
using System.Runtime.InteropServices;
using Microsoft.UI;
using Microsoft.UI.Windowing;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using WallpaperScheduler.Services;
using WallpaperScheduler.ViewModels;
using WallpaperScheduler.Views;
using Windows.Graphics;
using WinRT.Interop;

namespace WallpaperScheduler
{
    public sealed partial class MainWindow : Window
    {
        [DllImport("user32.dll")]
        private static extern uint GetDpiForWindow(IntPtr hWnd);

        private const int GWLP_WNDPROC = -4;
        private const uint WM_GETMINMAXINFO = 0x0024;

        private static IntPtr SetWindowLongPtrSafe(IntPtr hWnd, int nIndex, IntPtr dwNewLong)
        {
            return IntPtr.Size == 8
                ? SetWindowLongPtr64(hWnd, nIndex, dwNewLong)
                : new IntPtr(SetWindowLong32(hWnd, nIndex, dwNewLong.ToInt32()));
        }

        [DllImport("user32.dll", EntryPoint = "SetWindowLongPtr", SetLastError = true)]
        private static extern IntPtr SetWindowLongPtr64(IntPtr hWnd, int nIndex, IntPtr dwNewLong);

        [DllImport("user32.dll", EntryPoint = "SetWindowLong", SetLastError = true)]
        private static extern int SetWindowLong32(IntPtr hWnd, int nIndex, int dwNewLong);

        [DllImport("user32.dll")]
        private static extern IntPtr CallWindowProc(IntPtr lpPrevWndFunc, IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        private delegate IntPtr WndProcDelegate(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);

        [StructLayout(LayoutKind.Sequential)]
        private struct Point32 { public int X, Y; }

        [StructLayout(LayoutKind.Sequential)]
        private struct MINMAXINFO
        {
            public Point32 ptReserved;
            public Point32 ptMaxSize;
            public Point32 ptMaxPosition;
            public Point32 ptMinTrackSize;
            public Point32 ptMaxTrackSize;
        }

        private WndProcDelegate? _wndProc;
        private IntPtr _prevWndProc;
        private int _minWidthPx, _minHeightPx;
        private bool _navCompact;

        public MainViewModel ViewModel { get; }

        public MainWindow()
        {
            InitializeComponent();
            var app = (App)Application.Current;
            ViewModel = app.MainViewModel;
            this.Closed += MainWindow_Closed;
            ThemeService.Apply(this, app.ConfigService.Config.Settings.ThemeOverride);

            NavView.SelectionChanged += OnNavSelectionChanged;
            NavView.SelectedItem = NavView.MenuItems[0];
            SizeWindow();
            SetMinSize();
            AppWindow.Changed += OnAppWindowChanged;
            ApplyTrayIconVisibility(app.ConfigService.Config.Settings.HideTrayIcon);
        }

        public void ApplyTrayIconVisibility(bool hide)
        {
            if (hide)
            {
                TrayIcon.IconSource = null;
            }
            else if (TrayIcon.IconSource == null)
            {
                TrayIcon.IconSource = new Microsoft.UI.Xaml.Media.Imaging.BitmapImage(new Uri("ms-appx:///Assets/AppIcon.ico"));
                TrayIcon.ForceCreate();
            }
        }

        private double DpiScale() => GetDpiForWindow(Win32Interop.GetWindowFromWindowId(AppWindow.Id)) / 96.0;

        private void SizeWindow()
        {
            double scale = DpiScale();
            AppWindow.Resize(new SizeInt32((int)(1360 * scale), (int)(800 * scale)));
        }

        private void SetMinSize()
        {
            double scale = DpiScale();
            _minWidthPx = (int)(900 * scale);
            _minHeightPx = (int)(600 * scale);

            var hwnd = Win32Interop.GetWindowFromWindowId(AppWindow.Id);
            _wndProc = WndProc;
            _prevWndProc = SetWindowLongPtrSafe(hwnd, GWLP_WNDPROC,
                Marshal.GetFunctionPointerForDelegate(_wndProc));
        }

        private IntPtr WndProc(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam)
        {
            if (msg == WM_GETMINMAXINFO)
            {
                var mmi = Marshal.PtrToStructure<MINMAXINFO>(lParam);
                mmi.ptMinTrackSize = new Point32 { X = _minWidthPx, Y = _minHeightPx };
                Marshal.StructureToPtr(mmi, lParam, false);
            }
            return CallWindowProc(_prevWndProc, hWnd, msg, wParam, lParam);
        }

        private void OnAppWindowChanged(AppWindow sender, AppWindowChangedEventArgs args)
        {
            if (!args.DidSizeChange) return;
            double w = sender.Size.Width / DpiScale();
            bool compact = w < 1100;
            if (compact == _navCompact) return;
            _navCompact = compact;
            NavView.PaneDisplayMode = compact
                ? NavigationViewPaneDisplayMode.LeftCompact
                : NavigationViewPaneDisplayMode.Left;
            if (!compact) NavView.IsPaneOpen = true;
        }

        private void OnNavSelectionChanged(NavigationView sender, NavigationViewSelectionChangedEventArgs args)
        {
            if (args.SelectedItem is NavigationViewItem item)
            {
                string tag = item.Tag?.ToString() ?? "library";
                switch (tag)
                {
                    case "overview": ContentFrame.Navigate(typeof(OverviewPage)); break;
                    case "weekly": ContentFrame.Navigate(typeof(WeeklySchedulePage)); break;
                    case "monthly": ContentFrame.Navigate(typeof(MonthlyOverridesPage)); break;
                    case "dates": ContentFrame.Navigate(typeof(DateOverridesPage)); break;
                    case "settings": ContentFrame.Navigate(typeof(SettingsPage)); break;
                }
            }
        }

        private void MainWindow_Closed(object sender, WindowEventArgs args)
        {
            var config = ((App)Application.Current).ConfigService.Config;
            if (config.Settings.CloseButtonMinimizesToTray)
            {
                args.Handled = true;
                this.AppWindow.Hide();
            }
        }

        private void OnOpenAppClick(object sender, RoutedEventArgs e)
        {
            this.AppWindow.Show();
            this.Activate();
        }

        private void OnTogglePauseClick(object sender, RoutedEventArgs e)
        {
            ViewModel.TogglePause();
            TrayPauseItem.Text = ViewModel.IsPaused ? "Resume Schedule" : "Pause Schedule";
        }

        private void OnExitClick(object sender, RoutedEventArgs e)
        {
            this.Closed -= MainWindow_Closed;
            Application.Current.Exit();
        }
    }
}