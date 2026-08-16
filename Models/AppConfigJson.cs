using System.Text.Json.Serialization;

namespace WallpaperScheduler.Models
{
    [JsonSourceGenerationOptions(WriteIndented = true, PropertyNameCaseInsensitive = true)]
    [JsonSerializable(typeof(AppConfig))]
    public partial class AppConfigJsonContext : JsonSerializerContext
    {
    }
}