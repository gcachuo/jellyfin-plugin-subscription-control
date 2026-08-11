using System.Collections.Generic;
using MediaBrowser.Model.Plugins;

namespace EasyMovie.Plugin.Configuration;

public class PluginConfiguration : BasePluginConfiguration
{
    public string ApiUrl { get; set; } = "https://easymovie.lat/api/subscription.php";
    public int ExpiringThresholdDays { get; set; } = 7;
    public int TrialMaxDurationDays { get; set; } = 14;
    public int CacheDurationMinutes { get; set; } = 10;
    public int ApiTimeoutSeconds { get; set; } = 30;
    public bool EnableWebOverlay { get; set; }
    public List<string> WebOverlayClientNames { get; set; } = ["Jellyfin Web"];
    public List<string> NativePrerollClientNames { get; set; } = ["Jellyfin Android TV"];
    public VideoPaths Videos { get; set; } = new();

    public class VideoPaths
    {
        public string Active { get; set; } = string.Empty;
        public string Expiring { get; set; } = string.Empty;
        public string Expired { get; set; } = string.Empty;
        public string Courtesy { get; set; } = string.Empty;
    }
}
