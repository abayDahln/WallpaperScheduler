using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.UI.Xaml;
using WallpaperScheduler.Helpers;
using WallpaperScheduler.Services;
using WallpaperScheduler.ViewModels;

namespace WallpaperScheduler
{
    public partial class App : Application
    {
        private static readonly string CrashLogPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperSchedule", "crash.log");

        private Window? _window;
        public static Window? MainWindow { get; private set; }
        public ConfigService ConfigService { get; private set; } = null!;
        public SchedulerEngine SchedulerEngine { get; private set; } = null!;
        public MainViewModel MainViewModel { get; private set; } = null!;

        public App()
        {
            InitializeComponent();
            UnhandledException += (_, e) =>
            {
                try
                {
                    File.AppendAllText(CrashLogPath,
                        $"[{DateTime.Now:o}] {e.Exception}\n\n");
                }
                catch { }
            };
            AppDomain.CurrentDomain.UnhandledException += (_, e) =>
            {
                try
                {
                    File.AppendAllText(CrashLogPath,
                        $"[{DateTime.Now:o}] BACKGROUND {e.ExceptionObject}\n\n");
                }
                catch { }
            };
        }

        protected override void OnLaunched(Microsoft.UI.Xaml.LaunchActivatedEventArgs args)
        {
            ConfigService = new ConfigService();
            SchedulerEngine = new SchedulerEngine(ConfigService);
            MainViewModel = new MainViewModel(ConfigService, SchedulerEngine);

            // Auto-start sync
            AutoStartService.SetAutoStart(ConfigService.Config.Settings.AutoStart);

            SchedulerEngine.Start();
            _ = BackfillThumbsAsync();

            _window = new MainWindow();
            MainWindow = _window;

            string[] commandLineArgs = Environment.GetCommandLineArgs();
            bool startInTray = commandLineArgs.Contains("--tray");

            if (!startInTray)
            {
                _window.Activate();
            }
        }

        private async Task BackfillThumbsAsync()
        {
            try
            {
                foreach (var item in ConfigService.Config.WallpaperLibrary)
                {
                    await ThumbnailGenerator.EnsureThumbAsync(item);
                }
            }
            catch { }
        }
    }
}
