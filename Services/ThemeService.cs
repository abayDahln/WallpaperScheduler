using Microsoft.UI.Xaml;

namespace WallpaperScheduler.Services
{
    public static class ThemeService
    {
        // ponytail: applies theme to the window root element; Default = follow system
        public static void Apply(Window window, string theme)
        {
            if (window.Content is FrameworkElement root)
            {
                root.RequestedTheme = theme switch
                {
                    "light" => ElementTheme.Light,
                    "dark" => ElementTheme.Dark,
                    _ => ElementTheme.Default
                };
            }
        }
    }
}