using MediaBrowser.Model.Plugins;

namespace EasyMovie.Plugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiUrl { get; set; } = "https://easymovie.lat/api/subscription.php";
    public int ExpiringThresholdDays { get; set; } = 7;
    public int TrialMaxDurationDays { get; set; } = 14;
    public int CacheDurationMinutes { get; set; } = 10;
    public VideoPaths Videos { get; set; } = new();

    public class VideoPaths
    {
        public string Active { get; set; } = string.Empty;
        public string Expiring { get; set; } = string.Empty;
        public string Expired { get; set; } = string.Empty;
        public string Courtesy { get; set; } = string.Empty;
    }
}
