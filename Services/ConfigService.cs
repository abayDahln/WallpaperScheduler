using System;
using System.IO;
using System.Text.Json;
using WallpaperScheduler.Models;

namespace WallpaperScheduler.Services
{
    public class ConfigService
    {
        private static readonly string AppDataDir = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "WallpaperSchedule"
        );
        private static readonly string ConfigPath = Path.Combine(AppDataDir, "config.json");
        public static string WallpapersDir => Path.Combine(AppDataDir, "Wallpapers");
        public static string ThumbsDir => Path.Combine(AppDataDir, "Thumbs");

        public AppConfig Config { get; private set; } = new();

        public ConfigService()
        {
            EnsureDirectories();
            LoadConfig();
        }

        public void EnsureDirectories()
        {
            Directory.CreateDirectory(AppDataDir);
            Directory.CreateDirectory(WallpapersDir);
            Directory.CreateDirectory(ThumbsDir);
        }

        public void LoadConfig()
        {
            if (!File.Exists(ConfigPath))
            {
                Config = new AppConfig();
                SaveConfig();
                return;
            }

            try
            {
                string json = File.ReadAllText(ConfigPath);
                var loaded = JsonSerializer.Deserialize<AppConfig>(json, JsonOptions);
                Config = loaded ?? new AppConfig();
            }
            catch
            {
                // ponytail: backup corrupted file and initialize fallback config
                string backup = ConfigPath + ".bak";
                if (File.Exists(ConfigPath)) File.Copy(ConfigPath, backup, overwrite: true);
                Config = new AppConfig();
                SaveConfig();
            }
        }

        public void SaveConfig()
        {
            EnsureDirectories();
            string tmp = ConfigPath + ".tmp";
            string json = JsonSerializer.Serialize(Config, JsonOptions);
            File.WriteAllText(tmp, json);
            File.Move(tmp, ConfigPath, overwrite: true);
        }

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true,
            PropertyNameCaseInsensitive = true
        };
    }
}
