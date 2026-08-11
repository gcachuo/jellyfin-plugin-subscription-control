using EasyMovie.Plugin.Configuration;
using EasyMovie.Plugin.Models;

namespace EasyMovie.Plugin.Playback;

public sealed class PrerollVideoSelector
{
    public string Select(SubscriptionStatus status, PluginConfiguration.VideoPaths videos)
    {
        if (status.IsCourtesy) return videos.Courtesy;
        if (status.IsTrial && !string.IsNullOrWhiteSpace(videos.Trial)) return videos.Trial;
        if (status.IsExpiring || status.IsTrial) return videos.Expiring;
        return videos.Active;
    }
}
